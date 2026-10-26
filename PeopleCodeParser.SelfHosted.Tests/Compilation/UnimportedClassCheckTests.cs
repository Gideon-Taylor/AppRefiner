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
    public void Does_not_report_the_class_naming_itself()
    {
        // App Designer accepts a class's own short name without an import (it expands it on save)
        var (program, errors) = ParseTestHelper.Parse(@"
class Svc
   method Make() Returns Svc;
end-class;

method Make
   /+ Returns PKG:Svc +/
   Local Svc &me = create Svc();
   Return &me;
end-method;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Still_reports_another_class_inside_a_class()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
class Svc
   method Make();
end-class;

method Make
   Local Other &o;
end-method;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UnimportedClass && d.Message.Contains("Other"));
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
    public void Assumes_wildcard_covers_class_when_no_resolver_available()
    {
        // Without a resolver the wildcard can't be expanded, so it may cover any class
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Bar &x;
Local Baz &y;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Assumes_wildcard_covers_class_when_package_lists_no_classes()
    {
        // The database resolver lists no classes when it isn't connected
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Bar &x;");
        var fake = new FakeTypeMetadataResolver(); // package not registered

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Reports_class_an_expanded_wildcard_does_not_cover()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:*;
Local PKG:Foo:Baz &x;");
        var fake = new FakeTypeMetadataResolver();
        fake.AddPackageClasses("PKG:Foo", "Bar");

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.UnimportedClass);
    }

    [Fact]
    public void Reports_without_resolver_when_there_is_no_wildcard()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:Foo:Bar;
Local Baz &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

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
