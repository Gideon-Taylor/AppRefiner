using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class NotAllPathsReturnCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    private static IEnumerable<CompileDiagnostic> PathDiags(IReadOnlyList<CompileDiagnostic> diags) =>
        diags.Where(d => d.Code == DiagnosticCode.NotAllPathsReturn);

    [Fact]
    public void Function_with_fallthrough_reports()
    {
        var diags = Check(@"
Function F() Returns number
   Local number &x;
   &x = 1;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
        Assert.Contains(PathDiags(diags), d => d.Message.Contains("F"));
    }

    [Fact]
    public void Function_all_paths_return_is_clean()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   Else
      Return ""b"";
   End-If;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Function_one_armed_if_return_reports()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   End-If;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Function_throw_only_is_clean()
    {
        var diags = Check(@"
Function F() Returns number
   Throw &e;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Function_return_only_inside_while_reports()
    {
        var diags = Check(@"
Function F() Returns number
   While &x
      Return 1;
   End-While;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Procedure_without_return_type_is_not_checked()
    {
        var diags = Check(@"
Function F()
   Local number &x;
   &x = 1;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Method_with_fallthrough_reports()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Local number &x;
   &x = 1;
end-method;
");
        Assert.NotEmpty(PathDiags(diags));
        Assert.Contains(PathDiags(diags), d => d.Message.Contains("GetX"));
    }

    [Fact]
    public void Property_getter_with_fallthrough_reports()
    {
        var diags = Check(@"
class Sample
   property number Foo get;
end-class;

get Foo
   Local number &x;
   &x = 1;
end-get;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Function_only_unbound_break_reports_not_all_paths()
    {
        var diags = Check(@"
Function F() Returns number
   Break;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }
}
