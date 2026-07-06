using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingMethodImplementationCheckTests
{
    [Fact]
    public void Reports_when_declared_method_has_no_implementation()
    {
        const string src = @"
class Sample
   method Foo();
end-class;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(null));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingMethodImplementation &&
            d.Message.Contains("Foo"));
    }

    [Fact]
    public void Does_not_report_when_declared_method_is_implemented()
    {
        const string src = @"
class Sample
   method Foo();
end-class;

method Foo
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingMethodImplementation);
    }

    [Fact]
    public void Does_not_report_for_abstract_method_declaration()
    {
        const string src = @"
class Sample
   method Bar() abstract;
end-class;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingMethodImplementation);
    }
}
