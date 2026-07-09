using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// For functions/methods/property getters with a return type, requires every path to
/// exit via Return, Throw, Exit, or Error (no Normal / Break / Continue on the body).
/// Emits primary diagnostic(s) on the signature (declaration + implementation for methods)
/// and one secondary on the innermost incomplete region (keyword introducer when empty).
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
                // Property type lives on the declaration; impl "get Name" is closest to the body.
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

        // Skip secondary only if it exactly duplicates every primary (unlikely).
        bool duplicatesPrimary = primarySpans.Any(p =>
            p.Start.ByteIndex == secondarySpan.Value.Start.ByteIndex
            && p.End.ByteIndex == secondarySpan.Value.End.ByteIndex);
        if (duplicatesPrimary)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.NotAllPathsReturn,
            DiagnosticSeverity.Error,
            secondarySpan.Value,
            "This block can complete without returning a value."));
    }

    private static bool IsComplete(ExitMode modes) =>
        modes != ExitMode.None && (modes & ~ValidExits) == ExitMode.None;

    private static bool IsIncomplete(ExitMode? modes) =>
        modes is null || !IsComplete(modes.Value);

    /// <summary>
    /// Innermost incomplete block, then a displayable span (keyword when the block is empty).
    /// </summary>
    private static SourceSpan? ResolveSecondarySpan(BlockNode root)
    {
        var best = FindBestIncompleteBlock(root);
        if (best == null)
            return null;

        // Straight-line body with statements: last statement is easiest to see.
        if (ReferenceEquals(best, root) && root.Statements.Count > 0)
            return root.Statements[^1].SourceSpan;

        if (best.Statements.Count > 0)
            return best.SourceSpan;

        // Empty incomplete block — no statements to underline. Point at the keyword
        // or clause that introduces the empty path (Else, When-Other, When condition, …).
        return SpanForEmptyIncompleteBlock(best) ?? best.SourceSpan;
    }

    private static BlockNode? FindBestIncompleteBlock(BlockNode root)
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
        return best;
    }

    /// <summary>
    /// For an empty incomplete block, style the introducer the user can see
    /// (Else / When-Other / When condition / catch header / parent statement).
    /// </summary>
    private static SourceSpan? SpanForEmptyIncompleteBlock(BlockNode empty)
    {
        switch (empty.Parent)
        {
            case IfStatementNode ifn:
                if (ifn.ElseBlock == empty && ifn.ElseToken != null)
                    return ifn.ElseToken.SourceSpan;
                // Empty Then: no dedicated Then token on the node — use the condition.
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
                return catchNode.SourceSpan;

            case ForStatementNode or WhileStatementNode or RepeatStatementNode:
                return empty.Parent.SourceSpan;

            case MethodImplNode mi:
                // Empty method body: style the implementation name (near the code).
                return mi.NameToken.SourceSpan;

            case FunctionNode fn:
                return fn.NameToken.SourceSpan;

            case PropertyImplNode pi:
                return pi.NameToken.SourceSpan;
        }

        return empty.Parent?.SourceSpan;
    }

    private static SourceSpan SignatureSpan(Token nameToken, TypeNode returnType) =>
        new SourceSpan(nameToken.SourceSpan.Start, returnType.SourceSpan.End);

    /// <summary>
    /// Primary spans for a method: class-header declaration (if present) and the
    /// implementation header closest to the body.
    /// </summary>
    private static IReadOnlyList<SourceSpan> MethodPrimarySpans(MethodImplNode mi)
    {
        var spans = new List<SourceSpan>();

        // Declaration in class header (e.g. method GetX() Returns number;)
        if (mi.Declaration?.ReturnType != null)
        {
            var decl = mi.Declaration;
            if (decl.HeaderSpan.IsValid)
                spans.Add(decl.HeaderSpan);
            else
                spans.Add(SignatureSpan(decl.NameToken, decl.ReturnType));
        }

        // Implementation header (method Name … / annotations) — nearest the code.
        if (mi.ReturnTypeAnnotation != null)
            spans.Add(SignatureSpan(mi.NameToken, mi.ReturnTypeAnnotation));
        else
            spans.Add(mi.NameToken.SourceSpan);

        return spans;
    }
}
