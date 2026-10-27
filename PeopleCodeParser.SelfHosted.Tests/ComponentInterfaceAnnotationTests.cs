using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// <c>CompIntfc.X.Coll[.Child…]</c> annotates an item of a CI collection (DataItem) and a trailing
/// <c>[]</c> the collection itself (DataCollection); the path from the CI root goes into the identity.
/// </summary>
public class ComponentInterfaceAnnotationTests
{
    private static ParsedAnnotation? ParseAnnotation(string text) =>
        TypeAnnotationParser.TryParse(Token.CreateTrivia(TokenType.BlockComment, text,
            new SourceSpan(new SourcePosition(0), new SourcePosition(text.Length))));

    private static (ProgramNode Program, List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
    }

    private static IdentifierNode LastUse(ProgramNode program, string name) =>
        program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();

    [Theory]
    [InlineData("CompIntfc.USER_PROFILE", PeopleCodeType.ComponentInterface, null)]
    [InlineData("CompIntfc.USER_PROFILE.Roles", PeopleCodeType.DataItem, "Roles")]
    [InlineData("CompIntfc.USER_PROFILE.Roles.RouteControls", PeopleCodeType.DataItem, "Roles.RouteControls")]
    [InlineData("CompIntfc.USER_PROFILE.Roles[]", PeopleCodeType.DataCollection, "Roles")]
    [InlineData("CompIntfc.USER_PROFILE.Roles.RouteControls[]", PeopleCodeType.DataCollection, "Roles.RouteControls")]
    public void Parses_ci_paths(string written, PeopleCodeType type, string? path)
    {
        var parsed = ParseAnnotation($"/* as {written} */");

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Error);
        Assert.Equal(new AnnotationTarget(type, new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: path), written),
            parsed.Target);
        Assert.Equal(AnnotationCategory.ApiObject, parsed.Target!.Category);
    }

    [Theory]
    [InlineData("CompIntfc.USER_PROFILE[]")]
    [InlineData("CompIntfc.USER_PROFILE.")]
    [InlineData("CompIntfc.USER_PROFILE.Roles.")]
    [InlineData("CompIntfc.USER_PROFILE..Roles")]
    [InlineData("CompIntfc.USER_PROFILE.[]")]
    [InlineData("CompIntfc.USER_PROFILE.Roles[][]")]
    [InlineData("CompIntfc.USER_PROFILE.Ro[]les")]
    public void Malformed_ci_paths_are_unknown(string written)
    {
        var parsed = ParseAnnotation($"/* as {written} */");

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Target);
        Assert.Equal($"Unknown type '{written}' in annotation.", parsed.Error);
    }

    [Fact]
    public void Item_annotation_types_the_variable()
    {
        var (program, errors, warnings) = TypeCheck(
            "Local ApiObject /* as CompIntfc.USER_PROFILE.Roles */ &r;\nLocal any &x = &r;");

        Assert.Empty(errors);
        Assert.Empty(warnings);
        var use = LastUse(program, "&r");
        var type = use.GetInferredType();
        Assert.NotNull(type);
        Assert.Equal(PeopleCodeType.DataItem, type!.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), type.Identity);
        Assert.Equal(RefinementKind.Annotation, use.GetRefinementSource()?.Kind);
    }

    [Fact]
    public void Collection_annotation_types_the_variable()
    {
        var (program, errors, warnings) = TypeCheck(
            "Local ApiObject /* as CompIntfc.USER_PROFILE.Roles[] */ &c;\nLocal any &x = &c;");

        Assert.Empty(errors);
        Assert.Empty(warnings);
        var type = LastUse(program, "&c").GetInferredType();
        Assert.Equal(PeopleCodeType.DataCollection, type?.PeopleCodeType);
        Assert.Equal(new TypeIdentity(CompIntfcName: "USER_PROFILE", CompIntfcPath: "Roles"), type?.Identity);
    }

    [Fact]
    public void Item_annotation_needs_an_ApiObject_declaration()
    {
        var (_, _, warnings) = TypeCheck("Local Record /* as CompIntfc.USER_PROFILE.Roles */ &r;");

        Assert.Contains(warnings, w => w.Message ==
            "'as CompIntfc.USER_PROFILE.Roles' needs an ApiObject declaration; &r is Record.");
    }

    [Fact]
    public void Argument_of_a_different_item_path_warns()
    {
        var (_, errors, warnings) = TypeCheck(
            "Function F(&r As ApiObject /* as CompIntfc.USER_PROFILE.Roles */)\nEnd-Function;\n" +
            "Local ApiObject /* as CompIntfc.USER_PROFILE.EmailAddresses */ &e;\nF(&e);");

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal(
            "F expects &r as CompIntfc.USER_PROFILE.Roles (annotated); the argument is CompIntfc.USER_PROFILE.EmailAddresses.",
            warning.Message);
    }

    [Fact]
    public void Argument_of_a_different_annotated_ci_warns()
    {
        var (_, errors, warnings) = TypeCheck(
            "Function F(&c As ApiObject /* as CompIntfc.USER_PROFILE */)\nEnd-Function;\n" +
            "Local ApiObject /* as CompIntfc.OTHER_CI */ &o;\nF(&o);");

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("F expects &c as CompIntfc.USER_PROFILE (annotated); the argument is CompIntfc.OTHER_CI.", warning.Message);
    }

    [Fact]
    public void Argument_with_same_or_less_identity_is_silent()
    {
        var (_, errors, warnings) = TypeCheck(
            "Function F(&c As ApiObject /* as CompIntfc.USER_PROFILE */)\nEnd-Function;\n" +
            "Local ApiObject /* as CompIntfc.user_profile */ &same;\nF(&same);\n" +
            "Local ApiObject /* as ComponentInterface */ &plain;\nF(&plain);");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Assignment_of_a_different_path_names_both_paths()
    {
        var (_, _, warnings) = TypeCheck(
            "Local ApiObject /* as CompIntfc.USER_PROFILE.Roles[] */ &c;\n" +
            "Local ApiObject /* as CompIntfc.USER_PROFILE.EmailAddresses[] */ &e;\n" +
            "&c = &e;");

        Assert.Contains(warnings, w => w.Message ==
            "&c is annotated as CompIntfc.USER_PROFILE.Roles[] (line 1) but is assigned CompIntfc.USER_PROFILE.EmailAddresses[] here.");
    }

    // Review Focus 5
    [Fact]
    public void Named_ci_passed_to_parameter_annotated_with_a_different_ci_warns()
    {
        var (_, errors, warnings) = TypeCheck(
            "Function F(&c As ApiObject /* as CompIntfc.USER_PROFILE */)\nEnd-Function;\n" +
            "F(%Session.GetCompIntfc(CompIntfc.OTHER_CI));");

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Contains("CompIntfc.USER_PROFILE", warning.Message);
        Assert.Contains("CompIntfc.OTHER_CI", warning.Message);
    }
}
