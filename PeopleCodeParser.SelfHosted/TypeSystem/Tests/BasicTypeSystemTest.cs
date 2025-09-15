using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Basic test to understand the current state of the type system
/// </summary>
public class BasicTypeSystemTest : TypeInferenceTestBase
{
    [Fact]
    public async Task DebugBasicParsing_ShouldWork()
    {
        // Simple test to see what happens when we parse and run type inference
        var declaration = "Local string &x = 3";

        var program = ParseLocalDeclaration(declaration);

        // Check that parsing worked
        Assert.NotNull(program);
        Assert.Single(program.Functions);

        var function = program.Functions.First();
        Assert.NotNull(function.Body);

        // Run type inference
        var result = await Engine.InferTypesAsync(program, TypeInferenceMode.Quick);

        // Debug what we get
        Assert.NotNull(result);

        // For now, just verify the basics work
        // We'll improve the type checking logic separately
    }

    [Fact]
    public void DebugTypeInfoCompatibility_Integer()
    {
        // Test the basic type compatibility - correct PeopleCode behavior
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;

        // Test what actually happens
        var result = stringType.IsAssignableFrom(integerType);

        // This should be FALSE in PeopleCode (NO implicit string conversion except number/integer)
        // PeopleCode does NOT allow assigning integers to strings
        Assert.False(result);
    }

    [Fact]
    public void DebugTypeInfoCompatibility_Different()
    {
        // Debug the compatibility logic - correct PeopleCode behavior
        var stringType = PrimitiveTypeInfo.String;
        var integerType = PrimitiveTypeInfo.Integer;
        var booleanType = PrimitiveTypeInfo.Boolean;
        var numberType = PrimitiveTypeInfo.Number;

        // String should NOT accept integer (NO implicit conversion in PeopleCode)
        Assert.False(stringType.IsAssignableFrom(integerType), "String should NOT accept Integer");

        // String should NOT accept boolean (NO implicit conversion in PeopleCode)
        Assert.False(stringType.IsAssignableFrom(booleanType), "String should NOT accept Boolean");

        // Integer should NOT accept string
        Assert.False(integerType.IsAssignableFrom(stringType), "Integer should NOT accept String");

        // Boolean should NOT accept string
        Assert.False(booleanType.IsAssignableFrom(stringType), "Boolean should NOT accept String");

        // The ONLY valid implicit conversion: number accepts integer
        Assert.True(numberType.IsAssignableFrom(integerType), "Number SHOULD accept Integer (only valid conversion)");
    }
}