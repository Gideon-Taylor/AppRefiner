using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// For functions/methods/property getters with a return type, requires every path to
/// exit via Return, Throw, Exit, or Error (no Normal / Break / Continue on the body).
/// Emits primary diagnostic(s) on the signature (declaration + implementation for methods)
/// and one secondary on the fall-through locus: last live body statement, or (when that
/// statement is a multi-way construct) its best incomplete arm.
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
                    new[] { FunctionSignatureSpan(fn) },
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
            "This path can complete without returning a value.",
            IsSecondary: true));
    }

    private static bool IsComplete(ExitMode modes) =>
        modes != ExitMode.None && (modes & ~ValidExits) == ExitMode.None;

    private static bool IsIncomplete(ExitMode? modes) =>
        modes is null || !IsComplete(modes.Value);

    /// <summary>
    /// Secondary locus policy:
    /// <list type="bullet">
    /// <item>Body has <see cref="ExitMode.Normal"/> (can fall off the routine): tip at the
    /// last live statement of the body. If that statement is a multi-way construct
    /// (If / Evaluate / Try) that itself has Normal, tip at its best incomplete arm
    /// instead of End-If / End-Evaluate. This avoids blaming a nested Break when the
    /// real fall-off is code after the construct.</item>
    /// <item>Otherwise (Break / Continue leaked without Normal): deepest incomplete
    /// region under the body (existing arm-walker scoring).</item>
    /// </list>
    /// Spans cover statement/introducer content only — never leading indentation.
    /// Multi-line statements tip on their last token (e.g. End-If).
    /// </summary>
    private static SourceSpan? ResolveSecondarySpan(BlockNode root)
    {
        ExitMode modes = root.GetExitMode() ?? ExitMode.None;

        if (modes.HasFlag(ExitMode.Normal))
        {
            if (root.Statements.Count == 0)
                return SpanForEmptyIncompleteBlock(root) ?? ContentSpan(root);

            StatementNode last = root.Statements[^1];

            // Last act is multi-way fall-through → prefer incomplete arm tip.
            if (last is IfStatementNode or EvaluateStatementNode or TryStatementNode
                && last.GetExitMode()?.HasFlag(ExitMode.Normal) == true)
            {
                var arm = FindBestIncompleteArm(last);
                if (arm != null)
                    return SpanForIncompleteRegion(arm);
            }

            // Straight-line tail (or multi-way with no useful arm, or loop, etc.).
            return ContentSpan(last);
        }

        // No Normal: body set is incomplete via Break/Continue (etc.).
        var best = FindBestIncompleteBlock(root);
        return best == null ? null : SpanForIncompleteRegion(best);
    }

    private static SourceSpan? SpanForIncompleteRegion(BlockNode block)
    {
        if (block.Statements.Count == 0)
            return SpanForEmptyIncompleteBlock(block) ?? ContentSpan(block);

        return ContentSpan(block.Statements[^1]);
    }

    /// <summary>
    /// Token-based content span: no leading indent. Multi-line nodes use the last token
    /// only (End-If / End-Evaluate / …) so we never paint an entire multi-statement range.
    /// </summary>
    private static SourceSpan ContentSpan(AstNode node)
    {
        if (node.LastToken != null
            && node.FirstToken != null
            && node.FirstToken.SourceSpan.Start.Line != node.LastToken.SourceSpan.End.Line)
        {
            return node.LastToken.SourceSpan;
        }

        if (node.SourceSpan.IsValid)
            return node.SourceSpan;

        if (node.LastToken != null)
            return node.LastToken.SourceSpan;

        return node.FirstToken?.SourceSpan ?? default;
    }

    /// <summary>
    /// Best incomplete arm under a multi-way statement (If / Evaluate / Try), including
    /// nested incomplete structure inside those arms. Does not consider the enclosing
    /// function body — only arms of <paramref name="multiWay"/>.
    /// </summary>
    private static BlockNode? FindBestIncompleteArm(StatementNode multiWay)
    {
        BlockNode? best = null;
        var bestDepth = -1;
        var bestHasSibling = false;

        void Consider(BlockNode block, int depth)
        {
            if (!IsIncomplete(block.GetExitMode()))
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

        void WalkBlock(BlockNode block, int depth)
        {
            Consider(block, depth);
            foreach (var stmt in block.Statements)
                WalkNested(stmt, depth + 1);
        }

        void WalkNested(StatementNode stmt, int depth)
        {
            switch (stmt)
            {
                case IfStatementNode ifn:
                    WalkBlock(ifn.ThenBlock, depth);
                    if (ifn.ElseBlock != null)
                        WalkBlock(ifn.ElseBlock, depth);
                    break;
                case EvaluateStatementNode ev:
                    foreach (var w in ev.WhenClauses)
                        WalkBlock(w.Body, depth);
                    if (ev.WhenOtherBlock != null)
                        WalkBlock(ev.WhenOtherBlock, depth);
                    break;
                case ForStatementNode f:
                    WalkBlock(f.Body, depth);
                    break;
                case WhileStatementNode w:
                    WalkBlock(w.Body, depth);
                    break;
                case RepeatStatementNode r:
                    WalkBlock(r.Body, depth);
                    break;
                case TryStatementNode t:
                    WalkBlock(t.TryBlock, depth);
                    foreach (var c in t.CatchClauses)
                        WalkBlock(c.Body, depth);
                    break;
                case BlockNode b:
                    WalkBlock(b, depth);
                    break;
            }
        }

        switch (multiWay)
        {
            case IfStatementNode ifn:
                WalkBlock(ifn.ThenBlock, 0);
                if (ifn.ElseBlock != null)
                    WalkBlock(ifn.ElseBlock, 0);
                break;
            case EvaluateStatementNode ev:
                foreach (var w in ev.WhenClauses)
                    WalkBlock(w.Body, 0);
                if (ev.WhenOtherBlock != null)
                    WalkBlock(ev.WhenOtherBlock, 0);
                break;
            case TryStatementNode t:
                WalkBlock(t.TryBlock, 0);
                foreach (var c in t.CatchClauses)
                    WalkBlock(c.Body, 0);
                break;
        }

        return best;
    }

    /// <summary>
    /// Full-body incomplete-region walk (used when the body leaks Break/Continue
    /// without Normal). Prefer complete-sibling arms, then deeper, then earlier start.
    /// </summary>
    private static BlockNode? FindBestIncompleteBlock(BlockNode root)
    {
        BlockNode? best = null;
        var bestDepth = -1;
        var bestHasSibling = false;

        void Consider(BlockNode block, int depth)
        {
            if (!IsIncomplete(block.GetExitMode()))
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

    /// <summary>
    /// Empty incomplete arm: content span of the introducer that caused the block to exist
    /// (Else / When … condition / If condition / Catch / loop header / routine name).
    /// Token-based — does not include leading indentation.
    /// </summary>
    private static SourceSpan? SpanForEmptyIncompleteBlock(BlockNode empty)
    {
        switch (empty.Parent)
        {
            case IfStatementNode ifn:
                if (ifn.ElseBlock == empty && ifn.ElseToken != null)
                    return ifn.ElseToken.SourceSpan;
                if (ifn.ThenBlock == empty)
                {
                    // If … condition (Then keyword may not be a stored token).
                    var ifStart = ifn.FirstToken?.SourceSpan.Start ?? ifn.Condition.SourceSpan.Start;
                    return new SourceSpan(ifStart, ifn.Condition.SourceSpan.End);
                }
                break;

            case EvaluateStatementNode ev:
                if (ev.WhenOtherBlock == empty && ev.WhenOtherToken != null)
                    return ev.WhenOtherToken.SourceSpan;
                foreach (var when in ev.WhenClauses)
                {
                    if (when.Body != empty)
                        continue;
                    // When keyword through end of condition (no indent).
                    if (when.WhenToken != null)
                        return new SourceSpan(
                            when.WhenToken.SourceSpan.Start,
                            when.Condition.SourceSpan.End);
                    return when.Condition.SourceSpan;
                }
                break;

            case CatchStatementNode catchNode:
                if (catchNode.ExceptionVariable != null)
                    return catchNode.ExceptionVariable.SourceSpan;
                if (catchNode.ExceptionType != null)
                    return catchNode.ExceptionType.SourceSpan;
                return ContentSpan(catchNode);

            case ForStatementNode forNode:
                return ContentSpan(forNode);
            case WhileStatementNode whileNode:
                return ContentSpan(whileNode);
            case RepeatStatementNode repeatNode:
                return ContentSpan(repeatNode);

            case MethodImplNode mi:
                return MethodImplHeaderSpan(mi);

            case FunctionNode fn:
                return fn.FirstToken != null
                    ? new SourceSpan(fn.FirstToken.SourceSpan.Start, fn.NameToken.SourceSpan.End)
                    : fn.NameToken.SourceSpan;

            case PropertyImplNode pi:
                return pi.NameToken.SourceSpan;
        }

        return empty.Parent != null ? ContentSpan(empty.Parent) : null;
    }

    /// <summary>
    /// Function keyword through return type (e.g. <c>Function Foo(...) Returns string</c>).
    /// </summary>
    private static SourceSpan FunctionSignatureSpan(FunctionNode fn)
    {
        var start = fn.FirstToken?.SourceSpan.Start ?? fn.NameToken.SourceSpan.Start;
        return new SourceSpan(start, fn.ReturnType!.SourceSpan.End);
    }

    private static SourceSpan MethodDeclarationSignatureSpan(MethodNode decl)
    {
        if (decl.HeaderSpan.IsValid)
            return decl.HeaderSpan;

        var start = decl.FirstToken?.SourceSpan.Start ?? decl.NameToken.SourceSpan.Start;
        if (decl.ReturnType != null)
            return new SourceSpan(start, decl.ReturnType.SourceSpan.End);
        return new SourceSpan(start, decl.NameToken.SourceSpan.End);
    }

    private static IReadOnlyList<SourceSpan> MethodPrimarySpans(MethodImplNode mi)
    {
        var spans = new List<SourceSpan>();

        // Class-header declaration: method Name(...) Returns T;
        if (mi.Declaration?.ReturnType != null)
            spans.Add(MethodDeclarationSignatureSpan(mi.Declaration));

        // Implementation header: METHOD keyword through method name.
        // Never span through /+ Returns ... +/ annotations.
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
}
