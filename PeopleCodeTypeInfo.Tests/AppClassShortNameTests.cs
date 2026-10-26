using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// A bare app class type (metadata keeps names as written, e.g. "Config") accepts a qualified
/// value with the same class name (weak-spots §3 approximation).
/// </summary>
public class AppClassShortNameTests
{
    [Fact]
    public void Bare_type_accepts_qualified_value_with_same_class_name()
    {
        Assert.True(new AppClassTypeInfo("Config").IsAssignableFrom(new AppClassTypeInfo("PKG:Config")));
        Assert.True(new AppClassTypeInfo("config").IsAssignableFrom(new AppClassTypeInfo("PKG:SUB:CONFIG")));
    }

    [Fact]
    public void Bare_type_accepts_same_bare_value()
    {
        Assert.True(new AppClassTypeInfo("Config").IsAssignableFrom(new AppClassTypeInfo("Config")));
    }

    [Fact]
    public void Bare_type_rejects_different_class_name()
    {
        Assert.False(new AppClassTypeInfo("Config").IsAssignableFrom(new AppClassTypeInfo("PKG:Other")));
        Assert.False(new AppClassTypeInfo("Config").IsAssignableFrom(new AppClassTypeInfo("Other")));
    }

    [Fact]
    public void Qualified_type_still_compares_qualified_names()
    {
        Assert.False(new AppClassTypeInfo("PKG:Config").IsAssignableFrom(new AppClassTypeInfo("OTHER:Config")));
        Assert.True(new AppClassTypeInfo("PKG:Config").IsAssignableFrom(new AppClassTypeInfo("pkg:config")));
    }

    [Fact]
    public void Array_of_bare_type_accepts_array_of_qualified_value()
    {
        var expected = new ArrayTypeInfo(1, new AppClassTypeInfo("Config"));

        Assert.True(expected.IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("PKG:Config"))));
        Assert.False(expected.IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("PKG:Other"))));
    }
}
