using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeTypeInfo.Inference;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class MissingSuperCallCheckTests
{
    private static FakeTypeMetadataResolver ResolverWith(string qualifiedName, ProgramKind kind)
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass(qualifiedName, new TypeMetadata
        {
            QualifiedName = qualifiedName,
            Name = qualifiedName.Split(':').Last(),
            Kind = kind,
        });
        return fake;
    }

    private static IReadOnlyList<CompileDiagnostic> Check(
        string source,
        FakeTypeMetadataResolver? resolver = null)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Constructor_without_super_when_base_class_exists_is_error()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.MissingSuperCall &&
            d.Message.Contains("Sub") &&
            d.Message.Contains("%Super"));
    }

    [Fact]
    public void Constructor_with_super_assign_is_ok()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
   %Super = create PKG:Base();
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Super_anywhere_in_body_is_ok()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
   Local number &x;
   &x = 1;
   %Super = create PKG:Base();
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void No_base_class_no_error()
    {
        var diags = Check(@"
class Root
   method Root();
end-class;

method Root
end-method;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Non_constructor_method_no_error()
    {
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method DoWork();
end-class;

method DoWork
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Interface_parent_via_implements_does_not_require_super()
    {
        // Correct keyword, but Kind is what matters.
        var diags = Check(@"
import PKG:IBase;

class Sub implements PKG:IBase
   method Sub();
end-class;

method Sub
end-method;
", ResolverWith("PKG:IBase", ProgramKind.Interface));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Interface_parent_via_extends_does_not_require_super()
    {
        // PeopleCode allows extends/implements interchange — still an interface parent.
        var diags = Check(@"
import PKG:IBase;

class Sub extends PKG:IBase
   method Sub();
end-class;

method Sub
end-method;
", ResolverWith("PKG:IBase", ProgramKind.Interface));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Class_parent_via_implements_still_requires_super()
    {
        // Interchanged keyword: implements a *class* — Kind is AppClass, so %Super required.
        var diags = Check(@"
import PKG:Base;

class Sub implements PKG:Base
   method Sub();
end-class;

method Sub
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Builtin_base_without_super_is_error()
    {
        // Exception etc. are classes; no resolver lookup needed for builtins.
        var diags = Check(@"
class MyEx extends Exception
   method MyEx();
end-class;

method MyEx
end-method;
");

        Assert.Contains(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Without_resolver_does_not_report_for_app_class_base()
    {
        // Optional: cannot distinguish class vs interface without resolver — skip.
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
end-method;
");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Unresolvable_parent_does_not_report()
    {
        // Parent not in resolver — skip rather than false-positive an interface.
        var empty = new FakeTypeMetadataResolver();
        var diags = Check(@"
import PKG:Unknown;

class Sub extends PKG:Unknown
   method Sub();
end-class;

method Sub
end-method;
", empty);

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Interface_parent_constructor_with_other_work_still_no_super_required()
    {
        // PeopleBooks: system creates the interface super reference automatically —
        // even when the subclass has an authored constructor with other statements.
        var diags = Check(@"
import PKG:IBase;

class Sub implements PKG:IBase
   method Sub();
end-class;

method Sub
   Local number &x;
   &x = 1;
end-method;
", ResolverWith("PKG:IBase", ProgramKind.Interface));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }

    [Fact]
    public void Class_parent_constructor_with_other_work_still_requires_super()
    {
        // PeopleBooks general case: authored constructor must assign %Super for class parents.
        // (No-arg base only relaxes *omitting* the constructor, not skipping %Super when one exists.)
        var diags = Check(@"
import PKG:Base;

class Sub extends PKG:Base
   method Sub();
end-class;

method Sub
   Local number &x;
   &x = 1;
end-method;
", ResolverWith("PKG:Base", ProgramKind.AppClass));

        Assert.Contains(diags, d => d.Code == DiagnosticCode.MissingSuperCall);
    }
}
