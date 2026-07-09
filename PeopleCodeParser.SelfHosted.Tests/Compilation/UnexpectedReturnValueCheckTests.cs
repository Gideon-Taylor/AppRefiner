using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UnexpectedReturnValueCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Return_value_in_procedure_function_is_error()
    {
        var diags = Check(@"
Function F()
   Return 1;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnexpectedReturnValue &&
            d.Message.Contains("F") &&
            d.Message.Contains("does not declare a return type"));
    }

    [Fact]
    public void Return_value_in_value_function_is_ok()
    {
        var diags = Check(@"
Function F() Returns number
   Return 1;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);
    }

    [Fact]
    public void Bare_return_in_procedure_is_ok()
    {
        var diags = Check(@"
Function F()
   Return;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);
    }

    [Fact]
    public void Return_value_in_procedure_method_is_error()
    {
        var diags = Check(@"
class Sample
   method DoIt();
end-class;

method DoIt
   Return 1;
end-method;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnexpectedReturnValue &&
            d.Message.Contains("DoIt"));
    }

    [Fact]
    public void Return_value_in_value_method_is_ok()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Return 1;
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);
    }

    [Fact]
    public void Return_value_in_property_setter_is_error()
    {
        var diags = Check(@"
class Sample
   property number Foo get set;
end-class;

get Foo
   Return 1;
end-get;

set Foo
   Return 1;
end-set;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnexpectedReturnValue &&
            d.Message.Contains("setter", StringComparison.OrdinalIgnoreCase) &&
            d.Message.Contains("Foo"));
    }

    [Fact]
    public void Return_value_in_property_getter_is_ok()
    {
        var diags = Check(@"
class Sample
   property number Foo get;
end-class;

get Foo
   Return 1;
end-get;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);
    }

    [Fact]
    public void No_overlap_with_MissingReturnValue_on_same_statement()
    {
        // Bare return in procedure: neither MissingReturnValue nor UnexpectedReturnValue.
        var diags = Check(@"
Function F()
   Return;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingReturnValue);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);

        // Value return in value function: neither.
        diags = Check(@"
Function G() Returns number
   Return 1;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingReturnValue);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnexpectedReturnValue);
    }
}
