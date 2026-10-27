using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// App class names are compared whole. App Designer qualifies every class name on save and
/// inference qualifies the edited program's short names through its imports, so a short name
/// left over is one no import resolves, and it doesn't stand for any package's class.
/// </summary>
public class AppClassShortNameTests
{
    [Fact]
    public void Bare_type_rejects_qualified_value_with_same_class_name()
    {
        Assert.False(new AppClassTypeInfo("Config").IsAssignableFrom(new AppClassTypeInfo("PKG:Config")));
        Assert.False(new AppClassTypeInfo("PKG:Config").IsAssignableFrom(new AppClassTypeInfo("Config")));
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
    public void Array_of_class_compares_qualified_names()
    {
        var expected = new ArrayTypeInfo(1, new AppClassTypeInfo("PKG:Config"));

        Assert.True(expected.IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("pkg:config"))));
        Assert.False(expected.IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("OTHER:Config"))));
        Assert.False(expected.IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("Config"))));
    }
}
