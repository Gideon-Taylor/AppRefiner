using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.TypeSystem;
using System.Threading.Tasks;
using Xunit;

namespace PeopleCodeParser.SelfHosted.TypeSystem.Tests;

/// <summary>
/// Test to examine how reference expressions like HTML.FOO parse
/// </summary>
public class ReferenceTypeParsingTest : TypeInferenceTestBase
{
    [Fact]
    public async Task ParseHTMLReference_ShouldInferReferenceType()
    {
        var code = "GetHTMLText(HTML.FOO);";

        var program = ParseCode(code);
        var result = await Engine.InferTypesAsync(program, TypeInferenceMode.Quick);
        AssertSuccess(result);

        var functionCall = GetFirstFunctionCall(program);
        Assert.NotNull(functionCall);

        Assert.Single(functionCall!.Arguments);
        var argument = Assert.IsType<MemberAccessNode>(functionCall.Arguments[0]);

        var inferredType = argument.GetInferredType();
        var referenceType = Assert.IsType<ReferenceTypeInfo>(inferredType);
        Assert.False(referenceType.IsDynamic);
        Assert.Equal(ReferenceTypeIdentifier.HTML, referenceType.ReferenceIdentifier);
        Assert.Equal("FOO", referenceType.MemberName);
    }

    [Fact]
    public async Task ParseDynamicReference_ShouldInferDynamicReferenceType()
    {
        var code = @"GetHTMLText(@(""HTML.FOO""));";

        var program = ParseCode(code);
        var result = await Engine.InferTypesAsync(program, TypeInferenceMode.Quick);
        AssertSuccess(result);

        var functionCall = GetFirstFunctionCall(program);
        Assert.NotNull(functionCall);

        Assert.Single(functionCall!.Arguments);
        var argument = Assert.IsType<UnaryOperationNode>(functionCall.Arguments[0]);

        var inferredType = argument.GetInferredType();
        var referenceType = Assert.IsType<ReferenceTypeInfo>(inferredType);
        Assert.True(referenceType.IsDynamic);
        Assert.Null(referenceType.ReferenceIdentifier);
        Assert.Null(referenceType.MemberName);
    }
}
