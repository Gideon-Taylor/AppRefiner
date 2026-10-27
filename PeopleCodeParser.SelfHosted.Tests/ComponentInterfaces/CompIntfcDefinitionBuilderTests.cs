using PeopleCodeParser.SelfHosted.ComponentInterfaces;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.ComponentInterfaces;

/// <summary>
/// The CI definition is built from PSBCITEM rows (attached by BCITEMPARENT, ordered by
/// SEQUENCE_NBR_6) and the exported functions of the CI's Methods PeopleCode.
/// </summary>
public class CompIntfcDefinitionBuilderTests
{
    private const string Blank = " ";

    private static CompIntfcItemRow Row(int bcType, string parent, string name, int sequence, int access,
        string record, string field, string? comment = Blank, PeopleCodeType type = PeopleCodeType.String) =>
        new(bcType, parent, name, sequence, access, record, field, comment, type);

    // A subset of delivered USER_PROFILE's PSBCITEM rows; deliberately not in sequence order
    private static readonly CompIntfcItemRow[] UserProfileRows =
    {
        Row(4, "RouteControls", "RouteControlProfile", 41, 1, "RTE_CNTL_USERVW", "RTE_CNTL_PROFILE"),
        Row(3, "Roles", "RouteControls", 40, 1, "RTE_CNTL_USERVW", Blank),
        Row(1, "PS_ROOT", "UserID", 1, 1, "PSOPRDEFN_SRCH", "OPRID"),
        Row(2, "PS_ROOT", "UserID", 2, 1, "PSOPRDEFN_SRCH", "OPRID"),
        Row(4, "PS_ROOT", "UserID", 3, 1, "PSOPRDEFN", "OPRID"),
        Row(4, "PS_ROOT", "UserIDAlias", 4, 1, "PSOPRDEFN", "USERIDALIAS"),
        Row(4, "PS_ROOT", "UserDescription", 5, 1, "PSOPRDEFN", "OPRDEFNDESC"),
        Row(4, "PS_ROOT", "MultiLanguageEnabled", 6, 1, "PSOPRDEFN", "MULTILANG", "1 for True, 0 for False", PeopleCodeType.Number),
        Row(4, "PS_ROOT", "WorklistEntriesCount", 7, 2, "PSOPRDEFN", "WORKLISTENTRIES", Blank, PeopleCodeType.Number),
        Row(3, "PS_ROOT", "EmailAddresses", 10, 1, "PSUSEREMAIL", Blank),
        Row(4, "EmailAddresses", "EmailType", 11, 1, "PSUSEREMAIL", "EMAILTYPE"),
        Row(4, "EmailAddresses", "EmailAddress", 12, 1, "PSUSEREMAIL", "EMAILID"),
        Row(3, "PS_ROOT", "IDTypes", 20, 1, "PSOPRALIAS", Blank),
        Row(4, "IDTypes", "IDType", 21, 1, "PSOPRALIAS", "OPRALIASTYPE"),
        Row(3, "IDTypes", "Attributes", 22, 1, "PSOPRALIASFIELD", Blank),
        Row(4, "Attributes", "AttributeName", 23, 1, "PSOPRALIASFIELD", "FIELDNAME"),
        Row(4, "Attributes", "AttributeValue", 24, 1, "PSOPRALIASFIELD", "OPRALIASVALUE"),
        Row(3, "PS_ROOT", "Roles", 30, 1, "PSROLEUSER_VW", Blank),
        Row(4, "Roles", "RoleName", 31, 1, "PSROLEUSER_VW", "ROLENAME"),
        Row(4, "Roles", "Dynamic", 32, 2, "PSROLEUSER_VW", "DYNAMIC_SW"),
    };

    private static CompIntfcDefinition Build(IEnumerable<CompIntfcItemRow>? rows = null, int bits = 0,
        string? methodsSource = null) =>
        CompIntfcDefinitionBuilder.Build("USER_PROFILE", 3, bits, rows ?? UserProfileRows, methodsSource);

    [Fact]
    public void Builds_tree_from_parents()
    {
        var definition = Build();

        Assert.Equal("USER_PROFILE", definition.Name);
        Assert.Equal(3, definition.Version);
        Assert.Equal(new[] { "EmailAddresses", "IDTypes", "Roles" }, definition.Collections.Keys);

        var roles = definition.Collections["Roles"];
        Assert.Equal("PSROLEUSER_VW", roles.RecordName);
        var routeControls = roles.Collections["RouteControls"];
        Assert.Equal("RTE_CNTL_USERVW", routeControls.RecordName);
        Assert.Equal("RTE_CNTL_PROFILE", routeControls.Properties["RouteControlProfile"].FieldName);

        var attributes = definition.FindNode("IDTypes.Attributes");
        Assert.NotNull(attributes);
        Assert.Equal("PSOPRALIASFIELD", attributes!.RecordName);
        Assert.Equal(new[] { "AttributeName", "AttributeValue" }, attributes.Properties.Keys);

        var userId = definition.Properties["userid"];
        Assert.Equal("PSOPRDEFN", userId.RecordName);
        Assert.Equal("OPRID", userId.FieldName);
        Assert.Equal(PeopleCodeType.String, userId.FieldType);
    }

