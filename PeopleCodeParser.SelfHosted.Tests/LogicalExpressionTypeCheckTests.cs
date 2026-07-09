using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using Parser = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// T5: If / While / Repeat-Until require a logical (Boolean) condition expression.
/// </summary>
public class LogicalExpressionTypeCheckTests
{
    private static List<TypeError> TypeCheck(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return program.GetAllTypeErrors().ToList();
    }

    [Fact]
    public void If_string_condition_is_error()
    {
        var errors = TypeCheck(@"
Function F()
   Local string &s;
   If &s Then
   End-If;
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void If_boolean_condition_is_ok()
    {
        var errors = TypeCheck(@"
Function F()
   Local boolean &b;
   If &b Then
   End-If;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void If_comparison_is_ok()
    {
        var errors = TypeCheck(@"
Function F()
   Local number &n;
   If &n = 1 Then
   End-If;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void While_number_condition_is_error()
    {
        var errors = TypeCheck(@"
Function F()
   Local number &n;
   While &n
   End-While;
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Repeat_until_string_is_error()
    {
        var errors = TypeCheck(@"
Function F()
   Local string &s;
   Repeat
   Until &s;
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Any_condition_is_silent()
    {
        var errors = TypeCheck(@"
Function F()
   Local any &a;
   If &a Then
   End-If;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Equals("Logical expression required", StringComparison.OrdinalIgnoreCase));
    }
}
