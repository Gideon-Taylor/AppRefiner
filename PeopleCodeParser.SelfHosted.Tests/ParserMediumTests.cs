using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using ParserImpl = PeopleCodeParser.SelfHosted.PeopleCodeParser;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests;

/// <summary>
/// PM-1: chained assignment is invalid PeopleCode and must be a parse error.
/// PM-2: EVALUATE "When Not" must not drop the Not when no operator follows.
/// PM-3: Global/Component declarations with an initializer must be kept + reported.
/// PM-4: #If-false with no #Else must record a well-formed skipped span.
/// PM-5: #Else at end of token list must not throw from the parser constructor.
/// </summary>
public class ParserMediumTests
{
    [Fact]
    public void CompleteShorthandAssignment_ParsesRhs()
    {
        // PL-1: a complete "&x += 1;" must become one assignment, not a partial
        // node plus an orphaned literal statement
        var (program, _) = Parse("&x += 1;");

        var assignment = program.FindDescendants<AssignmentNode>().Single();
        Assert.Equal(AssignmentOperator.AddAssign, assignment.Operator);
        Assert.Empty(program.FindDescendants<PartialShortHandAssignmentNode>());
        Assert.Single(program.MainBlock!.Statements);
    }

    [Fact]
    public void IncompleteShorthand_StillProducesPartialNode()
    {
        // The partial node is AppRefiner's expansion-on-entry hook — must survive
        var (program, _) = Parse("&x +=");

        Assert.Single(program.FindDescendants<PartialShortHandAssignmentNode>());
    }

    [Fact]
    public void ChainedAssignment_ReportsParseError()
    {
        var (program, errors) = Parse("&a = &b = &c;");

        Assert.Contains(errors, e => e.Message.Contains("parentheses"));
        // The AST is still built so tooling can offer a wrap-in-parens fix
        Assert.Equal(2, program.FindDescendants<AssignmentNode>().Count());
    }

    [Fact]
    public void ParenthesizedComparisonOnRhs_IsNotAnError()
    {
        var (_, errors) = Parse("&a = (&b = &c);");

        Assert.Empty(errors);
    }

    [Fact]
    public void EvaluateWhenNot_KeepsNegation()
    {
        var (program, errors) = Parse("""
            Evaluate &x
            When Not &flag
               &y = 1;
            End-Evaluate;
            """);

        Assert.Empty(errors);
        var not = program.FindDescendants<UnaryOperationNode>().Single();
        Assert.Equal(UnaryOperator.Not, not.Operator);
    }

    [Fact]
    public void EvaluateWhenNotWithOperator_StillParses()
    {
        var (_, errors) = Parse("""
            Evaluate &x
            When Not = 5
               &y = 1;
            End-Evaluate;
            """);

        Assert.Empty(errors);
    }

    [Fact]
    public void GlobalWithInitializer_KeepsDeclarationAndReports()
    {
        var (program, errors) = Parse("Global number &n = 5;\n&x = &n;");

        Assert.Contains(errors, e => e.Message.Contains("initializer"));
        Assert.Contains(program.ComponentAndGlobalVariables, v => v.Name == "&n");
        Assert.Contains(program.FindDescendants<AssignmentNode>(),
            a => (a.Target as IdentifierNode)?.Name == "&x");
    }

    [Fact]
    public void FalseIfDirectiveWithoutElse_RecordsWellFormedSkippedSpan()
    {
        // Default ToolsRelease is 99.99.99, so this condition is false
        var (program, _) = Parse("#If #ToolsRel < \"8.50\" #Then\n&old = 1;\n#End-If\n&x = 1;");

        var span = Assert.Single(program.SkippedDirectiveSpans);
        Assert.True(span.End.Index > span.Start.Index,
            $"Skipped span is inverted: {span.Start.Index}..{span.End.Index}");
        Assert.True(span.Start.Index > 0);
    }

    [Fact]
    public void ElseDirectiveAtEndOfTokenList_DoesNotThrow()
    {
        var lexer = new PeopleCodeLexer("#If #ToolsRel < \"8.50\" #Then\n&a = 1;\n#Else");
        var tokens = lexer.TokenizeAll()
            .Where(t => t.Type != TokenType.EndOfFile)
            .ToList();

        // Constructor runs directive preprocessing; must not throw on arbitrary token lists
        var parser = new ParserImpl(tokens);
        parser.ParseProgram();
    }
}
