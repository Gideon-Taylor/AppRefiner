using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Comprehensive tests for type compatibility rules according to PeopleCode specifications
/// </summary>
public class TypeCompatibilityTests
{
    [Fact]
    public void String_ShouldNotBeNullable()
    {
        var stringType = PrimitiveTypeInfo.String;

        Assert.False(stringType.IsNullable, "String should not be nullable in PeopleCode");
    }

    [Fact]
    public void String_ShouldOnlyAcceptString()
    {
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var numberType = PrimitiveTypeInfo.Number;
        var booleanType = PrimitiveTypeInfo.Boolean;
        var dateType = PrimitiveTypeInfo.Date;
        var dateTimeType = PrimitiveTypeInfo.DateTime;
        var timeType = PrimitiveTypeInfo.Time;
        var anyType = AnyTypeInfo.Instance;

        // String should accept string and Any only
        Assert.True(stringType.IsAssignableFrom(stringType), "String should accept String");
        Assert.True(stringType.IsAssignableFrom(anyType), "String should accept Any");

        // String should NOT accept other primitive types
        Assert.False(stringType.IsAssignableFrom(integerType), "String should NOT accept Integer");
        Assert.False(stringType.IsAssignableFrom(numberType), "String should NOT accept Number");
        Assert.False(stringType.IsAssignableFrom(booleanType), "String should NOT accept Boolean");
        Assert.False(stringType.IsAssignableFrom(dateType), "String should NOT accept Date");
        Assert.False(stringType.IsAssignableFrom(dateTimeType), "String should NOT accept DateTime");
        Assert.False(stringType.IsAssignableFrom(timeType), "String should NOT accept Time");
    }

    [Fact]
    public void NumberInteger_BidirectionalCompatibility()
    {
        var numberType = PrimitiveTypeInfo.Number;
        var integerType = PrimitiveTypeInfo.Integer;

        // Bidirectional compatibility between number and integer
        Assert.True(numberType.IsAssignableFrom(integerType), "Number should accept Integer");
        Assert.True(integerType.IsAssignableFrom(numberType), "Integer should accept Number");
    }

    [Fact]
    public void Boolean_ShouldOnlyAcceptBoolean()
    {
        var booleanType = PrimitiveTypeInfo.Boolean;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var numberType = PrimitiveTypeInfo.Number;
        var anyType = AnyTypeInfo.Instance;

        // Boolean should accept boolean and Any only
        Assert.True(booleanType.IsAssignableFrom(booleanType), "Boolean should accept Boolean");
        Assert.True(booleanType.IsAssignableFrom(anyType), "Boolean should accept Any");

        // Boolean should NOT accept other primitive types
        Assert.False(booleanType.IsAssignableFrom(stringType), "Boolean should NOT accept String");
        Assert.False(booleanType.IsAssignableFrom(integerType), "Boolean should NOT accept Integer");
        Assert.False(booleanType.IsAssignableFrom(numberType), "Boolean should NOT accept Number");
    }

    [Fact]
    public void DateTypes_MutuallyIncompatible()
    {
        var dateType = PrimitiveTypeInfo.Date;
        var dateTimeType = PrimitiveTypeInfo.DateTime;
        var timeType = PrimitiveTypeInfo.Time;
        var anyType = AnyTypeInfo.Instance;

        // Each date type should accept itself and Any only
        Assert.True(dateType.IsAssignableFrom(dateType), "Date should accept Date");
        Assert.True(dateType.IsAssignableFrom(anyType), "Date should accept Any");
        Assert.True(dateTimeType.IsAssignableFrom(dateTimeType), "DateTime should accept DateTime");
        Assert.True(dateTimeType.IsAssignableFrom(anyType), "DateTime should accept Any");
        Assert.True(timeType.IsAssignableFrom(timeType), "Time should accept Time");
        Assert.True(timeType.IsAssignableFrom(anyType), "Time should accept Any");

        // Date types should NOT accept each other
        Assert.False(dateType.IsAssignableFrom(dateTimeType), "Date should NOT accept DateTime");
        Assert.False(dateType.IsAssignableFrom(timeType), "Date should NOT accept Time");
        Assert.False(dateTimeType.IsAssignableFrom(dateType), "DateTime should NOT accept Date");
        Assert.False(dateTimeType.IsAssignableFrom(timeType), "DateTime should NOT accept Time");
        Assert.False(timeType.IsAssignableFrom(dateType), "Time should NOT accept Date");
        Assert.False(timeType.IsAssignableFrom(dateTimeType), "Time should NOT accept DateTime");
    }

    [Fact]
    public void DateTypes_ShouldNotAcceptOtherPrimitives()
    {
        var dateType = PrimitiveTypeInfo.Date;
        var dateTimeType = PrimitiveTypeInfo.DateTime;
        var timeType = PrimitiveTypeInfo.Time;
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var numberType = PrimitiveTypeInfo.Number;
        var booleanType = PrimitiveTypeInfo.Boolean;

        var dateTypes = new[] { dateType, dateTimeType, timeType };
        var otherTypes = new[] { stringType, integerType, numberType, booleanType };

        foreach (var dateTypeInstance in dateTypes)
        {
            foreach (var otherType in otherTypes)
            {
                Assert.False(dateTypeInstance.IsAssignableFrom(otherType),
                    $"{dateTypeInstance.Name} should NOT accept {otherType.Name}");
            }
        }
    }

