using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// ER-3: Repeat/While must keep their already-parsed bodies when the condition
/// fails to parse. ER-4: unterminated comments must produce a lex error.
/// ER-5: unterminated plain strings recover at end-of-line and report at the
/// opening quote (plain string literals are single-line in PeopleCode).
/// </summary>
public class RecoveryTailTests
{
    [Fact]
    public void RepeatMissingUntilCondition_KeepsBody()
    {
        var (program, errors) = Parse("Repeat\n&a = 1;\nUntil ;\n&c = 3;");

        Assert.NotEmpty(errors);
        var repeat = program.FindDescendants<RepeatStatementNode>().Single();
        Assert.Single(repeat.Body.Statements);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&c");
    }

    [Fact]
    public void WhileMissingCondition_KeepsBody()
    {
        var (program, errors) = Parse("While ;\n&a = 1;\nEnd-While;\n&c = 3;");

        Assert.NotEmpty(errors);
        var whileNode = program.FindDescendants<WhileStatementNode>().Single();
        Assert.Single(whileNode.Body.Statements);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&c");
    }

    [Theory]
    [InlineData("&x = 1;\n/* runaway comment")]
    [InlineData("&x = 1;\n<* outer <* inner *> still open")]
    [InlineData("&x = 1;\nREM no terminating semicolon")]
    public void UnterminatedComment_ReportsLexError(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        lexer.TokenizeAll();

        Assert.Contains(lexer.Errors, e => e.Message.Contains("Unterminated"));
    }

    [Fact]
    public void UnterminatedString_RecoversAtEndOfLine()
    {
        var (program, errors) = Parse("&x = \"abc\n&y = 1;");

        // The statement on the next line must survive the stray quote
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&y");
    }

    [Fact]
    public void UnterminatedString_ReportsAtOpeningQuote()
    {
        var source = "&x = \"abc\n&y = 1;";
        var lexer = new PeopleCodeLexer(source);
        lexer.TokenizeAll();

        var error = lexer.Errors.Single(e => e.Message.Contains("Unterminated string"));
        Assert.Equal(source.IndexOf('"'), error.Position.Index);
    }
}
