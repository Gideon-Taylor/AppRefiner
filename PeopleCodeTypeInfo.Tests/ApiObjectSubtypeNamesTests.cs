using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// The ApiObject subtype names a <c>/* as T */</c> annotation accepts as a single-part target.
/// </summary>
public class ApiObjectSubtypeNamesTests
{
    [Fact]
    public void Includes_known_subtypes()
    {
        var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
        Assert.Contains("Session", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ComponentInterface", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Excludes_non_api_types()
    {
        var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
        Assert.DoesNotContain("Rowset", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiObject", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_name_is_a_subtype_with_a_known_enum_value()
    {
        foreach (var name in PeopleCodeTypeDatabase.GetApiObjectSubtypeNames())
        {
            Assert.True(PeopleCodeTypeDatabase.IsApiObjectSubtype(name), name);
            Assert.NotEqual(PeopleCodeType.Unknown, BuiltinTypeExtensions.FromString(name));
        }
    }

    [Fact]
    public void Is_sorted_and_distinct()
    {
        var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
