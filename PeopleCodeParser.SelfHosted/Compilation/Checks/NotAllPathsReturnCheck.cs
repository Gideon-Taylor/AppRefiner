using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// For functions/methods/property getters with a return type, requires every path to
/// exit via Return, Throw, Exit, or Error (no Normal / Break / Continue on the body).
/// Emits primary diagnostic(s) on the signature (declaration + implementation for methods)
/// and one secondary at a single locus on the incomplete path (never a whole multi-statement
/// block span — that would mask other squiggles and mis-point at nested fall-through).
/// </summary>
public sealed class NotAllPathsReturnCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    private static readonly ExitMode ValidExits =
        ExitMode.Return | ExitMode.Throw | ExitMode.Exit | ExitMode.Error;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        switch (node)
        {
            case FunctionNode fn when fn.ReturnType != null && fn.Body != null:
                CheckBody(
                    fn.Body,
                    fn.Name,
                    "function",
                    new[] { SignatureSpan(fn.NameToken, fn.ReturnType) },
                    sink);
                break;

            case MethodImplNode mi when mi.Body != null:
            {
                if (mi.Declaration?.ReturnType == null && mi.ReturnTypeAnnotation == null)
                    break;
                CheckBody(mi.Body, mi.Name, "method", MethodPrimarySpans(mi), sink);
                break;
            }

            case PropertyImplNode pi when pi.IsGetter && pi.Body != null:
            {
                var prop = pi.Parent as PropertyNode ?? pi.FindAncestor<PropertyNode>();
                if (prop == null)
                    break;
                var primaries = new List<SourceSpan> { pi.NameToken.SourceSpan };
                if (prop.NameToken.SourceSpan.Start.ByteIndex != pi.NameToken.SourceSpan.Start.ByteIndex)
                    primaries.Insert(0, prop.NameToken.SourceSpan);
                CheckBody(pi.Body, pi.Name, "property getter", primaries, sink);
                break;
            }
        }
    }

    private static void CheckBody(
        BlockNode body,
        string name,
        string kind,
        IReadOnlyList<SourceSpan> primarySpans,
        IDiagnosticSink sink)
    {
        ExitMode modes = CompletionAnalyzer.Analyze(body);
        if (IsComplete(modes))
            return;

        var message = $"Not all paths return a value in {kind} '{name}'.";
        foreach (var span in primarySpans)
        {
            if (!span.IsValid)
                continue;
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.NotAllPathsReturn,
                DiagnosticSeverity.Error,
                span,
                message));
        }

        var secondarySpan = ResolveSecondarySpan(body);
        if (secondarySpan is null || !secondarySpan.Value.IsValid)
            return;

        bool duplicatesPrimary = primarySpans.Any(p =>
            p.Start.ByteIndex == secondarySpan.Value.Start.ByteIndex
            && p.End.ByteIndex == secondarySpan.Value.End.ByteIndex);
        if (duplicatesPrimary)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.NotAllPathsReturn,
            DiagnosticSeverity.Error,
            secondarySpan.Value,
            "This path can complete without returning a value."));
    }

    private static bool IsComplete(ExitMode modes) =>
        modes != ExitMode.None && (modes & ~ValidExits) == ExitMode.None;

    private static bool IsIncomplete(ExitMode? modes) =>
        modes is null || !IsComplete(modes.Value);

    /// <summary>
    /// Single-locus secondary: introducer keyword for empty incomplete arms, otherwise
    /// the last statement's last token (e.g. End-If) of the chosen incomplete region —
    /// never the full multi-statement block span.
    /// </summary>
    private static SourceSpan? ResolveSecondarySpan(BlockNode root)
    {
        var best = FindBestIncompleteBlock(root);
        if (best == null)
            return null;

        // Straight-line routine body: point at the last statement's end token.
        if (ReferenceEquals(best, root) && root.Statements.Count > 0)
            return SingleLocusSpan(root.Statements[^1]);

        if (best.Statements.Count == 0)
            return SpanForEmptyIncompleteBlock(best) ?? SingleLocusSpan(best);

        // Non-empty incomplete region: last statement end (not the whole block).
        return SingleLocusSpan(best.Statements[^1]);
    }

    /// <summary>
    /// Prefer incomplete branch arms that have a complete sibling (the classic
    /// "this arm is the missing return") over nested fall-through inside that arm.
    /// Among equals, prefer deeper; then earlier start.
    /// </summary>
    private static BlockNode? FindBestIncompleteBlock(BlockNode root)
    {
        BlockNode? best = null;
        var bestDepth = -1;
        var bestHasSibling = false;

        void Consider(BlockNode block, int depth)
        {
            var mode = block.GetExitMode();
            if (!IsIncomplete(mode))
                return;

            var hasSibling = HasCompleteSiblingArm(block);

            if (best == null
                || (hasSibling && !bestHasSibling)
                || (hasSibling == bestHasSibling && depth > bestDepth)
                || (hasSibling == bestHasSibling && depth == bestDepth
                    && block.SourceSpan.Start.ByteIndex < best.SourceSpan.Start.ByteIndex))
            {
                best = block;
                bestDepth = depth;
                bestHasSibling = hasSibling;
            }
        }

        void Walk(BlockNode block, int depth)
        {
            Consider(block, depth);
            foreach (var stmt in block.Statements)
                WalkStatement(stmt, depth + 1);
        }

        void WalkStatement(StatementNode stmt, int depth)
        {
            switch (stmt)
            {
                case IfStatementNode ifn:
                    Walk(ifn.ThenBlock, depth);
                    if (ifn.ElseBlock != null)
                        Walk(ifn.ElseBlock, depth);
                    break;
                case EvaluateStatementNode ev:
                    foreach (var w in ev.WhenClauses)
                        Walk(w.Body, depth);
                    if (ev.WhenOtherBlock != null)
                        Walk(ev.WhenOtherBlock, depth);
                    break;
                case ForStatementNode f:
                    Walk(f.Body, depth);
                    break;
                case WhileStatementNode w:
                    Walk(w.Body, depth);
                    break;
                case RepeatStatementNode r:
                    Walk(r.Body, depth);
                    break;
                case TryStatementNode t:
                    Walk(t.TryBlock, depth);
                    foreach (var c in t.CatchClauses)
                        Walk(c.Body, depth);
                    break;
                case BlockNode b:
                    Walk(b, depth);
                    break;
            }
        }

        Walk(root, 0);
        return best;
    }

    /// <summary>
    /// True when this block is one arm of a multi-way construct and a sibling arm is complete
    /// (e.g. Then returns, Else falls through — the Else is the interesting incomplete path).
    /// </summary>
    private static bool HasCompleteSiblingArm(BlockNode block)
    {
        if (block.Parent is IfStatementNode ifn)
        {
            if (ifn.ElseBlock == block)
                return IsComplete(ifn.ThenBlock.GetExitMode() ?? ExitMode.None);
            if (ifn.ThenBlock == block && ifn.ElseBlock != null)
                return IsComplete(ifn.ElseBlock.GetExitMode() ?? ExitMode.None);
            return false;
        }

        if (block.Parent is EvaluateStatementNode ev)
        {
            // When-Other incomplete while some When is complete, or vice versa.
            if (ev.WhenOtherBlock == block)
                return ev.WhenClauses.Any(w => IsComplete(w.Body.GetExitMode() ?? ExitMode.None));

            foreach (var when in ev.WhenClauses)
            {
                if (when.Body != block)
                    continue;
                bool siblingComplete = ev.WhenClauses.Any(w =>
                    w.Body != block && IsComplete(w.Body.GetExitMode() ?? ExitMode.None));
                if (ev.WhenOtherBlock != null
                    && IsComplete(ev.WhenOtherBlock.GetExitMode() ?? ExitMode.None))
                    siblingComplete = true;
                return siblingComplete;
            }
        }

        if (block.Parent is TryStatementNode tryNode)
        {
            if (tryNode.TryBlock == block)
                return tryNode.CatchClauses.Any(c => IsComplete(c.Body.GetExitMode() ?? ExitMode.None));
            // Catch incomplete while try (or another catch) is complete.
            return IsComplete(tryNode.TryBlock.GetExitMode() ?? ExitMode.None)
                || tryNode.CatchClauses.Any(c =>
                    c.Body != block && IsComplete(c.Body.GetExitMode() ?? ExitMode.None));
        }

        return false;
    }

    private static SourceSpan? SpanForEmptyIncompleteBlock(BlockNode empty)
    {
        switch (empty.Parent)
        {
            case IfStatementNode ifn:
                if (ifn.ElseBlock == empty && ifn.ElseToken != null)
                    return ifn.ElseToken.SourceSpan;
                if (ifn.ThenBlock == empty)
                    return ifn.Condition.SourceSpan;
                break;

            case EvaluateStatementNode ev:
                if (ev.WhenOtherBlock == empty && ev.WhenOtherToken != null)
                    return ev.WhenOtherToken.SourceSpan;
                foreach (var when in ev.WhenClauses)
                {
                    if (when.Body == empty)
                        return when.Condition.SourceSpan;
                }
                break;

            case CatchStatementNode catchNode:
                if (catchNode.ExceptionVariable != null)
                    return catchNode.ExceptionVariable.SourceSpan;
                if (catchNode.ExceptionType != null)
                    return catchNode.ExceptionType.SourceSpan;
                return SingleLocusSpan(catchNode);

            case ForStatementNode forNode:
                return SingleLocusSpan(forNode);
            case WhileStatementNode whileNode:
                return SingleLocusSpan(whileNode);
            case RepeatStatementNode repeatNode:
                return SingleLocusSpan(repeatNode);

            case MethodImplNode mi:
                return mi.NameToken.SourceSpan;

            case FunctionNode fn:
                return fn.NameToken.SourceSpan;

            case PropertyImplNode pi:
                return pi.NameToken.SourceSpan;
        }

        return empty.Parent != null ? SingleLocusSpan(empty.Parent) : null;
    }

    private static SourceSpan SignatureSpan(Token nameToken, TypeNode returnType) =>
        new SourceSpan(nameToken.SourceSpan.Start, returnType.SourceSpan.End);

    private static IReadOnlyList<SourceSpan> MethodPrimarySpans(MethodImplNode mi)
    {
        var spans = new List<SourceSpan>();

        // Class-header declaration: method Name(...) Returns T;
        if (mi.Declaration?.ReturnType != null)
        {
            var decl = mi.Declaration;
            if (decl.HeaderSpan.IsValid)
                spans.Add(decl.HeaderSpan);
            else
                spans.Add(SignatureSpan(decl.NameToken, decl.ReturnType));
        }

        // Implementation header closest to the body: "method Name" only.
        // Never span through /+ Returns ... +/ annotations — those look like noise
        // and are not part of the callable header the user edits as code.
        spans.Add(MethodImplHeaderSpan(mi));

        return spans;
    }

    /// <summary>
    /// Span covering METHOD keyword through the method name (excludes annotations).
    /// </summary>
    private static SourceSpan MethodImplHeaderSpan(MethodImplNode mi)
    {
        if (mi.FirstToken != null)
            return new SourceSpan(mi.FirstToken.SourceSpan.Start, mi.NameToken.SourceSpan.End);
        return mi.NameToken.SourceSpan;
    }

    /// <summary>
    /// Single-token or short locus. Prefer keyword end tokens (End-If) over ';' .
    /// </summary>
    private static SourceSpan SingleLocusSpan(AstNode node)
    {
        var last = node.LastToken;
        if (last != null && last.Type != TokenType.Semicolon)
            return last.SourceSpan;

        // Trailing ';' alone is nearly invisible — use FirstToken..LastToken of the
        // node without preferring semicolon, or the penultimate token if available.
        if (last != null && last.Type == TokenType.Semicolon && node.FirstToken != null
            && node.FirstToken != last)
        {
            // For simple statements like &z = 4; the whole statement is fine (short).
            // Prefer the full statement span over ';' alone when ByteLength of name is small.
            var full = node.SourceSpan;
            if (full.IsValid && full.ByteLength <= 80)
                return full;
        }

        if (last != null)
            return last.SourceSpan;

        return node.SourceSpan;
    }
}
