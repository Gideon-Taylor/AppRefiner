using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using ParserImpl = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// CR-2: FindDescendants must not recurse per AST level — a deeply nested
/// tree would overflow the stack via nested iterator frames.
/// </summary>
public class FindDescendantsTests
{
    private static ProgramNode Parse(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserImpl(tokens);
        return parser.ParseProgram();
    }

    [Fact]
    public void VeryDeepTree_DoesNotOverflowStack()
    {
        ExpressionNode expr = new IdentifierNode("&leaf", IdentifierType.UserVariable);
        for (int i = 0; i < 100_000; i++)
        {
            expr = new ParenthesizedExpressionNode(expr);
        }

        var identifiers = expr.FindDescendants<IdentifierNode>().ToList();

        Assert.Single(identifiers);
        Assert.Equal("&leaf", identifiers[0].Name);
    }

    [Fact]
    public void ReturnsMatchesInPreOrder()
    {
        var program = Parse("&x = (1 + 2);");

        var literals = program.FindDescendants<LiteralNode>()
            .Select(l => l.Value?.ToString())
            .ToList();

        Assert.Equal(new[] { "1", "2" }, literals);
    }

    [Fact]
    public void ExcludesSelfFromResults()
    {
        var program = Parse("&x = (1 + 2);");
        var paren = program.FindDescendants<ParenthesizedExpressionNode>().Single();

        Assert.Empty(paren.FindDescendants<ParenthesizedExpressionNode>());
    }
}
