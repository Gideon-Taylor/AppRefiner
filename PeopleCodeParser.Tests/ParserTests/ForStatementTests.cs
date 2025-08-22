using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.Tests.Utilities;

namespace PeopleCodeParser.Tests.ParserTests;

/// <summary>
/// Tests for FOR statement parsing to ensure exact ANTLR grammar compliance
/// Grammar: FOR USER_VARIABLE EQ expression TO expression (STEP expression)? SEMI* statementBlock? END_FOR
/// </summary>
public class ForStatementTests
{
    /// <summary>
    /// Parse a FOR statement by wrapping it in a minimal program
    /// </summary>
    private static (ForStatementNode? forStatement, bool hasErrors) ParseForStatement(string forStatementCode)
    {
        var lexer = new PeopleCodeLexer(forStatementCode + ";"); // Add semicolon to complete the statement
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        
        var program = parser.ParseProgram();
        var hasErrors = lexer.Errors.Count > 0 || parser.Errors.Count > 0;
        
        // Extract the FOR statement from the program's main block
        if (program?.MainBlock?.Statements?.Count > 0 && program.MainBlock.Statements[0] is ForStatementNode forStmt)
        {
            return (forStmt, hasErrors);
        }
        
        return (null, hasErrors);
    }

    [Fact]
    public void ParseForStatement_BasicLoop_Success()
    {
        // FOR &i = 1 TO 10 END-FOR
        var code = "FOR &i = 1 TO 10 END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.NotNull(forStmt.FromValue);
        Assert.NotNull(forStmt.ToValue);
        Assert.Null(forStmt.StepValue);
        Assert.NotNull(forStmt.Body);
        Assert.Empty(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithStepValue_Success()
    {
        // FOR &i = 1 TO 10 STEP 2 END-FOR
        var code = "FOR &i = 1 TO 10 STEP 2 END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.NotNull(forStmt.FromValue);
        Assert.NotNull(forStmt.ToValue);
        Assert.NotNull(forStmt.StepValue);
    }

    [Fact]
    public void ParseForStatement_WithSingleSemicolon_Success()
    {
        // FOR &i = 1 TO 10 ; END-FOR
        var code = "FOR &i = 1 TO 10 ; END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.Empty(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithMultipleSemicolons_Success()
    {
        // FOR &i = 1 TO 10 ;; ; END-FOR
        var code = "FOR &i = 1 TO 10 ;; ; END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.Empty(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithStepAndSemicolons_Success()
    {
        // FOR &counter = 0 TO 100 STEP 5 ;; END-FOR
        var code = "FOR &counter = 0 TO 100 STEP 5 ;; END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&counter", forStmt.Variable);
        Assert.NotNull(forStmt.StepValue);
        Assert.Empty(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithStatementBlock_Success()
    {
        // FOR &i = 1 TO 5
        //   &sum = &sum + &i;
        // END-FOR
        var code = """
            FOR &i = 1 TO 5
              &sum = &sum + &i;
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.NotNull(forStmt.Body);
        Assert.Single(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithComplexExpressions_Success()
    {
        // FOR &i = GetStartValue() TO (GetEndValue() * 2)
        //   DoSomething(&i);
        // END-FOR
        var code = """
            FOR &i = GetStartValue() TO (GetEndValue() * 2)
              DoSomething(&i);
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&i", forStmt.Variable);
        Assert.NotNull(forStmt.FromValue);
        Assert.NotNull(forStmt.ToValue);
        Assert.Single(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_WithStepAndStatements_Success()
    {
        // FOR &j = 10 TO 1 STEP -1
        //   &result = &result + &j;
        //   ProcessValue(&j);
        // END-FOR
        var code = """
            FOR &j = 10 TO 1 STEP -1
              &result = &result + &j;
              ProcessValue(&j);
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&j", forStmt.Variable);
        Assert.NotNull(forStmt.StepValue);
        Assert.Equal(2, forStmt.Body.Statements.Count);
    }

    [Fact]
    public void ParseForStatement_WithSemicolonsAndStatements_Success()
    {
        // FOR &k = 1 TO 3 ; ;
        //   &total = &total + &k;
        // END-FOR
        var code = """
            FOR &k = 1 TO 3 ; ;
              &total = &total + &k;
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&k", forStmt.Variable);
        Assert.Single(forStmt.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_NestedForLoops_Success()
    {
        // FOR &i = 1 TO 3
        //   FOR &j = 1 TO 2
        //     &matrix(&i, &j) = &i * &j;
        //   END-FOR
        // END-FOR
        var code = """
            FOR &i = 1 TO 3
              FOR &j = 1 TO 2
                &matrix(&i, &j) = &i * &j;
              END-FOR
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var outerFor = result;
        Assert.Equal("&i", outerFor.Variable);
        Assert.Single(outerFor.Body.Statements);
        
        var innerStmt = outerFor.Body.Statements[0];
        Assert.IsType<ForStatementNode>(innerStmt);
        
        var innerFor = (ForStatementNode)innerStmt;
        Assert.Equal("&j", innerFor.Variable);
        Assert.Single(innerFor.Body.Statements);
    }

    [Fact]
    public void ParseForStatement_MissingUserVariable_ReturnsError()
    {
        // FOR someFunction() = 1 TO 10 END-FOR (invalid - must be USER_VARIABLE)
        var code = "FOR someFunction() = 1 TO 10 END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        // Should return null due to error
        Assert.Null(result);
        Assert.True(hasErrors);
    }

    [Fact]
    public void ParseForStatement_MissingEqualSign_RecoversGracefully()
    {
        // FOR &i 1 TO 10 END-FOR (missing =)
        var code = "FOR &i 1 TO 10 END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        // Should still parse with error recovery
        Assert.NotNull(result);
        Assert.True(hasErrors);
    }

    [Fact]
    public void ParseForStatement_MissingToKeyword_RecoversGracefully()
    {
        // FOR &i = 1 10 END-FOR (missing TO)
        var code = "FOR &i = 1 10 END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        // Should still parse with error recovery
        Assert.NotNull(result);
        Assert.True(hasErrors);
    }

    [Fact]
    public void ParseForStatement_MissingEndFor_RecoversGracefully()
    {
        // FOR &i = 1 TO 10 &sum = &sum + &i; (missing END-FOR)
        var code = "FOR &i = 1 TO 10 &sum = &sum + &i;";
        var (result, hasErrors) = ParseForStatement(code);
        
        // Should handle gracefully
        Assert.NotNull(result);
        Assert.True(hasErrors);
    }

    [Fact]
    public void ParseForStatement_EmptyStepExpression_RecoversGracefully()
    {
        // FOR &i = 1 TO 10 STEP END-FOR (missing step expression)
        var code = "FOR &i = 1 TO 10 STEP END-FOR";
        var (result, hasErrors) = ParseForStatement(code);
        
        // Should parse with error
        Assert.NotNull(result);
        Assert.True(hasErrors);
    }

    [Fact]
    public void ParseForStatement_VariousUserVariableNames_Success()
    {
        var testCases = new[]
        {
            ("&i", "&i"),
            ("&counter", "&counter"),
            ("&myVar", "&myVar"),
            ("&temp123", "&temp123"),
            ("&_index", "&_index")
        };

        foreach (var (input, expected) in testCases)
        {
            var code = $"FOR {input} = 1 TO 5 END-FOR";
            var (result, hasErrors) = ParseForStatement(code);
            
            Assert.NotNull(result);
            
            var forStmt = result;
            Assert.Equal(expected, forStmt.Variable);
        }
    }

    [Fact]
    public void ParseForStatement_ExactGrammarMatch_Success()
    {
        // Test the exact ANTLR grammar pattern:
        // FOR USER_VARIABLE EQ expression TO expression (STEP expression)? SEMI* statementBlock? END_FOR
        var code = """
            FOR &index = (startVal + 1) TO (endVal - 1) STEP increment ; ; ;
              ProcessItem(&index);
              &total = &total + GetValue(&index);
            END-FOR
            """;
        var (result, hasErrors) = ParseForStatement(code);
        
        Assert.NotNull(result);
        
        var forStmt = result;
        Assert.Equal("&index", forStmt.Variable);
        Assert.NotNull(forStmt.FromValue);
        Assert.NotNull(forStmt.ToValue);
        Assert.NotNull(forStmt.StepValue);
        Assert.Equal(2, forStmt.Body.Statements.Count);
    }
}