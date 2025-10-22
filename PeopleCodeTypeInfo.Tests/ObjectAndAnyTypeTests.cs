using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for the "object" and "any" type behavior in the PeopleCode type system.
///
/// Key rules:
/// - "object" type can hold any builtin object or AppClass, but NOT primitives
/// - "any" type can hold absolutely anything including primitives
/// - Primitives: string, integer, number, date, datetime, time, boolean
/// </summary>
public class ObjectAndAnyTypeTests
{
    #region Object Type Tests

    [Fact]
    public void ObjectType_AcceptsBuiltinObjects()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var recordType = TypeInfo.FromPeopleCodeType(PeopleCodeType.Record);
        var rowsetType = TypeInfo.FromPeopleCodeType(PeopleCodeType.Rowset);
        var fieldType = TypeInfo.FromPeopleCodeType(PeopleCodeType.Field);

        // Act & Assert
        Assert.True(objectType.IsAssignableFrom(recordType), "object should accept Record");
        Assert.True(objectType.IsAssignableFrom(rowsetType), "object should accept Rowset");
        Assert.True(objectType.IsAssignableFrom(fieldType), "object should accept Field");
    }

    [Fact]
    public void ObjectType_AcceptsAppClasses()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var appClassType = new AppClassTypeInfo("MyPackage:MyClass");

        // Act & Assert
        Assert.True(objectType.IsAssignableFrom(appClassType), "object should accept AppClass");
    }

    [Fact]
    public void ObjectType_RejectsAllPrimitives()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var numberType = PrimitiveTypeInfo.Number;
        var dateType = PrimitiveTypeInfo.Date;
        var dateTimeType = PrimitiveTypeInfo.DateTime;
        var timeType = PrimitiveTypeInfo.Time;
        var booleanType = PrimitiveTypeInfo.Boolean;

        // Act & Assert
        Assert.False(objectType.IsAssignableFrom(stringType), "object should NOT accept string");
        Assert.False(objectType.IsAssignableFrom(integerType), "object should NOT accept integer");
        Assert.False(objectType.IsAssignableFrom(numberType), "object should NOT accept number");
        Assert.False(objectType.IsAssignableFrom(dateType), "object should NOT accept date");
        Assert.False(objectType.IsAssignableFrom(dateTimeType), "object should NOT accept datetime");
        Assert.False(objectType.IsAssignableFrom(timeType), "object should NOT accept time");
        Assert.False(objectType.IsAssignableFrom(booleanType), "object should NOT accept boolean");
    }

    [Fact]
    public void ObjectType_AcceptsAnyType()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var anyType = AnyTypeInfo.Instance;

        // Act & Assert
        Assert.True(objectType.IsAssignableFrom(anyType), "object should accept any");
    }

    [Fact]
    public void ObjectType_AcceptsArrays()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var arrayOfRecord = new ArrayTypeInfo(1, TypeInfo.FromPeopleCodeType(PeopleCodeType.Record));
        var arrayOfString = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);

        // Act & Assert
        Assert.True(objectType.IsAssignableFrom(arrayOfRecord), "object should accept array of Record");
        Assert.True(objectType.IsAssignableFrom(arrayOfString), "object should accept array of string (arrays are objects)");
    }

    #endregion

    #region Any Type Tests

    [Fact]
    public void AnyType_AcceptsEverything()
    {
        // Arrange
        var anyType = AnyTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var recordType = TypeInfo.FromPeopleCodeType(PeopleCodeType.Record);
        var appClassType = new AppClassTypeInfo("MyPackage:MyClass");
        var arrayType = new ArrayTypeInfo(1, PrimitiveTypeInfo.String);

        // Act & Assert
        Assert.True(anyType.IsAssignableFrom(stringType), "any should accept string");
        Assert.True(anyType.IsAssignableFrom(integerType), "any should accept integer");
        Assert.True(anyType.IsAssignableFrom(recordType), "any should accept Record");
        Assert.True(anyType.IsAssignableFrom(appClassType), "any should accept AppClass");
        Assert.True(anyType.IsAssignableFrom(arrayType), "any should accept array");
    }

    [Fact]
    public void AnyType_AcceptsPrimitives()
    {
        // Arrange
        var anyType = AnyTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;
        var numberType = PrimitiveTypeInfo.Number;
        var dateType = PrimitiveTypeInfo.Date;
        var booleanType = PrimitiveTypeInfo.Boolean;

        // Act & Assert
        Assert.True(anyType.IsAssignableFrom(stringType), "any should accept string primitive");
        Assert.True(anyType.IsAssignableFrom(numberType), "any should accept number primitive");
        Assert.True(anyType.IsAssignableFrom(dateType), "any should accept date primitive");
        Assert.True(anyType.IsAssignableFrom(booleanType), "any should accept boolean primitive");
    }

    #endregion

    #region Common Type Tests

    [Fact]
    public void CommonType_BetweenObjectAndPrimitive_ShouldBeAny()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var stringType = PrimitiveTypeInfo.String;

        // Act
        var commonType = objectType.GetCommonType(stringType);

        // Assert
        Assert.IsType<AnyTypeInfo>(commonType);
    }

    [Fact]
    public void CommonType_BetweenObjectAndBuiltinObject_ShouldBeObject()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var recordType = TypeInfo.FromPeopleCodeType(PeopleCodeType.Record);

        // Act
        var commonType = objectType.GetCommonType(recordType);

        // Assert
        Assert.Same(objectType, commonType);
    }

    [Fact]
    public void CommonType_BetweenObjectAndAppClass_ShouldBeObject()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;
        var appClassType = new AppClassTypeInfo("MyPackage:MyClass");

        // Act
        var commonType = objectType.GetCommonType(appClassType);

        // Assert
        Assert.Same(objectType, commonType);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void FromPeopleCodeType_ReturnsObjectTypeInfoForObject()
    {
        // Act
        var result = TypeInfo.FromPeopleCodeType(PeopleCodeType.Object);

        // Assert
        Assert.IsType<ObjectTypeInfo>(result);
        Assert.Same(ObjectTypeInfo.Instance, result);
    }

    [Fact]
    public void FromPeopleCodeType_ReturnsAnyTypeInfoForAny()
    {
        // Act
        var result = TypeInfo.FromPeopleCodeType(PeopleCodeType.Any);

        // Assert
        Assert.IsType<AnyTypeInfo>(result);
        Assert.Same(AnyTypeInfo.Instance, result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ObjectType_Name_IsCorrect()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;

        // Act & Assert
        Assert.Equal("object", objectType.Name);
    }

    [Fact]
    public void ObjectType_Kind_IsBuiltinObject()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;

        // Act & Assert
        Assert.Equal(TypeKind.BuiltinObject, objectType.Kind);
    }

    [Fact]
    public void ObjectType_PeopleCodeType_IsObject()
    {
        // Arrange
        var objectType = ObjectTypeInfo.Instance;

        // Act & Assert
        Assert.Equal(PeopleCodeType.Object, objectType.PeopleCodeType);
    }

    #endregion
}
