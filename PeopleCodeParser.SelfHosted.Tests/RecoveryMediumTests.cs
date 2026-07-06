using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// R-6..R-9 from the recovery-opportunity audit: silent statement swallowing by
/// non-assignment expression slots, bare `create`, unbounded annotation skips,
/// and directive condition extraction crossing lines.
/// </summary>
public class RecoveryMediumTests
{
    [Fact]
    public void HalfTypedForBound_DoesNotSwallowNextStatement()
    {
        // R-6: previously parsed CLEAN (zero errors) with &x = 1 absorbed as the To-bound
        var source = "For &i = 1 To\n&x = 1;\nEnd-For;";
        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var forNode = program.FindDescendants<ForStatementNode>().Single();
        Assert.IsNotType<AssignmentNode>(forNode.ToValue);
        Assert.Single(forNode.Body.Statements);
    }

    [Fact]
    public void HalfTypedLocalInitializer_KeepsDeclarationAndNextStatement()
    {
        // R-6 + R-11: declaration survives without initializer; next statement intact
        var source = "Local number &n =\n&x = 1;";
        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.NotEmpty(program.FindDescendants<LocalVariableDeclarationNode>());
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&x");
    }

    [Fact]
    public void HalfTypedReturn_DoesNotSwallowNextStatement()
    {
        // R-6
        var source = "Return\n&x = 1;";
        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var ret = program.FindDescendants<ReturnStatementNode>().Single();
        Assert.Null(ret.Value);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&x");
    }

    [Fact]
    public void BareCreate_DoesNotSwallowNextStatement()
    {
        // R-7
        var source = "create\n&x = 1;";
        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&x");
    }

    [Fact]
    public void LoneAnnotationOpen_DoesNotEatRestOfFile()
    {
        // R-8: unbounded annotation skip previously consumed every token to EOF
        var source = """
            class TestClass
               method Foo();
               method Bar();
            end-class;

            method Foo
               /+
               &x = 1;
            end-method;

            method Bar
               &y = 2;
            end-method;
            """;

        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        var bar = program.AppClass!.Methods.Single(m => m.Name == "Bar");
        Assert.NotNull(bar.Implementation);
    }

    [Fact]
    public void HalfTypedIfDirective_DoesNotDeleteRegionToNextDirective()
    {
        // R-9: "#If" with no "#Then" used to bind to the NEXT directive's #Then,
        // deleting everything between as "condition tokens"
        var source = "#If\nLocal number &x = 1;\n#If #ToolsRel >= \"8.60\" #Then\n&y = 2;\n#End-If\n&z = 3;";
        var (program, errors) = Parse(source);

        Assert.NotEmpty(errors);
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&y");
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&z");
        // `Local number &x = 1;` has an initializer, so it is an executable main-block
        // statement (not a program-level declaration); assert it survived recovery there.
        Assert.Contains(program.FindDescendants<LocalVariableDeclarationWithAssignmentNode>(),
            d => d.VariableNameInfo.Name == "&x");
    }

    [Fact]
    public void DirectiveErrors_CarryRealPositions()
    {
        // R-9c: directive diagnostics used to be pinned at offset 0
        var source = "&a = 1;\n#Else\n&b = 2;";
        var (_, errors) = Parse(source);

        var directiveError = errors.First(e => e.Message.Contains("#Else"));
        Assert.True(directiveError.Location.Start.Index > 0,
            $"Directive error pinned at offset {directiveError.Location.Start.Index}");
    }
}
