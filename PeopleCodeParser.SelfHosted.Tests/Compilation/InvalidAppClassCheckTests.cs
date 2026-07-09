using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeTypeInfo.Contracts;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class InvalidAppClassCheckTests
{
    [Fact]
    public void Reports_when_app_class_is_unknown_to_resolver()
    {
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Missing &x;");
        var fake = new FakeTypeMetadataResolver();

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidAppClass && d.Message.Contains("PKG:Missing"));
    }

    [Fact]
    public void Does_not_report_when_resolver_knows_the_class()
    {
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Known &x;");
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Known");

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidAppClass);
    }

    [Fact]
    public void Does_not_report_without_a_resolver()
    {
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Missing &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidAppClass);
    }

    [Fact]
    public void Does_not_report_with_NullTypeMetadataResolver()
    {
        // Stand-in used when no DB is connected: every GetTypeMetadata returns null.
        // Must not flood the editor with "does not exist in the database" errors.
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Missing &x;");

        var diags = CompileChecker.Check(
            program, errors, NullTypeMetadataResolver.Instance, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidAppClass);
    }

    [Fact]
    public void Does_not_report_for_non_fully_qualified_class_reference()
    {
        // A bare (imported-style) class reference has no ':' — the check skips it.
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:*;
Local Missing &x;");
        var fake = new FakeTypeMetadataResolver();

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidAppClass);
    }
}
