using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class AmbiguousClassReferenceCheckTests
{
    // NOTE on node contexts: the check faithfully ports the old styler's three contexts —
    // ObjectCreationNode.Type, ProgramVariableNode.Type, and AppClassNode.BaseType.
    // "Local Foo &x;" parses to LocalVariableDeclarationNode (NOT ProgramVariableNode),
    // which the old styler never checked, so tests use "Global Foo &x;" (ProgramVariableNode)
    // and "create Foo()" (ObjectCreationNode) to reach the checked contexts.

    [Fact]
    public void Reports_when_two_explicit_imports_provide_same_class_name()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Foo;
import PKG:B:Foo;
Global Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
        Assert.Contains("Foo", diag.Message);
        Assert.Contains("2 different packages", diag.Message);
    }

    [Fact]
    public void Attaches_fix_context_with_class_name_and_both_conflicting_paths()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Foo;
import PKG:B:Foo;
Global Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
        var fix = Assert.IsType<AmbiguousClassReferenceFix>(diag.FixContext);
        Assert.Equal("Foo", fix.ClassName);
        Assert.Equal(2, fix.ConflictingPaths.Count);
        Assert.Contains("PKG:A:Foo", fix.ConflictingPaths);
        Assert.Contains("PKG:B:Foo", fix.ConflictingPaths);
    }

    [Fact]
    public void Reports_for_object_creation_of_ambiguous_class()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Foo;
import PKG:B:Foo;
Local any &x;
&x = create Foo();");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }

    [Fact]
    public void Does_not_report_when_only_one_import_provides_the_class()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Foo;
Global Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }

    [Fact]
    public void Does_not_report_for_fully_qualified_reference()
    {
        // Qualified references are never ambiguous, even when the simple name is.
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Foo;
import PKG:B:Foo;
Global PKG:A:Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }

    [Fact]
    public void Does_not_report_when_class_is_not_imported_at_all()
    {
        // Not-imported is UnimportedClassCheck's finding, not ambiguity.
        var (program, errors) = ParseTestHelper.Parse("Global Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }

    [Fact]
    public void Reports_when_two_wildcard_imports_both_contain_the_class_via_resolver()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:*;
import PKG:B:*;
Global Foo &x;");
        var fake = new FakeTypeMetadataResolver();
        fake.AddPackageClasses("PKG:A", "Foo");
        fake.AddPackageClasses("PKG:B", "Foo");

        var diags = CompileChecker.Check(program, errors, fake, new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
        var fix = Assert.IsType<AmbiguousClassReferenceFix>(diag.FixContext);
        Assert.Contains("PKG:A:Foo", fix.ConflictingPaths);
        Assert.Contains("PKG:B:Foo", fix.ConflictingPaths);
    }

    [Fact]
    public void Does_not_report_for_wildcard_imports_when_no_resolver_available()
    {
        // Without a resolver, wildcard imports cannot be expanded — mirrors the old
        // styler's DB-disconnected behavior (no ambiguity is detectable).
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:*;
import PKG:B:*;
Global Foo &x;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }

    [Fact]
    public void Reports_for_ambiguous_unqualified_base_class_in_extends_clause()
    {
        var (program, errors) = ParseTestHelper.Parse(@"
import PKG:A:Base;
import PKG:B:Base;
class Child extends Base
end-class;");

        var diags = CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.AmbiguousClassReference);
    }
}
