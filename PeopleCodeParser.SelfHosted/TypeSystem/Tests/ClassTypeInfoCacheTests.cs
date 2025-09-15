using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

public class ClassTypeInfoCacheTests
{
    private static ClassMethodInfo CreateMethodInfo(string name)
    {
        return new ClassMethodInfo(name, MemberAccessibility.Public);
    }

    [Fact]
    public void TryGetClassInfo_ShouldReturnFalse_WhenNotCached()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        var found = PeopleCodeTypeRegistry.TryGetClassInfo("SamplePkg:SampleClass", out var classInfo);

        Assert.False(found);
        Assert.Null(classInfo);
    }

    [Fact]
    public void CacheClassInfo_ShouldStoreMetadata_ForLaterLookups()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        var property = new ClassPropertyInfo("MyProperty", MemberAccessibility.Public, PrimitiveTypeInfo.String);
        var method = new ClassMethodInfo(
            "DoSomething",
            MemberAccessibility.Protected,
            parameterTypes: new[] { PrimitiveTypeInfo.String },
            returnType: PrimitiveTypeInfo.Number);

        var classInfo = new ClassTypeInfo(
            "SamplePkg:SampleClass",
            baseClassName: "BasePkg:BaseClass",
            implementedInterfaces: new[] { "InterfaceA" },
            properties: new[] { property },
            methods: new[] { method });

        PeopleCodeTypeRegistry.CacheClassInfo(classInfo);

        Assert.True(PeopleCodeTypeRegistry.TryGetClassInfo("SamplePkg:SampleClass", out var cached));
        Assert.NotNull(cached);
        Assert.Equal("BasePkg:BaseClass", cached!.BaseClassName);
        Assert.Contains("InterfaceA", cached.ImplementedInterfaces);

        Assert.True(cached.Properties.ContainsKey("MyProperty"));
        Assert.True(cached.Properties.ContainsKey("myproperty"));
        var cachedProperty = cached.Properties["myproperty"];
        Assert.Equal(MemberAccessibility.Public, cachedProperty.Accessibility);
        Assert.Equal(PrimitiveTypeInfo.String, cachedProperty.Type);

        Assert.True(cached.Methods.ContainsKey("DoSomething"));
        Assert.True(cached.Methods.ContainsKey("dosomething"));
        var cachedMethod = cached.Methods["dosomething"];
        Assert.Equal(MemberAccessibility.Protected, cachedMethod.Accessibility);
        Assert.Equal("DoSomething", cachedMethod.Name);
        Assert.Single(cachedMethod.ParameterTypes);
        Assert.Equal(PrimitiveTypeInfo.String, cachedMethod.ParameterTypes[0]);
        Assert.Equal(PrimitiveTypeInfo.Number, cachedMethod.ReturnType);

        PeopleCodeTypeRegistry.ClearClassInfoCache();
    }

    [Fact]
    public void CacheClassInfo_ShouldBeCaseInsensitive_OnClassName()
    {
        PeopleCodeTypeRegistry.ClearClassInfoCache();

        var classInfo = new ClassTypeInfo("SamplePkg:SampleClass");
        PeopleCodeTypeRegistry.CacheClassInfo(classInfo);

        Assert.True(PeopleCodeTypeRegistry.TryGetClassInfo("samplepkg:sampleclass", out var cached));
        Assert.NotNull(cached);

        PeopleCodeTypeRegistry.ClearClassInfoCache();
    }
}
