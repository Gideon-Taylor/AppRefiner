using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Inlines a single-assignment local variable: replaces every read with the
    /// initializer expression and deletes the declaration. Refuses when inlining
    /// would duplicate side effects or observe a stale value. Known limitation
    /// (accepted in the design spec): instance/global variables mutated
    /// indirectly by calls between the declaration and a read are not detected.
    /// </summary>
    public class InlineVariable : BaseRefactor
    {
        public new static string RefactorName => "Inline Variable";
        public new static string RefactorDescription => "Replaces reads of a single-assignment local variable with its initializer and removes the declaration";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RunOnIncompleteParse => false;

        private readonly List<LocalVariableDeclarationWithAssignmentNode> declarationNodes = new();

        public InlineVariable(ScintillaEditor editor) : base(editor) { }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            declarationNodes.Add(node);
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            GenerateInline();
        }

        protected override void OnReset()
        {
            declarationNodes.Clear();
        }

        private void GenerateInline()
        {
            // Same cursor-resolution approach as RenameLocalVariable
            VariableInfo? variable = null;
            foreach (var scope in GetAllScopes())
            {
                foreach (var candidate in GetVariablesInScope(scope))
                {
                    if (candidate.References.Any(r => r.SourceSpan.ContainsPosition(CurrentPosition)))
                    {
                        variable = candidate;
                        break;
                    }
                }
                if (variable != null) break;
            }

            if (variable == null)
            {
                SetFailure("No variable found at the cursor position.");
                return;
            }

            if (variable.Kind != VariableKind.Local)
            {
                SetFailure($"{variable.Name} is not a local variable — only locals can be inlined.");
                return;
            }

            var declNode = declarationNodes.FirstOrDefault(d =>
                string.Equals(d.VariableName, variable.Name, StringComparison.OrdinalIgnoreCase)
                && variable.References.Any(r => r.ReferenceType == ReferenceType.Declaration
                    && d.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex)));

            if (declNode == null)
            {
                SetFailure($"{variable.Name} is not declared with an initializer (Local <type> {variable.Name} = <value>;) — only such declarations can be inlined.");
                return;
            }

            // Exactly one write: the initializer. Writes inside the declaration span are
            // the initializer itself regardless of how the visitor classified it.
            var externalWrites = variable.References
                .Where(r => r.ReferenceType == ReferenceType.Write
                         && !declNode.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex))
                .ToList();
            if (externalWrites.Count > 0)
            {
                SetFailure($"{variable.Name} is assigned {externalWrites.Count} more time(s) after its declaration — only single-assignment variables can be inlined.");
                return;
            }

            var reads = variable.References
                .Where(r => r.ReferenceType == ReferenceType.Read
                         && !declNode.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex))
                .OrderBy(r => r.SourceSpan.Start.ByteIndex)
                .ToList();
            if (reads.Count == 0)
            {
                SetFailure($"{variable.Name} is never read — use the Delete Unused Variable quick fix instead.");
                return;
            }

            // Safety 1: side-effect duplication
            bool initializerHasCalls = declNode.InitialValue.HasSideEffects
                || declNode.InitialValue is FunctionCallNode or ObjectCreationNode or ObjectCreateShortHand
                || declNode.InitialValue.FindDescendants<FunctionCallNode>().Any()
                || declNode.InitialValue.FindDescendants<ObjectCreationNode>().Any()
                || declNode.InitialValue.FindDescendants<ObjectCreateShortHand>().Any();
            if (initializerHasCalls && reads.Count > 1)
            {
                SetFailure($"The initializer of {variable.Name} calls a function and the variable is read {reads.Count} times — inlining would evaluate it {reads.Count} times.");
                return;
            }

            // Safety 2: stale value — variables referenced in the initializer must not be
            // written between the declaration and the last read
            int lastReadStart = reads[^1].SourceSpan.Start.ByteIndex;
            var initIdentifiers = declNode.InitialValue.FindDescendants<IdentifierNode>().ToList();
            if (declNode.InitialValue is IdentifierNode selfIdentifier)
                initIdentifiers.Add(selfIdentifier);

            if (initIdentifiers.Any(i => string.Equals(i.Name, variable.Name, StringComparison.OrdinalIgnoreCase)))
            {
                SetFailure($"The initializer of {variable.Name} references {variable.Name} itself — inlining would produce invalid code.");
                return;
            }

            foreach (var ident in initIdentifiers)
            {
                if (ident.IdentifierType != IdentifierType.UserVariable)
                    continue;

                var referenced = FindVariableByReferencePosition(ident.SourceSpan.Start.ByteIndex);
                if (referenced == null)
                    continue;

                bool mutated = referenced.References.Any(r => r.ReferenceType == ReferenceType.Write
                    && r.SourceSpan.Start.ByteIndex >= declNode.SourceSpan.End.ByteIndex
                    && r.SourceSpan.Start.ByteIndex < lastReadStart);
                if (mutated)
                {
                    SetFailure($"{referenced.Name} is reassigned between the declaration and a use of {variable.Name} — inlining would change the value observed.");
                    return;
                }
            }

            GenerateEdits(declNode, reads);
        }

        private VariableInfo? FindVariableByReferencePosition(int byteIndex)
        {
            foreach (var scope in GetAllScopes())
                foreach (var v in GetVariablesInScope(scope))
                    if (v.References.Any(r => r.SourceSpan.Start.ByteIndex == byteIndex))
                        return v;
            return null;
        }

        private void GenerateEdits(LocalVariableDeclarationWithAssignmentNode declNode, List<VariableReference> reads)
        {
            string initText = GetSourceText(declNode.InitialValue.SourceSpan);
            if (NeedsParentheses(declNode.InitialValue))
                initText = "(" + initText + ")";

            foreach (var read in reads)
            {
                EditText(read.SourceSpan.Start.ByteIndex, read.SourceSpan.End.ByteIndex,
                    initText, $"Inline {declNode.VariableName}");
            }

            DeleteDeclaration(declNode);
        }

        /// <summary>
        /// Atomic expressions never need wrapping; anything with an operator does,
        /// so precedence in the surrounding context is preserved.
        /// </summary>
        private static bool NeedsParentheses(ExpressionNode initializer) => initializer switch
        {
            LiteralNode or IdentifierNode or MemberAccessNode or PropertyAccessNode
                or ArrayAccessNode or FunctionCallNode or ParenthesizedExpressionNode
                or ObjectCreationNode or ObjectCreateShortHand or ClassConstantNode
                or MetadataExpressionNode => false,
            _ => true
        };

        /// <summary>
        /// Deletes the declaration statement plus its semicolon; when the statement is
        /// alone on its line, the whole line goes (including the line break).
        /// </summary>
        private void DeleteDeclaration(LocalVariableDeclarationWithAssignmentNode declNode)
        {
            int delStart = declNode.SourceSpan.Start.ByteIndex;
            int delEnd = declNode.SourceSpan.End.ByteIndex;

            // Consume trailing whitespace + semicolon
            while (delEnd < SourceBytes.Length
                && (SourceBytes[delEnd] == (byte)' ' || SourceBytes[delEnd] == (byte)'\t'))
                delEnd++;
            if (delEnd < SourceBytes.Length && SourceBytes[delEnd] == (byte)';')
                delEnd++;

            int lineStart = delStart;
            while (lineStart > 0 && SourceBytes[lineStart - 1] != (byte)'\n')
                lineStart--;

            bool onlyIndentBefore = true;
            for (int i = lineStart; i < delStart; i++)
            {
                if (SourceBytes[i] != (byte)' ' && SourceBytes[i] != (byte)'\t')
                {
                    onlyIndentBefore = false;
                    break;
                }
            }

            int afterEnd = delEnd;
            while (afterEnd < SourceBytes.Length
                && (SourceBytes[afterEnd] == (byte)' ' || SourceBytes[afterEnd] == (byte)'\t'))
                afterEnd++;
            bool nothingAfter = afterEnd >= SourceBytes.Length
                || SourceBytes[afterEnd] == (byte)'\r' || SourceBytes[afterEnd] == (byte)'\n';

            if (onlyIndentBefore && nothingAfter)
            {
                // Whole line: include trailing EOL
                int lineEnd = afterEnd;
                if (lineEnd < SourceBytes.Length && SourceBytes[lineEnd] == (byte)'\r') lineEnd++;
                if (lineEnd < SourceBytes.Length && SourceBytes[lineEnd] == (byte)'\n') lineEnd++;
                DeleteText(lineStart, lineEnd, $"Remove declaration of {declNode.VariableName}");
            }
            else
            {
                DeleteText(delStart, delEnd, $"Remove declaration of {declNode.VariableName}");
            }
        }
    }
}
