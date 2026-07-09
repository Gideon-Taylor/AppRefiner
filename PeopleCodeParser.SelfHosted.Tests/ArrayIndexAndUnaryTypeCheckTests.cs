using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using Parser = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Type-inference rules for array indexing (T1/T2) and unary Not/negate (T3).
/// Errors are recorded during inference (same home as binary operator checks).
/// </summary>
public class ArrayIndexAndUnaryTypeCheckTests
{
    private static List<TypeError> InferErrors(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        return program.GetAllTypeErrors().ToList();
    }

    [Fact]
    public void Index_on_non_array_is_error()
    {
        var errors = InferErrors(@"
Function F()
   Local string &s;
   Local string &x;
   &x = &s[1];
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Cannot index", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Index_on_array_is_ok()
    {
        var errors = InferErrors(@"
Function F()
   Local array of string &a;
   Local string &x;
   &x = &a[1];
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Cannot index", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Array index must be numeric", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Non_numeric_index_is_error()
    {
        var errors = InferErrors(@"
Function F()
   Local array of string &a;
   Local string &i;
   Local string &x;
   &x = &a[&i];
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Array index must be numeric", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Any_base_and_any_index_are_silent()
    {
        var errors = InferErrors(@"
Function F()
   Local any &a;
   Local any &i;
   Local any &x;
   &x = &a[&i];
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Cannot index", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Array index must be numeric", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Not_on_string_is_error()
    {
        var errors = InferErrors(@"
Function F()
   Local string &s;
   Local boolean &b;
   &b = Not &s;
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Not", StringComparison.OrdinalIgnoreCase) &&
            e.Message.Contains("string", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Not_on_boolean_is_ok()
    {
        var errors = InferErrors(@"
Function F()
   Local boolean &s;
   Local boolean &b;
   &b = Not &s;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("logical operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Negate_on_string_is_error()
    {
        var errors = InferErrors(@"
Function F()
   Local string &s;
   Local number &n;
   &n = - &s;
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("unary negate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Negate_on_number_is_ok()
    {
        var errors = InferErrors(@"
Function F()
   Local number &s;
   Local number &n;
   &n = - &s;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("unary negate", StringComparison.OrdinalIgnoreCase));
    }
}
