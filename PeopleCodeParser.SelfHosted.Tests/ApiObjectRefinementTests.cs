using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A variable's declared type stays separate from its refined type, so ApiObject/any
/// variables re-refine on reassignment, and each use carries where its refinement came from.
/// </summary>
public class ApiObjectRefinementTests
{
    private static ProgramNode Infer(string source, bool inferAutoDeclaredTypes = false)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance,
            inferAutoDeclaredTypes: inferAutoDeclaredTypes);
        return program;
    }

    private static IdentifierNode LastUse(ProgramNode program, string name)
    {
        return program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
    }

    [Fact]
    public void Reassigned_ApiObject_variable_takes_latest_type()
    {
        var program = Infer(
            "Local ApiObject &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);\n&x.Save();");

        Assert.Equal("ComponentInterface", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Use_after_assignment_carries_refinement_source()
    {
        var program = Infer(
            "Local ApiObject &x;\n&x = %Session;\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        var src = LastUse(program, "&x").GetRefinementSource();

        Assert.NotNull(src);
        Assert.Equal(RefinementKind.Assignment, src!.Kind);
        Assert.Equal(1, src.Line);
    }

    [Fact]
    public void Any_declared_variable_keeps_refining()
    {
        var program = Infer(
            "Local any &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);\n&x.Save();");

        Assert.Equal("ComponentInterface", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Auto_declared_variable_is_typed_on_first_assignment_only()
    {
        var program = Infer("&n = 1;\n&n = \"text\";\n&m = &n;", inferAutoDeclaredTypes: true);

        // VisitLiteral types 1 as Integer, but TypeInferenceVisitor.SetInferredType normalizes Integer
        // to Number on every node it stamps, so &n is number. The point is the later string doesn't win.
        Assert.Equal("number", LastUse(program, "&n").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Auto_declared_variable_re_refines_api_type_when_inference_of_auto_declared_is_off()
    {
        // Production runs with inferAutoDeclaredTypes off: the auto-declared variable is never
        // pinned to its first type, so refinement deliberately follows the latest assignment.
        var program = Infer(
            "&x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);\n&x.Save();",
            inferAutoDeclaredTypes: false);

        Assert.Equal("ComponentInterface", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }
}
