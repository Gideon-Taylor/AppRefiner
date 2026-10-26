using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Consumer-side behavior for ApiObject and @ApiObject subtypes.
/// </summary>
public class ApiObjectTypeTests
{
    [Fact]
    public void ApiObjectSubtypes_Are_PeopleCodeType_Enum_Values()
    {
        Assert.Equal(PeopleCodeType.Session, BuiltinTypeExtensions.FromString("Session"));
        Assert.Equal(PeopleCodeType.ComponentInterface, BuiltinTypeExtensions.FromString("ComponentInterface"));
        Assert.Equal(PeopleCodeType.PSMessageCollection, BuiltinTypeExtensions.FromString("PSMessageCollection"));
        Assert.Equal(PeopleCodeType.ApiObject, BuiltinTypeExtensions.FromString("ApiObject"));
    }

    // Checked at run time in App Designer (2026-10): an item doesn't convert to Row, InsertItem
    // returns the new item, and DeleteItem assigns to a boolean
    [Theory]
    [InlineData("Item", "DataItem")]
    [InlineData("ItemByKeys", "DataItem")]
    [InlineData("CurrentItem", "DataItem")]
    [InlineData("GetEffectiveItem", "DataItem")]
    [InlineData("InsertItem", "DataItem")]
    [InlineData("DeleteItem", "boolean")]
    public void DataCollection_Item_Methods_Return_Items(string method, string returnType)
    {
        var info = PeopleCodeTypeDatabase.GetMethod("DataCollection", method);

        Assert.NotNull(info);
        Assert.Equal(returnType, info!.ReturnType.ToString(), ignoreCase: true);
    }

    [Fact]
    public void ApiObjectSubtypes_Are_BuiltinObjects()
    {
        Assert.True(PeopleCodeType.Session.IsBuiltinObject());
        Assert.True(PeopleCodeType.ComponentInterface.IsBuiltinObject());
        Assert.True(PeopleCodeType.Tree.IsBuiltinObject());
    }

    [Fact]
    public void TypeWithDimensionality_Parse_Uses_Enum_Not_AppClassPath()
    {
        var twd = TypeWithDimensionality.Parse("Session");
        Assert.Equal(PeopleCodeType.Session, twd.Type);
        Assert.Null(twd.AppClassPath);

        var ci = TypeWithDimensionality.Parse("ComponentInterface");
        Assert.Equal(PeopleCodeType.ComponentInterface, ci.Type);
        Assert.Null(ci.AppClassPath);
    }

    [Fact]
    public void ApiObject_Accepts_ApiObjectSubtype()
    {
        var apiObject = TypeInfo.FromPeopleCodeType(PeopleCodeType.ApiObject);
        var session = TypeInfo.FromPeopleCodeType(PeopleCodeType.Session);

        Assert.True(apiObject.IsAssignableFrom(session),
            "ApiObject should accept Session");
        Assert.True(apiObject.IsAssignableFrom(apiObject));
    }

    [Fact]
    public void ApiObject_DoesNotAccept_UnrelatedBuiltin()
    {
        var apiObject = TypeInfo.FromPeopleCodeType(PeopleCodeType.ApiObject);
        var rowset = TypeInfo.FromPeopleCodeType(PeopleCodeType.Rowset);

        Assert.False(apiObject.IsAssignableFrom(rowset),
            "ApiObject should not accept Rowset");
    }

    [Fact]
    public void IsDeclarableTypeName_Rejects_ApiObjectSubtypes_WhenPresent()
    {
        Assert.True(PeopleCodeTypeDatabase.IsDeclarableTypeName("ApiObject"));
        Assert.True(PeopleCodeTypeDatabase.IsDeclarableTypeName("Rowset"));

        if (PeopleCodeTypeDatabase.IsApiObjectSubtype("Session"))
        {
            Assert.False(PeopleCodeTypeDatabase.IsDeclarableTypeName("Session"));
        }

        if (PeopleCodeTypeDatabase.IsApiObjectSubtype("ComponentInterface"))
        {
            Assert.False(PeopleCodeTypeDatabase.IsDeclarableTypeName("ComponentInterface"));
        }
    }

    [Fact]
    public void ResolveType_Session_Is_BuiltinObjectTypeInfo()
    {
        var typeInfo = PeopleCodeTypeDatabase.ResolveType("Session");
        Assert.IsType<BuiltinObjectTypeInfo>(typeInfo);
        Assert.Equal(PeopleCodeType.Session, typeInfo.PeopleCodeType);
    }

    [Fact]
    public void SignatureParser_Uses_Enum_For_NamedReturnType()
    {
        var func = SignatureParser.ParseFunctionSignature(
            "GetCompIntfc(comp_intfc: @COMPINTFC|string) -> ComponentInterface");

        Assert.Equal(PeopleCodeType.ComponentInterface, func.ReturnType.Type);
        Assert.Null(func.ReturnType.AppClassPath);
    }

    [Fact]
    public void ToTypeInfo_Uses_Enum()
    {
        var twd = new TypeWithDimensionality(PeopleCodeType.Session, 0);
        var typeInfo = PeopleCodeTypeDatabase.ToTypeInfo(twd);
        Assert.Equal(PeopleCodeType.Session, typeInfo.PeopleCodeType);
    }
}
