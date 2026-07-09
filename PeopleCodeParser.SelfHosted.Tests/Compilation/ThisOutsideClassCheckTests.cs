using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class ThisOutsideClassCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void This_in_app_class_method_is_ok()
    {
        var diags = Check(@"
class Sample
   method DoIt();
end-class;

method DoIt
   Local Sample &s = %This;
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.ThisOutsideClass);
    }

    [Fact]
    public void This_in_plain_function_is_error()
    {
        var diags = Check(@"
Function F()
   Local object &o = %This;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.ThisOutsideClass &&
            d.Message.Contains("%This"));
    }

    [Fact]
    public void This_in_record_style_program_is_error()
    {
        var diags = Check(@"
Local object &o;
&o = %This;
");
        Assert.Contains(diags, d => d.Code == DiagnosticCode.ThisOutsideClass);
    }
}
