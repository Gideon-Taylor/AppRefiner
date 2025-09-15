using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Tests for TypeInfo classes and type compatibility logic
/// </summary>
public class TypeInfoTests
{
    [Fact]
    public void PrimitiveTypeInfo_SamePrimitive_ShouldBeAssignable()
    {
        var stringType = PrimitiveTypeInfo.String;
        var anotherStringType = new PrimitiveTypeInfo("string");

        Assert.True(stringType.IsAssignableFrom(anotherStringType));
        Assert.True(anotherStringType.IsAssignableFrom(stringType));
    }

    [Fact]
    public void PrimitiveTypeInfo_DifferentPrimitive_ShouldNotBeAssignable()
    {
        var integerType = PrimitiveTypeInfo.Integer;
        var booleanType = PrimitiveTypeInfo.Boolean;

        // Integer should not accept boolean (no implicit conversion)
        Assert.False(integerType.IsAssignableFrom(booleanType));
    }

    [Fact]
    public void AnyTypeInfo_ShouldAcceptAllTypes()
    {
        var anyType = AnyTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var arrayType = new ArrayTypeInfo(1, stringType);
        var appClassType = new AppClassTypeInfo("MyClass");

        Assert.True(anyType.IsAssignableFrom(stringType));
        Assert.True(anyType.IsAssignableFrom(integerType));
        Assert.True(anyType.IsAssignableFrom(arrayType));
        Assert.True(anyType.IsAssignableFrom(appClassType));
        Assert.True(anyType.IsAssignableFrom(anyType));
    }

    [Fact]
    public void AllTypes_ShouldAcceptAny()
    {
        var anyType = AnyTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var arrayType = new ArrayTypeInfo(1, stringType);

        Assert.True(stringType.IsAssignableFrom(anyType));
        Assert.True(integerType.IsAssignableFrom(anyType));
        Assert.True(arrayType.IsAssignableFrom(anyType));
    }

    [Fact]
    public void PrimitiveTypeInfo_IntegerToNumber_ShouldBeAssignable()
    {
        var numberType = PrimitiveTypeInfo.Number;
        var integerType = PrimitiveTypeInfo.Integer;

        Assert.True(numberType.IsAssignableFrom(integerType));
        Assert.True(integerType.IsAssignableFrom(numberType)); // Bidirectional compatibility
    }

    [Fact]
    public void PrimitiveTypeInfo_NumberToInteger_ShouldBeAssignable()
    {
        var numberType = PrimitiveTypeInfo.Number;
        var integerType = PrimitiveTypeInfo.Integer;

        // Bidirectional compatibility between number and integer in PeopleCode
        Assert.True(numberType.IsAssignableFrom(integerType));
        Assert.True(integerType.IsAssignableFrom(numberType));
    }

