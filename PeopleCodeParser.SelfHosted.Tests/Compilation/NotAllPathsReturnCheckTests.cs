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

    // A1: Repeat body always runs; body that only Returns means the function returns.
    [Fact]
    public void Function_return_only_inside_repeat_is_clean()
    {
        var diags = Check(@"
Function F() Returns number
   Local boolean &done;
   Repeat
      Return 1;
   Until &done;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Function_repeat_with_fallthrough_body_reports()
    {
        var diags = Check(@"
Function F() Returns number
   Local boolean &done;
   Local number &x;
   Repeat
      &x = 1;
   Until &done;
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
    public void Empty_else_secondary_styles_else_token_not_indent()
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
        Assert.True(secondary.IsSecondary);
        Assert.True(secondary.Span.IsValid);
        Assert.True(secondary.Span.Start.Line >= 3);
        // Content only — Else keyword, not leading indentation (column 0).
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Empty_when_secondary_styles_when_through_condition_not_indent()
    {
        var diags = Check(@"
Function F(&f As Field) Returns string
   Evaluate &f.Name
   When ""A""
      Return ""a"";
   When Field.LASTUPDDTTM
   When-Other
      Return """";
   End-Evaluate;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.IsSecondary);
        // When keyword through condition — not just Field.LASTUPDDTTM, not indent.
        Assert.True(secondary.Span.ByteLength > "Field.LASTUPDDTTM".Length,
            $"expected When through condition, got {secondary.Span.ByteLength} bytes");
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Secondary_is_last_token_of_last_statement_not_whole_block()
    {
        // Nested fall-through inside Else; complete Then sibling means secondary targets
        // the Else path's last statement's last token (End-If), not a multi-line paint of Else.
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
        Assert.True(secondary.IsSecondary);
        Assert.True(secondary.Span.IsValid);
        // Single line only — not the whole Else multi-statement body.
        Assert.Equal(secondary.Span.Start.Line, secondary.Span.End.Line);
        // Last statement of Else is the nested If; last token is End-If.
        Assert.True(secondary.Span.End.Line >= 8,
            "secondary should sit on the nested End-If line at the end of the Else path");
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Secondary_last_statement_is_content_span_not_indent()
    {
        var diags = Check(@"
Function F() Returns number
   Local number &x;
   &x = 1;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.IsSecondary);
        Assert.Equal(secondary.Span.Start.Line, secondary.Span.End.Line);
        // Statement content (&x = 1;), not a single token and not indent.
        Assert.True(secondary.Span.ByteLength >= 6);
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Secondary_after_evaluate_break_points_at_trailing_statement_not_break()
    {
        // Break is valid Evaluate control flow (absorbed to Normal). The real bug is
        // falling off the function after End-Evaluate — tip at the trailing Local, not Break.
        var diags = Check(@"
Function Foo(&b As string) Returns string
   Evaluate &b
   When = Field.OPRID
      Return ""a"";
   When = Field.LASTUPDDTTM
      Local integer &x;
      Break;
   When-Other
      Return """";
   End-Evaluate;
   Local integer &y;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.IsSecondary);
        Assert.Equal(secondary.Span.Start.Line, secondary.Span.End.Line);
        // Trailing Local integer &y; is the last body statement (after End-Evaluate).
        Assert.True(secondary.Span.End.Line >= 10,
            $"expected secondary on trailing Local &y (late line), got line {secondary.Span.End.Line}");
        Assert.True(secondary.Span.ByteLength >= "Local integer &y".Length);
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Secondary_after_incomplete_if_points_at_trailing_statement()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return ""a"";
   End-If;
   Local string &s;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.IsSecondary);
        // Last body statement is Local string &s;, not the one-armed If.
        Assert.True(secondary.Span.End.Line >= 4,
            $"expected secondary on trailing Local, got line {secondary.Span.End.Line}");
        Assert.True(secondary.Span.ByteLength >= "Local string &s".Length);
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
    }

    [Fact]
    public void Secondary_evaluate_only_empty_when_still_points_at_when()
    {
        // When Evaluate is the last body statement, arm-pick still tips at the empty When.
        var diags = Check(@"
Function F(&f As Field) Returns string
   Evaluate &f.Name
   When ""A""
      Return ""a"";
   When Field.LASTUPDDTTM
   When-Other
      Return """";
   End-Evaluate;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("without returning a value")).ToList();

        Assert.NotEmpty(diags);
        var secondary = diags[0];
        Assert.True(secondary.IsSecondary);
        Assert.True(secondary.Span.ByteLength > "Field.LASTUPDDTTM".Length,
            $"expected When through condition, got {secondary.Span.ByteLength} bytes");
        Assert.True(secondary.Span.Start.Column > 0,
            "secondary must not paint leading indent");
        // Should not sit on End-Evaluate (last line of the Evaluate statement).
        Assert.True(secondary.Span.End.Line < 8,
            $"empty When tip should be before End-Evaluate, got line {secondary.Span.End.Line}");
    }

    [Fact]
    public void Primary_function_span_includes_function_keyword()
    {
        var diags = Check(@"
Function Foo(&b As string) Returns string
   Local string &s;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("Not all paths return")).ToList();

        Assert.NotEmpty(diags);
        var primary = diags[0];
        Assert.False(primary.IsSecondary);
        // "Function Foo(&b As string) Returns string" — longer than name+params+returns alone.
        Assert.True(primary.Span.ByteLength >= "Function Foo".Length,
            $"expected Function keyword included, got byte length {primary.Span.ByteLength}");
        Assert.True(primary.Span.Start.Column <= 1,
            "Function keyword is at the start of the header line");
    }

    [Fact]
    public void Primary_method_declaration_span_includes_method_keyword()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Local number &x;
end-method;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn
            && d.Message.Contains("Not all paths return")).ToList();

        var declPrimary = diags.Where(d => d.Span.Start.Line < 4).OrderBy(d => d.Span.Start.Line).First();
        Assert.True(declPrimary.Span.ByteLength >= "method GetX".Length,
            $"expected method keyword included, got byte length {declPrimary.Span.ByteLength}");

        var implPrimary = diags.Where(d => d.Span.Start.Line >= 4).OrderBy(d => d.Span.Start.Line).First();
        Assert.True(implPrimary.Span.ByteLength >= "method GetX".Length,
            $"impl primary should include method keyword, got {implPrimary.Span.ByteLength}");
    }
}
