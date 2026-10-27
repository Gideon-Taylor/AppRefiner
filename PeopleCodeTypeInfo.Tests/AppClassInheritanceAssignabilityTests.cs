using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// An app class type accepts a value whose inheritance chain (base classes and interfaces)
/// reaches it, directly and in calls. Arrays are invariant: an array of a subclass is not an
/// array of its base class.
/// </summary>
public class AppClassInheritanceAssignabilityTests
{
    private static AppClassTypeInfo Derived() => new("PKG:Derived", inheritanceChain: new[]
    {
        new InheritanceChainEntry("PKG:Derived", isBuiltin: false),
        new InheritanceChainEntry("PKG:Base", isBuiltin: false),
        new InheritanceChainEntry("PKG:IThing", isBuiltin: false)
    });

    [Fact]
    public void Base_and_interface_accept_derived_value()
    {
        Assert.True(new AppClassTypeInfo("PKG:Base").IsAssignableFrom(Derived()));
        Assert.True(new AppClassTypeInfo("pkg:ithing").IsAssignableFrom(Derived()));
    }

    [Fact]
    public void Bare_base_accepts_derived_value()
    {
        Assert.True(new AppClassTypeInfo("Base").IsAssignableFrom(Derived()));
    }

    [Fact]
    public void Derived_and_unrelated_types_reject()
    {
        var baseValue = new AppClassTypeInfo("PKG:Base");
        Assert.False(new AppClassTypeInfo("PKG:Derived").IsAssignableFrom(baseValue));
        Assert.False(new AppClassTypeInfo("PKG:Other").IsAssignableFrom(Derived()));
    }

    [Fact]
    public void Array_of_base_rejects_array_of_derived()
    {
        var expected = new ArrayTypeInfo(1, new AppClassTypeInfo("PKG:Base"));
        Assert.False(expected.IsAssignableFrom(new ArrayTypeInfo(1, Derived())));
        Assert.False(new ArrayTypeInfo(1, new AppClassTypeInfo("PKG:IThing")).IsAssignableFrom(new ArrayTypeInfo(1, Derived())));
    }

    [Fact]
    public void Array_of_class_accepts_array_of_same_class()
    {
        Assert.True(new ArrayTypeInfo(1, new AppClassTypeInfo("pkg:derived")).IsAssignableFrom(new ArrayTypeInfo(1, Derived())));
        Assert.True(new ArrayTypeInfo(1, new AppClassTypeInfo("Derived")).IsAssignableFrom(new ArrayTypeInfo(1, Derived())));
        Assert.True(new ArrayTypeInfo(1, Derived()).IsAssignableFrom(new ArrayTypeInfo(1, new AppClassTypeInfo("Derived"))));
    }

    private static readonly TypeWithDimensionality Base = new(PeopleCodeType.AppClass, appClassPath: "PKG:Base");

    // F(thing: PKG:Base|string, n: number)
    private static FunctionInfo UnionThenNumber() => new()
    {
        Name = "F",
        ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
        Parameters = new List<Parameter>
        {
            new UnionParameter { Name = "thing", AllowedTypes = new() { Base, new(PeopleCodeType.String) } },
            new SingleParameter { Name = "n", ParameterType = new TypeWithDimensionality(PeopleCodeType.Number) }
        }
    };

    [Fact]
    public void Union_parameter_accepts_derived_value()
    {
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);

        Assert.True(validator.Validate(UnionThenNumber(), new TypeInfo[] { Derived(), PrimitiveTypeInfo.Number }).IsValid);
    }

    [Fact]
    public void Next_types_after_derived_value_in_union()
    {
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);

        var next = validator.GetAllowedNextTypes(UnionThenNumber(), new TypeInfo[] { Derived() });

        Assert.Contains(next, t => t.ParameterName == "n");
    }

    [Fact]
    public void Array_parameter_rejects_array_of_derived()
    {
        var function = new FunctionInfo
        {
            Name = "G",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = new List<Parameter>
            {
                new SingleParameter { Name = "things", ParameterType = new(PeopleCodeType.AppClass, 1, "PKG:Base") }
            }
        };
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);

        Assert.False(validator.Validate(function, new TypeInfo[] { new ArrayTypeInfo(1, Derived()) }).IsValid);
    }
}
