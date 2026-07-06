using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeTypeInfo.Inference;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class UnimplementedAbstractMemberCheckTests
{
    /// <summary>
    /// Interface requiring a zero-arg method and a property, built with the shared
    /// signature helpers so the scheme matches what TypeMetadataBuilder produces.
    /// </summary>
    private static TypeMetadata SimpleInterface() => new()
    {
        QualifiedName = "PKG:ISimple",
        Name = "ISimple",
        Kind = ProgramKind.Interface,
        AbstractMemberSignatures = new[]
        {
            TypeMetadata.MethodSignature("DoIt", 0),
            TypeMetadata.PropertySignature("Name"),
        },
    };

    private static FakeTypeMetadataResolver ResolverWithInterface()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:ISimple", SimpleInterface());
        return fake;
    }

    [Fact]
    public void Reports_when_interface_member_is_not_implemented()
    {
        const string src = @"
import PKG:ISimple;

class Impl implements PKG:ISimple
   method Other();
end-class;

method Other
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, ResolverWithInterface(),
            new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.StartsWith("Missing implementations:", diag.Message);
        Assert.Contains("\n - Method: DoIt", diag.Message);
        Assert.Contains("\n - Property: Name", diag.Message);
        // Methods are grouped before properties, as in the old styler tooltip.
        Assert.True(diag.Message.IndexOf("Method: DoIt", StringComparison.Ordinal)
                    < diag.Message.IndexOf("Property: Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_report_when_all_interface_members_are_implemented()
    {
        const string src = @"
import PKG:ISimple;

class Impl implements PKG:ISimple
   method DoIt();
   property string Name;
end-class;

method DoIt
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, ResolverWithInterface(),
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
    }

    [Fact]
    public void Does_not_report_when_mid_class_concretely_implements_interface_member()
    {
        // Sub -> PKG:Mid (concrete DoIt + Name) -> PKG:ISimple (requires DoIt + Name).
        // Mid has no base class, so the walk advances via the InterfaceName fallback.
        const string src = @"
import PKG:Mid;

class Sub extends PKG:Mid
   method Own();
end-class;

method Own
end-method;
";
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:ISimple", SimpleInterface());
        fake.AddClass("PKG:Mid", new TypeMetadata
        {
            QualifiedName = "PKG:Mid",
            Name = "Mid",
            Kind = ProgramKind.AppClass,
            InterfaceName = "PKG:ISimple",
            ConcreteMemberSignatures = new[]
            {
                TypeMetadata.MethodSignature("DoIt", 0),
                TypeMetadata.PropertySignature("Name"),
            },
        });

        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, fake,
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
    }

    [Fact]
    public void Reports_requirement_propagated_through_mid_class_that_does_not_implement_it()
    {
        // Same hierarchy, but Mid implements nothing -> the interface requirement
        // propagates all the way down to Sub.
        const string src = @"
import PKG:Mid;

class Sub extends PKG:Mid
   method Own();
end-class;

method Own
end-method;
";
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:ISimple", SimpleInterface());
        fake.AddClass("PKG:Mid", new TypeMetadata
        {
            QualifiedName = "PKG:Mid",
            Name = "Mid",
            Kind = ProgramKind.AppClass,
            InterfaceName = "PKG:ISimple",
        });

        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, fake,
            new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
        Assert.Contains("DoIt", diag.Message);
    }

    [Fact]
    public void Unresolvable_deeper_base_keeps_requirements_already_collected()
    {
        // Mid is resolvable and abstract; its own base is not registered. The walk
        // stops there but Mid's unmet requirement is still reported (a deeper base can
        // never retract a requirement a shallower level failed to satisfy).
        const string src = @"
import PKG:Mid;

class Sub extends PKG:Mid
   method Own();
end-class;

method Own
end-method;
";
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Mid", new TypeMetadata
        {
            QualifiedName = "PKG:Mid",
            Name = "Mid",
            Kind = ProgramKind.AppClass,
            BaseClassName = "PKG:Unknown",
            AbstractMemberSignatures = new[] { TypeMetadata.MethodSignature("DoIt", 0) },
        });

        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, fake,
            new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
        Assert.Contains("DoIt", diag.Message);
    }

    [Fact]
    public void Circular_hierarchy_reports_once_and_terminates()
    {
        const string src = @"
import PKG:Loop;

class Sub extends PKG:Loop
   method Own();
end-class;

method Own
end-method;
";
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Loop", new TypeMetadata
        {
            QualifiedName = "PKG:Loop",
            Name = "Loop",
            Kind = ProgramKind.AppClass,
            BaseClassName = "PKG:Loop", // self-referential metadata must not hang
            AbstractMemberSignatures = new[] { TypeMetadata.MethodSignature("MustDo", 1) },
        });

        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, fake,
            new CompileCheckContextInput(null));

        var diag = Assert.Single(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
        Assert.Contains("MustDo", diag.Message);
    }

    [Fact]
    public void Does_not_report_without_resolver()
    {
        const string src = @"
import PKG:ISimple;

class Impl implements PKG:ISimple
   method Other();
end-class;

method Other
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, resolver: null,
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
    }

    [Fact]
    public void Does_not_report_for_class_without_base_type()
    {
        const string src = @"
class Standalone
   method Own();
end-class;

method Own
end-method;
";
        var (program, errors) = ParseTestHelper.Parse(src);
        var diags = CompileChecker.Check(program, errors, ResolverWithInterface(),
            new CompileCheckContextInput(null));

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.UnimplementedAbstractMember);
    }
}
