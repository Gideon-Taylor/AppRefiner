using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A call's effective parameter annotations and refined return, as the signature tooltips show them.
/// </summary>
public class EffectiveParameterAnnotationTests
{
    private static FunctionCallNode CallTo(string source, string name)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program), NullTypeMetadataResolver.Instance);
        return program.FindDescendants<FunctionCallNode>().Single(c => c.Function is IdentifierNode id && id.Name == name);
    }

    [Fact]
    public void Same_program_function_parameter_annotation()
    {
        var call = CallTo(
            "Function ProcessRole(&role As ApiObject /* as CompIntfc.USER_PROFILE.Roles */, &n As number)\nEnd-Function;\n" +
            "Local ApiObject &r;\nProcessRole(&r, 1);",
            "ProcessRole");
        var info = call.GetFunctionInfo()!;

        var annotation = call.GetEffectiveParameterAnnotation(info, 0);
        Assert.Equal(PeopleCodeType.DataItem, annotation!.Value.Type);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), annotation.Value.Identity);
        Assert.Null(call.GetEffectiveParameterAnnotation(info, 1));
        Assert.Null(call.GetEffectiveParameterAnnotation(info, 5));
    }

    [Fact]
    public void Annotated_return_is_refined()
    {
        var call = CallTo(
            "Function GetSess() Returns ApiObject /* as Session */\n   Return %Session;\nEnd-Function;\n" +
            "Local ApiObject &s = GetSess();",
            "GetSess");

        Assert.Equal(PeopleCodeType.Session, call.GetFunctionInfo()!.RefinedReturnType!.Value.Type);
    }
}
