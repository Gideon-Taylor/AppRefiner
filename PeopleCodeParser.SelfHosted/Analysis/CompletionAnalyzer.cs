using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Analysis;

/// <summary>
/// Computes, for every statement and block in a subtree, the set of ways control can
/// leave it (<see cref="ExitMode"/>) — the classic normal-vs-abrupt completion /
/// reachability analysis, adapted to PeopleCode. Results are annotated onto each node
/// via <see cref="AstNodeExtensions.SetExitMode"/>.
///
/// Soundness: the analysis only ever OVER-approximates <see cref="ExitMode.Normal"/>,
/// so a consumer keying off Normal is never wrong in the dangerous direction.
/// <para>
/// <b>Loops:</b> For/While always include <see cref="ExitMode.Normal"/> (zero iterations
/// possible). Repeat always runs the body at least once, so it does <i>not</i> force
/// Normal — if every path through the body is abrupt (Return/Throw/Exit/Error) and there
/// is no Break/Continue-to-absorb, the Repeat itself has no Normal either.
/// </para>
/// </summary>
public static class CompletionAnalyzer
{
    /// <summary>
    /// Analyzes <paramref name="root"/>, annotating it and every descendant
    /// statement/block, and returns the root block's exit-mode set.
    /// </summary>
    public static ExitMode Analyze(BlockNode root) => AnalyzeBlock(root);

    private static ExitMode AnalyzeBlock(BlockNode block)
    {
        ExitMode result = ExitMode.None;
        bool reachable = true;

        foreach (var statement in block.Statements)
        {
            // Always visit so nested/dead structure is annotated too.
            ExitMode stmtModes = AnalyzeStatement(statement);
            if (!reachable)
                continue; // dead code: annotated, but does not affect the block

            // Abrupt modes escape the block; Normal only means "can reach next stmt".
            result |= (stmtModes & ~ExitMode.Normal);
            if (!stmtModes.HasFlag(ExitMode.Normal))
                reachable = false;
        }

        if (reachable)
            result |= ExitMode.Normal; // fell off the end

        block.SetExitMode(result);
        return result;
    }

    private static ExitMode AnalyzeStatement(StatementNode statement)
    {
        ExitMode modes = statement switch
        {
            ReturnStatementNode => ExitMode.Return,
            ThrowStatementNode => ExitMode.Throw,
            ExitStatementNode => ExitMode.Exit,
            ErrorStatementNode => ExitMode.Error,
            BreakStatementNode => ExitMode.Break,
            ContinueStatementNode => ExitMode.Continue,
            IfStatementNode ifNode => AnalyzeIf(ifNode),
            EvaluateStatementNode evalNode => AnalyzeEvaluate(evalNode),
            TryStatementNode tryNode => AnalyzeTry(tryNode),
            ForStatementNode forNode => AnalyzeForOrWhile(forNode.Body),
            WhileStatementNode whileNode => AnalyzeForOrWhile(whileNode.Body),
            RepeatStatementNode repeatNode => AnalyzeRepeat(repeatNode.Body),
            BlockNode block => AnalyzeBlock(block),
            _ => ExitMode.Normal, // plain statements fall through
        };

        statement.SetExitMode(modes);
        return modes;
    }

    private static ExitMode AnalyzeIf(IfStatementNode node)
    {
        ExitMode then = AnalyzeBlock(node.ThenBlock);
        if (node.ElseBlock == null)
            return then | ExitMode.Normal; // the untaken (no-else) path falls through
        return then | AnalyzeBlock(node.ElseBlock);
    }

    private static ExitMode AnalyzeEvaluate(EvaluateStatementNode node)
    {
        ExitMode union = ExitMode.None;
        foreach (var whenClause in node.WhenClauses)
            union |= AnalyzeBlock(whenClause.Body);

        if (node.WhenOtherBlock != null)
            union |= AnalyzeBlock(node.WhenOtherBlock);
        else
            union |= ExitMode.Normal; // unmatched scrutinee falls through the Evaluate

        // A Break in a When/When-Other body binds to THIS Evaluate: from the
        // Evaluate's perspective that is normal completion.
        return Absorb(union, ExitMode.Break);
    }

    private static ExitMode AnalyzeTry(TryStatementNode node)
    {
        ExitMode union = AnalyzeBlock(node.TryBlock);
        foreach (var catchClause in node.CatchClauses)
        {
            ExitMode catchModes = AnalyzeBlock(catchClause.Body);
            catchClause.SetExitMode(catchModes);
            union |= catchModes;
        }
        return union;
    }

    /// <summary>
    /// For / While: body may run zero times, so the loop always contributes Normal.
    /// Break/Continue bind to the loop and are absorbed into Normal.
    /// </summary>
    private static ExitMode AnalyzeForOrWhile(BlockNode body)
    {
        ExitMode inner = AnalyzeBlock(body);
        return Absorb(inner, ExitMode.Break | ExitMode.Continue) | ExitMode.Normal;
    }

    /// <summary>
    /// Repeat-Until: body always runs at least once. Do not force Normal.
    /// If the body can fall through to Until (or Break leaves the loop), Normal
    /// remains after Absorb; if every path is abrupt (Return/Throw/Exit/Error)
    /// with no Break, the Repeat has no Normal either.
    /// </summary>
    private static ExitMode AnalyzeRepeat(BlockNode body)
    {
        ExitMode inner = AnalyzeBlock(body);
        return Absorb(inner, ExitMode.Break | ExitMode.Continue);
    }

    /// <summary>
    /// Removes <paramref name="bound"/> modes from the set; if any were present, folds
    /// them into <see cref="ExitMode.Normal"/> (they bound to the construct being
    /// analyzed, so control resumes normally after it).
    /// </summary>
    private static ExitMode Absorb(ExitMode modes, ExitMode bound)
    {
        if ((modes & bound) != ExitMode.None)
            modes = (modes & ~bound) | ExitMode.Normal;
        return modes;
    }
}
