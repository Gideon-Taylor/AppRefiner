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

    [Fact]
    public void Reports_primary_and_secondary_for_incomplete_if_branch()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   Else
      Local string &s;
      &s = ""x"";
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn).ToList();

        Assert.True(diags.Count >= 2, "expected primary signature + secondary block");
        Assert.Contains(diags, d => d.Message.Contains("Not all paths return"));
        Assert.Contains(diags, d => d.Message.Contains("without returning a value"));
    }

    [Fact]
    public void Secondary_prefers_innermost_incomplete_block()
    {
        var diags = Check(@"
Function F(&b As boolean, &c As boolean) Returns number
   If &b Then
      If &c Then
         Return 1;
      Else
         Local number &n;
         &n = 2;
      End-If;
   Else
      Return 0;
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn).ToList();

        Assert.Contains(diags, d => d.Message.Contains("without returning a value"));
        var secondary = diags.First(d => d.Message.Contains("without returning a value"));
        Assert.True(secondary.Span.Start.Line >= 4);
    }

    [Fact]
    public void Method_styles_both_declaration_and_implementation_headers()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   /+ Returns number +/
   Local number &x;
   &x = 1;
end-method;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("Not all paths return")).ToList();

        Assert.True(diags.Count >= 2, "expected primary on declaration and implementation headers");
        Assert.All(diags, d => Assert.True(d.Span.IsValid));
        Assert.Contains(diags, d => d.Span.Start.Line < 4);
        Assert.Contains(diags, d => d.Span.Start.Line >= 4);
        // Impl primary must be method Name only — not the /+ Returns +/ annotation line.
        var implPrimary = diags.Where(d => d.Span.Start.Line >= 4).OrderBy(d => d.Span.Start.Line).First();
        Assert.True(implPrimary.Span.Start.Line == 5 || implPrimary.Span.Start.Line == 4,
            "impl primary should be on method header line, not deep in annotations/body");
        Assert.True(implPrimary.Span.ByteLength < 40,
            "impl primary must not span through /+ annotations");
    }

    [Fact]
    public void Empty_else_secondary_styles_else_keyword()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   Else
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn).ToList();

        Assert.Contains(diags, d => d.Message.Contains("Not all paths return"));
        var secondary = diags.First(d => d.Message.Contains("without returning a value"));
        // Else keyword sits after the Then branch's Return line.
        Assert.True(secondary.Span.IsValid);
        Assert.True(secondary.Span.Start.Line >= 3);
    }

    [Fact]
    public void Secondary_is_single_locus_not_whole_nested_block()
    {
        // Nested fall-through inside Else; complete Then sibling means secondary targets
        // the Else path's end (nested End-If keyword), not a multi-line paint of Else.
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   Else
      Local number &x;
      &x = 1;
      If &x > 2 Then
         Local number &z;
         &z = 4;
      End-If;
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.Span.IsValid);
        Assert.True(secondary.Span.ByteLength < 40,
            $"secondary span too large ({secondary.Span.ByteLength} bytes); expected single locus");
        // Prefer End-If text, not a lone ';'
        Assert.False(secondary.Span.ByteLength <= 1,
            "secondary should be End-If keyword, not a single semicolon");
    }
}
