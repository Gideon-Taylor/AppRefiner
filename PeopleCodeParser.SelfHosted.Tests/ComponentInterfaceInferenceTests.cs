using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// GetCompIntfc(CompIntfc.X) tags the CI with its name, and members of a named CI (or of an item
/// whose collection path is known) are typed from the CI's definition.
/// </summary>
public class ComponentInterfaceInferenceTests
{
    private const string GetCi = "Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);\n";

    private static ProgramNode Infer(string source, bool unknownLookup = false)
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddComponentInterface(ComponentInterfaceFixtures.UserProfile());
        resolver.SetComponentInterfaceUnknown(unknownLookup);

        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, resolver);
        return program;
    }

    private static TypeInfo LastUseType(ProgramNode program, string name)
    {
        var use = program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
        var type = use.GetInferredType();
        Assert.NotNull(type);
        return type!;
    }

    private static TypeInfo MemberType(ProgramNode program, string memberName)
    {
        // The last use in source order (an outer chain link is visited before its target)
        var member = program.FindDescendants<MemberAccessNode>()
            .Where(m => m.MemberName == memberName)
            .OrderBy(m => m.MemberNameSpan.Start.Index)
            .Last();
        var node = member.Parent is FunctionCallNode call && ReferenceEquals(call.Function, member) ? (AstNode)call : member;
        var type = node.GetInferredType();
        Assert.NotNull(type);
        return type!;
    }

    [Fact]
    public void Ci_from_GetCompIntfc_is_tagged()
    {
        var program = Infer(GetCi + "Local any &x = &ci;");

        var type = LastUseType(program, "&ci");
        Assert.Equal(PeopleCodeType.ComponentInterface, type.PeopleCodeType);
        Assert.Equal("USER_PROFILE", type.EffectiveIdentity?.CompIntfcName);
    }

    [Fact]
    public void Property_has_field_type()
    {
        var program = Infer(GetCi + "&s = &ci.UserID;\n&d = &ci.AccountLockDate;");

        var userId = MemberType(program, "UserID");
        Assert.Equal(PeopleCodeType.String, userId.PeopleCodeType);
        Assert.True(userId.IsAssignable);
        Assert.Equal(PeopleCodeType.Date, MemberType(program, "AccountLockDate").PeopleCodeType);
    }

    [Fact]
    public void Collection_and_item_carry_path()
    {
        var program = Infer(GetCi + "&c = &ci.Roles;\n&r = &ci.Roles.Item(1);");

        var collection = MemberType(program, "Roles");
        Assert.Equal(PeopleCodeType.DataCollection, collection.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), collection.Identity);

        var item = MemberType(program, "Item");
        Assert.Equal(PeopleCodeType.DataItem, item.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), item.Identity);
    }

    [Theory]
    [InlineData("ItemByKeys(\"X\")")]
    [InlineData("CurrentItem()")]
    [InlineData("GetEffectiveItem(%Date, 1)")]
    [InlineData("InsertItem(1)")]
    public void Other_item_methods_carry_path(string call)
    {
        var program = Infer(GetCi + $"&r = &ci.Roles.{call};");

        var item = MemberType(program, call.Substring(0, call.IndexOf('(')));
        Assert.Equal(PeopleCodeType.DataItem, item.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), item.Identity);
    }

    [Fact]
    public void Nested_collection_path()
    {
        var program = Infer(GetCi + "&rc = &ci.Roles.Item(1).RouteControls.Item(1);");

        var item = MemberType(program, "Item");
        Assert.Equal(PeopleCodeType.DataItem, item.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles.RouteControls"), item.Identity);
    }

    [Fact]
    public void Item_property_type()
    {
        var program = Infer(GetCi + "Local ApiObject &role = &ci.Roles.Item(1);\n&n = &role.RoleName;");

        Assert.Equal(PeopleCodeType.String, MemberType(program, "RoleName").PeopleCodeType);
    }

    [Fact]
    public void Custom_method_returns_declared_type()
    {
        var program = Infer(GetCi + "&b = &ci.SetPassword(\"x\");");

        Assert.Equal(PeopleCodeType.Boolean, MemberType(program, "SetPassword").PeopleCodeType);
        var call = program.FindDescendants<FunctionCallNode>()
            .Single(c => c.Function is MemberAccessNode { MemberName: "SetPassword" });
        Assert.Equal("SetPassword", call.GetFunctionInfo()?.Name);
    }

    [Fact]
    public void Annotated_item_parameter_is_typed()
    {
        var program = Infer(
            "Function F(&r As ApiObject /* as CompIntfc.USER_PROFILE.Roles */)\n   Local any &n = &r.RoleName;\nEnd-Function;");

        Assert.Equal(PeopleCodeType.String, MemberType(program, "RoleName").PeopleCodeType);
    }

    [Fact]
    public void Unknown_lookup_keeps_any()
    {
        var program = Infer(GetCi + "&s = &ci.UserID;", unknownLookup: true);

        Assert.Equal(TypeKind.Any, MemberType(program, "UserID").Kind);
        Assert.Equal("USER_PROFILE", LastUseType(program, "&ci").EffectiveIdentity?.CompIntfcName);
    }

    [Fact]
    public void Missing_ci_keeps_any()
    {
        var program = Infer("Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.OTHER_CI);\n&s = &ci.UserID;");

        Assert.Equal(TypeKind.Any, MemberType(program, "UserID").Kind);
        Assert.Equal("OTHER_CI", LastUseType(program, "&ci").EffectiveIdentity?.CompIntfcName);
    }

    [Fact]
    public void Name_not_in_definition_keeps_any()
    {
        var program = Infer(GetCi + "&s = &ci.NoSuchProp;\n&t = &ci.Roles.Item(1).NoSuchProp;");

        Assert.All(program.FindDescendants<MemberAccessNode>().Where(m => m.MemberName == "NoSuchProp"),
            m => Assert.Equal(TypeKind.Any, m.GetInferredType()?.Kind));
    }

    [Fact]
    public void Unnamed_ci_unchanged()
    {
        var program = Infer("Local string &name;\nLocal ApiObject &ci = %Session.GetCompIntfc(&name);\n&s = &ci.UserID;");

        var ci = LastUseType(program, "&ci");
        Assert.Equal(PeopleCodeType.ComponentInterface, ci.PeopleCodeType);
        Assert.Null(ci.EffectiveIdentity?.CompIntfcName);
        Assert.Equal(TypeKind.Any, MemberType(program, "UserID").Kind);
    }
}
