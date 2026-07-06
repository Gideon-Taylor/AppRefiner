using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UnimportedClassCheckTests
{
    [Fact]
    public void Reports_when_class_is_not_imported()
    {
        // Explicit-import checking needs no resolver.
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Foo:Bar &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnimportedClass && d.Message.Contains("Bar"));
    }

    [Fact]
    public void Does_not_report_when_class_is_explicitly_imported()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:Bar;
Local PKG:Foo:Bar &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Does_not_report_when_wildcard_import_covers_class_via_resolver()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Bar &x;");
        var fake = new FakeTypeMetadataResolver();
        fake.AddPackageClasses("PKG:Foo", "Bar");

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Reports_under_wildcard_import_when_no_resolver_available()
    {
        // Wildcard expansion depends on the resolver — without one the wildcard
        // cannot vouch for the class, matching the old styler's DB-less behavior.
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Bar &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Reports_under_wildcard_import_when_resolver_does_not_know_the_package()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Bar &x;");
        var fake = new FakeTypeMetadataResolver(); // package not registered

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Attaches_class_name_as_fix_context()
    {
        var (program, errors) = ParseTestHelper.Parse("Local PKG:Foo:Bar &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UnimportedClass);
        Assert.Equal("Bar", diag.FixContext);
    }

    [Fact]
    public void Reports_for_object_creation_of_unimported_class()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
Local any &x;
&x = create PKG:Foo:Baz();");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.UnimportedClass && d.Message.Contains("Baz"));
    }
}
