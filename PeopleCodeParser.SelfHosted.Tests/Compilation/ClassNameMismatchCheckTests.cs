using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class ClassNameMismatchCheckTests
{
    private const string ClassBody = @"
   property string Foo;
end-class;";

    [Fact]
    public void Reports_when_class_name_differs_from_expected()
    {
        var (program, errors) = ParseTestHelper.Parse("class Baz" + ClassBody);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: "Bar"));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.ClassNameMismatch);
    }

    [Fact]
    public void Does_not_report_when_class_name_matches_expected()
    {
        var (program, errors) = ParseTestHelper.Parse("class Bar" + ClassBody);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: "Bar"));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.ClassNameMismatch);
    }

    [Fact]
    public void Does_not_report_when_expected_name_unavailable()
    {
        var (program, errors) = ParseTestHelper.Parse("class Baz" + ClassBody);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.ClassNameMismatch);
    }
}
