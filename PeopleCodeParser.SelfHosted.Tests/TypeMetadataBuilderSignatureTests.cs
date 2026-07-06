using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Covers the abstract/concrete member signature sets added to TypeMetadata, consumed by
/// AppRefiner's UnimplementedAbstractMembers styler. Signature scheme is owned by
/// TypeMetadata.MethodSignature / TypeMetadata.PropertySignature so the builder and
/// the styler cannot drift.
/// </summary>
public class TypeMetadataBuilderSignatureTests
{
    [Fact]
    public void Interface_members_are_all_abstract_signatures()
    {
        const string src = @"
interface ISimple
   method DoIt();
   property string Name get;
end-interface;
";
        var (program, _) = Parse(src);
        var meta = TypeMetadataBuilder.ExtractMetadata(program, "PKG:ISimple");

        Assert.Equal(ProgramKind.Interface, meta.Kind);
        Assert.Contains(TypeMetadata.MethodSignature("DoIt", 0), meta.AbstractMemberSignatures);
        Assert.Contains(TypeMetadata.PropertySignature("Name"), meta.AbstractMemberSignatures);
        Assert.Empty(meta.ConcreteMemberSignatures);
    }

    [Fact]
    public void Class_splits_abstract_and_concrete_signatures()
    {
        const string src = @"
class Base
   method Base();
   method DoAbstract() abstract;
   method DoConcrete(&p as string);
   property number Count abstract;
   property string Title;
end-class;

method Base
end-method;

method DoConcrete
end-method;
";
        var (program, _) = Parse(src);
        var meta = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Base");

        Assert.Equal(ProgramKind.AppClass, meta.Kind);

        // Abstract requirements: abstract method + abstract property only.
        Assert.Contains(TypeMetadata.MethodSignature("DoAbstract", 0), meta.AbstractMemberSignatures);
        Assert.Contains(TypeMetadata.PropertySignature("Count"), meta.AbstractMemberSignatures);
        Assert.DoesNotContain(TypeMetadata.MethodSignature("DoConcrete", 1), meta.AbstractMemberSignatures);

        // Concrete members: non-abstract method + non-abstract property only.
        Assert.Contains(TypeMetadata.MethodSignature("DoConcrete", 1), meta.ConcreteMemberSignatures);
        Assert.Contains(TypeMetadata.PropertySignature("Title"), meta.ConcreteMemberSignatures);
        Assert.DoesNotContain(TypeMetadata.MethodSignature("DoAbstract", 0), meta.ConcreteMemberSignatures);

        // The constructor is excluded from both sets.
        Assert.DoesNotContain(TypeMetadata.MethodSignature("Base", 0), meta.AbstractMemberSignatures);
        Assert.DoesNotContain(TypeMetadata.MethodSignature("Base", 0), meta.ConcreteMemberSignatures);
    }
}