    [Fact]
    public void ArrayTypeInfo_SameDimensions_ShouldBeAssignableWithCompatibleElements()
    {
        var stringArrayType = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);
        var anotherStringArrayType = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);

        Assert.True(stringArrayType.IsAssignableFrom(anotherStringArrayType));
    }

    [Fact]
    public void ArrayTypeInfo_DifferentDimensions_ShouldNotBeAssignable()
    {
        var oneDimensionalArray = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);
        var twoDimensionalArray = new ArrayTypeInfo(2, PrimitiveTypeInfo.String);

        Assert.False(oneDimensionalArray.IsAssignableFrom(twoDimensionalArray));
        Assert.False(twoDimensionalArray.IsAssignableFrom(oneDimensionalArray));
    }

    [Fact]
    public void ArrayTypeInfo_UntypedArrays_ShouldBeAssignable()
    {
        var untypedArray1 = new ArrayTypeInfo(1, null);
        var untypedArray2 = new ArrayTypeInfo(1, null);
        var typedArray = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);

        Assert.True(untypedArray1.IsAssignableFrom(untypedArray2));
        Assert.True(untypedArray1.IsAssignableFrom(typedArray));
        Assert.True(typedArray.IsAssignableFrom(untypedArray1));
    }

    [Fact]
    public void AppClassTypeInfo_SameClass_ShouldBeAssignable()
    {
        var classType1 = new AppClassTypeInfo("MyPackage:MyClass");
        var classType2 = new AppClassTypeInfo("MyPackage:MyClass");

        Assert.True(classType1.IsAssignableFrom(classType2));
        Assert.True(classType2.IsAssignableFrom(classType1));
    }

    [Fact]
    public void AppClassTypeInfo_DifferentClass_ShouldNotBeAssignable()
    {
        var classType1 = new AppClassTypeInfo("MyPackage:MyClass");
        var classType2 = new AppClassTypeInfo("MyPackage:OtherClass");

        Assert.False(classType1.IsAssignableFrom(classType2));
        Assert.False(classType2.IsAssignableFrom(classType1));
    }

    [Fact]
    public void AppClassTypeInfo_ParsesQualifiedNameCorrectly()
    {
        var qualifiedClass = new AppClassTypeInfo("MyPackage:SubPackage:MyClass");

        Assert.Equal("MyPackage:SubPackage:MyClass", qualifiedClass.QualifiedName);
        Assert.Equal("MyClass", qualifiedClass.ClassName);
        Assert.Equal(new[] { "MyPackage", "SubPackage" }, qualifiedClass.PackagePath);
    }

    [Fact]
    public void AppClassTypeInfo_ParsesSimpleNameCorrectly()
    {
        var simpleClass = new AppClassTypeInfo("MyClass");

        Assert.Equal("MyClass", simpleClass.QualifiedName);
        Assert.Equal("MyClass", simpleClass.ClassName);
        Assert.Empty(simpleClass.PackagePath);
    }

    [Fact]
    public void VoidTypeInfo_ShouldNotAcceptAnyAssignment()
    {
        var voidType = VoidTypeInfo.Instance;
        var anyType = AnyTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;

        Assert.False(voidType.IsAssignableFrom(anyType));
        Assert.False(voidType.IsAssignableFrom(stringType));
        Assert.False(voidType.IsAssignableFrom(voidType));
    }

    [Fact]
    public void VoidTypeInfo_IsNotNullable()
    {
        var voidType = VoidTypeInfo.Instance;

        Assert.False(voidType.IsNullable);
    }

    [Fact]
    public void GetCommonType_SameTypes_ReturnsSameType()
    {
        var stringType = PrimitiveTypeInfo.String;
        var anotherStringType = new PrimitiveTypeInfo("string");

        var commonType = stringType.GetCommonType(anotherStringType);

        Assert.Equal(stringType.Name, commonType.Name);
    }

    [Fact]
    public void GetCommonType_IncompatibleTypes_ReturnsAny()
    {
        var booleanType = PrimitiveTypeInfo.Boolean;
        var integerType = PrimitiveTypeInfo.Integer;

        var commonType = booleanType.GetCommonType(integerType);

        Assert.Equal(AnyTypeInfo.Instance.Name, commonType.Name);
    }

    [Fact]
    public void GetCommonType_WithAny_ReturnsAny()
    {
        var stringType = PrimitiveTypeInfo.String;
        var anyType = AnyTypeInfo.Instance;

        var commonType = stringType.GetCommonType(anyType);

        Assert.Equal(anyType, commonType);
    }

    [Fact]
    public void TypeInfo_EqualsAndHashCode_WorkCorrectly()
    {
        var string1 = PrimitiveTypeInfo.String;
        var string2 = new PrimitiveTypeInfo("string");
        var string3 = new PrimitiveTypeInfo("STRING"); // Different case
        var integer = PrimitiveTypeInfo.Integer;

        Assert.Equal(string1, string2);
        Assert.Equal(string1, string3); // Should be case-insensitive
        Assert.NotEqual(string1, integer);

        Assert.Equal(string1.GetHashCode(), string2.GetHashCode());
        Assert.Equal(string1.GetHashCode(), string3.GetHashCode());
    }
}