using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.ComponentInterfaces;

/// <summary>
/// CompIntfcTarget finds the definition level a tagged CI value refers to, and lists that
/// level's members the same way for inference, the checks and the editor.
/// </summary>
public class CompIntfcTargetTests
{
    private static FakeTypeMetadataResolver Resolver(bool unknown = false)
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddComponentInterface(ComponentInterfaceFixtures.UserProfile());
        resolver.SetComponentInterfaceUnknown(unknown);
        return resolver;
    }

    private static TypeInfo Ci(string name = "USER_PROFILE") =>
        TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface).WithIdentity(new TypeIdentity(CompIntfcName: name));

    private static TypeInfo Of(PeopleCodeType type, string path) =>
        TypeInfo.FromPeopleCodeType(type).WithIdentity(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: path));

    [Fact]
    public void Root_of_named_ci()
    {
        var ci = CompIntfcTarget.Find(Ci(), Resolver());

        Assert.NotNull(ci);
        Assert.True(ci!.IsRoot);
        Assert.Null(ci.Path);
        Assert.Equal("CompIntfc.USER_PROFILE", ci.DisplayName);
    }

    [Fact]
    public void Item_level_by_path()
    {
        var ci = CompIntfcTarget.Find(Of(PeopleCodeType.DataItem, "Roles.RouteControls"), Resolver());

        Assert.NotNull(ci);
        Assert.False(ci!.IsRoot);
        Assert.Equal("RouteControls", ci.Node.Name);
        Assert.Equal("CompIntfc.USER_PROFILE.Roles.RouteControls", ci.DisplayName);
    }

    [Fact]
    public void Nothing_without_a_found_definition()
    {
        Assert.Null(CompIntfcTarget.Find(Ci(), null));
        Assert.Null(CompIntfcTarget.Find(Ci(), Resolver(unknown: true)));
        Assert.Null(CompIntfcTarget.Find(Ci("OTHER_CI"), Resolver()));
        Assert.Null(CompIntfcTarget.Find(TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface), Resolver()));
        Assert.Null(CompIntfcTarget.Find(Of(PeopleCodeType.DataItem, "NoSuchCollection"), Resolver()));
    }

    [Fact]
    public void Collection_value_has_no_target()
    {
        // A DataCollection's members are the collection type's own
        Assert.Null(CompIntfcTarget.Find(Of(PeopleCodeType.DataCollection, "Roles"), Resolver()));
    }

    [Fact]
    public void Root_properties_include_keys_once()
    {
        var names = CompIntfcTarget.Find(Ci(), Resolver())!.AllProperties().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "UserID", "WorklistEntriesCount", "AccountLockDate", "OperatorID" }, names);
    }

    [Fact]
    public void Item_properties_are_its_level()
    {
        var names = CompIntfcTarget.Find(Of(PeopleCodeType.DataItem, "Roles"), Resolver())!.AllProperties().Select(p => p.Name);

        Assert.Equal(new[] { "RoleName", "Dynamic" }, names);
    }

    [Fact]
    public void Custom_methods_only_on_root()
    {
        Assert.Equal("SetPassword", CompIntfcTarget.Find(Ci(), Resolver())!.FindCustomMethod("setpassword")?.Name);
        Assert.Null(CompIntfcTarget.Find(Of(PeopleCodeType.DataItem, "Roles"), Resolver())!.FindCustomMethod("SetPassword"));
    }

    [Fact]
    public void Collection_type_extends_path()
    {
        var item = CompIntfcTarget.Find(Of(PeopleCodeType.DataItem, "Roles"), Resolver())!;
        var type = item.CollectionType(item.Node.Collections["RouteControls"]);

        Assert.Equal(PeopleCodeType.DataCollection, type.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles.RouteControls"), type.Identity);
    }

    [Fact]
    public void Property_value_type()
    {
        var root = CompIntfcTarget.Find(Ci(), Resolver())!;

        Assert.Equal(PeopleCodeType.Date, CompIntfcTarget.PropertyValueType(root.FindProperty("AccountLockDate")!).PeopleCodeType);
        Assert.Equal(TypeKind.Any, CompIntfcTarget.PropertyValueType(root.FindProperty("UserID")! with { FieldType = PeopleCodeType.Unknown }).Kind);
    }
}