    [Fact]
    public void Keys_are_listed()
    {
        var definition = Build();

        var getKey = Assert.Single(definition.GetKeys);
        Assert.Equal(("UserID", "PSOPRDEFN_SRCH", "OPRID"), (getKey.Name, getKey.RecordName, getKey.FieldName));
        var createKey = Assert.Single(definition.CreateKeys);
        Assert.Equal(("UserID", "PSOPRDEFN_SRCH", "OPRID"), (createKey.Name, createKey.RecordName, createKey.FieldName));
        Assert.Empty(definition.FindKeys);
    }

    [Fact]
    public void Keys_are_ordered_by_sequence()
    {
        var rows = new[]
        {
            Row(5, "PS_ROOT", "UserDescription", 9, 1, "PSOPRDEFN_SRCH", "OPRDEFNDESC"),
            Row(5, "PS_ROOT", "UserID", 8, 1, "PSOPRDEFN_SRCH", "OPRID"),
        };

        var definition = Build(rows);

        Assert.Equal(new[] { "UserID", "UserDescription" }, definition.FindKeys.Select(k => k.Name));
    }

    [Fact]
    public void Access_2_is_read_only()
    {
        var definition = Build();

        Assert.True(definition.Properties["WorklistEntriesCount"].IsReadOnly);
        Assert.False(definition.Properties["UserID"].IsReadOnly);
        Assert.True(definition.Collections["Roles"].Properties["Dynamic"].IsReadOnly);
        Assert.False(definition.Collections["Roles"].Properties["RoleName"].IsReadOnly);
    }

    [Fact]
    public void Blank_comment_is_null()
    {
        var definition = Build();

        Assert.Null(definition.Properties["UserID"].Comment);
        Assert.Equal("1 for True, 0 for False", definition.Properties["MultiLanguageEnabled"].Comment);

        var rows = new[] { Row(4, "PS_ROOT", "UserID", 1, 1, "PSOPRDEFN", "OPRID", "") };
        Assert.Null(Build(rows).Properties["UserID"].Comment);
    }

    [Fact]
    public void Standard_method_bits()
    {
        var definition = Build(bits: 4 | 8);

        Assert.Equal(CompIntfcStandardMethods.Find | CompIntfcStandardMethods.Get, definition.StandardMethods);
        Assert.True(definition.IsStandardMethodEnabled("Find"));
        Assert.True(definition.IsStandardMethodEnabled("Get"));
        Assert.False(definition.IsStandardMethodEnabled("Save"));
    }

    [Fact]
    public void Methods_from_source_skip_NoExport()
    {
        var definition = Build(methodsSource:
            "Function SetPassword(&p As string) Returns boolean\n   Return True;\nEnd-Function;\n" +
            "Function Helper() NoExport\nEnd-Function;");

        var method = Assert.Single(definition.CustomMethods.Values);
        Assert.Equal("SetPassword", method.Name);
        Assert.Same(method, definition.CustomMethods["setpassword"]);
        Assert.Equal(PeopleCodeType.Boolean, method.Signature.ReturnType.Type);
        var parameter = Assert.IsType<SingleParameter>(Assert.Single(method.Signature.Parameters));
        Assert.Equal(PeopleCodeType.String, parameter.ParameterType.Type);
    }

    [Fact]
    public void Declared_functions_are_not_methods()
    {
        var definition = Build(methodsSource:
            "Declare Function Other PeopleCode FUNCLIB_X.FIELD FieldFormula;\n" +
            "Function SetPassword(&p As string) Returns boolean\n   Return True;\nEnd-Function;");

        Assert.Equal(new[] { "SetPassword" }, definition.CustomMethods.Keys);
    }

    [Fact]
    public void No_methods_source_means_none()
    {
        Assert.Empty(Build(methodsSource: null).CustomMethods);
        Assert.Empty(Build(methodsSource: "").CustomMethods);
    }

    [Fact]
    public void Unknown_field_type_is_any()
    {
        var rows = new[] { Row(4, "PS_ROOT", "Mystery", 1, 1, "PSOPRDEFN", "MYSTERY", Blank, PeopleCodeType.Unknown) };

        var definition = Build(rows);

        Assert.Equal(PeopleCodeType.Unknown, definition.Properties["Mystery"].FieldType);
    }

    [Fact]
    public void Orphan_row_is_skipped()
    {
        var rows = UserProfileRows.Append(Row(4, "NoSuchCollection", "Ghost", 99, 1, "PSOPRDEFN", "OPRID"));

        var definition = Build(rows);

        Assert.False(definition.Properties.ContainsKey("Ghost"));
        Assert.DoesNotContain(definition.Collections.Values, c => c.Properties.ContainsKey("Ghost"));
        Assert.Equal(3, definition.Collections.Count);
    }

    [Fact]
    public void Row_with_null_parent_is_skipped()
    {
        var rows = UserProfileRows.Append(Row(4, null!, "Ghost", 99, 1, "PSOPRDEFN", "OPRID"));

        var definition = Build(rows);

        Assert.False(definition.Properties.ContainsKey("Ghost"));
        Assert.DoesNotContain(definition.Collections.Values, c => c.Properties.ContainsKey("Ghost"));
    }
}
