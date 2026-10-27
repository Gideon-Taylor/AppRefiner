using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A node holds a list of type errors and a list of type warnings: distinct messages are all
/// kept, a repeated message once.
/// </summary>
public class TypeDiagnosticListTests
{
    private static ProgramNode ParseClean(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);
        return program;
    }

    private static List<TypeError> InferAndCheck(ProgramNode program)
    {
        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);
        return program.GetAllTypeWarnings().ToList();
    }

    [Fact]
    public void Two_different_warnings_on_one_node_are_both_kept()
    {
        var program = ParseClean("Local string &a;");
        Assert.Empty(InferAndCheck(program));
        var node = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        node.AddTypeWarning(new TypeError("a", node));
        node.AddTypeWarning(new TypeError("b", node));

        Assert.Equal(new[] { "a", "b" }, node.GetTypeWarnings().Select(w => w.Message));
        var all = program.GetAllTypeWarnings().Select(w => w.Message).ToList();
        Assert.Contains("a", all);
        Assert.Contains("b", all);
    }

    [Fact]
    public void Same_message_twice_is_kept_once()
    {
        var program = ParseClean("Local string &a;");
        var node = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        node.AddTypeWarning(new TypeError("a", node));
        node.AddTypeWarning(new TypeError("a", node));

        Assert.Single(node.GetTypeWarnings());
    }

    [Fact]
    public void Two_different_errors_on_one_node_are_both_kept()
    {
        var program = ParseClean("Local string &a;");
        var node = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        node.AddTypeError(new TypeError("a", node));
        node.AddTypeError(new TypeError("b", node));
        node.AddTypeError(new TypeError("a", node));

        Assert.Equal(new[] { "a", "b" }, node.GetTypeErrors().Select(e => e.Message));
        Assert.Equal(new[] { "a", "b" }, program.GetAllTypeErrors().Select(e => e.Message));
        Assert.True(node.HasTypeError());
    }

    [Fact]
    public void Rerunning_checker_does_not_duplicate_errors()
    {
        var program = ParseClean("Local array of string &a;\n&a.Push(42);");
        InferAndCheck(program);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        Assert.Single(program.GetAllTypeErrors());
    }

    [Fact]
    public void Rerunning_inference_does_not_duplicate_warnings()
    {
        var program = ParseClean("Local string /* as Session */ &a;");
        var metadata = TypeMetadataBuilder.ExtractMetadata(program);

        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);

        Assert.Single(program.GetAllTypeWarnings());
    }

    [Fact]
    public void Running_inference_clears_earlier_diagnostics()
    {
        var program = ParseClean("Local string &a;");
        var node = program.FindDescendants<LocalVariableDeclarationNode>().Single();
        node.AddTypeWarning(new TypeError("stale warning", node));
        node.AddTypeError(new TypeError("stale error", node));

        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program), NullTypeMetadataResolver.Instance);

        Assert.Empty(program.GetAllTypeWarnings());
        Assert.Empty(program.GetAllTypeErrors());
    }

    [Fact]
    public void Multi_variable_misfit_warns_per_variable()
    {
        var program = ParseClean("Local string /* as Session */ &a, &b;");
        var warnings = InferAndCheck(program).Select(w => w.Message).ToList();

        Assert.Equal(2, warnings.Count);
        Assert.Contains("'as Session' needs an ApiObject declaration; &a is string.", warnings);
        Assert.Contains("'as Session' needs an ApiObject declaration; &b is string.", warnings);
    }
}
