using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// PC-1: per PeopleBooks, NOT binds looser than comparison and tighter than AND/OR:
/// unary -, **, * /, + -, relational/=, NOT, AND, OR.
/// `Not &a = 1` must parse as Not(&a = 1), not (Not &a) = 1.
/// </summary>
public class NotPrecedenceTests
{
    private static ExpressionNode IfCondition(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return program.FindDescendants<IfStatementNode>().First().Condition;
    }

    [Fact]
    public void NotBindsLooserThanEquality()
    {
        var condition = IfCondition("If Not &a = 1 Then\n   &x = 1;\nEnd-If;");

        var not = Assert.IsType<UnaryOperationNode>(condition);
        Assert.Equal(UnaryOperator.Not, not.Operator);
        var comparison = Assert.IsType<BinaryOperationNode>(not.Operand);
        Assert.Equal(BinaryOperator.Equal, comparison.Operator);
    }

    [Fact]
    public void DoubleNot_Parses()
    {
        var condition = IfCondition("If Not Not &a Then\n   &x = 1;\nEnd-If;");

        var outerNot = Assert.IsType<UnaryOperationNode>(condition);
        Assert.Equal(UnaryOperator.Not, outerNot.Operator);
        var innerNot = Assert.IsType<UnaryOperationNode>(outerNot.Operand);
        Assert.Equal(UnaryOperator.Not, innerNot.Operator);
    }

    [Fact]
    public void NotBindsTighterThanAnd()
    {
        var condition = IfCondition("If Not &a And &b Then\n   &x = 1;\nEnd-If;");

        var and = Assert.IsType<BinaryOperationNode>(condition);
        Assert.Equal(BinaryOperator.And, and.Operator);
        var not = Assert.IsType<UnaryOperationNode>(and.Left);
        Assert.Equal(UnaryOperator.Not, not.Operator);
    }

    [Fact]
    public void NotEqualOperatorForm_StillParses()
    {
        var condition = IfCondition("If &a Not = 1 Then\n   &x = 1;\nEnd-If;");

        var comparison = Assert.IsType<BinaryOperationNode>(condition);
        Assert.Equal(BinaryOperator.Equal, comparison.Operator);
        Assert.True(comparison.NotFlag);
    }

    [Fact]
    public void NotOverConcatenation_AppliesToWholeConcat()
    {
        var condition = IfCondition("If Not &a | &b Then\n   &x = 1;\nEnd-If;");

        var not = Assert.IsType<UnaryOperationNode>(condition);
        Assert.Equal(UnaryOperator.Not, not.Operator);
        var concat = Assert.IsType<BinaryOperationNode>(not.Operand);
        Assert.Equal(BinaryOperator.Concatenate, concat.Operator);
    }
}
