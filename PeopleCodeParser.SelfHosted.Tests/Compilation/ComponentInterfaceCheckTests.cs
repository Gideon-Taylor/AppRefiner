using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

/// <summary>
/// Members of a named CI (and of its known items) are checked against the CI's definition:
/// unknown members, read-only assignments, disabled standard methods and unknown CompIntfc.X
/// references. Nothing new is reported unless the definition lookup succeeds (or, for
/// references, definitively says the CI is missing).
/// </summary>
public class ComponentInterfaceCheckTests
{
    private const string GetCi = "Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);\n";
    private const string GetRole = GetCi + "Local ApiObject &role = &ci.Roles.Item(1);\n";

    private static IReadOnlyList<CompileDiagnostic> Run(string source, bool unknownLookup = false,
        CompIntfcDefinition? definition = null)
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddComponentInterface(definition ?? ComponentInterfaceFixtures.UserProfile());
        resolver.SetComponentInterfaceUnknown(unknownLookup);

        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        var programMetadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, programMetadata, resolver);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null));
    }

    private static void AssertSingleError(IReadOnlyList<CompileDiagnostic> diags, string message)
    {
        var diag = Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(message, diag.Message);
    }

    // Each case that reports with a found definition, used again with an Unknown lookup
    public static TheoryData<string> ReportingCases => new()
    {
        GetCi + "Local any &x = &ci.Bogus;",
        GetCi + "&ci.Bogus();",
        GetRole + "Local any &x = &role.Bogus;",
        GetCi + "&ci.WorklistEntriesCount = 1;",
        GetRole + "&role.Dynamic = \"Y\";",
        GetCi + "&ci.AccountLockDate = \"x\";",
        "Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.NO_SUCH);",
    };

    [Fact]
    public void Unknown_property_on_named_ci()
    {
        var diags = Run(GetCi + "Local any &x = &ci.Bogus;");

        AssertSingleError(diags,
            "'Bogus' is not a property of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 1)");
        Assert.Equal(DiagnosticCode.InvalidMemberAccess, diags[0].Code);
    }

    [Fact]
    public void Unknown_method_on_named_ci()
    {
        var diags = Run(GetCi + "&ci.Bogus();");

        AssertSingleError(diags,
            "'Bogus' is not a method of CompIntfc.USER_PROFILE (&ci is ComponentInterface, assigned on line 1)");
    }

    [Fact]
    public void Custom_and_standard_methods_ok()
    {
        var diags = Run(GetCi + "Local boolean &b = &ci.SetPassword(\"x\");\n&b = &ci.Get();\nLocal any &v = &ci.GetPropertyByName(\"UserID\");");

        Assert.Empty(diags);
    }

    [Fact]
    public void Known_members_ok()
    {
        var diags = Run(GetRole
            + "Local any &a = &ci.UserID;\nLocal any &c = &ci.Roles;\nLocal any &n = &role.RoleName;\n"
            + "Local any &rc = &role.RouteControls;\n&ci.InteractiveMode = True;\n&n = &ci.WorklistEntriesCount;\n&n = &role.Dynamic;");

        Assert.Empty(diags);
    }

    [Fact]
    public void Key_only_name_is_a_property()
    {
        var diags = Run(GetCi + "&ci.OperatorID = \"PS\";");

        Assert.Empty(diags);
    }

    [Fact]
    public void Unknown_property_on_item()
    {
        var diags = Run(GetRole + "Local any &x = &role.Bogus;");

        AssertSingleError(diags,
            "'Bogus' is not a property of CompIntfc.USER_PROFILE.Roles (&role is DataItem, assigned on line 2)");
    }

    [Fact]
    public void Read_only_assignment()
    {
        AssertSingleError(Run(GetCi + "&ci.WorklistEntriesCount = 1;"),
            "'WorklistEntriesCount' is read-only on CompIntfc.USER_PROFILE");

        Assert.Empty(Run(GetCi + "Local number &n = &ci.WorklistEntriesCount;"));
    }

    [Fact]
    public void Read_only_on_item()
    {
        AssertSingleError(Run(GetRole + "&role.Dynamic = \"Y\";"),
            "'Dynamic' is read-only on CompIntfc.USER_PROFILE.Roles");
    }

    [Fact]
    public void Disabled_standard_method()
    {
        var definition = ComponentInterfaceFixtures.UserProfile(
            CompIntfcStandardMethods.Cancel | CompIntfcStandardMethods.Create
            | CompIntfcStandardMethods.Get | CompIntfcStandardMethods.Save);

        AssertSingleError(Run(GetCi + "Local any &found = &ci.Find();", definition: definition),
            "CompIntfc.USER_PROFILE doesn't enable Find()");
        Assert.Empty(Run(GetCi + "Local boolean &b = &ci.Get();", definition: definition));
    }

    [Fact]
    public void Unknown_ci_reference()
    {
        var diags = Run("Local ApiObject &ci = %Session.GetCompIntfc(CompIntfc.NO_SUCH);");

        AssertSingleError(diags, "Component interface 'NO_SUCH' doesn't exist");
    }

    [Fact]
    public void Unknown_ci_reference_outside_GetCompIntfc()
    {
        var diags = Run("Local any &r = CompIntfc.NO_SUCH;");

        AssertSingleError(diags, "Component interface 'NO_SUCH' doesn't exist");
    }

    [Fact]
    public void Known_ci_reference_ok()
    {
        Assert.Empty(Run("Local any &r = CompIntfc.USER_PROFILE;"));
    }

    [Theory]
    [MemberData(nameof(ReportingCases))]
    public void Unknown_lookup_reports_nothing(string source)
    {
        Assert.Empty(Run(source, unknownLookup: true));
    }

    [Fact]
    public void Unknown_lookup_reports_nothing_for_disabled_method()
    {
        var definition = ComponentInterfaceFixtures.UserProfile(CompIntfcStandardMethods.Get);

        Assert.Empty(Run(GetCi + "Local any &found = &ci.Find();", unknownLookup: true, definition: definition));
    }

    [Fact]
    public void Unnamed_ci_unchanged()
    {
        var diags = Run("Local string &name;\nLocal ApiObject &ci = %Session.GetCompIntfc(&name);\n&ci.Bogus();\nLocal any &x = &ci.Bogus;");

        Assert.Empty(diags);
    }

    [Fact]
    public void Item_has_only_ItemNum_besides_its_fields()
    {
        // ItemNum is the only documented DataItem property; the CI's own builtin properties aren't an item's
        Assert.Empty(Run(GetRole + "Local number &n = &role.ItemNum;"));
        AssertSingleError(Run(GetRole + "Local any &m = &role.InteractiveMode;"),
            "'InteractiveMode' is not a property of CompIntfc.USER_PROFILE.Roles (&role is DataItem, assigned on line 2)");
        AssertSingleError(Run(GetRole + "Local any &x = &role.Bogus;"),
            "'Bogus' is not a property of CompIntfc.USER_PROFILE.Roles (&role is DataItem, assigned on line 2)");
    }

    private static void AssertSingleWarning(IReadOnlyList<CompileDiagnostic> diags, string message)
    {
        var diag = Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
        Assert.Equal(message, diag.Message);
    }

    // Annotations naming an unknown CI or path, one per place an annotation can go
    public static TheoryData<string, string> UnknownAnnotationCases => new()
    {
        { "Local ApiObject /* as CompIntfc.NO_SUCH */ &x;", "Component interface 'NO_SUCH' doesn't exist" },
        { "Local ApiObject /* as CompIntfc.USER_PROFILE.Bogus */ &x;", "'Bogus' is not a collection of CompIntfc.USER_PROFILE" },
        { "Local ApiObject /* as CompIntfc.user_profile.roles.Bogus[] */ &x;", "'Bogus' is not a collection of CompIntfc.USER_PROFILE.Roles" },
        { "Local ApiObject /* as CompIntfc.USER_PROFILE.Bogus.RouteControls */ &x;", "'Bogus' is not a collection of CompIntfc.USER_PROFILE" },
        { "Local any &a;\nLocal ApiObject &x = &a /* as CompIntfc.NO_SUCH */;", "Component interface 'NO_SUCH' doesn't exist" },
        { "Function F(&p As ApiObject /* as CompIntfc.USER_PROFILE.Bogus */)\nEnd-Function;", "'Bogus' is not a collection of CompIntfc.USER_PROFILE" },
        { "Function F() Returns ApiObject /* as CompIntfc.NO_SUCH */\n   Return Null;\nEnd-Function;", "Component interface 'NO_SUCH' doesn't exist" },
    };

    [Theory]
    [MemberData(nameof(UnknownAnnotationCases))]
    public void Annotation_with_unknown_ci_or_path_warns(string source, string message)
    {
        AssertSingleWarning(Run(source), message);
    }

    [Theory]
    [MemberData(nameof(UnknownAnnotationCases))]
    public void Annotation_with_unknown_lookup_is_silent(string source, string message)
    {
        _ = message;
        Assert.Empty(Run(source, unknownLookup: true));
    }

    [Theory]
    [InlineData("Local ApiObject /* as CompIntfc.USER_PROFILE */ &x;")]
    [InlineData("Local ApiObject /* as CompIntfc.USER_PROFILE.Roles */ &x;")]
    [InlineData("Local ApiObject /* as CompIntfc.USER_PROFILE.roles.RouteControls[] */ &x;")]
    [InlineData("Local any &a;\nLocal ApiObject &x = &a /* as CompIntfc.USER_PROFILE.EmailAddresses */;")]
    public void Annotation_with_known_path_is_silent(string source)
    {
        Assert.Empty(Run(source));
    }

    [Fact]
    public void Annotated_class_property_with_unknown_path_warns()
    {
        var diags = Run("class C\n   property ApiObject /* as CompIntfc.USER_PROFILE.Bogus[] */ Profile;\nend-class;");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message == "'Bogus' is not a collection of CompIntfc.USER_PROFILE");
    }

    [Fact]
    public void Property_type_mismatch_on_assign()
    {
        var diags = Run(GetCi + "&ci.AccountLockDate = \"x\";");

        var diag = Assert.Single(diags);
        Assert.Equal(DiagnosticCode.TypeError, diag.Code);
        Assert.Equal("Cannot assign type 'string' to 'date'", diag.Message);
    }
}
