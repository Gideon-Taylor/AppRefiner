using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A semicolon closing a When condition is what separates the stacked-When idiom
/// (<c>When = 1</c>, which shares the next clause's block) from a real arm that matches
/// and runs nothing (<c>When = 1;</c>). The parser must keep it.
/// </summary>
public class EvaluateWhenTerminatorTests
{
    private static EvaluateStatementNode ParseEvaluate(string body)
    {
        var (program, errors) = Parse($"Function F()\n{body}\nEnd-Function;");
        Assert.Empty(errors);
        return program.FindDescendants<EvaluateStatementNode>().Single();
    }

    [Fact]
    public void BodylessWhen_HasNoTerminator()
    {
        var ev = ParseEvaluate("Evaluate &x\nWhen = 1\nWhen = 2\n&y = 1;\nEnd-Evaluate;");

        Assert.Null(ev.WhenClauses[0].ConditionTerminator);
        Assert.Empty(ev.WhenClauses[0].Body.Statements);
    }

    [Fact]
    public void TerminatedEmptyWhen_RecordsTheSemicolon()
    {
        var ev = ParseEvaluate("Evaluate &x\nWhen = 1;\nWhen = 2\n&y = 1;\nEnd-Evaluate;");

        Assert.NotNull(ev.WhenClauses[0].ConditionTerminator);
        Assert.Empty(ev.WhenClauses[0].Body.Statements);
    }

    [Fact]
    public void TerminatedWhen_StillBindsFollowingStatementsAsItsBody()
    {
        // Valid PeopleCode: the terminator is inert once statements follow.
        var ev = ParseEvaluate("Evaluate &x\nWhen = 1;\n&y = 1;\nEnd-Evaluate;");

        Assert.NotNull(ev.WhenClauses[0].ConditionTerminator);
        Assert.Single(ev.WhenClauses[0].Body.Statements);
    }

    [Fact]
    public void RepeatedSemicolons_RecordTheFirst()
    {
        var ev = ParseEvaluate("Evaluate &x\nWhen = 1;;;\nWhen = 2\n&y = 1;\nEnd-Evaluate;");

        Assert.NotNull(ev.WhenClauses[0].ConditionTerminator);
        Assert.Empty(ev.WhenClauses[0].Body.Statements);
    }
}
