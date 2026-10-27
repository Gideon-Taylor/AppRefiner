using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// The USER_PROFILE component interface definition shared by the CI inference and check tests.
/// </summary>
internal static class ComponentInterfaceFixtures
{
    private static Dictionary<string, T> Dict<T>(params (string Key, T Value)[] entries)
    {
        var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries) dict[key] = value;
        return dict;
    }

    private static CompIntfcProperty Prop(string name, PeopleCodeType type, bool readOnly = false,
        string record = "PSOPRDEFN", string field = "") =>
        new(name, record, field.Length > 0 ? field : name.ToUpperInvariant(), type, readOnly, null);

    /// <summary>
    /// USER_PROFILE: UserID, read-only WorklistEntriesCount and AccountLockDate at the root, a
    /// get key OPRID with no property row, Roles (with read-only Dynamic and nested RouteControls)
    /// and EmailAddresses collections, and a SetPassword custom method. Every standard method is
    /// enabled unless <paramref name="standardMethods"/> says otherwise.
    /// </summary>
    public static CompIntfcDefinition UserProfile(
        CompIntfcStandardMethods standardMethods = CompIntfcStandardMethods.Cancel | CompIntfcStandardMethods.Create
            | CompIntfcStandardMethods.Find | CompIntfcStandardMethods.Get | CompIntfcStandardMethods.Save)
    {
        // The custom method's signature comes from the CI's Methods PeopleCode, as the loader builds it
        var (methods, methodErrors) = Parse("Function SetPassword(&pwd As string) Returns boolean\n   Return True;\nEnd-Function;");
        Assert.Empty(methodErrors);
        var setPassword = TypeMetadataBuilder.ExtractMetadata(methods).Methods["SetPassword"];

        return new CompIntfcDefinition
        {
            Name = "USER_PROFILE",
            StandardMethods = standardMethods,
            Properties = Dict(
                ("UserID", Prop("UserID", PeopleCodeType.String, field: "OPRID")),
                ("WorklistEntriesCount", Prop("WorklistEntriesCount", PeopleCodeType.Number, readOnly: true)),
                ("AccountLockDate", Prop("AccountLockDate", PeopleCodeType.Date))),
            GetKeys = new[] { Prop("OperatorID", PeopleCodeType.String, field: "OPRID") },
            Collections = Dict(
                ("Roles", new CompIntfcCollection
                {
                    Name = "Roles",
                    RecordName = "PSROLEUSER_VW",
                    Properties = Dict(
                        ("RoleName", Prop("RoleName", PeopleCodeType.String, record: "PSROLEUSER_VW", field: "ROLENAME")),
                        ("Dynamic", Prop("Dynamic", PeopleCodeType.String, readOnly: true, record: "PSROLEUSER_VW", field: "DYNAMIC_SW"))),
                    Collections = Dict(
                        ("RouteControls", new CompIntfcCollection
                        {
                            Name = "RouteControls",
                            RecordName = "PSRTEPROF_VW",
                            Properties = Dict(
                                ("RouteControlProfile", Prop("RouteControlProfile", PeopleCodeType.String, record: "PSRTEPROF_VW", field: "RTE_CNTL_PROFILE"))),
                        })),
                }),
                ("EmailAddresses", new CompIntfcCollection
                {
                    Name = "EmailAddresses",
                    RecordName = "PSUSEREMAIL",
                    Properties = Dict(
                        ("EmailAddress", Prop("EmailAddress", PeopleCodeType.String, record: "PSUSEREMAIL", field: "EMAILID"))),
                })),
            CustomMethods = Dict(("SetPassword", new CompIntfcMethod("SetPassword", setPassword))),
        };
    }
}
