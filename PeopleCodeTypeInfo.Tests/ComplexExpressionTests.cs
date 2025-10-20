using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for complex expression type inference including:
/// - Function calls with array returns (Split)
/// - Array access with index
/// - Pipe operator (string concatenation)
/// - Parenthesized expressions
/// - Arithmetic expressions
/// </summary>
public class ComplexExpressionTests
{
    /// <summary>
    /// Test: local string &helloWorld = "Hello World";
    ///       local any &newValue = (Split(&helloWorld," ")[1] | " Dave");
    ///
    /// Verifies:
    /// - Split() returns array of string
    /// - Split()[1] returns string (array access reduces dimensionality)
    /// - Split()[1] | " Dave" returns string (pipe concatenation)
    /// - Parenthesized expression returns string
    /// </summary>
    [Fact]
    public void ComplexExpression_SplitWithArrayAccessAndPipe_InfersStringType()
    {
        var source = @"
function test();
   local string &helloWorld;
   local any &newValue;

   &helloWorld = ""Hello World"";
   &newValue = (Split(&helloWorld, "" "")[1] | "" Dave"");
end-function;
";

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        // Find the assignment statement: &newValue = (Split(&helloWorld, " ")[1] | " Dave")
        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        var assignment = assignments.FirstOrDefault(a =>
            a.Target is IdentifierNode id && id.Name.Equals("&newValue", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(assignment);

        // The value expression is: (Split(&helloWorld, " ")[1] | " Dave")
        var parenthesizedExpr = assignment.Value as ParenthesizedExpressionNode;
        Assert.NotNull(parenthesizedExpr);

        // Inner expression is: Split(&helloWorld, " ")[1] | " Dave"
        var pipeExpr = parenthesizedExpr.Expression as BinaryOperationNode;
        Assert.NotNull(pipeExpr);
        Assert.Equal(BinaryOperator.Concatenate, pipeExpr.Operator);

        // Left side of pipe is: Split(&helloWorld, " ")[1]
        var arrayAccessExpr = pipeExpr.Left as ArrayAccessNode;
        Assert.NotNull(arrayAccessExpr);

        // Array expression is: Split(&helloWorld, " ")
        var splitCall = arrayAccessExpr.Array as FunctionCallNode;
        Assert.NotNull(splitCall);

        // Test 1: Split() should return array of string
        var splitType = visitor.GetInferredType(splitCall);
        Assert.NotNull(splitType);
        Assert.IsType<ArrayTypeInfo>(splitType);
        var splitArrayType = (ArrayTypeInfo)splitType;
        Assert.Equal(1, splitArrayType.Dimensions);
        Assert.Equal(PeopleCodeType.String, splitArrayType.ElementType.PeopleCodeType);

        // Test 2: Split()[1] should return string (array access reduces dimensionality)
        var arrayAccessType = visitor.GetInferredType(arrayAccessExpr);
        Assert.NotNull(arrayAccessType);
        Assert.Equal(PeopleCodeType.String, arrayAccessType.PeopleCodeType);

        // Test 3: Split()[1] | " Dave" should return string (pipe concatenation)
        var pipeType = visitor.GetInferredType(pipeExpr);
        Assert.NotNull(pipeType);
        Assert.Equal(PeopleCodeType.String, pipeType.PeopleCodeType);

        // Test 4: Entire parenthesized expression should return string
        var parenthesizedType = visitor.GetInferredType(parenthesizedExpr);
        Assert.NotNull(parenthesizedType);
        Assert.Equal(PeopleCodeType.String, parenthesizedType.PeopleCodeType);
    }

    /// <summary>
    /// Test: local any &test = (Len(&newValue) + 3) * 2
    ///
    /// Verifies:
    /// - Len() returns number (Integer normalized to Number during type inference)
    /// - Len() + 3 returns number
    /// - (Len() + 3) * 2 returns number
    /// - Parenthesized expression returns number
    ///
    /// Note: PeopleCode doesn't meaningfully distinguish Integer and Number at runtime,
    /// so type inference normalizes all Integer types to Number.
    /// </summary>
    [Fact]
    public void ComplexExpression_LenWithArithmeticOperations_InfersNumberType()
    {
        var source = @"
function test();
   local string &newValue;
   local any &test;

   &newValue = ""Hello"";
   &test = (Len(&newValue) + 3) * 2;
end-function;
";

        // Parse the source
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        // Extract metadata
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");

        // Run type inference
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        // Find the assignment statement: &test = (Len(&newValue) + 3) * 2
        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        var assignment = assignments.FirstOrDefault(a =>
            a.Target is IdentifierNode id && id.Name.Equals("&test", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(assignment);

        // The value expression is: (Len(&newValue) + 3) * 2
        var multiplyExpr = assignment.Value as BinaryOperationNode;
        Assert.NotNull(multiplyExpr);
        Assert.Equal(BinaryOperator.Multiply, multiplyExpr.Operator);

        // Left side of multiply is: (Len(&newValue) + 3)
        var parenthesizedExpr = multiplyExpr.Left as ParenthesizedExpressionNode;
        Assert.NotNull(parenthesizedExpr);

        // Inner expression is: Len(&newValue) + 3
        var addExpr = parenthesizedExpr.Expression as BinaryOperationNode;
        Assert.NotNull(addExpr);
        Assert.Equal(BinaryOperator.Add, addExpr.Operator);

        // Left side of add is: Len(&newValue)
        var lenCall = addExpr.Left as FunctionCallNode;
        Assert.NotNull(lenCall);

        // Test 1: Len() should return number (Integer normalized to Number)
        var lenType = visitor.GetInferredType(lenCall);
        Assert.NotNull(lenType);
        Assert.Equal(PeopleCodeType.Number, lenType.PeopleCodeType);

        // Test 2: Len(&newValue) + 3 should return number
        var addType = visitor.GetInferredType(addExpr);
        Assert.NotNull(addType);
        Assert.Equal(PeopleCodeType.Number, addType.PeopleCodeType);

        // Test 3: (Len(&newValue) + 3) should return number (parenthesized)
        var parenthesizedType = visitor.GetInferredType(parenthesizedExpr);
        Assert.NotNull(parenthesizedType);
        Assert.Equal(PeopleCodeType.Number, parenthesizedType.PeopleCodeType);

        // Test 4: (Len(&newValue) + 3) * 2 should return number
        var multiplyType = visitor.GetInferredType(multiplyExpr);
        Assert.NotNull(multiplyType);
        Assert.Equal(PeopleCodeType.Number, multiplyType.PeopleCodeType);
    }

    /// <summary>
    /// Test simple pipe operator: "Hello" | " World"
    /// </summary>
    [Fact]
    public void PipeOperator_TwoStrings_InfersStringType()
    {
        var source = @"
function test();
   local any &result;
   &result = ""Hello"" | "" World"";
end-function;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        var assignment = assignments.First();

        var pipeExpr = assignment.Value as BinaryOperationNode;
        Assert.NotNull(pipeExpr);
        Assert.Equal(BinaryOperator.Concatenate, pipeExpr.Operator);

        var pipeType = visitor.GetInferredType(pipeExpr);
        Assert.NotNull(pipeType);
        Assert.Equal(PeopleCodeType.String, pipeType.PeopleCodeType);
    }

    /// <summary>
    /// Test arithmetic type promotion: integer + integer = number
    /// (Integer literals are normalized to Number during type inference)
    /// </summary>
    [Fact]
    public void ArithmeticOperator_IntegerPlusInteger_InfersNumberType()
    {
        var source = @"
function test();
   local any &result;
   &result = 5 + 10;
end-function;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        var assignment = assignments.First();

        var addExpr = assignment.Value as BinaryOperationNode;
        Assert.NotNull(addExpr);
        Assert.Equal(BinaryOperator.Add, addExpr.Operator);

        var addType = visitor.GetInferredType(addExpr);
        Assert.NotNull(addType);
        Assert.Equal(PeopleCodeType.Number, addType.PeopleCodeType);
    }

    /// <summary>
    /// Test arithmetic type promotion: number * integer = number
    /// </summary>
    [Fact]
    public void ArithmeticOperator_NumberTimesInteger_InfersNumberType()
    {
        var source = @"
function test();
   local any &result;
   &result = 5.5 * 2;
end-function;
";

        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, cache);

        var function = program.Functions[0];
        var assignments = FindAllAssignments(function);
        var assignment = assignments.First();

        var multiplyExpr = assignment.Value as BinaryOperationNode;
        Assert.NotNull(multiplyExpr);
        Assert.Equal(BinaryOperator.Multiply, multiplyExpr.Operator);

        var multiplyType = visitor.GetInferredType(multiplyExpr);
        Assert.NotNull(multiplyType);
        Assert.Equal(PeopleCodeType.Number, multiplyType.PeopleCodeType);
    }

    // Helper method to find all assignments in a function
    private static List<AssignmentNode> FindAllAssignments(FunctionNode function)
    {
        var collector = new AssignmentCollector();
        function.Accept(collector);
        return collector.Assignments;
    }

    // Helper visitor to collect all assignment nodes
    private class AssignmentCollector : AstVisitorBase
    {
        public List<AssignmentNode> Assignments { get; } = new();

        public override void VisitAssignment(AssignmentNode node)
        {
            Assignments.Add(node);
            base.VisitAssignment(node);
        }
    }
}
