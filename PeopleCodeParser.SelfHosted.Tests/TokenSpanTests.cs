using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// SP-1: token-less nodes reported spans at (0,0), sending span-based tools to
/// document offset 0. SP-2: Exit spans must include the exit code.
/// SP-3: astral-plane characters must yield one Invalid token, not two.
/// </summary>
public class TokenSpanTests
{
    [Fact]
    public void ForIteratorVariable_HasRealSpan()
    {
        var source = "For &i = 1 To 3\n   &x = &i;\nEnd-For;";
        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var forNode = program.FindDescendants<ForStatementNode>().Single();
        Assert.Equal(source.IndexOf("&i"), forNode.Iterator.SourceSpan.Start.Index);
    }

    [Fact]
    public void ForRecordFieldIterator_HasRealSpan()
    {
        var source = "For REC.FLD = 1 To 3\n   &x = 1;\nEnd-For;";
        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var forNode = program.FindDescendants<ForStatementNode>().Single();
        Assert.Equal(source.IndexOf("REC"), forNode.Iterator.SourceSpan.Start.Index);
    }

    [Fact]
    public void CatchExceptionType_HasRealSpan()
    {
        var source = "Try\n   &z = 1;\nCatch Exception &ex\n   &a = 1;\nEnd-Try;";
        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var catchNode = program.FindDescendants<CatchStatementNode>().Single();
        Assert.Equal(source.IndexOf("Exception"), catchNode.ExceptionType!.SourceSpan.Start.Index);
    }

    [Fact]
    public void ExitCode_IsIncludedInExitSpan()
    {
        var source = "Exit 1;";
        var (program, errors) = Parse(source);

        Assert.Empty(errors);
        var exit = program.FindDescendants<ExitStatementNode>().Single();
        Assert.NotNull(exit.ExitCode);
        Assert.True(exit.SourceSpan.End.Index > source.IndexOf('1'));
    }

    [Fact]
    public void AstralPlaneCharacter_ProducesSingleInvalidToken()
    {
        var lexer = new PeopleCodeLexer("\U0001F600");
        var tokens = lexer.TokenizeAll();

        Assert.Equal(1, tokens.Count(t => t.Type == TokenType.Invalid));
    }
}
