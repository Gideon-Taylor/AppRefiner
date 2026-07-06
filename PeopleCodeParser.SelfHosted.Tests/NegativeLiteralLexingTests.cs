using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// LX-1: '-' adjacent to a digit must lex as a Minus operator, not fuse into a
/// negative literal — `&x = &y-1;` is valid PeopleCode and must parse as subtraction.
/// </summary>
public class NegativeLiteralLexingTests
{
    [Fact]
    public void SubtractionWithoutSpaces_ParsesAsSubtraction()
    {
        var (program, errors) = Parse("&x = &y-1;");

        Assert.Empty(errors);
        var subtract = program.FindDescendants<BinaryOperationNode>().Single();
        Assert.Equal(BinaryOperator.Subtract, subtract.Operator);
    }

    [Fact]
    public void SubtractionWithSpaces_ParsesAsSubtraction()
    {
        var (program, errors) = Parse("&x = &y - 1;");

        Assert.Empty(errors);
        var subtract = program.FindDescendants<BinaryOperationNode>().Single();
        Assert.Equal(BinaryOperator.Subtract, subtract.Operator);
    }

    [Fact]
    public void NegativeLiteralAssignment_ParsesAsUnaryNegate()
    {
        var (program, errors) = Parse("&x = -1;");

        Assert.Empty(errors);
        var negate = program.FindDescendants<UnaryOperationNode>().Single();
        Assert.Equal(UnaryOperator.Negate, negate.Operator);
        var literal = Assert.IsType<LiteralNode>(negate.Operand);
        Assert.Equal("1", literal.Value?.ToString());
    }

    [Fact]
    public void NegativeConstantDeclaration_ParsesWithoutErrors()
    {
        var (program, errors) = Parse("Constant &NEG = -1;\n\n&x = &NEG;");

        Assert.Empty(errors);
        Assert.Contains(program.FindDescendants<LiteralNode>(),
            l => l.Value?.ToString() == "-1");
    }

    [Fact]
    public void NegativeDecimalConstantDeclaration_ParsesWithoutErrors()
    {
        var (program, errors) = Parse("Constant &NEG = -1.5;\n\n&x = &NEG;");

        Assert.Empty(errors);
        Assert.Contains(program.FindDescendants<LiteralNode>(),
            l => l.Value?.ToString() == "-1.5");
    }
}
