using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

/// <summary>
/// T6: private/protected member visibility after existence succeeds.
/// </summary>
public class InaccessibleMemberCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> RunClass(
        string source, string qualifiedName, FakeTypeMetadataResolver resolver)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, programMetadata, resolver);
        return CompileChecker.Check(program, errors, resolver,
            new CompileCheckContextInput(null, programMetadata));
    }

    private static FakeTypeMetadataResolver ResolverWithBaseHavingPrivateAndProtected()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Base", new TypeMetadata
        {
            QualifiedName = "PKG:Base",
            Name = "Base",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["PublicM"] = new FunctionInfo { Name = "PublicM", Visibility = MemberVisibility.Public },
                ["ProtectedM"] = new FunctionInfo { Name = "ProtectedM", Visibility = MemberVisibility.Protected },
                ["PrivateM"] = new FunctionInfo { Name = "PrivateM", Visibility = MemberVisibility.Private },
            },
            Properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["PublicP"] = new PropertyInfo(PeopleCodeType.Number, 0)
                    { Name = "PublicP", Visibility = MemberVisibility.Public },
                ["ProtectedP"] = new PropertyInfo(PeopleCodeType.Number, 0)
                    { Name = "ProtectedP", Visibility = MemberVisibility.Protected },
                ["PrivateP"] = new PropertyInfo(PeopleCodeType.Number, 0)
                    { Name = "PrivateP", Visibility = MemberVisibility.Private },
            },
        });
        return fake;
    }

    [Fact]
    public void Subclass_cannot_access_base_private_method()
    {
        var resolver = ResolverWithBaseHavingPrivateAndProtected();
        // Sub needs Base in inheritance for type inference of %This as Sub, and
        // %Super / base-typed access. Use a local of type Base for the access.
        var diags = RunClass(@"
import PKG:Base;

class Sub extends PKG:Base
   method Probe();
end-class;

method Probe
   Local PKG:Base &b;
   &b = create PKG:Base();
   &b.PrivateM();
end-method;
", "PKG:Sub", resolver);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("PrivateM", StringComparison.OrdinalIgnoreCase) &&
            d.Message.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Subclass_can_access_base_protected_method()
    {
        var resolver = ResolverWithBaseHavingPrivateAndProtected();
        // Wire Sub's base in SelfMetadata via extends + resolver BaseClassName for inheritance walk.
        // SelfMetadata from live program will have BaseClassName = PKG:Base when extracted.
        var diags = RunClass(@"
import PKG:Base;

class Sub extends PKG:Base
   method Probe();
end-class;

method Probe
   Local PKG:Base &b;
   &b = create PKG:Base();
   &b.ProtectedM();
end-method;
", "PKG:Sub", resolver);

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("ProtectedM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Subclass_can_access_base_public_method()
    {
        var resolver = ResolverWithBaseHavingPrivateAndProtected();
        var diags = RunClass(@"
import PKG:Base;

class Sub extends PKG:Base
   method Probe();
end-class;

method Probe
   Local PKG:Base &b;
   &b = create PKG:Base();
   &b.PublicM();
end-method;
", "PKG:Sub", resolver);

        Assert.DoesNotContain(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("PublicM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Same_class_can_access_own_private()
    {
        var diags = RunClass(@"
class Sample
   method PublicEntry();
private
   method Hidden();
end-class;

method PublicEntry
   %This.Hidden();
end-method;

method Hidden
end-method;
", "PKG:Sample", new FakeTypeMetadataResolver());

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InaccessibleMember);
    }

    [Fact]
    public void External_class_cannot_access_other_private()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Other", new TypeMetadata
        {
            QualifiedName = "PKG:Other",
            Name = "Other",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["Secret"] = new FunctionInfo { Name = "Secret", Visibility = MemberVisibility.Private },
            },
        });

        var diags = RunClass(@"
import PKG:Other;

class Client
   method Call();
end-class;

method Call
   Local PKG:Other &o;
   &o = create PKG:Other();
   &o.Secret();
end-method;
", "PKG:Client", fake);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sibling_cannot_access_other_siblings_protected()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Base", new TypeMetadata
        {
            QualifiedName = "PKG:Base",
            Name = "Base",
            Kind = ProgramKind.AppClass,
        });
        fake.AddClass("PKG:Sibling", new TypeMetadata
        {
            QualifiedName = "PKG:Sibling",
            Name = "Sibling",
            Kind = ProgramKind.AppClass,
            BaseClassName = "PKG:Base",
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["FamilyOnly"] = new FunctionInfo
                {
                    Name = "FamilyOnly",
                    Visibility = MemberVisibility.Protected,
                },
            },
        });

        // Sub also extends Base — sibling of Sibling, not a subclass of Sibling.
        var diags = RunClass(@"
import PKG:Base;
import PKG:Sibling;

class Sub extends PKG:Base
   method Probe();
end-class;

method Probe
   Local PKG:Sibling &s;
   &s = create PKG:Sibling();
   &s.FamilyOnly();
end-method;
", "PKG:Sub", fake);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("FamilyOnly", StringComparison.OrdinalIgnoreCase) &&
            d.Message.Contains("protected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Instance_variable_is_private_from_outside()
    {
        var fake = new FakeTypeMetadataResolver();
        fake.AddClass("PKG:Holder", new TypeMetadata
        {
            QualifiedName = "PKG:Holder",
            Name = "Holder",
            Kind = ProgramKind.AppClass,
            InstanceVariables = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["&Secret"] = new PropertyInfo(PeopleCodeType.Number, 0)
                {
                    Name = "&Secret",
                    Visibility = MemberVisibility.Private,
                },
            },
        });

        var diags = RunClass(@"
import PKG:Holder;

class Client
   method Read();
end-class;

method Read
   Local PKG:Holder &h;
   Local number &n;
   &h = create PKG:Holder();
   &n = &h.Secret;
end-method;
", "PKG:Client", fake);

        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InaccessibleMember &&
            d.Message.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }
}
