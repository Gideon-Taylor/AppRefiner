using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Metadata stores a parameter's app class type by the short name written in the signature
/// ("Config"), while inference qualifies the argument ("PKG:Config"). The call validator
/// matches the two on the class name (weak-spots §3).
/// </summary>
public class ShortNameParameterTests
{
    private static List<TypeError> TypeErrors(string source, ITypeMetadataResolver resolver, string? qualifiedName = null)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, resolver);
        TypeCheckerVisitor.Run(program, resolver, resolver.Cache);

        return program.GetAllTypeErrors().ToList();
    }

    private static TypeMetadata Extract(string source, string qualifiedName)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
    }

    private const string OwnClassSource =
        "class Svc\n" +
        "   method Svc(&other As Svc);\n" +
        "   method Compare(&other As Svc) Returns boolean;\n" +
        "   method Make();\n" +
        "end-class;\n" +
        "\n" +
        "method Svc\n" +
        "end-method;\n" +
        "\n" +
        "method Compare\n" +
        "   Return True;\n" +
        "end-method;\n" +
        "\n" +
        "method Make\n" +
        "   Local Svc &a;\n" +
        "   Local Svc &b = create Svc(&a);\n" +
        "   Local Svc &c = create Svc(%This);\n" +
        "   Local boolean &eq = %This.Compare(&a);\n" +
        "end-method;\n";

    [Fact]
    public void Own_class_short_name_parameter_accepts_own_class_values()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Svc", Extract(OwnClassSource, "PKG:Svc"));

        Assert.Empty(TypeErrors(OwnClassSource, resolver, "PKG:Svc"));
    }

    private static FakeTypeMetadataResolver CrossResolver()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Config", Extract("class Config\nend-class;\n", "PKG:Config"));
        resolver.AddClass("PKG:Other", Extract("class Other\nend-class;\n", "PKG:Other"));
        resolver.AddClass("PKG:Factory", Extract(
            "import PKG:Config;\n" +
            "class Factory\n" +
            "   method Get() Returns Config;\n" +
            "   method GetAll() Returns array of Config;\n" +
            "   property Config Current;\n" +
            "end-class;\n", "PKG:Factory"));
        resolver.AddClass("PKG:Svc", Extract(
            "import PKG:Config;\n" +
            "class Svc\n" +
            "   method Svc(&cfg As Config);\n" +
            "   method Use(&cfg As Config);\n" +
            "   method UseAll(&cfgs As array of Config);\n" +
            "end-class;\n", "PKG:Svc"));
        return resolver;
    }

    // The import lets inference qualify Factory's "Returns Config" to PKG:Config
    private const string CrossPrelude =
        "import PKG:Config;\n" +
        "Local PKG:Factory &f = create PKG:Factory();\n" +
        "Local PKG:Svc &s = create PKG:Svc(&f.Get());\n";

    [Theory]
    [InlineData("&s.Use(&f.Get());")]
    [InlineData("&s.Use(&f.Current);")]
    [InlineData("Local PKG:Svc &s2 = create PKG:Svc(&f.Get());")]
    [InlineData("Local PKG:Config &c = create PKG:Config();\n&s.Use(&c);")]
    [InlineData("&s.UseAll(&f.GetAll());")]
    [InlineData("Local array of PKG:Config &arr;\n&s.UseAll(&arr);")]
    public void Cross_program_short_name_parameter_accepts_qualified_value(string call)
    {
        Assert.Empty(TypeErrors(CrossPrelude + call, CrossResolver()));
    }

    [Fact]
    public void Unrelated_class_argument_still_errors()
    {
        var errors = TypeErrors(CrossPrelude + "&s.Use(create PKG:Other());", CrossResolver());

        var error = Assert.Single(errors);
        Assert.Contains("Argument 1", error.Message);
    }

    [Fact]
    public void Unrelated_class_array_argument_still_errors()
    {
        // A typed local: CreateArrayRept's result is not typed by its element, so it can't mismatch
        var errors = TypeErrors(CrossPrelude + "Local array of PKG:Other &o;\n&s.UseAll(&o);", CrossResolver());

        var error = Assert.Single(errors);
        Assert.Contains("Argument 1", error.Message);
    }
}
