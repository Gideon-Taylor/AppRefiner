using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests.Analysis;

public class CompletionAnalyzerTests
{
    [Fact]
    public void ExitMode_RoundTripsThroughAttributes()
    {
        var (program, _) = Parse("Function F()\nReturn;\nEnd-Function;");
        var body = program.Functions.Single().Body!;

        Assert.Null(body.GetExitMode());

        body.SetExitMode(ExitMode.Return | ExitMode.Normal);

        Assert.Equal(ExitMode.Return | ExitMode.Normal, body.GetExitMode());
    }

    // Wraps a body in a param-less function and analyzes the function body block.
    private static ExitMode AnalyzeBody(string body)
    {
        var (program, errors) = Parse($"Function F()\n{body}\nEnd-Function;");
        Assert.Empty(errors);
        return CompletionAnalyzer.Analyze(program.Functions.Single().Body!);
    }

    [Fact]
    public void Return_ExitsViaReturn_NotNormal()
        => Assert.Equal(ExitMode.Return, AnalyzeBody("Return;"));

    [Fact]
    public void Throw_ExitsViaThrow()
        => Assert.Equal(ExitMode.Throw, AnalyzeBody("Throw &e;"));

    [Fact]
    public void Exit_ExitsViaExit()
        => Assert.Equal(ExitMode.Exit, AnalyzeBody("Exit;"));

    [Fact]
    public void Error_ExitsViaError()
        => Assert.Equal(ExitMode.Error, AnalyzeBody("Error \"boom\";"));

    [Fact]
    public void PlainStatement_FallsThrough()
        => Assert.Equal(ExitMode.Normal, AnalyzeBody("&x = 1;"));

    [Fact]
    public void EmptyBody_FallsThrough()
        => Assert.Equal(ExitMode.Normal, AnalyzeBody(""));

    [Fact]
    public void IfWithBothBranchesReturn_DoesNotFallThrough()
        => Assert.Equal(ExitMode.Return,
            AnalyzeBody("If &b Then\nReturn;\nElse\nReturn;\nEnd-If;"));

    [Fact]
    public void IfWithOnlyThen_CanFallThrough()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal,
            AnalyzeBody("If &b Then\nReturn;\nEnd-If;"));

    [Fact]
    public void IfThenReturns_ElseFallsThrough_UnionsBoth()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal,
            AnalyzeBody("If &b Then\nReturn;\nElse\n&x = 1;\nEnd-If;"));

    [Fact]
    public void StatementAfterReturn_IsDeadButAnnotated()
    {
        var (program, _) = Parse("Function F()\nReturn;\n&x = 1;\nEnd-Function;");
        var body = program.Functions.Single().Body!;

        Assert.Equal(ExitMode.Return, CompletionAnalyzer.Analyze(body));
        // The dead statement is still visited and carries its own exit mode...
        Assert.Equal(ExitMode.Normal, body.Statements[1].GetExitMode());
        // ...but it did not add Normal to the block's set.
    }

    [Fact]
    public void EvaluateWithoutWhenOther_CanFallThrough()
        => Assert.True(AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nEnd-Evaluate;")
            .HasFlag(ExitMode.Normal));

    [Fact]
    public void EvaluateAllReturn_WithWhenOther_DoesNotFallThrough()
        => Assert.Equal(ExitMode.Return, AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nWhen = 2\nReturn;\nWhen-Other\nReturn;\nEnd-Evaluate;"));

    [Fact]
    public void EvaluateOneWhenFallsThrough_WithWhenOther_KeepsNormal()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal, AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nWhen = 2\n&y = 1;\nWhen-Other\nReturn;\nEnd-Evaluate;"));

    [Fact]
    public void BreakInWhen_AbsorbedIntoEvaluateNormal()
    {
        var mode = AnalyzeBody(
            "Evaluate &x\nWhen = 1\nBreak;\nWhen-Other\nBreak;\nEnd-Evaluate;");
        Assert.False(mode.HasFlag(ExitMode.Break)); // Break bound to the Evaluate, absorbed
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void Loop_AlwaysFallsThrough_AndAbsorbsBreak()
    {
        var mode = AnalyzeBody("While &x\nBreak;\nEnd-While;");
        Assert.False(mode.HasFlag(ExitMode.Break)); // Break bound to the loop, absorbed
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void Loop_AbsorbsContinue()
    {
        var mode = AnalyzeBody("While &x\nContinue;\nEnd-While;");
        Assert.False(mode.HasFlag(ExitMode.Continue));
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void ReturnInLoop_PropagatesReturn_ButStaysNormal()
    {
        var mode = AnalyzeBody("While &x\nReturn;\nEnd-While;");
        Assert.True(mode.HasFlag(ExitMode.Return)); // Return escapes the loop
        Assert.True(mode.HasFlag(ExitMode.Normal)); // loop may run zero times
    }

    [Fact]
    public void TryCatch_UnionsBothPaths()
    {
        var mode = AnalyzeBody(
            "try\nReturn;\ncatch Exception &e\n&x = 1;\nend-try;");
        Assert.True(mode.HasFlag(ExitMode.Return)); // try path returns
        Assert.True(mode.HasFlag(ExitMode.Normal)); // catch path falls through
    }

    [Fact]
    public void TryCatch_AnnotatesCatchClauseWithItsBodyExit()
    {
        var (program, errors) = Parse("Function F()\ntry\nReturn;\ncatch Exception &e\nReturn;\nend-try;\nEnd-Function;");
        Assert.Empty(errors);
        var body = program.Functions.Single().Body!;
        CompletionAnalyzer.Analyze(body);
        var catchNode = program.FindDescendants<CatchStatementNode>().Single();
        Assert.Equal(ExitMode.Return, catchNode.GetExitMode());
    }

    // The reported bug: every When returns, so no When body falls through.
    [Fact]
    public void MotivatingCase_AllWhensReturn_NoWhenBodyFallsThrough()
    {
        var source = """
            Function Calculate_File_Type(&Field As string) Returns string
               Evaluate &Field
                  When = "A"
                     Return "TC";
                  When = "B"
                     Return "UT";
                  When = "C"
                     Return "STL";
               End-Evaluate;
            End-Function;
            """;
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        var evaluate = program.FindDescendants<EvaluateStatementNode>().Single();
        CompletionAnalyzer.Analyze(program.Functions.Single().Body!);

        foreach (var whenClause in evaluate.WhenClauses)
            Assert.False(whenClause.Body.GetExitMode()!.Value.HasFlag(ExitMode.Normal));
    }

    // All branches return via a nested If — must NOT be seen as fall-through.
    [Fact]
    public void MotivatingCase_NestedIfBothBranchesReturn_DoesNotFallThrough()
        => Assert.False(AnalyzeBody("If &b Then\nReturn \"a\";\nElse\nReturn \"b\";\nEnd-If;")
            .HasFlag(ExitMode.Normal));

    // Trailing assignment after a one-armed If DOES fall through.
    [Fact]
    public void MotivatingCase_IfThenReturn_ThenAssignment_FallsThrough()
        => Assert.True(AnalyzeBody("If &b Then\nReturn \"a\";\nEnd-If;\n&thing = 3;")
            .HasFlag(ExitMode.Normal));
}
