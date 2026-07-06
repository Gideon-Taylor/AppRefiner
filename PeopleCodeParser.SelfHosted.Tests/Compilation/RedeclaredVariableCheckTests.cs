using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class RedeclaredVariableCheckTests
{
    private static System.Collections.Generic.IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));
    }

    [Fact]
    public void Reports_redeclared_local_in_same_scope()
    {
        var diags = Check(@"
Function Foo()
   Local number &n;
   Local number &n;
End-Function;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.RedeclaredVariable &&
            d.Message.Contains("already declared in this scope", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_not_report_single_local_declaration()
    {
        var diags = Check(@"
Function Foo()
   Local number &n;
End-Function;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.RedeclaredVariable);
    }

    [Fact]
    public void Reports_local_shadowing_parameter()
    {
        var diags = Check(@"
class Cls
   method Bar(&x as string);
end-class;

method Bar
   Local string &x;
end-method;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.RedeclaredVariable &&
            d.Message.Contains("parameter", System.StringComparison.OrdinalIgnoreCase));
    }
}
