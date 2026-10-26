using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Declarations record the type each declared name holds right after them, for the hover on
/// the declared name.
/// </summary>
public class DeclaredVariableTypeTests
{
    private static List<DeclaredVariableType> Declared(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program), NullTypeMetadataResolver.Instance);
        return program.FindDescendants<AstNode>().SelectMany(n => n.GetDeclaredVariableTypes()).ToList();
    }

    [Fact]
    public void Declaration_with_value_shows_the_assigned_type()
    {
        var declared = Assert.Single(Declared("Local ApiObject &q = %Session.GetQuery();"));

        Assert.Equal("&q", declared.Name);
        Assert.Equal("Query", declared.Type.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Assignment, declared.Source!.Kind);
        Assert.Equal(0, declared.NameSpan.Start.Line);
    }

    [Fact]
    public void Declaration_shows_the_narrowed_type()
    {
        var declared = Assert.Single(Declared("Local ApiObject &x;\nLocal ApiObject &ci = &x.GetCompIntfc(CompIntfc.A);"), d => d.Name == "&x");

        Assert.Equal("Session", declared.Type.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Narrowing, declared.Source!.Kind);
    }

    [Fact]
    public void Undecided_declaration_shows_its_candidates()
    {
        var declared = Assert.Single(Declared("Local ApiObject &u;\n&u.Delete();"));

        Assert.Null(declared.Source);
        Assert.Equal(new[] { "PortalRegistry", "Query" }, declared.Candidates);
    }

    [Fact]
    public void Each_name_in_a_multi_name_declaration_is_recorded()
    {
        var declared = Declared("Local string &a, &b;");

        Assert.Equal(new[] { "&a", "&b" }, declared.Select(d => d.Name));
        Assert.All(declared, d => Assert.Equal("string", d.Type.Name, ignoreCase: true));
        Assert.NotEqual(declared[0].NameSpan, declared[1].NameSpan);
    }

    [Fact]
    public void Parameter_shows_its_narrowed_type()
    {
        var declared = Assert.Single(Declared(
            "Function F(&x As ApiObject)\n   Local ApiObject &ci = &x.GetCompIntfc(CompIntfc.A);\nEnd-Function;"), d => d.Name == "&x");

        Assert.Equal("Session", declared.Type.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Narrowing, declared.Source!.Kind);
        Assert.Equal(0, declared.NameSpan.Start.Line);
    }

    [Fact]
    public void Parameter_shows_its_annotated_type()
    {
        var declared = Assert.Single(Declared(
            "Function F(&s As ApiObject /* as Session */)\nEnd-Function;"), d => d.Name == "&s");

        Assert.Equal("Session", declared.Type.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Annotation, declared.Source!.Kind);
    }

    [Fact]
    public void Global_declaration_shows_the_narrowed_type()
    {
        var declared = Assert.Single(Declared("Global ApiObject &g;\nLocal ApiObject &ci = &g.GetCompIntfc(CompIntfc.A);"), d => d.Name == "&g");

        Assert.Equal("Session", declared.Type.Name, ignoreCase: true);
    }
}
