using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Narrowing;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests.Narrowing;

/// <summary>
/// Usage narrowing (spec §8): the members used directly on an <c>ApiObject</c> variable settle
/// which API type it holds, at the first use that leaves one candidate.
/// </summary>
public class UsageNarrowingAnalyzerTests
{
    private static (ProgramNode Program, NarrowingResults Results) Analyze(string source)
    {
        var (program, errors) = Parse(source);
        Assert.Empty(errors);
        return (program, UsageNarrowingAnalyzer.Analyze(program));
    }

    private static NarrowingResult? ForLocal(string source, string name = "&x")
    {
        var (program, results) = Analyze(source);
        var declaration = program.FindDescendants<LocalVariableDeclarationNode>().First();
        return results.For(declaration, name);
    }

    private static List<string> ApiTypesWithMethod(string name) =>
        Enum.GetValues<PeopleCodeType>()
            .Select(t => t.GetTypeName())
            .Where(PeopleCodeTypeDatabase.IsApiObjectSubtype)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => PeopleCodeTypeDatabase.GetMethod(t, name) != null)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void One_distinguishing_method_settles_type()
    {
        var result = ForLocal("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);");

        Assert.NotNull(result);
        Assert.Equal("Session", result!.NarrowedType);
        Assert.Equal("GetCompIntfc", result.DecidingUse!.MemberName);
        Assert.Equal(new[] { "Session" }, result.Candidates);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Chained_members_do_not_count()
    {
        var result = ForLocal("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A).Save();");

        Assert.Equal("Session", result!.NarrowedType);
        Assert.Equal("GetCompIntfc", result.DecidingUse!.MemberName);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Annotated_variable_is_not_narrowed()
    {
        Assert.Null(ForLocal("Local ApiObject /* as Query */ &x;\n&x.GetCompIntfc(CompIntfc.A);"));
    }

    [Fact]
    public void Assignment_annotation_excludes_variable()
    {
        Assert.Null(ForLocal("Local ApiObject &x;\n&x = %Session /* as Session */;\n&x.GetCompIntfc(CompIntfc.A);"));
    }

    [Fact]
    public void Any_and_auto_declared_are_not_narrowed()
    {
        Assert.Null(ForLocal("Local any &a;\n&a.GetCompIntfc(CompIntfc.A);", "&a"));

        var (program, results) = Analyze("&b.GetCompIntfc(CompIntfc.A);");
        var autoDeclared = Assert.Single(program.AutoDeclaredVariables);
        Assert.Null(results.For(autoDeclared.DeclarationNode, "&b"));
    }

    [Fact]
    public void Use_matching_nothing_first_gives_N3()
    {
        var result = ForLocal("Local ApiObject &x;\n&x.Zzz();");

        var conflict = Assert.Single(result!.Conflicts);
        Assert.Equal("'Zzz' is not a member of any API object type.", conflict.Message);
        Assert.Equal("Zzz", conflict.Use.MemberName);
        Assert.Null(result.NarrowedType);
    }

    [Fact]
    public void Use_after_settling_gives_N2()
    {
        var (program, results) = Analyze("Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Zzz();");
        var result = results.For(program.FindDescendants<LocalVariableDeclarationNode>().Single(), "&x");

        var conflict = Assert.Single(result!.Conflicts);
        Assert.Equal("&x was inferred as Session from GetCompIntfc() on line 2, but Session has no member 'Zzz'.", conflict.Message);
        Assert.Equal("Session", result.NarrowedType);
        Assert.Same(conflict, results.ConflictAt(conflict.Use));
    }

    [Fact]
    public void Conflict_with_several_candidates_gives_N1()
    {
        // Open is on PortalRegistry and Query only
        const string member = "Open";
        var owners = ApiTypesWithMethod(member);
        Assert.InRange(owners.Count, 2, 3);

        var result = ForLocal($"Local ApiObject &x;\n&x.{member}();\n&x.Zzz();");

        var conflict = Assert.Single(result!.Conflicts);
        Assert.Equal(
            $"'Zzz' is not a member of {UsageNarrowingAnalyzer.CandidatePhrase(owners)}. " +
            $"Earlier uses of &x ({member}() on line 2) suggested one of those types.",
            conflict.Message);
        Assert.Null(result.NarrowedType);
        Assert.Equal(owners, result.Candidates);
    }

    [Fact]
    public void Candidate_phrase_follows_count()
    {
        Assert.Equal("A or B", UsageNarrowingAnalyzer.CandidatePhrase(new[] { "A", "B" }));
        Assert.Equal("A, B or C", UsageNarrowingAnalyzer.CandidatePhrase(new[] { "A", "B", "C" }));
        Assert.Equal("one of 4 types (A, B, C, …)", UsageNarrowingAnalyzer.CandidatePhrase(new[] { "A", "B", "C", "D" }));
    }

    [Fact]
    public void Ci_dynamic_property_does_not_settle_ci()
    {
        var result = ForLocal("Local ApiObject &x;\n&y = &x.EMPLID;");

        Assert.NotNull(result);
        Assert.Empty(result!.Conflicts);
        Assert.Null(result.NarrowedType);
        Assert.Null(result.DecidingUse);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Undeclared_property_does_not_block_a_later_method()
    {
        // A typo'd property doesn't narrow to the dynamic-property types and hide GetCompIntfc
        var result = ForLocal("Local ApiObject &x;\n&b = &x.IsConected;\n&ci = &x.GetCompIntfc(CompIntfc.A);");

        Assert.Equal("Session", result!.NarrowedType);
        Assert.Equal("GetCompIntfc", result.DecidingUse!.MemberName);
        Assert.Empty(result.Conflicts);
    }

    [Theory]
    [InlineData("InteractiveMode", "ComponentInterface")]
    [InlineData("ItemNum", "DataItem")]
    public void Property_declared_on_a_dynamic_type_settles_it(string property, string type)
    {
        var result = ForLocal($"Local ApiObject &x;\n&y = &x.{property};");

        Assert.Equal(type, result!.NarrowedType);
        Assert.Equal(property, result.DecidingUse!.MemberName);
    }

    [Fact]
    public void Ci_field_named_like_a_declared_property_keeps_ci()
    {
        // Name is declared on 25 API types, but a CI can also have a NAME field
        var result = ForLocal("Local ApiObject &x;\n&n = &x.Name;\n&b = &x.Get();");

        Assert.Equal("ComponentInterface", result!.NarrowedType);
        Assert.Equal("Get", result.DecidingUse!.MemberName);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Property_declared_only_on_a_collection_does_not_settle_it()
    {
        // CurrentItemNumber could still be a CI or item field
        var result = ForLocal("Local ApiObject &x;\n&n = &x.CurrentItemNumber;");

        Assert.Null(result!.NarrowedType);
        Assert.Equal(new[] { "ComponentInterface", "DataCollection", "DataItem" }, result.Candidates);
    }

    [Fact]
    public void Dynamic_property_settles_ci_once_data_item_is_ruled_out()
    {
        // Save is on ComponentInterface but not DataItem, so EMPLID leaves ComponentInterface alone
        var result = ForLocal("Local ApiObject &x;\n&b = &x.Save();\n&y = &x.EMPLID;");

        Assert.Equal("ComponentInterface", result!.NarrowedType);
        Assert.Equal("EMPLID", result.DecidingUse!.MemberName);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Ci_method_narrows_to_ci()
    {
        // Save is on nine API types; Get is on ComponentInterface only
        var result = ForLocal("Local ApiObject &x;\n&b = &x.Save();\n&b = &x.Get();");

        Assert.Equal("ComponentInterface", result!.NarrowedType);
        Assert.Equal("Get", result.DecidingUse!.MemberName);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Same_name_in_two_methods_narrows_separately()
    {
        const string queryOnly = "RunToRowset";
        Assert.Equal(new[] { "Query" }, ApiTypesWithMethod(queryOnly));

        var (program, results) = Analyze(
            "class C\n" +
            "   method A();\n" +
            "   method B();\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   Local ApiObject &x;\n" +
            "   &x.GetCompIntfc(CompIntfc.A);\n" +
            "end-method;\n" +
            "\n" +
            "method B\n" +
            "   Local ApiObject &x;\n" +
            $"   &x.{queryOnly}();\n" +
            "end-method;\n");

        var declarations = program.FindDescendants<LocalVariableDeclarationNode>().ToList();
        Assert.Equal(2, declarations.Count);

        Assert.Equal("Session", results.For(declarations[0], "&x")!.NarrowedType);
        Assert.Equal("Query", results.For(declarations[1], "&x")!.NarrowedType);
    }

    [Fact]
    public void Parameter_and_instance_variable_are_narrowed()
    {
        var (program, results) = Analyze(
            "class C\n" +
            "   method A(&p As ApiObject);\n" +
            "private\n" +
            "   instance ApiObject &i;\n" +
            "end-class;\n" +
            "\n" +
            "method A\n" +
            "   &p.GetCompIntfc(CompIntfc.A);\n" +
            "   &i.GetQuery();\n" +
            "end-method;\n");

        var parameter = program.FindDescendants<ParameterNode>().Single();
        var instance = program.FindDescendants<ProgramVariableNode>().Single();

        Assert.Equal("Session", results.For(parameter, "&p")!.NarrowedType);
        Assert.Equal("Session", results.For(instance, "&i")!.NarrowedType);
    }

    [Fact]
    public void Use_nested_in_object_creation_inside_assignment_counts()
    {
        var result = ForLocal("Local ApiObject &x;\n&y = create Foo(Bar(&x.GetCompIntfc(CompIntfc.A)));");

        Assert.Equal("Session", result!.NarrowedType);
    }

    [Fact]
    public void Variable_name_lookup_is_case_insensitive()
    {
        var (program, results) = Analyze("Local ApiObject &x;\n&X.getcompintfc(CompIntfc.A);");
        var declaration = program.FindDescendants<LocalVariableDeclarationNode>().Single();

        Assert.Equal("Session", results.For(declaration, "&X")!.NarrowedType);
    }
}
