using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// The program being edited may use short class names (App Designer qualifies them on save);
/// every consumer of its metadata or its extends clause sees them qualified through its imports.
/// </summary>
public class ImportResolutionTests
{
    private const string Sub =
        "import PKG:Base;\n" +
        "import WILD:*;\n" +
        "class Sub extends Base\n" +
        "   method Probe(&w As Widget) Returns Widget;\n" +
        "   property Sub Next;\n" +
        "end-class;\n" +
        "\n" +
        "method Probe\n" +
        "   %This.ProtectedM();\n" +
        "   %This.Missing();\n" +
        "   Return &w;\n" +
        "end-method;\n";

    private static FakeTypeMetadataResolver Resolver()
    {
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddPackageClasses("WILD", "Widget");
        resolver.AddClass("PKG:Base", new TypeMetadata
        {
            QualifiedName = "PKG:Base",
            Name = "Base",
            Kind = ProgramKind.AppClass,
            Methods = new Dictionary<string, FunctionInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProtectedM"] = new FunctionInfo { Name = "ProtectedM", Visibility = MemberVisibility.Protected },
            },
        });
        return resolver;
    }

    private static ProgramNode ParseClean(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return program;
    }

    [Fact]
    public void Edited_metadata_is_qualified_through_explicit_wildcard_and_own_names()
    {
        var metadata = TypeMetadataBuilder.ExtractEditedMetadata(ParseClean(Sub), "PKG:Sub", Resolver());

        Assert.Equal("PKG:Base", metadata.BaseClassName);
        var probe = metadata.Methods["Probe"];
        Assert.Equal("WILD:Widget", probe.ReturnType.AppClassPath);
        Assert.Equal("WILD:Widget", Assert.IsType<SingleParameter>(probe.Parameters[0]).ParameterType.AppClassPath);
        Assert.Equal("PKG:Sub", metadata.Properties["Next"].AppClassPath);
    }

    [Fact]
    public void Without_resolver_only_wildcards_stay_short()
    {
        var metadata = TypeMetadataBuilder.ExtractEditedMetadata(ParseClean(Sub), "PKG:Sub", resolver: null);

        Assert.Equal("PKG:Base", metadata.BaseClassName);
        Assert.Equal("Widget", metadata.Methods["Probe"].ReturnType.AppClassPath);
    }

    [Fact]
    public void Base_type_name_is_qualified()
    {
        var program = ParseClean(Sub);

        Assert.Equal("PKG:Base", ImportResolution.BaseTypeName(program.AppClass!, Resolver()));
        Assert.Null(ImportResolution.BaseTypeName(ParseClean("class Lone\nend-class;\n").AppClass!, Resolver()));
    }

    [Fact]
    public void Reference_through_wildcard_is_qualified()
    {
        var program = ParseClean(Sub);
        var widget = program.FindDescendants<AppClassTypeNode>().First(t => t.ClassName == "Widget");

        Assert.Equal("WILD:Widget", ImportResolution.QualifiedName(widget, Resolver()));
        Assert.Equal("Widget", ImportResolution.QualifiedName(widget, resolver: null));
    }

    [Fact]
    public void First_import_of_a_short_name_wins()
    {
        var program = ParseClean("import A:Thing;\nimport B:Thing;\nLocal Thing &t;\n");
        var thing = program.FindDescendants<AppClassTypeNode>().Last();

        Assert.Equal("A:Thing", ImportResolution.QualifiedName(thing, resolver: null));
        Assert.Equal("A:Thing", ImportResolution.BuildImportMap(program, null, null)["Thing"]);
    }

    // Production builds the compile checks' self metadata separately from inference's copy
    private static IReadOnlyList<CompileDiagnostic> Check(Func<ProgramNode, TypeMetadata> selfMetadata)
    {
        var (program, errors) = Parse(Sub);
        var resolver = Resolver();
        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program, "PKG:Sub"), resolver);
        return CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null, selfMetadata(program)));
    }

    [Fact]
    public void Inherited_members_are_checked_through_short_extends()
    {
        var diags = Check(program => TypeMetadataBuilder.ExtractEditedMetadata(program, "PKG:Sub", Resolver()));

        // Unqualified, the walk stopped at "Base" and skipped the member: Missing went unreported
        Assert.Contains(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess && d.Message.Contains("Missing"));
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InaccessibleMember);
    }
}
