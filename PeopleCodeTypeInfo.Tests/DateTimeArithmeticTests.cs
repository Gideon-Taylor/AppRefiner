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
/// Tests for date/time arithmetic operations in the TypeInferenceVisitor
/// </summary>
public class DateTimeArithmeticTests
{
    /// <summary>
    /// Test: time + number => time
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_TimePlusNumber_InfersTimeType()
    {
        var source = @"
function test();
   local time &t;
   local number &n;
   local any &result;
   &t = %Time;
   &n = 3600;
   &result = &t + &n;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Time, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: number + time => time (commutative)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_NumberPlusTime_InfersTimeType()
    {
        var source = @"
function test();
   local time &t;
   local number &n;
   local any &result;
   &t = %Time;
   &n = 3600;
   &result = &n + &t;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Time, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: time - number => time
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_TimeMinusNumber_InfersTimeType()
    {
        var source = @"
function test();
   local time &t;
   local number &n;
   local any &result;
   &t = %Time;
   &n = 3600;
   &result = &t - &n;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Time, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: date + number => date
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DatePlusNumber_InfersDateType()
    {
        var source = @"
function test();
   local date &d;
   local number &n;
   local any &result;
   &d = %Date;
   &n = 7;
   &result = &d + &n;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Date, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: number + date => date (commutative)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_NumberPlusDate_InfersDateType()
    {
        var source = @"
function test();
   local date &d;
   local number &n;
   local any &result;
   &d = %Date;
   &n = 7;
   &result = &n + &d;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Date, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: date - number => date
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateMinusNumber_InfersDateType()
    {
        var source = @"
function test();
   local date &d;
   local number &n;
   local any &result;
   &d = %Date;
   &n = 7;
   &result = &d - &n;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Date, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: date - date => number
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateMinusDate_InfersNumberType()
    {
        var source = @"
function test();
   local date &d1;
   local date &d2;
   local any &result;
   &d1 = %Date;
   &d2 = %Date;
   &result = &d1 - &d2;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: time - time => number
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_TimeMinusTime_InfersNumberType()
    {
        var source = @"
function test();
   local time &t1;
   local time &t2;
   local any &result;
   &t1 = %Time;
   &t2 = %Time;
   &result = &t1 - &t2;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: date + time => datetime
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DatePlusTime_InfersDateTimeType()
    {
        var source = @"
function test();
   local date &d;
   local time &t;
   local any &result;
   &d = %Date;
   &t = %Time;
   &result = &d + &t;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.DateTime, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: time + date => datetime (commutative)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_TimePlusDate_InfersDateTimeType()
    {
        var source = @"
function test();
   local date &d;
   local time &t;
   local any &result;
   &d = %Date;
   &t = %Time;
   &result = &t + &d;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.DateTime, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: datetime - datetime => number
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimeMinusDateTime_InfersNumberType()
    {
        var source = @"
function test();
   local datetime &dt1;
   local datetime &dt2;
   local any &result;
   &dt1 = %DateTime;
   &dt2 = %DateTime;
   &result = &dt1 - &dt2;
end-function;
";

        var (visitor, binaryOp) = ParseAndGetBinaryOp(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.Number, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: datetime - time => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimeMinusTime_GeneratesTypeError()
    {
        var source = @"
function test();
   local datetime &dt;
   local time &t;
   local any &result;
   &dt = %DateTime;
   &t = %Time;
   &result = &dt - &t;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);

        // Verify type error was generated
        var errors = program.GetAllTypeErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("Cannot subtract time from datetime", errors[0].Message);
    }

    /// <summary>
    /// Test: time - datetime => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_TimeMinusDateTime_GeneratesTypeError()
    {
        var source = @"
function test();
   local datetime &dt;
   local time &t;
   local any &result;
   &dt = %DateTime;
   &t = %Time;
   &result = &t - &dt;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);

        var errors = program.GetAllTypeErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("Cannot subtract datetime from time", errors[0].Message);
    }

    /// <summary>
    /// Test: datetime + datetime => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimePlusDateTime_GeneratesTypeError()
    {
        var source = @"
function test();
   local datetime &dt1;
   local datetime &dt2;
   local any &result;
   &dt1 = %DateTime;
   &dt2 = %DateTime;
   &result = &dt1 + &dt2;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);

        var errors = program.GetAllTypeErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("Cannot add datetime to datetime", errors[0].Message);
    }

    /// <summary>
    /// Test: datetime + time => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimePlusTime_GeneratesTypeError()
    {
        var source = @"
function test();
   local datetime &dt;
   local time &t;
   local any &result;
   &dt = %DateTime;
   &t = %Time;
   &result = &dt + &t;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);

        var errors = program.GetAllTypeErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("Cannot add time to datetime", errors[0].Message);
    }

    /// <summary>
    /// Test: datetime + number => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimePlusNumber_InfersDateTime()
    {
        var source = @"
function test();
   local datetime &dt;
   local number &n;
   local any &result;
   &dt = %DateTime;
   &n = 3600;
   &result = &dt + &n;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Add);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.DateTime, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: datetime - number => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateTimeMinusNumber_InfersDateTime()
    {
        var source = @"
function test();
   local datetime &dt;
   local number &n;
   local any &result;
   &dt = %DateTime;
   &n = 3600;
   &result = &dt - &n;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(PeopleCodeType.DateTime, inferredType.PeopleCodeType);
    }

    /// <summary>
    /// Test: date - time => ERROR (not allowed)
    /// </summary>
    [Fact]
    public void DateTimeArithmetic_DateMinusTime_GeneratesTypeError()
    {
        var source = @"
function test();
   local date &d;
   local time &t;
   local any &result;
   &d = %Date;
   &t = %Time;
   &result = &d - &t;
end-function;
";

        var (visitor, binaryOp, program) = ParseAndGetBinaryOpWithProgram(source, BinaryOperator.Subtract);

        var inferredType = visitor.GetInferredType(binaryOp);
        Assert.NotNull(inferredType);
        Assert.Equal(TypeKind.Unknown, inferredType.Kind);

        var errors = program.GetAllTypeErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("Cannot subtract time from date", errors[0].Message);
    }

    // Helper methods

    private static (TypeInferenceVisitor visitor, BinaryOperationNode binaryOp) ParseAndGetBinaryOp(
        string source, BinaryOperator expectedOperator)
    {
        var (visitor, binaryOp, _) = ParseAndGetBinaryOpWithProgram(source, expectedOperator);
        return (visitor, binaryOp);
    }

    private static (TypeInferenceVisitor visitor, BinaryOperationNode binaryOp, ProgramNode program)
        ParseAndGetBinaryOpWithProgram(string source, BinaryOperator expectedOperator)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "TestProgram");
        var cache = new TypeCache();
        var visitor = TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        var function = program.Functions[0];
        var binaryOp = FindBinaryOperation(function, expectedOperator);

        Assert.NotNull(binaryOp);

        return (visitor, binaryOp, program);
    }

    private static BinaryOperationNode? FindBinaryOperation(FunctionNode function, BinaryOperator op)
    {
        var finder = new BinaryOpFinder(op);
        function.Accept(finder);
        return finder.FoundOp;
    }

    private class BinaryOpFinder : AstVisitorBase
    {
        private readonly BinaryOperator _targetOp;
        public BinaryOperationNode? FoundOp { get; private set; }

        public BinaryOpFinder(BinaryOperator targetOp)
        {
            _targetOp = targetOp;
        }

        public override void VisitBinaryOperation(BinaryOperationNode node)
        {
            if (FoundOp == null && node.Operator == _targetOp)
            {
                FoundOp = node;
            }
            base.VisitBinaryOperation(node);
        }
    }
}
