using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Tests.Compilation;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Narrowing;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests.Narrowing;

/// <summary>
/// Usage narrowing wired into inference (spec §4, §8 "Where it runs", §10): a narrowed type is
/// seeded at the declaration, conflicts replace the generic invalid-member error, and an
/// assignment of a different type warns once and replaces it.
/// </summary>
public class NarrowingInferenceTests
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

    /// <summary>
    /// The full pipeline, including <c>InvalidMemberAccessCheck</c> and collected type warnings.
    /// </summary>
    private static IReadOnlyList<CompileDiagnostic> Check(string source, FakeTypeMetadataResolver? resolver = null, string? qualifiedName = null) =>
        CheckProgram(source, resolver, qualifiedName).Diagnostics;

    /// <summary>
    /// <see cref="Check"/>, also returning the inferred tree. <paramref name="beforeCheck"/> runs
    /// between inference and the checks.
    /// </summary>
    private static (ProgramNode Program, IReadOnlyList<CompileDiagnostic> Diagnostics) CheckProgram(
        string source, FakeTypeMetadataResolver? resolver = null, string? qualifiedName = null, Action<ProgramNode>? beforeCheck = null)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        resolver ??= new FakeTypeMetadataResolver();
        var metadata = qualifiedName == null
            ? TypeMetadataBuilder.ExtractMetadata(program)
            : TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        TypeInferenceVisitor.Run(program, metadata, resolver);
        beforeCheck?.Invoke(program);
        return (program, CompileChecker.Check(program, errors, resolver, new CompileCheckContextInput(null, qualifiedName == null ? null : metadata)));
    }

    /// <summary>The use of <paramref name="name"/> on 1-based <paramref name="line"/>.</summary>
    private static IdentifierNode UseOn(ProgramNode program, string name, int line) =>
        program.FindDescendants<IdentifierNode>()
            .First(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && n.SourceSpan.Start.Line == line - 1);

    private static List<string> ApiTypesWithMethod(string name) =>
        Enum.GetValues<PeopleCodeType>()
            .Select(t => t.GetTypeName())
            .Where(PeopleCodeTypeDatabase.IsApiObjectSubtype)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => PeopleCodeTypeDatabase.GetMethod(t, name) != null)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void Narrowed_type_reaches_uses_before_the_deciding_one()
    {
        var (program, _, _) = TypeCheck("Local ApiObject &x;\n&y = &x.ErrorPending;\n&x.GetCompIntfc(CompIntfc.A);");

        var early = UseOn(program, "&x", 2);
        Assert.Equal("Session", early.GetInferredType()!.Name, ignoreCase: true);
        Assert.Equal(RefinementKind.Narrowing, early.GetRefinementSource()!.Kind);
        Assert.Equal("Session", UseOn(program, "&x", 3).GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Conflict_is_one_warning_and_no_member_error()
    {
        var diags = Check("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Zzz();");

        var onLine3 = Assert.Single(diags, d => d.Span.Start.Line == 2);
        Assert.Equal(DiagnosticCode.TypeWarning, onLine3.Code);
        Assert.Equal("&x was inferred as Session from GetCompIntfc() on line 2, but Session has no member 'Zzz'.", onLine3.Message);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
    }

    [Fact]
    public void Assigned_variable_keeps_normal_member_error()
    {
        var diags = Check("Local ApiObject &x = %Session;\n&x.Zzz();");

        var error = Assert.Single(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
        Assert.Equal("'Zzz' is not a known method on 'Session' (&x is Session, assigned on line 1)", error.Message);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.TypeWarning);
    }

    [Fact]
    public void Inherited_annotation_beats_narrowing()
    {
        const string baseSource =
            "class Base\n" +
            "   method Use(&s As ApiObject /* as Session */);\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "end-method;\n";
        const string subSource =
            "class Sub extends PKG:Base\n" +
            "   method Use(&s As ApiObject);\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "   &s.Zzz();\n" +
            "end-method;\n";

        var (baseProgram, baseErrors) = Parse(baseSource);
        Assert.Empty(baseErrors);
        var resolver = new FakeTypeMetadataResolver();
        resolver.AddClass("PKG:Base", TypeMetadataBuilder.ExtractMetadata(baseProgram, "PKG:Base"));

        var diags = Check(subSource, resolver, "PKG:Sub");

        var error = Assert.Single(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
        Assert.StartsWith("'Zzz' is not a known method on 'Session' (&s is Session, annotated on line", error.Message);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.TypeWarning);
    }

    [Fact]
    public void Rerun_on_same_tree_drops_stale_refinement_source()
    {
        const string baseSource =
            "class Base\n" +
            "   method Use(&s As ApiObject /* as Session */);\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "end-method;\n";
        const string subSource =
            "class Sub extends PKG:Base\n" +
            "   method Use(&s As ApiObject);\n" +
            "end-class;\n" +
            "\n" +
            "method Use\n" +
            "   &s.Zzz();\n" +
            "end-method;\n";

        var (baseProgram, baseErrors) = Parse(baseSource);
        Assert.Empty(baseErrors);
        var withBase = new FakeTypeMetadataResolver();
        withBase.AddClass("PKG:Base", TypeMetadataBuilder.ExtractMetadata(baseProgram, "PKG:Base"));

        var (program, errors) = Parse(subSource);
        Assert.Empty(errors);
        var metadata = TypeMetadataBuilder.ExtractMetadata(program, "PKG:Sub");
        var use = program.FindDescendants<MemberAccessNode>().Single(m => m.MemberName == "Zzz");
        var target = (IdentifierNode)use.Target;

        // First pass: the inherited annotation types &s
        TypeInferenceVisitor.Run(program, metadata, withBase);
        Assert.Equal(RefinementKind.Annotation, target.GetRefinementSource()!.Kind);
        Assert.False(use.HasNarrowingConflict());

        // Second pass on the same tree without the base class: nothing refines &s any more, so the
        // stale Annotation stamp must go and the narrowing conflict (N3) takes the use
        TypeInferenceVisitor.Run(program, metadata, new FakeTypeMetadataResolver());
        Assert.Null(target.GetRefinementSource());
        Assert.True(use.HasNarrowingConflict());
    }

    [Fact]
    public void Assignment_replaces_narrowed_type_with_N4()
    {
        var (program, _, warnings) = TypeCheck("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x = %Session.GetQuery();");

        var warning = Assert.Single(warnings);
        Assert.Equal("Uses of &x suggest Session (GetCompIntfc() on line 2), but it is assigned Query on line 3.", warning.Message);
        Assert.DoesNotContain(warnings, w => w.Message.Contains("more than one API type"));
    }

    [Fact]
    public void Agreeing_assignment_is_silent()
    {
        var (_, _, warnings) = TypeCheck("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x = %Session;");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Undecided_uses_carry_candidates()
    {
        // Open is on PortalRegistry and Query only (Task 1's N1 test)
        var owners = ApiTypesWithMethod("Open");
        Assert.InRange(owners.Count, 2, 3);

        var (program, _, _) = TypeCheck("Local ApiObject &x;\n&x.Open();");

        var use = UseOn(program, "&x", 2);
        Assert.Equal(owners, use.GetNarrowingCandidates());
        Assert.Equal("ApiObject", use.GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Undecided_use_gets_no_base_type_member_error()
    {
        var owners = ApiTypesWithMethod("Open");

        var diags = Check("Local ApiObject &x;\n&x.Open();\n&x.Zzz();");

        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
        var warning = Assert.Single(diags, d => d.Code == DiagnosticCode.TypeWarning);
        Assert.Equal(2, warning.Span.Start.Line);
        Assert.Equal(
            $"'Zzz' is not a member of {UsageNarrowingAnalyzer.CandidatePhrase(owners)}. " +
            "Earlier uses of &x (Open() on line 2) suggested one of those types.",
            warning.Message);
    }

    [Fact]
    public void Deciding_use_after_conflicting_assignment_gets_member_error_not_N4()
    {
        var diags = Check("Local ApiObject &x;\n&x = %Session.GetQuery();\n&x.GetCompIntfc(CompIntfc.A);");

        var diag = Assert.Single(diags);
        Assert.Equal(DiagnosticCode.InvalidMemberAccess, diag.Code);
        Assert.Equal(2, diag.Span.Start.Line);
    }

    [Fact]
    public void Refinement_source_origin_names_kind_and_deciding_use()
    {
        var (program, parseErrors) = Parse("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);");
        Assert.Empty(parseErrors);
        var use = program.FindDescendants<MemberAccessNode>().Single(m => m.MemberName == "GetCompIntfc");

        Assert.Equal("annotated", new RefinementSource(RefinementKind.Annotation, use).Origin);
        Assert.Equal("assigned", new RefinementSource(RefinementKind.Assignment, use).Origin);
        Assert.Equal("inferred from GetCompIntfc()", new RefinementSource(RefinementKind.Narrowing, use).Origin);
        Assert.Equal("inferred", new RefinementSource(RefinementKind.Narrowing, program).Origin);
    }

    [Fact]
    public void Member_error_on_narrowed_target_names_the_deciding_use()
    {
        // Every non-fitting counted use is a marked conflict, so no real source reaches the
        // check with a narrowed target; drop the marker to exercise the suffix directly
        var (_, diags) = CheckProgram(
            "Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Zzz();",
            beforeCheck: program =>
            {
                foreach (var use in program.FindDescendants<MemberAccessNode>())
                    use.Attributes.Remove(AstNode.NarrowingConflictAttributeKey);
            });

        var error = Assert.Single(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);
        Assert.Equal("'Zzz' is not a known method on 'Session' (&x is Session, inferred from GetCompIntfc() on line 2)", error.Message);
    }

    [Fact]
    public void Half_typed_member_after_settling_keeps_type()
    {
        var (program, diags) = CheckProgram("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Ge;\n&x.GetCompIntfc(CompIntfc.B);");

        // A bare &x.Ge; statement also gets the checker's statement-level "Expression values must be
        // assigned to a variable." error; that is about the statement, not the member, so only
        // member and warning diagnostics are counted here
        var warning = Assert.Single(diags, d => d.Code == DiagnosticCode.TypeWarning);
        Assert.Equal(2, warning.Span.Start.Line);
        Assert.Equal("&x was inferred as Session from GetCompIntfc() on line 2, but Session has no member 'Ge'.", warning.Message);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess);

        Assert.Equal("Session", UseOn(program, "&x", 4).GetInferredType()!.Name, ignoreCase: true);
    }
}
