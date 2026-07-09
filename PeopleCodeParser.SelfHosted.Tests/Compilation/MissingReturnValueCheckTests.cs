using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingReturnValueCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Bare_return_in_value_function_is_error()
    {
        var diags = Check(@"
Function F() Returns number
   Return;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingReturnValue &&
            d.Message.Contains("F") &&
            d.Message.Contains("must include a value"));
    }

    [Fact]
    public void Return_with_value_in_function_is_ok()
    {
        var diags = Check(@"
Function F() Returns number
   Return 1;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingReturnValue);
    }

    [Fact]
    public void Bare_return_in_procedure_is_ok()
    {
        var diags = Check(@"
Function F()
   Return;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingReturnValue);
    }

    [Fact]
    public void Bare_return_in_method_with_return_type_is_error()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Return;
end-method;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingReturnValue &&
            d.Message.Contains("GetX"));
    }

    [Fact]
    public void Bare_return_in_property_getter_is_error()
    {
        var diags = Check(@"
class Sample
   property number Foo get;
end-class;

get Foo
   Return;
end-get;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingReturnValue &&
            d.Message.Contains("Foo"));
    }
}
