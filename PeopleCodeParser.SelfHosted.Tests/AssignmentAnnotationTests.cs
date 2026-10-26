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
/// A <c>/* as T */</c> annotation after an assignment's value refines the variable at that point
/// (spec §7): T replaces the value's type, and a conflicting value warns while T wins.
/// </summary>
public class AssignmentAnnotationTests
{
    private const string GetIt = "Function GetIt() Returns ApiObject\nEnd-Function;\n";

    private static (ProgramNode Program, List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
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
    public void Annotation_types_unresolved_value()
    {
        var (program, _, warnings) = TypeCheck(
            GetIt + "Local ApiObject &d = GetIt() /* as ComponentInterface */;\n&d.Save();");

        Assert.Equal("ComponentInterface", LastUse(program, "&d").GetInferredType()!.Name, ignoreCase: true);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Annotation_after_semicolon_counts()
    {
        var (program, _, _) = TypeCheck(
            GetIt + "Local ApiObject &d;\n&d = GetIt(); /* as Session */\n&d.GetCompIntfc(CompIntfc.A);");

        Assert.Equal("Session", LastUse(program, "&d").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Annotation_adds_ci_name()
    {
        var (program, _, warnings) = TypeCheck(
            "Local string &n;\nLocal ApiObject &ci = %Session.GetCompIntfc(&n) /* as CompIntfc.FOO */;\n&ci.Save();");

        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), LastUse(program, "&ci").GetInferredType()!.Identity);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Conflicting_value_warns_and_annotation_wins()
    {
        var (program, _, warnings) = TypeCheck("Local ApiObject &x = %Session /* as Tree */;\n&x.Foo();");

        Assert.Contains(warnings, w => w.Message == "The value is Session, but it is annotated as Tree. Using Tree.");
        Assert.Equal("Tree", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Latest_wins_after_annotated_assignment()
    {
        var (program, errors, warnings) = TypeCheck(
            GetIt + "Local ApiObject &x = GetIt() /* as Session */;\n&x = GetIt() /* as Tree */;\n&x.Foo();");

        Assert.Equal("Tree", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
        Assert.DoesNotContain(warnings, w => w.Message.Contains("more than one API type"));
    }

    [Fact]
    public void Declaration_annotation_still_governs()
    {
        var (program, _, warnings) = TypeCheck(
            GetIt + "Local ApiObject /* as Session */ &x;\n&x = GetIt() /* as Tree */;\n&x.GetCompIntfc(CompIntfc.A);");

        Assert.Contains(warnings, w => w.Message == "&x is annotated as Session (line 3) but is assigned Tree here.");
        Assert.Equal("Session", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Declaration_conflict_wins_over_assignment_conflict()
    {
        var (_, _, warnings) = TypeCheck(
            "Local ApiObject /* as Session */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.A) /* as Tree */;");

        var only = Assert.Single(warnings, w => w.Node.SourceSpan.Start.Line == 1);
        Assert.Equal("&x is annotated as Session (line 1) but is assigned Tree here.", only.Message);
    }

    [Fact]
    public void Declaration_plus_more_detailed_assignment_annotation_adds_name()
    {
        var (program, _, warnings) = TypeCheck(
            "Local string &n;\nLocal ApiObject /* as ComponentInterface */ &x;\n" +
            "&x = %Session.GetCompIntfc(&n) /* as CompIntfc.FOO */;\n&x.Save();");

        var use = LastUse(program, "&x");
        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), use.GetInferredType()!.Identity);
        Assert.Equal(RefinementKind.Annotation, use.GetRefinementSource()!.Kind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Error_annotation_warns_on_value_and_refines_normally()
    {
        var (program, _, warnings) = TypeCheck("Local ApiObject &x = %Session /* as Foo */;\n&x.GetCompIntfc(CompIntfc.A);");

        var warning = Assert.Single(warnings);
        Assert.Equal("Unknown type 'Foo' in annotation.", warning.Message);
        Assert.IsAssignableFrom<ExpressionNode>(warning.Node);
        Assert.Equal("Session", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Misfit_annotation_warns_on_value()
    {
        var (_, _, warnings) = TypeCheck("Local Rowset &rs;\n&rs = CreateRowset(Record.JOB) /* as Session */;");

        var warning = Assert.Single(warnings);
        Assert.Equal("'as Session' needs an ApiObject declaration; &rs is Rowset.", warning.Message, ignoreCase: true);
        Assert.IsAssignableFrom<ExpressionNode>(warning.Node);
    }

    [Fact]
    public void Annotation_on_property_target_is_ignored()
    {
        const string source =
            "class Foo\n" +
            "   method Bar();\n" +
            "   property ApiObject P;\n" +
            "end-class;\n" +
            "\n" +
            "Function GetIt() Returns ApiObject\nEnd-Function;\n" +
            "\n" +
            "method Bar\n" +
            "   Local Foo &obj;\n" +
            "   &obj.P = GetIt() /* as Session */;\n" +
            "end-method;";

        var (_, _, warnings) = TypeCheck(source);

        Assert.Empty(warnings);
    }
}
