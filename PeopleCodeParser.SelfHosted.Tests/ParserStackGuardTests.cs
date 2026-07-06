using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using ParserImpl = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// CR-1: pathologically deep nesting must produce a parse error, never a
/// StackOverflowException (which is uncatchable and kills the host process).
/// </summary>
public class ParserStackGuardTests
{
    private static (ProgramNode Program, IReadOnlyList<ParseError> Errors) Parse(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserImpl(tokens);
        var program = parser.ParseProgram();
        return (program, parser.Errors);
    }

    [Fact]
    public void DeeplyNestedParentheses_ReportsErrorInsteadOfCrashing()
    {
        var source = "&x = " + new string('(', 100_000) + "1;";

        var (program, errors) = Parse(source);

        Assert.NotNull(program);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void DeeplyNestedIfStatements_ReportsErrorInsteadOfCrashing()
    {
        var depth = 50_000;
        var source =
            string.Concat(Enumerable.Repeat("If True Then\n", depth)) +
            string.Concat(Enumerable.Repeat("End-If;\n", depth));

        var (program, errors) = Parse(source);

        Assert.NotNull(program);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void DeeplyNestedUnaryMinus_ReportsErrorInsteadOfCrashing()
    {
        var source = "&x = " + new string('-', 100_000) + "1;";

        var (program, errors) = Parse(source);

        Assert.NotNull(program);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ModeratelyNestedParentheses_ParsesWithoutErrors()
    {
        var source = "&x = " + new string('(', 64) + "1" + new string(')', 64) + ";";

        var (program, errors) = Parse(source);

        Assert.NotNull(program);
        Assert.Empty(errors);
    }

    [Fact]
    public void ModeratelyNestedIfStatements_ParseWithoutErrors()
    {
        var depth = 40;
        var source =
            string.Concat(Enumerable.Repeat("If True Then\n", depth)) +
            "&x = 1;\n" +
            string.Concat(Enumerable.Repeat("End-If;\n", depth));

        var (program, errors) = Parse(source);

        Assert.NotNull(program);
        Assert.Empty(errors);
    }
}
