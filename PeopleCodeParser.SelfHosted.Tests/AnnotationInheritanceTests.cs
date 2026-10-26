using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// A subclass method inherits the annotations on its base declaration unless its own header
/// declaration has its own (spec §7 "Inheritance").
/// </summary>
public class AnnotationInheritanceTests
{
    private const string BaseSource =
        "class Base\n" +
        "   method Get() Returns ApiObject /* as Session */;\n" +
        "   method Use(&s As ApiObject /* as Session */);\n" +
        "end-class;\n" +
        "\n" +
        "method Get\n" +
        "   Return %Session;\n" +
        "end-method;\n" +
        "\n" +
        "method Use\n" +
        "end-method;\n";

    private const string PlainSubHeader =
        "class Sub extends PKG:Base\n" +
        "   method Get() Returns ApiObject;\n" +
        "   method Use(&s As ApiObject);\n" +
        "end-class;\n";

    private const string TreeSubHeader =
        "class Sub extends PKG:Base\n" +
        "   method Get() Returns ApiObject /* as Tree */;\n" +
        "   method Use(&s As ApiObject /* as Tree */);\n" +
        "end-class;\n";

    private const string MisfitSubHeader =
        "class Sub extends PKG:Base\n" +
        "   method Get() Returns ApiObject /* as Record.JOB */;\n" +
        "   method Use(&s As ApiObject /* as Record.JOB */);\n" +
        "end-class;\n";

    private const string SubBody =
        "\n" +
        "method Get\n" +
        "   Return %Session;\n" +
        "end-method;\n" +
        "\n" +
        "method Use\n" +
        "   &s.GetCompIntfc(CompIntfc.A);\n" +
        "end-method;\n";

    private static string CallerSource(string className) =>
        $"Local {className} &o = create {className}();\n" +
        "Local ApiObject &x = &o.Get();";

    /// <summary>
    /// A class overriding Get and Use with the given annotation text (e.g. <c>" /* as Session */"</c>)
    /// after the return and parameter types.
    /// </summary>
    private static string ClassSource(string name, string? baseName, string returnAnnotation = "", string parameterAnnotation = "") =>
        $"class {name}{(baseName == null ? "" : " extends " + baseName)}\n" +
        $"   method Get() Returns ApiObject{returnAnnotation};\n" +
        $"   method Use(&s As ApiObject{parameterAnnotation});\n" +
        "end-class;\n" +
        SubBody;

