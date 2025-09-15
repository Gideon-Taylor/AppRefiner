using PeopleCodeParser.SelfHosted.TypeSystem;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Tests for the TypeInferenceEngine class
/// </summary>
public class TypeInferenceEngineTests : TypeInferenceTestBase
{
    [Fact]
    public async Task StringIntegerMismatch_ShouldReportTypeError()
    {
        // Test case: Local string &x = 3; should fail type checking
        var declaration = "Local string &x = 3;";

        var result = await InferTypesAsync(declaration);

        AssertTypeError(result, TypeErrorKind.TypeMismatch, "string");
    }

    [Fact]
    public async Task ValidStringDeclaration_ShouldSucceed()
    {
        // Test case: Local string &y = "hello"; should pass type checking
        var declaration = "Local string &y = \"hello\";";

        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);
    }

    [Fact]
    public async Task AnyTypeDeclaration_ShouldInferCorrectType()
    {
        // Test case: Local any &z = 123; should infer integer type
        var declaration = "Local any &z = 123;";

        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);

        var program = ParseLocalDeclaration(declaration);
        var localVar = GetFirstLocalVariable(program);

        Assert.NotNull(localVar);
        // The variable should have been assigned a type during inference
        // This will depend on implementation - the AST nodes should have type information attached
    }

    [Fact]
    public async Task ArrayDeclaration_ShouldSucceed()
    {
        // Test case: Local array of string &arr;
        var declaration = "Local array of string &arr;";

        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);
    }

    [Fact]
    public async Task IntegerToNumberConversion_ShouldSucceed()
    {
        // Test case: Local number &x = 42; should succeed (integer can be assigned to number)
        var declaration = "Local number &x = 42;";

        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);
    }

    [Fact]
    public async Task NumberToStringMismatch_ShouldReportTypeError()
    {
        // Test case: Local string &x = 42.5; should fail (PeopleCode does NOT allow implicit string conversion)
        var declaration = "Local string &x = 42.5;";

        var result = await InferTypesAsync(declaration);

        AssertTypeError(result, TypeErrorKind.TypeMismatch, "string");
    }

    [Fact]
    public async Task BooleanDeclaration_ShouldSucceed()
    {
        // Test case: Local boolean &flag = True;
        var declaration = "Local boolean &flag = True;";

        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);
    }

    [Fact]
    public async Task BooleanToStringMismatch_ShouldReportTypeError()
    {
        // Test case: Local boolean &flag = "true"; should fail
        var declaration = "Local boolean &flag = \"true\";";

        var result = await InferTypesAsync(declaration);

        AssertTypeError(result, TypeErrorKind.TypeMismatch, "boolean");
    }

    [Fact]
    public async Task MultipleDeclarations_ShouldValidateAll()
    {
        // Test multiple declarations with mixed valid and invalid cases
        var code = @"
            Local string &valid = ""hello"";
            Local integer &invalid = ""123"";
            Local number &alsoValid = 42;
        ";

        var result = await InferTypesAsync(code);

        // Should have one error for the invalid declaration
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        AssertTypeError(result, TypeErrorKind.TypeMismatch, "integer");
    }

    [Fact]
    public async Task EmptyFunction_ShouldSucceed()
    {
        // Test edge case: empty function should not cause errors
        var code = "";

        var result = await InferTypesAsync(code);

        AssertSuccess(result);
    }

    [Theory]
    [InlineData("Local string &x;")]
    [InlineData("Local integer &y;")]
    [InlineData("Local number &z;")]
    [InlineData("Local boolean &flag;")]
    [InlineData("Local date &d;")]
    [InlineData("Local datetime &dt;")]
    [InlineData("Local time &t;")]
    public async Task DeclarationWithoutInitializer_ShouldSucceed(string declaration)
    {
        // Test declarations without initializers should all be valid
        var result = await InferTypesAsync(declaration);

        AssertSuccess(result);
    }

    [Fact]
    public async Task DisabledMode_ShouldReturnEmptyResult()
    {
        // Test that disabled mode returns empty result without processing
        var declaration = "Local string &x = 3;"; // This would normally be an error

        var result = await InferTypesAsync(declaration, TypeInferenceMode.Disabled);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal(TypeInferenceMode.Disabled, result.Mode);
    }
}