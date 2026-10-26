using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using Xunit;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// Type-checker rules for reassigning ApiObject/any variables: the declared type governs
/// compatibility, and an ApiObject variable that changes API subtype gets a warning, not an error.
/// </summary>
public class ApiObjectReassignmentCheckTests
{
    private const string OneTypeWarning = "more than one API type";

    private static (ProgramNode Program, List<TypeError> Errors, List<TypeError> Warnings) TypeCheck(string source)
    {
        var (program, parseErrors) = Parse(source);
        Assert.Empty(parseErrors);

        var metadata = TypeMetadataBuilder.ExtractMetadata(program);
        TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance);
        TypeCheckerVisitor.Run(program, NullTypeMetadataResolver.Instance, NullTypeMetadataResolver.Instance.Cache);

        return (program, program.GetAllTypeErrors().ToList(), program.GetAllTypeWarnings().ToList());
    }

    private static IdentifierNode LastUse(ProgramNode program, string name)
    {
        return program.FindDescendants<IdentifierNode>()
            .Where(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.SourceSpan.Start.Line)
            .ThenBy(n => n.SourceSpan.Start.Column)
            .Last();
    }

    [Fact]
    public void Reassigning_ApiObject_to_another_subtype_is_not_an_error()
    {
        var (_, errors, _) = TypeCheck(
            "Local ApiObject &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
    }

    [Fact]
    public void Reassigning_ApiObject_to_another_subtype_warns_with_both_types_and_lines()
    {
        var (_, _, warnings) = TypeCheck(
            "Local ApiObject &x;\n&x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        Assert.Contains(warnings, w => w.Message ==
            "&x is assigned more than one API type (Session on line 2, ComponentInterface on line 3). Use a separate variable for each type.");
    }

    [Fact]
    public void Reassigning_same_subtype_does_not_warn()
    {
        var (_, _, warnings) = TypeCheck("Local ApiObject &x = %Session;\n&x = %Session;");

        Assert.DoesNotContain(warnings, w => w.Message.Contains(OneTypeWarning));
    }

    [Fact]
    public void First_assignment_does_not_warn()
    {
        var (_, _, warnings) = TypeCheck("Local ApiObject &x;\n&x = %Session;");

        Assert.DoesNotContain(warnings, w => w.Message.Contains(OneTypeWarning));
    }

    [Fact]
    public void Lowercase_apiobject_declaration_behaves_the_same()
    {
        var (_, errors, warnings) = TypeCheck(
            "Local apiobject &x;\n&x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
        Assert.Contains(warnings, w => w.Message.Contains(OneTypeWarning));
    }

    [Fact]
    public void Any_declared_variable_gets_neither_error_nor_warning()
    {
        var (_, errors, warnings) = TypeCheck(
            "Local any &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Assigning_unresolved_ApiObject_value_keeps_type_and_is_silent()
    {
        var (program, errors, warnings) = TypeCheck(
            "Function GetThing() Returns ApiObject\nEnd-Function;\nLocal ApiObject &x = %Session;\n&x = GetThing();\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);");

        // The bare &x.GetCompIntfc(...) statement raises an unrelated "Return values must assigned
        // to a variable" error, so only the assignment-related diagnostics are asserted absent.
        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
        Assert.DoesNotContain(warnings, w => w.Message.Contains(OneTypeWarning));
        Assert.Equal("Session", LastUse(program, "&x").GetInferredType()!.Name, ignoreCase: true);
    }

    [Fact]
    public void Record_reassigned_to_different_named_record_is_silent()
    {
        var (_, errors, warnings) = TypeCheck(
            "Local Record &r = CreateRecord(Record.JOB);\n&r = CreateRecord(Record.PERSONAL_DATA);");

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Any_declared_variable_reassigned_non_api_value_is_silent()
    {
        var (_, errors, warnings) = TypeCheck("Local any &x = %Session;\n&x = \"done\";");

        Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"));
        Assert.DoesNotContain(warnings, w => w.Message.Contains(OneTypeWarning));
    }

    [Fact]
    public void Invalid_assignment_to_ApiObject_variable_names_declared_type()
    {
        var (_, errors, _) = TypeCheck(
            "Local ApiObject &x = %Session;\n&x = CreateRecord(Record.JOB);");

        Assert.Contains(errors, e => e.Message.Contains("to 'ApiObject'", StringComparison.OrdinalIgnoreCase));
    }
}
