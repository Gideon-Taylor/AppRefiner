using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

public class DebugTypeTest
{
    [Fact]
    public void DebugTypeCompatibility()
    {
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var booleanType = PrimitiveTypeInfo.Boolean;
        var numberType = PrimitiveTypeInfo.Number;

        // Correct PeopleCode behavior: NO implicit conversions except integer to number
        // For "Local string &x = 3;" we need stringType.IsAssignableFrom(integerType)
        var stringAcceptsInteger = stringType.IsAssignableFrom(integerType);

        // For "Local integer &invalid = "123";" we need integerType.IsAssignableFrom(stringType)
        var integerAcceptsString = integerType.IsAssignableFrom(stringType);

        // For "Local boolean &flag = "true";" we need booleanType.IsAssignableFrom(stringType)
        var booleanAcceptsString = booleanType.IsAssignableFrom(stringType);

        // Only valid implicit conversion: number accepts integer
        var numberAcceptsInteger = numberType.IsAssignableFrom(integerType);

        // All should be false except number accepts integer
        Assert.False(stringAcceptsInteger); // String does NOT accept integer
        Assert.False(integerAcceptsString); // Integer does NOT accept string
        Assert.False(booleanAcceptsString); // Boolean does NOT accept string
        Assert.True(numberAcceptsInteger); // Number DOES accept integer (only valid conversion)
    }
}