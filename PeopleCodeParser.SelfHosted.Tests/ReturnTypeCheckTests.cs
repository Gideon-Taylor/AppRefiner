using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
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

    // Declared app class return types written as short names resolve the same way as locals:
    // through imports, or to the program's own class (no self-import).

    private static List<TypeError> TypeCheckAs(string source, string qualifiedName)
    {
        var lexer = new PeopleCodeLexer(source);
        var parser = new Parser(lexer.TokenizeAll());
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return program.GetAllTypeErrors().ToList();
    }

    [Fact]
    public void Return_This_from_method_returning_own_short_name_is_ok()
    {
        var errors = TypeCheckAs(@"
class Svc
   method Clone() Returns Svc;
end-class;

method Clone
   Return %This;
end-method;
", "PKG:Svc");
        Assert.Empty(errors);
    }

    [Fact]
    public void Return_imported_class_from_method_returning_short_name_is_ok()
    {
        var errors = TypeCheckAs(@"
import OTHER:Thing;

class Svc
   method Get() Returns Thing;
end-class;

method Get
   Local Thing &t;
   Return &t;
end-method;
", "PKG:Svc");
        Assert.Empty(errors);
    }

    [Fact]
    public void Return_imported_class_from_function_returning_short_name_is_ok()
    {
        var errors = TypeCheckAs(@"
import OTHER:Thing;

Function Get() Returns Thing
   Local Thing &t;
   Return &t;
End-Function;
", "PKG:Svc");
        Assert.Empty(errors);
    }

    [Fact]
    public void Return_This_from_getter_of_own_short_name_is_ok()
    {
        var errors = TypeCheckAs(@"
class Svc
   property Svc Me get;
end-class;

get Me
   Return %This;
end-get;
", "PKG:Svc");
        Assert.Empty(errors);
    }

    [Fact]
    public void Rerun_refreshes_declared_return_type_stamp()
    {
        var parser = new Parser(new PeopleCodeLexer(@"
class Svc
   method Get() Returns PKG:Sub;
end-class;
").TokenizeAll());
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc");
        var resolver = new FakeTypeMetadataResolver();

        // First pass: PKG:Sub is not known yet, so its chain stops at itself
        TypeInferenceVisitor.Run(program, metadata, resolver);

        // PKG:Sub becomes resolvable (e.g. saved) before the styler re-runs inference
        resolver.AddClass("PKG:Base");
        resolver.AddClass("PKG:Sub", new TypeMetadata { QualifiedName = "PKG:Sub", Name = "Sub", BaseClassName = "PKG:Base" });
        TypeInferenceVisitor.Run(program, metadata, resolver);

        var returnType = program.FindDescendants<AppClassTypeNode>().Single();
        var stamp = Assert.IsType<AppClassTypeInfo>(returnType.GetInferredType());
        Assert.Contains(stamp.InheritanceChain, e => e.QualifiedName == "PKG:Base");
    }
}
