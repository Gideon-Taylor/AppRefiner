using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class AssignmentToConstantCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Assign_to_class_constant_is_error()
    {
        var diags = Check(@"
class Sample
   method Bump();
private
   Constant &MAX = 10;
end-class;

method Bump
   &MAX = 11;
end-method;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.AssignmentToConstant &&
            d.Message.Contains("&MAX", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assign_to_program_constant_is_error()
    {
        var diags = Check(@"
Constant &LIMIT = 5;
Function F()
   &LIMIT = 6;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.AssignmentToConstant &&
            d.Message.Contains("&LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assign_to_local_is_ok()
    {
        var diags = Check(@"
class Sample
   method Bump();
end-class;

method Bump
   Local number &x;
   &x = 1;
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AssignmentToConstant);
    }

    [Fact]
    public void Read_of_constant_is_ok()
    {
        var diags = Check(@"
class Sample
   method GetMax() Returns number;
private
   Constant &MAX = 10;
end-class;

method GetMax
   Local number &x;
   &x = &MAX;
   Return &x;
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AssignmentToConstant);
    }
}
