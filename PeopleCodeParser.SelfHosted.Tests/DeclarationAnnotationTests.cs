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
/// A <c>/* as T */</c> annotation after a declaration's type types the variable for its whole
/// scope (spec §7): more detailed assignments add to it, conflicting ones warn and leave it.
/// </summary>
public class DeclarationAnnotationTests
{
    private static (ProgramNode Program, List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
    }

    private static List<TypeError> Warnings(string source) => TypeCheck(source).Warnings;

    private static IdentifierNode LastUse(ProgramNode program, string name)
    {
        return program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
    }

    private static string? LastUseTypeName(string source, string name) =>
        LastUse(TypeCheck(source).Program, name).GetInferredType()?.Name;

    [Fact]
    public void Local_annotation_types_the_variable()
    {
        var (program, _, _) = TypeCheck(
            "Local ApiObject /* as Session */ &x;\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        var use = LastUse(program, "&x");
        Assert.Equal("Session", use.GetInferredType()!.Name, ignoreCase: true);
        var src = use.GetRefinementSource();
        Assert.NotNull(src);
        Assert.Equal(RefinementKind.Annotation, src!.Kind);
        Assert.Equal(0, src.Line);
    }

    [Fact]
    public void Multi_variable_declaration_types_every_variable()
    {
        const string source =
            "Local ApiObject /* as Session */ &c, &d;\n&c.GetCompIntfc(CompIntfc.A);\n&d.GetCompIntfc(CompIntfc.B);";

        Assert.Equal("Session", LastUseTypeName(source, "&c"), ignoreCase: true);
        Assert.Equal("Session", LastUseTypeName(source, "&d"), ignoreCase: true);
    }

    [Fact]
    public void Parameter_annotation_types_parameter_in_body()
    {
        const string source =
            "Function Foo(&s As ApiObject /* as Session */)\n   &s.GetCompIntfc(CompIntfc.A);\nEnd-Function;";

        Assert.Equal("Session", LastUseTypeName(source, "&s"), ignoreCase: true);
    }

    [Fact]
    public void Global_and_instance_annotations_apply()
    {
        Assert.Equal("Session",
            LastUseTypeName("Global ApiObject /* as Session */ &g;\n&g.GetCompIntfc(CompIntfc.A);", "&g"),
            ignoreCase: true);

        const string classSource =
            "class Foo\n" +
            "   method Bar();\n" +
            "private\n" +
            "   instance ApiObject /* as Session */ &s;\n" +
            "end-class;\n" +
            "\n" +
            "method Bar\n" +
            "   &s.GetCompIntfc(CompIntfc.A);\n" +
            "end-method;";

        Assert.Equal("Session", LastUseTypeName(classSource, "&s"), ignoreCase: true);
    }

    [Fact]
    public void Conflicting_assignment_warns_and_annotation_stays()
    {
        var (program, errors, warnings) = TypeCheck(
            "Local ApiObject /* as Session */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.A);\n&x.GetCompIntfc(CompIntfc.B);");

        Assert.Contains(warnings, w => w.Message ==
            "&x is annotated as Session (line 1) but is assigned CompIntfc.A here.");
        Assert.Single(warnings, w => w.Message.Contains("&x"));
        Assert.Equal("Session", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void Same_subtype_assignment_does_not_warn()
    {
        var warnings = Warnings(
            "Local ApiObject /* as ComponentInterface */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Less_detailed_assignment_keeps_annotation()
    {
        var (program, _, warnings) = TypeCheck(
            // GetCompIntfc(&n) is a CI with no name (GetCompIntfc(CompIntfc.A) would be a conflict)
            "Local ApiObject /* as CompIntfc.FOO */ &x;\nLocal string &n;\n&x = %Session.GetCompIntfc(&n);\n&x.Save();");

        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), LastUse(program, "&x").GetInferredType()!.Identity);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Assigning_a_different_named_ci_conflicts_and_keeps_annotation()
    {
        var (program, _, warnings) = TypeCheck(
            "Local ApiObject /* as CompIntfc.FOO */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.A);\n&x.Save();");

        Assert.Equal(new TypeIdentity(CompIntfcName: "FOO"), LastUse(program, "&x").GetInferredType()!.Identity);
        var warning = Assert.Single(warnings);
        Assert.Equal("&x is annotated as CompIntfc.FOO (line 1) but is assigned CompIntfc.A here.", warning.Message);
    }

    [Fact]
    public void Misfit_annotation_warns()
    {
        var warnings = Warnings("Local Rowset /* as Session */ &rs;");

        Assert.Contains(warnings, w => w.Message.Equals(
            "'as Session' needs an ApiObject declaration; &rs is Rowset.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_annotation_warns()
    {
        var warnings = Warnings("Local ApiObject /* as CompIntfc */ &x;");

        Assert.Contains(warnings, w => w.Message ==
            "Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME?");
    }

    [Fact]
    public void Record_annotation_on_record_variable()
    {
        var (program, _, _) = TypeCheck("Local Record /* as Record.JOB */ &r;\n&r.GetField(Field.EMPLID);");

        var type = Assert.IsType<RecordTypeInfo>(LastUse(program, "&r").GetInferredType());
        Assert.Equal("JOB", type.RecordName, ignoreCase: true);
    }

    [Fact]
    public void Record_identity_conflict_names_both_records()
    {
        var warnings = Warnings("Local Record /* as Record.JOB */ &r;\n&r = CreateRecord(Record.PERSON);");

        Assert.Contains(warnings, w => w.Message ==
            "&r is annotated as Record.JOB (line 1) but is assigned Record.PERSON here.");
    }

    [Fact]
    public void Wrong_category_assignment_to_annotated_variable_reports_once()
    {
        var (_, errors, warnings) = TypeCheck("Local ApiObject /* as Session */ &x;\n&x = CreateRecord(Record.JOB);");

        var diagnostics = errors.Concat(warnings).Where(d => d.Node.SourceSpan.Start.Line == 1).ToList();
        var only = Assert.Single(diagnostics);
        Assert.Contains("Cannot assign", only.Message);
    }

    [Fact]
    public void Field_annotation_gains_record_from_assignment()
    {
        var (program, _, warnings) = TypeCheck(
            "Local Field /* as Field.EMPLID */ &f;\nLocal Record &r = CreateRecord(Record.JOB);\n" +
            "&f = &r.GetField(Field.EMPLID);\n&f.Name;");

        var use = LastUse(program, "&f");
        var type = Assert.IsType<FieldTypeInfo>(use.GetInferredType());
        Assert.Equal("JOB", type.RecordName, ignoreCase: true);
        Assert.Equal("EMPLID", type.FieldName, ignoreCase: true);
        Assert.Equal(RefinementKind.Annotation, use.GetRefinementSource()!.Kind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Any_declared_variable_accepts_annotation()
    {
        Assert.Equal("Session",
            LastUseTypeName("Local any /* as Session */ &x;\n&x.GetCompIntfc(CompIntfc.A);", "&x"),
            ignoreCase: true);
    }

    [Fact]
    public void Misfit_return_annotation_warns()
    {
        var warnings = Warnings("Function F() Returns Rowset /* as Session */\nEnd-Function;");

        Assert.Contains(warnings, w => w.Message.Equals(
            "'as Session' needs an ApiObject declaration; F is Rowset.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_return_annotation_warns()
    {
        var warnings = Warnings("Function F() Returns ApiObject /* as CompIntfc */\nEnd-Function;");

        Assert.Contains(warnings, w => w.Message ==
            "Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME?");
    }

    [Fact]
    public void Misfit_method_header_return_annotation_warns_once()
    {
        const string classSource =
            "class Foo\n" +
            "   method Bar() Returns Rowset /* as Session */;\n" +
            "end-class;\n" +
            "\n" +
            "method Bar\n" +
            "   Local Rowset &rs;\n" +
            "   Return &rs;\n" +
            "end-method;";

        var warnings = Warnings(classSource);

        Assert.Single(warnings, w => w.Message.Equals(
            "'as Session' needs an ApiObject declaration; Bar is Rowset.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Any_annotated_variable_assigned_string_warns()
    {
        var (program, _, warnings) = TypeCheck(
            "Local any /* as Session */ &x;\n&x = \"abc\";\n&x.GetCompIntfc(CompIntfc.A);");

        var warning = Assert.Single(warnings);
        Assert.Equal("&x is annotated as Session (line 1) but is assigned string here.", warning.Message);
        Assert.Equal("Session", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Declare_function_return_annotation_is_ignored()
    {
        var warnings = Warnings("Declare Function F PeopleCode REC.FLD FieldFormula Returns Rowset /* as Session */;");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Annotated_property_is_typed_inside_its_class()
    {
        const string classSource =
            "class Foo\n" +
            "   method Bar();\n" +
            "   property ApiObject /* as Session */ Sess;\n" +
            "end-class;\n" +
            "\n" +
            "method Bar\n" +
            "   %This.Sess.GetCompIntfc(CompIntfc.A);\n" +
            "end-method;";

        // %This resolves through the class's qualified name, so the program needs one
        var (program, parseErrors) = Parse(classSource);
        Assert.Empty(parseErrors);
        TypeInferenceVisitor.Run(program, TypeMetadataBuilder.ExtractMetadata(program, "PKG:Foo"), NullTypeMetadataResolver.Instance);

        var sess = program.FindDescendants<MemberAccessNode>().Single(m => m.MemberName == "Sess");
        Assert.Equal("Session", sess.GetInferredType()!.Name, ignoreCase: true);
        Assert.Empty(program.GetAllTypeWarnings());
    }
}
