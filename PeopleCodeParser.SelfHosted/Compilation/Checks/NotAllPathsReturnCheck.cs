using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// For functions/methods/property getters with a return type, requires every path to
/// exit via Return, Throw, Exit, or Error (no Normal / Break / Continue on the body).
/// Emits a primary diagnostic on the signature and one secondary on the innermost
/// incomplete block.
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
                CheckBody(fn.Body, fn.Name, "function", SignatureSpan(fn.NameToken, fn.ReturnType), sink);
                break;

            case MethodImplNode mi when mi.Body != null:
            {
                if (mi.Declaration?.ReturnType == null && mi.ReturnTypeAnnotation == null)
                    break;
                CheckBody(mi.Body, mi.Name, "method", MethodSignatureSpan(mi), sink);
                break;
            }

            case PropertyImplNode pi when pi.IsGetter && pi.Body != null:
            {
                // Getter always returns the property type.
                var prop = pi.Parent as PropertyNode ?? pi.FindAncestor<PropertyNode>();
                if (prop == null)
                    break;
                CheckBody(pi.Body, pi.Name, "property getter", pi.NameToken.SourceSpan, sink);
                break;
            }
        }
    }

    private static void CheckBody(
        BlockNode body,
        string name,
        string kind,
        SourceSpan signatureSpan,
        IDiagnosticSink sink)
    {
        ExitMode modes = CompletionAnalyzer.Analyze(body);
        if (IsComplete(modes))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.NotAllPathsReturn,
            DiagnosticSeverity.Error,
            signatureSpan,
            $"Not all paths return a value in {kind} '{name}'."));

        var secondarySpan = FindBestIncompleteSpan(body) ?? body.SourceSpan;
        // Avoid duplicate identical span noise when secondary collapses to signature span.
        if (secondarySpan.Start.ByteIndex != signatureSpan.Start.ByteIndex
            || secondarySpan.End.ByteIndex != signatureSpan.End.ByteIndex)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.NotAllPathsReturn,
                DiagnosticSeverity.Error,
                secondarySpan,
                "This block can complete without returning a value."));
        }
    }

    private static bool IsComplete(ExitMode modes) =>
        modes != ExitMode.None && (modes & ~ValidExits) == ExitMode.None;

    private static bool IsIncomplete(ExitMode? modes) =>
        modes is null || !IsComplete(modes.Value);

    /// <summary>
    /// Innermost block that still has Normal or an invalid mode; tie-break earliest start.
    /// Falls back to last statement span of the root body.
    /// </summary>
    private static SourceSpan? FindBestIncompleteSpan(BlockNode root)
    {
        BlockNode? best = null;
        var bestDepth = -1;

        void Consider(BlockNode block, int depth)
        {
            var mode = block.GetExitMode();
            if (!IsIncomplete(mode))
                return;

            if (best == null
                || depth > bestDepth
                || (depth == bestDepth
                    && block.SourceSpan.Start.ByteIndex < best.SourceSpan.Start.ByteIndex))
            {
                best = block;
                bestDepth = depth;
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

        if (best == null)
            return null;

        // Straight-line body: prefer last statement for a visible in-body marker.
        if (ReferenceEquals(best, root) && root.Statements.Count > 0)
            return root.Statements[^1].SourceSpan;

        return best.SourceSpan;
    }

    private static SourceSpan SignatureSpan(Token nameToken, TypeNode returnType) =>
        new SourceSpan(nameToken.SourceSpan.Start, returnType.SourceSpan.End);

    /// <summary>
    /// Primary span for a method with a return type. Prefer the declaration header
    /// (class body) over combining impl name with declaration return type, which
    /// would invert Start/End because the return type appears earlier in the file.
    /// </summary>
    private static SourceSpan MethodSignatureSpan(MethodImplNode mi)
    {
        if (mi.Declaration?.ReturnType != null)
        {
            var decl = mi.Declaration;
            if (decl.HeaderSpan.IsValid)
                return decl.HeaderSpan;
            return SignatureSpan(decl.NameToken, decl.ReturnType);
        }

        if (mi.ReturnTypeAnnotation != null)
            return SignatureSpan(mi.NameToken, mi.ReturnTypeAnnotation);

        return mi.NameToken.SourceSpan;
    }
}