    private static TypeMetadata Extract(string source, string qualifiedName)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
    }

    private static FakeTypeMetadataResolver ResolverWith(string? subHeader, bool includeBase = true)
    {
        var resolver = new FakeTypeMetadataResolver();
        if (includeBase)
            resolver.AddClass("PKG:Base", Extract(BaseSource, "PKG:Base"));
        if (subHeader != null)
            resolver.AddClass("PKG:Sub", Extract(subHeader + SubBody, "PKG:Sub"));
        return resolver;
    }

    /// <summary>
    /// PKG:A annotates Get and Use as Session; PKG:B extends A and overrides both with the
    /// given annotation text (none by default).
    /// </summary>
    private static FakeTypeMetadataResolver ThreeLevelResolver(string middleAnnotation = "")
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:A", Extract(ClassSource("A", null, " /* as Session */", " /* as Session */"), "PKG:A"));
        resolver.AddClass("PKG:B", Extract(ClassSource("B", "PKG:A", middleAnnotation, middleAnnotation), "PKG:B"));
        return resolver;
    }

    private static ProgramNode Infer(string source, string? qualifiedName, ITypeMetadataResolver resolver)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        var metadata = qualifiedName == null
            ? TypeMetadataBuilder.ExtractMetadata(program)
            : TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, resolver);
        return program;
    }

    private static string? GetCallType(ProgramNode program) =>
        program.FindDescendants<FunctionCallNode>()
            .Single(c => c.Function is MemberAccessNode { MemberName: "Get" })
            .GetInferredType()?.Name;

    /// <summary>The inferred type of the <c>&amp;o.Get()</c> call in <see cref="CallerSource"/>.</summary>
    private static string? CallerGetType(string subHeader, ITypeMetadataResolver? resolver = null) =>
        GetCallType(Infer(CallerSource("PKG:Sub"), null, resolver ?? ResolverWith(subHeader)));

    /// <summary>The inferred type of the last <c>&amp;s</c> use in a class's <c>Use</c> body.</summary>
    private static string? BodyParameterType(string source, string qualifiedName, ITypeMetadataResolver resolver)
    {
        var program = Infer(source, qualifiedName, resolver);
        var use = program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals("&s", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .Last();
        return use.GetInferredType()?.Name;
    }

    /// <summary>The inferred type of <c>&amp;s</c> in PKG:Sub's <c>Use</c> body.</summary>
    private static string? BodyParameterType(string subHeader, ITypeMetadataResolver? resolver = null) =>
        BodyParameterType(subHeader + SubBody, "PKG:Sub", resolver ?? ResolverWith(null));

    [Fact]
    public void Caller_inherits_base_return_annotation()
    {
        Assert.Equal("Session", CallerGetType(PlainSubHeader), ignoreCase: true);
    }

    [Fact]
    public void Body_parameter_inherits_base_annotation()
    {
        Assert.Equal("Session", BodyParameterType(PlainSubHeader), ignoreCase: true);
    }

    [Fact]
    public void This_call_in_subclass_body_inherits_base_return_annotation()
    {
        const string source =
            PlainSubHeader +
            "\n" +
            "method Get\n" +
            "   Return %Session;\n" +
            "end-method;\n" +
            "\n" +
            "method Use\n" +
            "   Local ApiObject &x = %This.Get();\n" +
            "end-method;\n";

        var program = Infer(source, "PKG:Sub", ResolverWith(null));

        Assert.Equal("Session", GetCallType(program), ignoreCase: true);
    }

    [Fact]
    public void Own_annotation_overrides_base()
    {
        Assert.Equal("Tree", CallerGetType(TreeSubHeader), ignoreCase: true);
        Assert.Equal("Tree", BodyParameterType(TreeSubHeader), ignoreCase: true);
    }

    [Fact]
    public void Own_misfit_annotation_does_not_fall_back_to_base()
    {
        Assert.Equal("ApiObject", CallerGetType(MisfitSubHeader), ignoreCase: true);
        Assert.Equal("ApiObject", BodyParameterType(MisfitSubHeader), ignoreCase: true);
    }

    [Fact]
    public void Missing_base_metadata_means_no_inheritance()
    {
        Assert.Equal("ApiObject", BodyParameterType(PlainSubHeader, new FakeTypeMetadataResolver()), ignoreCase: true);
    }

    [Fact]
    public void Caller_with_missing_base_metadata_does_not_inherit()
    {
        var resolver = ResolverWith(PlainSubHeader, includeBase: false);

        Assert.Equal("ApiObject", CallerGetType(PlainSubHeader, resolver), ignoreCase: true);
    }

    [Fact]
    public void Parameter_annotation_inherits_through_two_levels()
    {
        Assert.Equal("Session",
            BodyParameterType(ClassSource("C", "PKG:B"), "PKG:C", ThreeLevelResolver()),
            ignoreCase: true);
    }

    [Fact]
    public void Intermediate_misfit_parameter_annotation_blocks_inheritance()
    {
        Assert.Equal("ApiObject",
            BodyParameterType(ClassSource("C", "PKG:B"), "PKG:C", ThreeLevelResolver(" /* as Foo */")),
            ignoreCase: true);
    }

    [Fact]
    public void Return_annotation_inherits_through_two_levels()
    {
        var resolver = ThreeLevelResolver();
        resolver.AddClass("PKG:C", Extract(ClassSource("C", "PKG:B"), "PKG:C"));

        Assert.Equal("Session", GetCallType(Infer(CallerSource("PKG:C"), null, resolver)), ignoreCase: true);
    }
}
