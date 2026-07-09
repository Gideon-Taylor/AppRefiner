using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class InvalidBreakContinueCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Break_outside_loop_or_evaluate_is_error()
    {
        var diags = Check(@"
Function F()
   Break;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Continue_outside_loop_is_error()
    {
        var diags = Check(@"
Function F()
   Continue;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Continue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Break_in_evaluate_when_is_ok()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         Break;
   End-Evaluate;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }

    [Fact]
    public void Continue_in_evaluate_when_without_loop_is_error()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         Continue;
   End-Evaluate;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Continue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Break_and_continue_in_while_are_ok()
    {
        var diags = Check(@"
Function F()
   While &x
      If &y Then
         Break;
      End-If;
      Continue;
   End-While;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }

    [Fact]
    public void Break_in_loop_inside_evaluate_is_ok()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         While &y
            Break;
         End-While;
   End-Evaluate;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }
}
