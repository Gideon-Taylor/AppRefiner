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
    public void Repro_function_local_not_conflicting_with_top_level_local_after()
    {
        var diags = Check(@"
Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &taxRate;
   &taxRate = 3;
   Return &taxRate;
End-Function;

Local number &taxRate;

&taxRate = 4;
");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.RedeclaredVariable);
    }

    [Fact]
    public void Does_not_report_program_local_when_conflict_is_in_child_function_scope()
    {
        // The program-level local below the function is checked in the Global scope. The
        // function's same-named local sits at a LOWER byte offset, but it lives in a child
        // (function) scope that Global cannot see, so it must not be treated as a conflict.
        // Scope accessibility gates the positional test — the check is not purely positional.
        var diags = Check(@"
Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &foo;
   Return &foo;
End-Function;

Local number &foo;
");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.RedeclaredVariable);
    }

    [Fact]
    public void Reports_function_local_conflicting_with_program_local_before()
    {
        // Counterpart to the repro above: when the top-level Local is declared ABOVE the
        // function it is a program-wide declaration, so the function's local genuinely
        // shadows it and SHOULD be reported.
        var diags = Check(@"
Local number &taxRate;

Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &taxRate;
   &taxRate = 3;
   Return &taxRate;
End-Function;

&taxRate = 4;
");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.RedeclaredVariable &&
            d.Message.Contains("already declared in this scope", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reports_function_local_shadowing_global_declared_before()
    {
        // A Global declared ABOVE the function is program-wide and in scope for the
        // function, so a function local with the same name shadows it and SHOULD report.
        var diags = Check(@"
Global number &foo;
Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &foo;
   Return &foo;
End-Function;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.RedeclaredVariable &&
            d.Message.Contains("global", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_not_report_function_local_when_global_declared_after()
    {
        // A Global declared BELOW the function does not conflict with the function's local
        // (nothing was declared before it), and the declaration must still parse cleanly.
        var (program, errors) = ParseTestHelper.Parse(@"
Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &foo;
   Return &foo;
End-Function;

Global number &foo;
");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.RedeclaredVariable);
        Assert.Empty(errors); // the below-function Global must parse without error
    }

    [Fact]
    public void Does_not_report_and_parses_mixed_declarations_after_function()
    {
        // Program-level Local and Global declarations may follow a function definition.
        // They must all parse (no declaration form is rerouted into the main block), and
        // none conflicts with the function's local because each is declared after it.
        var (program, errors) = ParseTestHelper.Parse(@"
Function GetTaxRateForState(&normalizedState As string) Returns number
   Local number &foo;
   Return &foo;
End-Function;

Local number &baz;
Global number &foo;
");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.Empty(errors); // both post-function declarations must parse cleanly
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
