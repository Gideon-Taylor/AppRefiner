using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileDiagnosticTests
{
    [Fact]
    public void Diagnostic_carries_code_severity_span_and_message()
    {
        var span = new SourceSpan(new SourcePosition(0, 0, 1, 0), new SourcePosition(5, 5, 1, 5));
        var d = new CompileDiagnostic(DiagnosticCode.SyntaxError, DiagnosticSeverity.Error, span, "boom");

        Assert.Equal(DiagnosticCode.SyntaxError, d.Code);
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("boom", d.Message);
        Assert.Null(d.FixContext);
    }
}
