using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileCheckerTests
{
    [Fact]
    public void Syntax_errors_become_diagnostics()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n =;"); // malformed
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.SyntaxError);
    }

    [Fact]
    public void No_resolver_skips_type_checks_but_still_returns()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n = 1;");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.TypeError);
    }

    [Fact]
    public void Diagnostics_are_sorted_by_start_offset()
    {
        var (program, errors) = ParseTestHelper.Parse("Local number &n =;\nLocal string &s =;");
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));

        for (int i = 1; i < diags.Count; i++)
            Assert.True(diags[i - 1].Span.Start.ByteIndex <= diags[i].Span.Start.ByteIndex);
    }
}
