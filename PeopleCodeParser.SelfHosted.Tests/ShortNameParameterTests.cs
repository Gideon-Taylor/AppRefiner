using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// App Designer qualifies every class name on save, so fetched programs are always qualified;
/// only the program being edited can use short names. Inference qualifies that program's own
/// signatures through its imports, so they match the qualified values passed to them, and
/// class names are otherwise compared whole.
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
        resolver.AddClass("OTHER:Config", Extract("class Config\nend-class;\n", "OTHER:Config"));
        // Fetched programs as saved: App Designer has qualified every class name
        resolver.AddClass("PKG:Factory", Extract(
            "import PKG:Config;\n" +
            "class Factory\n" +
            "   method Get() Returns PKG:Config;\n" +
            "   method GetAll() Returns array of PKG:Config;\n" +
            "   property PKG:Config Current;\n" +
            "end-class;\n", "PKG:Factory"));
        resolver.AddClass("PKG:Svc", Extract(
            "import PKG:Config;\n" +
            "class Svc\n" +
            "   method Svc(&cfg As PKG:Config);\n" +
            "   method Use(&cfg As PKG:Config);\n" +
            "   method UseAll(&cfgs As array of PKG:Config);\n" +
            "end-class;\n", "PKG:Svc"));
        return resolver;
    }

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
    [InlineData("Local Config &c = create Config();\n&s.Use(&c);")]
    [InlineData("Local array of Config &arr;\n&s.UseAll(&arr);")]
    public void Cross_program_parameter_accepts_values_named_either_way(string call)
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

    [Fact]
    public void Same_short_name_in_another_package_errors()
    {
        var errors = TypeErrors(CrossPrelude + "&s.Use(create OTHER:Config());", CrossResolver());

        var error = Assert.Single(errors);
        Assert.Contains("Argument 1", error.Message);
    }

    // The edited program, unsaved: its own signatures and extends still use imported short names
    private static string Edited(string body) =>
        "import PKG:Config;\n" +
        "import PKG:Base;\n" +
        "class Svc extends Base\n" +
        "   method Svc();\n" +
        "   method Use(&cfg As Config) Returns Config;\n" +
        "   method Run();\n" +
        "end-class;\n" +
        "\n" +
        "method Svc\n" +
        "   %Super = create Base();\n" +
        "end-method;\n" +
        "\n" +
        "method Use\n" +
        "   Return &cfg;\n" +
        "end-method;\n" +
        "\n" +
        "method Run\n" +
        body +
        "end-method;\n";

    private static FakeTypeMetadataResolver EditedResolver()
    {
        var resolver = CrossResolver();
        resolver.AddClass("PKG:Base", Extract(
            "class Base\n   method Ping(&cfg As PKG:Config);\nend-class;\n", "PKG:Base"));
        return resolver;
    }

    [Theory]
    [InlineData("   Local PKG:Config &c = %This.Use(create PKG:Config());\n")]
    [InlineData("   Local Config &c = %This.Use(create Config());\n")]
    [InlineData("   %This.Ping(%This.Use(create Config()));\n")]
    public void Edited_program_short_name_signatures_match_qualified_values(string body)
    {
        Assert.Empty(TypeErrors(Edited(body), EditedResolver(), "PKG:Svc"));
    }

    [Fact]
    public void Edited_program_signatures_and_extends_are_qualified_through_imports()
    {
        var (program, _) = Parse(Edited(""));
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Svc");

        TypeInferenceVisitor.Run(program, metadata, EditedResolver());

        Assert.Equal("PKG:Base", metadata.BaseClassName);
        var use = metadata.Methods["Use"];
        Assert.Equal("PKG:Config", use.ReturnType.AppClassPath);
        Assert.Equal("PKG:Config", Assert.IsType<SingleParameter>(use.Parameters[0]).ParameterType.AppClassPath);
    }

    [Fact]
    public void Edited_program_rejects_same_short_name_from_another_package()
    {
        var errors = TypeErrors(Edited("   Local Config &c = %This.Use(create OTHER:Config());\n"), EditedResolver(), "PKG:Svc");

        Assert.Contains(errors, e => e.Message.Contains("Argument 1"));
    }
}
