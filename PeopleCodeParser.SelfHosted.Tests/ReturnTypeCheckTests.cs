using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using Parser = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Type-checker rules for return expressions vs declared routine return types.
/// Requires type inference before TypeCheckerVisitor (same as the live app pipeline).
/// </summary>
public class ReturnTypeCheckTests
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
    public void Return_string_from_number_function_is_type_error()
    {
        var errors = TypeCheck(@"
Function F() Returns number
   Return ""nope"";
End-Function;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase) &&
            e.Message.Contains("number", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Return_number_from_number_function_is_ok()
    {
        var errors = TypeCheck(@"
Function F() Returns number
   Return 1;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Return_integer_from_number_function_is_ok()
    {
        // Number/Integer are bidirectionally compatible in the type checker.
        var errors = TypeCheck(@"
Function F() Returns number
   Local integer &i;
   &i = 1;
   Return &i;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Procedure_return_expression_not_checked_against_declared_type()
    {
        // No declared return type — VisitReturn must not invent a type error.
        var errors = TypeCheck(@"
Function F()
   Return 1;
End-Function;
");
        Assert.DoesNotContain(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Method_return_type_mismatch_is_type_error()
    {
        var errors = TypeCheck(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Return ""x"";
end-method;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Property_getter_return_type_mismatch_is_type_error()
    {
        var errors = TypeCheck(@"
class Sample
   property number Foo get;
end-class;

get Foo
   Return ""x"";
end-get;
");
        Assert.Contains(errors, e =>
            e.Message.Contains("Cannot return type", StringComparison.OrdinalIgnoreCase));
    }
}
