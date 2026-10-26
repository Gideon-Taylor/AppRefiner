using PeopleCodeParser.SelfHosted.Annotations;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests.Annotations;

/// <summary>
/// <c>/* as T */</c> annotations are found by position: directly after a type node, or directly
/// after an assignment's value (either side of the <c>;</c>), on the same line.
/// </summary>
public class AnnotationIndexTests
{
    private static (ProgramNode Program, AnnotationIndex Index) Build(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return (program, AnnotationIndex.Build(program));
    }

    private static void AssertTarget(ParsedAnnotation? annotation, PeopleCodeType expected)
    {
        Assert.NotNull(annotation);
        Assert.NotNull(annotation!.Target);
        Assert.Equal(expected, annotation.Target!.Type);
    }

    [Fact]
    public void Finds_annotation_after_local_type()
    {
        var (program, index) = Build("Local ApiObject /* as Session */ &x;");

        var decl = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        AssertTarget(index.ForType(decl.Type), PeopleCodeType.Session);
    }

    [Fact]
    public void Finds_annotation_after_parameter_type()
    {
        var (program, index) = Build(
            "Function Foo(&a As ApiObject /* as Session */, &b As string)\nEnd-Function;");

        var parameters = program.Functions.Single().Parameters;

        AssertTarget(index.ForType(parameters[0].Type), PeopleCodeType.Session);
        Assert.Null(index.ForType(parameters[1].Type));
    }

    [Fact]
    public void Finds_annotation_after_function_return_type()
    {
        var (program, index) = Build("Function Foo() Returns ApiObject /* as Session */\nEnd-Function;");

        AssertTarget(index.ForType(program.Functions.Single().ReturnType), PeopleCodeType.Session);
    }

    [Fact]
    public void Finds_annotation_after_method_header_types()
    {
        var (program, index) = Build(
            "class Foo\n" +
            "   method GetCI(&s As ApiObject /* as Session */) Returns ApiObject /* as CompIntfc.FOO */;\n" +
            "end-class;");

        var method = program.AppClass!.Methods.Single();

        AssertTarget(index.ForType(method.Parameters.Single().Type), PeopleCodeType.Session);
        var ret = index.ForType(method.ReturnType);
        AssertTarget(ret, PeopleCodeType.ComponentInterface);
        Assert.Equal("FOO", ret!.Target!.Identity?.CompIntfcName);
    }

    [Fact]
    public void Finds_annotation_after_property_and_instance_types()
    {
        var (program, index) = Build(
            "class Foo\n" +
            "   property ApiObject /* as Session */ CurrentSession get;\n" +
            "private\n" +
            "   instance ApiObject /* as Tree */ &t;\n" +
            "end-class;");

        var appClass = program.AppClass!;

        AssertTarget(index.ForType(appClass.Properties.Single().Type), PeopleCodeType.Session);
        AssertTarget(index.ForType(appClass.InstanceVariables.Single().Type), PeopleCodeType.Tree);
    }

    [Fact]
    public void Finds_annotation_after_global_and_component_types()
    {
        var (program, index) = Build(
            "Global ApiObject /* as Session */ &g;\nComponent ApiObject /* as Tree */ &c;");

        var vars = program.ComponentAndGlobalVariables;

        AssertTarget(index.ForType(vars[0].Type), PeopleCodeType.Session);
        AssertTarget(index.ForType(vars[1].Type), PeopleCodeType.Tree);
    }

    [Fact]
    public void Finds_value_annotation_before_semicolon()
    {
        var (program, index) = Build("&ci = &s.GetCompIntfc(&n) /* as CompIntfc.FOO */;");

        var assignment = program.FindDescendants<AssignmentNode>().Single();

        AssertTarget(index.ForValue(assignment.Value), PeopleCodeType.ComponentInterface);
    }

    [Fact]
    public void Finds_value_annotation_after_semicolon()
    {
        var (program, index) = Build("&d = GetIt(); /* as ComponentInterface */");

        var assignment = program.FindDescendants<AssignmentNode>().Single();

        AssertTarget(index.ForValue(assignment.Value), PeopleCodeType.ComponentInterface);
    }

    [Fact]
    public void Finds_value_annotation_on_local_with_initial_value()
    {
        var (program, index) = Build("Local ApiObject &s = GetX() /* as Session */;");

        var decl = program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>().Single();

        AssertTarget(index.ForValue(decl.InitialValue), PeopleCodeType.Session);
        Assert.Null(index.ForType(decl.Type));
    }

    [Fact]
    public void Ignores_annotation_on_next_line()
    {
        var (program, index) = Build("Local ApiObject &x;\n/* as Session */");

        var decl = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        Assert.Null(index.ForType(decl.Type));
    }

    [Fact]
    public void Ignores_value_annotation_after_semicolon_on_next_line()
    {
        var (program, index) = Build("&d = GetIt();\n/* as ComponentInterface */");

        var assignment = program.FindDescendants<AssignmentNode>().Single();

        Assert.Null(index.ForValue(assignment.Value));
    }

    [Fact]
    public void Ignores_annotation_after_variable_name()
    {
        var (program, index) = Build("Local ApiObject &x /* as Session */;");

        var decl = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        Assert.Null(index.ForType(decl.Type));
    }

    [Fact]
    public void Ignores_annotation_inside_argument()
    {
        var (program, index) = Build("&x = Foo(Bar() /* as Session */);");

        var assignment = program.FindDescendants<AssignmentNode>().Single();

        Assert.Null(index.ForValue(assignment.Value));
    }

    [Fact]
    public void Ignores_prose_and_commented_out_code()
    {
        var (program, index) = Build(
            "Local ApiObject /* store it as Session for later */ &x;\nLocal ApiObject /* &y = %Session; */ &y;");

        var decls = program.FindDescendants<LocalVariableDeclarationNode>().ToList();

        Assert.Null(index.ForType(decls[0].Type));
        Assert.Null(index.ForType(decls[1].Type));
    }

    [Fact]
    public void Returns_null_for_null_nodes()
    {
        var (_, index) = Build("Local ApiObject /* as Session */ &x;");

        Assert.Null(index.ForType(null));
        Assert.Null(index.ForValue(null));
    }

    [Fact]
    public void Tolerates_parse_errors()
    {
        var (program, _) = Parse("Local ApiObject /* as Session */ &x\nLocal");

        var ex = Record.Exception(() => AnnotationIndex.Build(program));

        Assert.Null(ex);
    }

    [Fact]
    public void Program_caches_its_annotation_index()
    {
        var (program, _) = Parse("Local ApiObject /* as Session */ &x;");

        Assert.Same(program.Annotations, program.Annotations);
        var decl = program.FindDescendants<LocalVariableDeclarationNode>().Single();
        AssertTarget(program.Annotations.ForType(decl.Type), PeopleCodeType.Session);
    }

    [Fact]
    public void Collects_comment_contexts_from_preceding_significant_tokens()
    {
        var (program, _) = Parse("&d = GetIt(); /* as ComponentInterface */");

        var context = program.CommentContexts.Single();

        Assert.Equal("/* as ComponentInterface */", context.Comment.Text);
        Assert.Equal(";", context.Previous!.Text);
        Assert.Equal(")", context.BeforePrevious!.Text);
    }
}
