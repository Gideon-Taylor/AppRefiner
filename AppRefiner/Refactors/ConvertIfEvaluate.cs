using System.Text;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Converts between an If/Else-If chain and an Evaluate statement, whichever
    /// direction applies at the cursor. Convertibility uses CompletionAnalyzer:
    /// PeopleCode Evaluate falls through after a matching When unless control leaves
    /// the clause, so a generated When body gets a trailing Break only when its body
    /// can reach its end, and an Evaluate converts back to If only when no When body
    /// can complete normally.
    /// </summary>
    public class ConvertIfEvaluate : BaseRefactor
    {
        public new static string RefactorName => "Convert If ↔ Evaluate";
        public new static string RefactorDescription => "Converts an If/Else-If chain to an Evaluate statement, or back";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RunOnIncompleteParse => false;

        private static readonly BinaryOperator[] ComparisonOperators =
        {
            BinaryOperator.Equal, BinaryOperator.NotEqual,
            BinaryOperator.LessThan, BinaryOperator.LessThanOrEqual,
            BinaryOperator.GreaterThan, BinaryOperator.GreaterThanOrEqual
        };

        private IfStatementNode? topIfNode;
        private EvaluateStatementNode? evaluateNode;

        public ConvertIfEvaluate(ScintillaEditor editor) : base(editor) { }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);
            Analyze(node);
        }

        private void Analyze(ProgramNode program)
        {
            // BlockNodes are excluded: a block containing a single statement shares that
            // statement's exact span, and the stable descending sort would pick whichever
            // FindDescendants enumerates first — the block. For the program's top-level
            // block the parent walk then dead-ends at ProgramNode, so a cursor on the
            // outer If's own condition line (or its Else keyword) failed to resolve.
            // Blocks are containers, never the construct the user means; the parent walk
            // reaches their owning statement anyway.
            var statement = program.FindDescendants<StatementNode>()
                .Where(s => s is not BlockNode && s.SourceSpan.ContainsPosition(CurrentPosition))
                .OrderByDescending(s => s.SourceSpan.Start.ByteIndex)
                .FirstOrDefault();

            AstNode? cur = statement;
            while (cur != null && cur is not IfStatementNode && cur is not EvaluateStatementNode)
                cur = cur.Parent;

            switch (cur)
            {
                case IfStatementNode ifNode:
                    topIfNode = ifNode;
                    ConvertIfToEvaluate();
                    break;
                case EvaluateStatementNode evalNode:
                    evaluateNode = evalNode;
                    ConvertEvaluateToIf();
                    break;
                default:
                    SetFailure("Place the cursor inside an If/Else-If chain or an Evaluate statement.");
                    break;
            }
        }

        private void ConvertIfToEvaluate()
        {
            // Climb to the topmost chain member: a chain link's Else block contains
            // exactly one statement, which is the next If (PeopleCode has no ElseIf)
            var ifNode = topIfNode!;
            while (ifNode.Parent is BlockNode parentBlock
                && parentBlock.Statements.Count == 1
                && parentBlock.Parent is IfStatementNode parentIf
                && ReferenceEquals(parentIf.ElseBlock, parentBlock))
            {
                ifNode = parentIf;
            }

            var links = new List<IfStatementNode>();
            BlockNode? finalElse = null;
            var current = ifNode;
            while (true)
            {
                links.Add(current);
                if (current.ElseBlock is { } elseBlock
                    && elseBlock.Statements.Count == 1
                    && elseBlock.Statements[0] is IfStatementNode nextIf)
                {
                    current = nextIf;
                    continue;
                }
                finalElse = current.ElseBlock;
                break;
            }

            var comparisons = new List<BinaryOperationNode>();
            foreach (var link in links)
            {
                if (link.Condition is not BinaryOperationNode cmp
                    || cmp.NotFlag
                    || !ComparisonOperators.Contains(cmp.Operator))
                {
                    SetFailure("Every condition in the chain must be a simple comparison (=, <>, <, <=, >, >=) to convert to Evaluate.");
                    return;
                }
                comparisons.Add(cmp);
            }

            // Scrutinee: the expression common to all comparisons, on either side
            var whens = MatchScrutinee(comparisons, useLeftOfFirst: true)
                     ?? MatchScrutinee(comparisons, useLeftOfFirst: false);
            if (whens == null)
            {
                SetFailure("The conditions do not all compare the same expression, so the chain cannot become an Evaluate.");
                return;
            }

            foreach (var body in links.Select(l => l.ThenBlock).Concat(finalElse != null ? new[] { finalElse } : Array.Empty<BlockNode>()))
            {
                if (ContainsEvaluateBoundBreak(body, ignoreTrailing: false))
                {
                    SetFailure("A body in this chain contains a Break that targets an enclosing loop — wrapping it in an Evaluate would silently retarget the Break.");
                    return;
                }
            }

            BuildEvaluateText(ifNode, links, whens, finalElse);
        }

        /// <summary>
        /// Tries one side of the first comparison as the scrutinee and checks every
        /// comparison against it. Returns per-link (operator symbol, value expression),
        /// with the operator mirrored when the scrutinee is on the right (5 &lt; &amp;x
        /// becomes When &gt; 5). Null when any comparison doesn't involve the scrutinee.
        /// </summary>
        private List<(string OpSymbol, ExpressionNode Value)>? MatchScrutinee(
            List<BinaryOperationNode> comparisons, bool useLeftOfFirst)
        {
            var first = comparisons[0];
            var scrutineeExpr = useLeftOfFirst ? first.Left : first.Right;
            string scrutNorm = NormalizeExpressionText(scrutineeExpr);

            var result = new List<(string, ExpressionNode)>();
            foreach (var cmp in comparisons)
            {
                if (NormalizeExpressionText(cmp.Left) == scrutNorm)
                    result.Add((cmp.Operator.GetSymbol(), cmp.Right));
                else if (NormalizeExpressionText(cmp.Right) == scrutNorm)
                    result.Add((MirrorOperator(cmp.Operator).GetSymbol(), cmp.Left));
                else
                    return null;
            }
            ScrutineeText = GetSourceText(scrutineeExpr.SourceSpan);
            return result;
        }

        private string? ScrutineeText;

        private string NormalizeExpressionText(ExpressionNode expr)
        {
            var trimmed = GetSourceText(expr.SourceSpan).Trim();
            if (trimmed.Contains('"'))
                return trimmed; // literal content must match exactly; conservative: may miss, never wrong
            return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ").ToLowerInvariant();
        }

        /// <summary>
        /// Wraps expression text in parentheses unless the node is atomic, so pasting
        /// it into a comparison/Or-join context cannot change associativity.
        /// </summary>
        private string ParenthesizedText(ExpressionNode expr)
        {
            string text = GetSourceText(expr.SourceSpan);
            return expr switch
            {
                LiteralNode or IdentifierNode or MemberAccessNode
                    or ArrayAccessNode or FunctionCallNode or ParenthesizedExpressionNode
                    or ObjectCreationNode or ObjectCreateShortHand or ClassConstantNode
                    or MetadataExpressionNode => text,
                _ => "(" + text + ")"
            };
        }

        private static BinaryOperator MirrorOperator(BinaryOperator op) => op switch
        {
            BinaryOperator.LessThan => BinaryOperator.GreaterThan,
            BinaryOperator.LessThanOrEqual => BinaryOperator.GreaterThanOrEqual,
            BinaryOperator.GreaterThan => BinaryOperator.LessThan,
            BinaryOperator.GreaterThanOrEqual => BinaryOperator.LessThanOrEqual,
            _ => op // = and <> are symmetric
        };

        /// <summary>
        /// True when the block contains a Break that would bind to an Evaluate/loop at
        /// this block's level — i.e., not nested inside an inner For/While/Repeat/Evaluate.
        /// With ignoreTrailing, a Break that is the block's own last top-level statement
        /// is skipped (used by Evaluate→If, where that Break is expected and dropped).
        /// </summary>
        private static bool ContainsEvaluateBoundBreak(BlockNode body, bool ignoreTrailing)
        {
            foreach (var breakNode in body.FindDescendants<BreakStatementNode>())
            {
                if (ignoreTrailing
                    && body.Statements.Count > 0
                    && ReferenceEquals(body.Statements[^1], breakNode))
                    continue;

                bool bound = true;
                for (AstNode? cur = breakNode.Parent; cur != null && !ReferenceEquals(cur, body); cur = cur.Parent)
                {
                    if (cur is ForStatementNode or WhileStatementNode or RepeatStatementNode or EvaluateStatementNode)
                    {
                        bound = false;
                        break;
                    }
                }
                if (bound) return true;
            }
            return false;
        }

        private void BuildEvaluateText(IfStatementNode topIf, List<IfStatementNode> links,
            List<(string OpSymbol, ExpressionNode Value)> whens, BlockNode? finalElse)
        {
            string indent = GetLineIndent(topIf.SourceSpan.Start.ByteIndex);
            string unit = DetectIndentUnit(links[0], indent);
            var sb = new StringBuilder();

            sb.Append($"Evaluate {ScrutineeText}{NewLine}");
            for (int i = 0; i < links.Count; i++)
            {
                var thenBlock = links[i].ThenBlock;
                sb.Append($"{indent}When {whens[i].OpSymbol} {GetSourceText(whens[i].Value.SourceSpan)}{NewLine}");
                sb.Append(RenderBody(thenBlock, indent + unit, dropTrailingBreak: false));
                // Evaluate falls through: add a trailing Break only when the body can
                // reach its end. A body that already always terminates (Return/Throw/
                // etc.) needs none — an added Break would be unreachable, and omitting
                // it keeps If<->Evaluate round-trips a fixpoint. Empty bodies fall
                // through, so they still get one.
                if (CompletionAnalyzer.Analyze(thenBlock).HasFlag(ExitMode.Normal))
                    sb.Append($"{indent + unit}Break;{NewLine}");
            }
            if (finalElse != null)
            {
                sb.Append($"{indent}When-Other{NewLine}");
                sb.Append(RenderBody(finalElse, indent + unit, dropTrailingBreak: false));
            }
            sb.Append($"{indent}End-Evaluate");

            ReplaceStatement(topIf, sb.ToString());
        }

        /// <summary>
        /// Indent unit inferred from the delta between the statement's line and its Then
        /// block's first line; App Designer's three-space convention as fallback.
        /// </summary>
        private string DetectIndentUnit(IfStatementNode firstLink, string baseIndent)
        {
            if (firstLink.ThenBlock.Statements.Count > 0)
            {
                string bodyIndent = GetLineIndent(firstLink.ThenBlock.Statements[0].SourceSpan.Start.ByteIndex);
                if (bodyIndent.Length > baseIndent.Length && bodyIndent.StartsWith(baseIndent))
                    return bodyIndent.Substring(baseIndent.Length);
            }
            return "   ";
        }

        /// <summary>
        /// Renders a block's statements re-indented at newIndent. Captures from the line
        /// start of the first statement so every line carries its real indentation, then
        /// strips the common leading whitespace and prepends newIndent.
        /// </summary>
        private string RenderBody(BlockNode block, string newIndent, bool dropTrailingBreak)
        {
            var statements = block.Statements;
            int count = statements.Count;
            if (dropTrailingBreak && count > 0 && statements[^1] is BreakStatementNode)
                count--;
            if (count == 0)
                return string.Empty;

            int start = statements[0].SourceSpan.Start.ByteIndex;
            while (start > 0 && SourceBytes[start - 1] != (byte)'\n'
                && (SourceBytes[start - 1] == (byte)' ' || SourceBytes[start - 1] == (byte)'\t'))
                start--;

            int end = statements[count - 1].SourceSpan.End.ByteIndex;
            while (end < SourceBytes.Length
                && (SourceBytes[end] == (byte)' ' || SourceBytes[end] == (byte)'\t'))
                end++;
            if (end < SourceBytes.Length && SourceBytes[end] == (byte)';')
                end++;

            string text = GetSourceText(start, end);
            var lines = text.Replace("\r\n", "\n").Split('\n');

            int minIndent = int.MaxValue;
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0) continue;
                int ws = 0;
                while (ws < line.Length && (line[ws] == ' ' || line[ws] == '\t')) ws++;
                minIndent = Math.Min(minIndent, ws);
            }
            if (minIndent == int.MaxValue) minIndent = 0;

            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                    sb.Append(NewLine);
                else
                    sb.Append(newIndent + line.Substring(Math.Min(minIndent, line.Length)) + NewLine);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Replaces the statement's span with new text, carrying over a trailing
        /// semicolon whether it sits inside the span (ParseStatement consumes the
        /// terminator before fixing LastToken, so spans normally include it) or
        /// just after it.
        /// </summary>
        private void ReplaceStatement(StatementNode statement, string newText)
        {
            int start = statement.SourceSpan.Start.ByteIndex;
            int end = statement.SourceSpan.End.ByteIndex;
            if (end < SourceBytes.Length && SourceBytes[end] == (byte)';')
            {
                // semicolon outside the span: extend the range and re-emit
                end++;
                newText += ";";
            }
            else if (end > start && SourceBytes[end - 1] == (byte)';')
            {
                // statement spans include a trailing semicolon (ParseStatement consumes it
                // before fixing LastToken) — keep the range, re-emit the terminator
                newText += ";";
            }
            EditText(start, end, newText, "Convert If ↔ Evaluate");
        }

        private void ConvertEvaluateToIf()
        {
            var ev = evaluateNode!;
            if (ev.WhenClauses.Count == 0)
            {
                SetFailure("This Evaluate has no When clauses to convert.");
                return;
            }

            // Consecutive empty-bodied Whens group with the next non-empty one as
            // Or-joined conditions (the standard PeopleCode stacked-When idiom)
            var groups = new List<(List<WhenClause> Whens, BlockNode Body)>();
            var pending = new List<WhenClause>();
            foreach (var whenClause in ev.WhenClauses)
            {
                pending.Add(whenClause);
                if (whenClause.Body.Statements.Count == 0)
                    continue;

                // Fall-through = control can reach the end of the body. A trailing
                // Return/Throw/Exit/Error/Break/Continue (or an If whose every branch
                // does so) prevents it; a plain trailing statement does not.
                if (CompletionAnalyzer.Analyze(whenClause.Body).HasFlag(ExitMode.Normal))
                {
                    SetFailure("A When clause falls through (control can reach the end of its body) — intentional fall-through cannot be expressed as If/Else and will not be converted.");
                    return;
                }
                if (ContainsEvaluateBoundBreak(whenClause.Body, ignoreTrailing: true))
                {
                    SetFailure("A When body contains a Break in the middle of its logic — converting to If/Else would change where execution resumes.");
                    return;
                }
                groups.Add((new List<WhenClause>(pending), whenClause.Body));
                pending.Clear();
            }

            if (pending.Count > 0)
            {
                SetFailure("The last When clause(s) have no body — there is nothing for their conditions to execute, so the Evaluate cannot be converted.");
                return;
            }

            if (ev.WhenOtherBlock != null
                && ContainsEvaluateBoundBreak(ev.WhenOtherBlock, ignoreTrailing: true))
            {
                SetFailure("The When-Other body contains a Break in the middle of its logic — converting to If/Else would change where execution resumes.");
                return;
            }

            BuildIfChainText(ev, groups);
        }

        private void BuildIfChainText(EvaluateStatementNode ev,
            List<(List<WhenClause> Whens, BlockNode Body)> groups)
        {
            string scrutinee = ParenthesizedText(ev.Expression);
            string baseIndent = GetLineIndent(ev.SourceSpan.Start.ByteIndex);
            string unit = groups.Count > 0 && groups[0].Body.Statements.Count > 0
                ? InferUnitFromBody(groups[0].Body, baseIndent)
                : "   ";

            string Condition(List<WhenClause> whens) => string.Join(" Or ",
                whens.Select(w =>
                    $"{scrutinee} {(w.Operator ?? BinaryOperator.Equal).GetSymbol()} {ParenthesizedText(w.Condition)}"));

            // PeopleCode has no ElseIf: each subsequent group is a nested If inside Else,
            // one indent level deeper, each closing its own End-If
            string Build(int groupIndex, string indent)
            {
                var (whens, body) = groups[groupIndex];
                var sb = new StringBuilder();
                sb.Append($"If {Condition(whens)} Then{NewLine}");
                sb.Append(RenderBody(body, indent + unit, dropTrailingBreak: true));

                bool hasMoreGroups = groupIndex + 1 < groups.Count;
                if (hasMoreGroups)
                {
                    sb.Append($"{indent}Else{NewLine}");
                    sb.Append(indent + unit);
                    sb.Append(Build(groupIndex + 1, indent + unit));
                    sb.Append(NewLine);
                }
                else if (ev.WhenOtherBlock != null)
                {
                    sb.Append($"{indent}Else{NewLine}");
                    sb.Append(RenderBody(ev.WhenOtherBlock, indent + unit, dropTrailingBreak: true));
                }

                // Inner End-Ifs need statement separators; the outermost semicolon is
                // handled by ReplaceStatement
                sb.Append(groupIndex == 0 ? $"{indent}End-If" : $"{indent}End-If;");
                return sb.ToString();
            }

            ReplaceStatement(ev, Build(0, baseIndent));
        }

        private string InferUnitFromBody(BlockNode body, string baseIndent)
        {
            string bodyIndent = GetLineIndent(body.Statements[0].SourceSpan.Start.ByteIndex);
            return bodyIndent.Length > baseIndent.Length && bodyIndent.StartsWith(baseIndent)
                ? bodyIndent.Substring(baseIndent.Length)
                : "   ";
        }
    }
}
