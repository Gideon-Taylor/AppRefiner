using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A parameter declared ApiObject with a <c>/* as T */</c> annotation still accepts any API value,
/// but a call passing a different API subtype gets a warning on the argument (spec §7).
/// </summary>
public class AnnotatedParameterCallTests
{
    private const string FooSource = "Function Foo(&s As ApiObject /* as Session */)\nEnd-Function;\n";

    private static (List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(
        string source, ITypeMetadataResolver? resolver = null, string? qualifiedName = null)
    {
        resolver ??= NullTypeMetadataResolver.Instance;
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, resolver);
        TypeCheckerVisitor.Run(program, resolver, resolver.Cache);

        return (program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
    }

    [Fact]
    public void Plain_ApiObject_argument_is_silent()
    {
        var (errors, warnings) = TypeCheck(FooSource + "Local ApiObject &a;\nFoo(&a);");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Matching_subtype_argument_is_silent()
    {
        var (errors, warnings) = TypeCheck(FooSource + "Foo(%Session);");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Different_subtype_argument_warns()
    {
        var (errors, warnings) = TypeCheck(
            FooSource + "Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\nFoo(&t);");

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("Foo expects &s as Session (annotated); the argument is CompIntfc.A.", warning.Message);
        Assert.IsType<IdentifierNode>(warning.Node);
    }

    [Fact]
    public void Cross_program_annotated_parameter_warns()
    {
        var (svcProgram, svcErrors) = Parse(
            "class Svc\n" +
            "   method Use(&s As ApiObject /* as Session */);\n" +
            "end-class;\n");
        Assert.Empty(svcErrors);
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Svc", TypeMetadataBuilder.ExtractMetadata(svcProgram, "PKG:Svc"));

        var (errors, warnings) = TypeCheck(
            "Local PKG:Svc &svc = create PKG:Svc();\n" +
            "Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\n" +
            "&svc.Use(&t);",
            resolver);

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("Use expects &s as Session (annotated); the argument is CompIntfc.A.", warning.Message);
    }

    [Fact]
    public void Call_site_warning_uses_inherited_parameter_annotation()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Base", Extract(
            "class Base\n" +
            "   method Use(&s As ApiObject /* as Session */);\n" +
            "end-class;\n", "PKG:Base"));
        resolver.AddClass("PKG:Sub", Extract(
            "class Sub extends PKG:Base\n" +
            "   method Use(&s As ApiObject);\n" +
            "end-class;\n", "PKG:Sub"));

        var (errors, warnings) = TypeCheck(
            "Local PKG:Sub &o = create PKG:Sub();\n" +
            "Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\n" +
            "&o.Use(&t);",
            resolver);

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("Use expects &s as Session (annotated); the argument is CompIntfc.A.", warning.Message);
    }

    private const string SvcConstructorSource =
        "class Svc\n" +
        "   method Svc(&s As ApiObject /* as Session */);\n" +
        "end-class;\n";

    private static FakeTypeMetadataResolver SvcResolver()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Svc", Extract(SvcConstructorSource, "PKG:Svc"));
        return resolver;
    }

    [Fact]
    public void Constructor_different_subtype_argument_warns()
    {
        var (errors, warnings) = TypeCheck(
            "Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\n" +
            "Local PKG:Svc &o = create PKG:Svc(&t);",
            SvcResolver());

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("Svc expects &s as Session (annotated); the argument is CompIntfc.A.", warning.Message);
        Assert.IsType<IdentifierNode>(warning.Node);
    }

    [Fact]
    public void Constructor_matching_subtype_argument_is_silent()
    {
        var (errors, warnings) = TypeCheck("Local PKG:Svc &o = create PKG:Svc(%Session);", SvcResolver());

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Constructor_plain_ApiObject_argument_is_silent()
    {
        var (errors, warnings) = TypeCheck(
            "Local ApiObject &a;\nLocal PKG:Svc &o = create PKG:Svc(&a);", SvcResolver());

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Default_constructor_create_is_silent()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Empty", Extract("class Empty\nend-class;\n", "PKG:Empty"));

        var (errors, warnings) = TypeCheck("Local PKG:Empty &o = create PKG:Empty();", resolver);

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    private const string SameProgramSvcSource =
        "class Svc\n" +
        "   method Svc(&s As ApiObject /* as Session */);\n" +
        "   method Make();\n" +
        "end-class;\n" +
        "\n" +
        "method Svc\n" +
        "end-method;\n" +
        "\n" +
        "method Make\n" +
        "   Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\n" +
        "   Local Svc &o = create Svc(&t);\n" +
        "end-method;\n";

    [Fact]
    public void Same_program_constructor_argument_warns()
    {
        var (errors, warnings) = TypeCheck(SameProgramSvcSource, qualifiedName: "PKG:Svc");

        Assert.Empty(errors);
        var warning = Assert.Single(warnings);
        Assert.Equal("Svc expects &s as Session (annotated); the argument is CompIntfc.A.", warning.Message);
    }

    [Fact]
    public void Same_program_short_name_local_accepts_This()
    {
        var (errors, warnings) = TypeCheck(
            "class Svc\n" +
            "   method Make();\n" +
            "end-class;\n" +
            "\n" +
            "method Make\n" +
            "   Local Svc &me = %This;\n" +
            "end-method;\n",
            qualifiedName: "PKG:Svc");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Same_program_short_name_method_return_assigns_cleanly()
    {
        var (errors, warnings) = TypeCheck(
            "class Svc\n" +
            "   method Clone() Returns Svc;\n" +
            "   method Make();\n" +
            "end-class;\n" +
            "\n" +
            "method Clone\n" +
            "   Return %This;\n" +
            "end-method;\n" +
            "\n" +
            "method Make\n" +
            "   Local Svc &c = %This.Clone();\n" +
            "end-method;\n",
            qualifiedName: "PKG:Svc");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Same_program_short_name_property_assigns_cleanly()
    {
        var (errors, warnings) = TypeCheck(
            "class Svc\n" +
            "   method Make();\n" +
            "   property Svc Next;\n" +
            "end-class;\n" +
            "\n" +
            "method Make\n" +
            "   Local Svc &n = %This.Next;\n" +
            "end-method;\n",
            qualifiedName: "PKG:Svc");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    private static TypeMetadata Extract(string source, string qualifiedName)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
    }
}