    [Fact]
    public void BuiltinObjectTypes_ShouldOnlyAcceptSameTypeOrNull()
    {
        var recordType = BuiltinObjectTypeInfo.Record;
        var rowsetType = BuiltinObjectTypeInfo.Rowset;
        var anyType = AnyTypeInfo.Instance;

        // Record should accept Record and Any only
        Assert.True(recordType.IsAssignableFrom(recordType), "Record should accept Record");
        Assert.True(recordType.IsAssignableFrom(anyType), "Record should accept Any");
        Assert.True(recordType.IsNullable, "Record should be nullable");

        // Rowset should accept Rowset and Any only
        Assert.True(rowsetType.IsAssignableFrom(rowsetType), "Rowset should accept Rowset");
        Assert.True(rowsetType.IsAssignableFrom(anyType), "Rowset should accept Any");
        Assert.True(rowsetType.IsNullable, "Rowset should be nullable");

        // Different builtin object types should NOT accept each other
        Assert.False(recordType.IsAssignableFrom(rowsetType), "Record should NOT accept Rowset");
        Assert.False(rowsetType.IsAssignableFrom(recordType), "Rowset should NOT accept Record");
    }

    [Fact]
    public void AppClassTypes_ShouldOnlyAcceptSameType()
    {
        var appClass1 = new AppClassTypeInfo("MyPackage:MyClass");
        var appClass2 = new AppClassTypeInfo("MyPackage:MyClass"); // Same class
        var appClass3 = new AppClassTypeInfo("MyPackage:OtherClass"); // Different class
        var anyType = AnyTypeInfo.Instance;

        // Same app class type should be compatible
        Assert.True(appClass1.IsAssignableFrom(appClass2), "AppClass should accept same AppClass");
        Assert.True(appClass1.IsAssignableFrom(anyType), "AppClass should accept Any");
        Assert.True(appClass1.IsNullable, "AppClass should be nullable");

        // Different app class types should NOT be compatible
        Assert.False(appClass1.IsAssignableFrom(appClass3), "AppClass should NOT accept different AppClass");
    }

    [Fact]
    public void PrimitiveTypes_ShouldAcceptAny()
    {
        var anyType = AnyTypeInfo.Instance;
        var primitiveTypes = new[]
        {
            PrimitiveTypeInfo.String,
            PrimitiveTypeInfo.Integer,
            PrimitiveTypeInfo.Number,
            PrimitiveTypeInfo.Boolean,
            PrimitiveTypeInfo.Date,
            PrimitiveTypeInfo.DateTime,
            PrimitiveTypeInfo.Time
        };

        foreach (var primitiveType in primitiveTypes)
        {
            Assert.True(primitiveType.IsAssignableFrom(anyType),
                $"{primitiveType.Name} should accept Any");
        }
    }

    [Fact]
    public void Any_ShouldAcceptAllTypes()
    {
        var anyType = AnyTypeInfo.Instance;
        var allTypes = new TypeInfo[]
        {
            PrimitiveTypeInfo.String,
            PrimitiveTypeInfo.Integer,
            PrimitiveTypeInfo.Number,
            PrimitiveTypeInfo.Boolean,
            PrimitiveTypeInfo.Date,
            PrimitiveTypeInfo.DateTime,
            PrimitiveTypeInfo.Time,
            BuiltinObjectTypeInfo.Record,
            BuiltinObjectTypeInfo.Rowset,
            new AppClassTypeInfo("MyClass"),
            new ArrayTypeInfo(1, PrimitiveTypeInfo.String),
            VoidTypeInfo.Instance,
            UnknownTypeInfo.Instance
        };

        foreach (var type in allTypes)
        {
            Assert.True(anyType.IsAssignableFrom(type),
                $"Any should accept {type.Name}");
        }
    }

    [Fact]
    public void Void_ShouldNotAcceptAnyType()
    {
        var voidType = VoidTypeInfo.Instance;
        var testTypes = new TypeInfo[]
        {
            PrimitiveTypeInfo.String,
            PrimitiveTypeInfo.Integer,
            AnyTypeInfo.Instance,
            BuiltinObjectTypeInfo.Record,
            new AppClassTypeInfo("MyClass"),
            VoidTypeInfo.Instance // Even itself
        };

        foreach (var type in testTypes)
        {
            Assert.False(voidType.IsAssignableFrom(type),
                $"Void should NOT accept {type.Name}");
        }

        Assert.False(voidType.IsNullable, "Void should not be nullable");
    }

    [Fact]
    public void GetCommonType_NumberAndInteger_ShouldReturnNumber()
    {
        var numberType = PrimitiveTypeInfo.Number;
        var integerType = PrimitiveTypeInfo.Integer;

        var commonType1 = numberType.GetCommonType(integerType);
        var commonType2 = integerType.GetCommonType(numberType);

        Assert.Equal("number", commonType1.Name);
        Assert.Equal("number", commonType2.Name);
    }

    [Fact]
    public void GetCommonType_IncompatibleDateTypes_ShouldReturnAny()
    {
        var dateType = PrimitiveTypeInfo.Date;
        var dateTimeType = PrimitiveTypeInfo.DateTime;
        var timeType = PrimitiveTypeInfo.Time;

        var commonType1 = dateType.GetCommonType(dateTimeType);
        var commonType2 = dateType.GetCommonType(timeType);
        var commonType3 = dateTimeType.GetCommonType(timeType);

        Assert.Equal("any", commonType1.Name);
        Assert.Equal("any", commonType2.Name);
        Assert.Equal("any", commonType3.Name);
    }

    [Fact]
    public void GetCommonType_StringAndOtherPrimitives_ShouldReturnAny()
    {
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var booleanType = PrimitiveTypeInfo.Boolean;
        var dateType = PrimitiveTypeInfo.Date;

        var commonType1 = stringType.GetCommonType(integerType);
        var commonType2 = stringType.GetCommonType(booleanType);
        var commonType3 = stringType.GetCommonType(dateType);

        Assert.Equal("any", commonType1.Name);
        Assert.Equal("any", commonType2.Name);
        Assert.Equal("any", commonType3.Name);
    }
}