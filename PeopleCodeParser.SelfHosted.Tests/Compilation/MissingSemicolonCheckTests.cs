using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingSemicolonCheckTests
{
    private static System.Collections.Generic.IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(ExpectedClassName: null));
    }

    [Fact]
    public void Reports_non_last_statement_missing_semicolon()
    {
        // First statement omits its terminator; the parser recovers it as a well-formed
        // ExpressionStatementNode (HasSemicolon=false) with no ErrorStatementNode in the block.
        var diags = Check("Function Foo()\n   &x = 1\n   &y = 2;\nEnd-Function;");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.MissingSemicolon &&
            d.Message == "Missing semicolon");
    }

    [Fact]
    public void Does_not_report_when_statement_terminated()
    {
        var diags = Check("Function Foo()\n   &x = 1;\n   &y = 2;\nEnd-Function;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSemicolon);
    }

    [Fact]
    public void Does_not_report_last_statement_missing_semicolon()
    {
        // The final statement before End-Function needs no semicolon (SkipLast(1) exempts it).
        var diags = Check("Function Foo()\n   &x = 1\nEnd-Function;");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSemicolon);
    }
}
