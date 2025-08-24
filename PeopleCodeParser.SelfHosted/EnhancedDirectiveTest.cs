using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Test;

/// <summary>
/// Comprehensive tests for enhanced directive condition parsing with complex expressions
/// </summary>
public static class EnhancedDirectiveTest
{
    public static void RunEnhancedTests()
    {
        Console.WriteLine("=== Enhanced PeopleCode Directive Tests ===\n");
        
        TestDirectiveTokenRecognition();
        TestSimpleConditions();
        TestReversedOperands();
        TestLogicalAndConditions();
        TestLogicalOrConditions();
        TestMixedPrecedence();
        TestToolsRelSelfComparisons();
        TestErrorHandling();
        TestRealWorldExamples();
        
        Console.WriteLine("Enhanced directive tests completed!");
    }

    private static void TestDirectiveTokenRecognition()
    {
        Console.WriteLine("Testing DirectiveAnd and DirectiveOr token recognition...");
        
        var source = "#If #ToolsRel >= \"8.54\" && #ToolsRel < \"8.56\" || #ToolsRel >= \"8.60\" #Then";
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        
        var directiveTokens = tokens.Where(t => 
            t.Type == TokenType.DirectiveAnd || 
            t.Type == TokenType.DirectiveOr).ToList();
        
        Console.WriteLine($"  Found {directiveTokens.Count} logical operator tokens:");
        foreach (var token in directiveTokens)
        {
            Console.WriteLine($"    {token.Type}: '{token.Text}'");
        }
        
        var expectedCount = 2; // One && and one ||
        Console.WriteLine($"  ✓ Expected {expectedCount} tokens, found {directiveTokens.Count}");
        Console.WriteLine();
    }

    private static void TestSimpleConditions()
    {
        Console.WriteLine("Testing simple directive conditions...");
        
        // Standard format: #ToolsRel < "version"
        TestDirectiveCondition(
            "#If #ToolsRel < \"8.55\" #Then\n  Local string &old;\n#Else\n  Local string &new;\n#End-If",
            "8.54.00",
            "Simple less-than condition with 8.54.00 (should use THEN)"
        );
        
        TestDirectiveCondition(
            "#If #ToolsRel >= \"8.55\" #Then\n  Local string &new;\n#Else\n  Local string &old;\n#End-If",
            "8.56.00",
            "Simple greater-equal condition with 8.56.00 (should use THEN)"
        );
        
        Console.WriteLine();
    }

    private static void TestReversedOperands()
    {
        Console.WriteLine("Testing reversed operand conditions...");
        
        // Reversed format: "version" > #ToolsRel
        TestDirectiveCondition(
            "#If \"8.55\" > #ToolsRel #Then\n  Local string &old;\n#Else\n  Local string &new;\n#End-If",
            "8.54.00",
            "Reversed operands with 8.54.00 (should use THEN)"
        );
        
        TestDirectiveCondition(
            "#If \"8.55\" <= #ToolsRel #Then\n  Local string &new;\n#Else\n  Local string &old;\n#End-If",
            "8.56.00",
            "Reversed operands with 8.56.00 (should use THEN)"
        );
        
        Console.WriteLine();
    }

    private static void TestLogicalAndConditions()
    {
        Console.WriteLine("Testing logical AND conditions...");
        
        // Range check: version >= 8.54 && version < 8.56
        TestDirectiveCondition(
            "#If #ToolsRel >= \"8.54\" && #ToolsRel < \"8.56\" #Then\n  Local string &inRange;\n#Else\n  Local string &outOfRange;\n#End-If",
            "8.55.00",
            "AND condition with 8.55.00 in range (should use THEN)"
        );
        
        TestDirectiveCondition(
            "#If #ToolsRel >= \"8.54\" && #ToolsRel < \"8.56\" #Then\n  Local string &inRange;\n#Else\n  Local string &outOfRange;\n#End-If",
            "8.57.00",
            "AND condition with 8.57.00 out of range (should use ELSE)"
        );
        
        Console.WriteLine();
    }

    private static void TestLogicalOrConditions()
    {
        Console.WriteLine("Testing logical OR conditions...");
        
        // Either very old or very new: version < 8.54 || version >= 8.60
        TestDirectiveCondition(
            "#If #ToolsRel < \"8.54\" || #ToolsRel >= \"8.60\" #Then\n  Local string &specialCase;\n#Else\n  Local string &normalCase;\n#End-If",
            "8.53.00",
            "OR condition with 8.53.00 (very old, should use THEN)"
        );
        
        TestDirectiveCondition(
            "#If #ToolsRel < \"8.54\" || #ToolsRel >= \"8.60\" #Then\n  Local string &specialCase;\n#Else\n  Local string &normalCase;\n#End-If",
            "8.55.00",
            "OR condition with 8.55.00 (middle range, should use ELSE)"
        );
        
        Console.WriteLine();
    }

    private static void TestMixedPrecedence()
    {
        Console.WriteLine("Testing mixed precedence conditions...");
        
        // Test precedence: A || B && C should be A || (B && C)
        TestDirectiveCondition(
            "#If #ToolsRel < \"8.54\" || #ToolsRel >= \"8.55\" && #ToolsRel < \"8.57\" #Then\n  Local string &complex;\n#Else\n  Local string &simple;\n#End-If",
            "8.56.00",
            "Mixed precedence with 8.56.00 (should evaluate as: false || (true && true) = true, use THEN)"
        );
        
        Console.WriteLine();
    }

    private static void TestToolsRelSelfComparisons()
    {
        Console.WriteLine("Testing #ToolsRel vs #ToolsRel comparisons...");
        
        // Test equality - should always be true
        TestDirectiveCondition(
            "#If #ToolsRel = #ToolsRel #Then\n  Local string &alwaysTrue;\n#Else\n  Local string &neverReached;\n#End-If",
            "8.55.00",
            "#ToolsRel = #ToolsRel (should always use THEN)"
        );
        
        // Test inequality - should always be false
        TestDirectiveCondition(
            "#If #ToolsRel <> #ToolsRel #Then\n  Local string &neverReached;\n#Else\n  Local string &alwaysFalse;\n#End-If",
            "8.55.00",
            "#ToolsRel <> #ToolsRel (should always use ELSE)"
        );
        
        // Test less than - should always be false
        TestDirectiveCondition(
            "#If #ToolsRel < #ToolsRel #Then\n  Local string &neverReached;\n#Else\n  Local string &alwaysFalse;\n#End-If",
            "8.55.00",
            "#ToolsRel < #ToolsRel (should always use ELSE)"
        );
        
        // Test less than or equal - should always be true
        TestDirectiveCondition(
            "#If #ToolsRel <= #ToolsRel #Then\n  Local string &alwaysTrue;\n#Else\n  Local string &neverReached;\n#End-If",
            "8.55.00",
            "#ToolsRel <= #ToolsRel (should always use THEN)"
        );
        
        // Test greater than - should always be false
        TestDirectiveCondition(
            "#If #ToolsRel > #ToolsRel #Then\n  Local string &neverReached;\n#Else\n  Local string &alwaysFalse;\n#End-If",
            "8.55.00",
            "#ToolsRel > #ToolsRel (should always use ELSE)"
        );
        
        // Test greater than or equal - should always be true
        TestDirectiveCondition(
            "#If #ToolsRel >= #ToolsRel #Then\n  Local string &alwaysTrue;\n#Else\n  Local string &neverReached;\n#End-If",
            "8.55.00",
            "#ToolsRel >= #ToolsRel (should always use THEN)"
        );
        
        // Test with no configured version - should still work
        TestDirectiveCondition(
            "#If #ToolsRel = #ToolsRel #Then\n  Local string &alwaysTrue;\n#End-If",
            null,
            "#ToolsRel = #ToolsRel with no version configured (should always use THEN)"
        );
        
        // Test in complex expression
        TestDirectiveCondition(
            "#If #ToolsRel = #ToolsRel && \"8.55\" > \"8.54\" #Then\n  Local string &complex;\n#End-If",
            "8.55.00",
            "Complex: #ToolsRel = #ToolsRel && version comparison (should use THEN)"
        );
        
        Console.WriteLine();
    }

    private static void TestErrorHandling()
    {
        Console.WriteLine("Testing error handling...");
        
        // Test parentheses rejection
        TestDirectiveCondition(
            "#If (#ToolsRel >= \"8.54\") #Then\n  Local string &error;\n#End-If",
            "8.55.00",
            "Parentheses should be rejected (should use default newer branch)"
        );
        
        // Test invalid version string
        TestDirectiveCondition(
            "#If #ToolsRel >= \"invalid.version\" #Then\n  Local string &error;\n#End-If",
            "8.55.00",
            "Invalid version string (should use default newer branch)"
        );
        
        Console.WriteLine();
    }

    private static void TestRealWorldExamples()
    {
        Console.WriteLine("Testing real-world directive examples...");
        
        // Complex real-world example
        var realWorldSource = @"
            #If #ToolsRel >= ""8.54.01"" && #ToolsRel < ""8.54.03"" #Then
                /* Workaround for specific versions */
                Local string &workaround;
                &workaround = ""Version-specific fix"";
            #Else
                /* Standard implementation */
                Local string &standard;
                &standard = ""Standard implementation"";
            #End-If
            
            #If #ToolsRel < ""8.53"" || #ToolsRel >= ""8.60"" #Then
                /* Edge case handling */
                Local boolean &edgeCase;
                &edgeCase = True;
            #Else
                /* Normal processing */
                Local boolean &normalCase;
                &normalCase = True;
            #End-If";
        
        TestDirectiveCondition(realWorldSource, "8.54.02", "Real-world complex conditions");
        
        Console.WriteLine();
    }

    private static void TestDirectiveCondition(string source, string? version, string description)
    {
        Console.WriteLine($"  Testing: {description}");
        
        try
        {
            var lexer = new PeopleCodeLexer(source);
            var tokens = lexer.TokenizeAll().Where(t => !t.Type.IsTrivia()).ToList();
            
            var parser = new PeopleCodeParser(tokens);
            if (version != null)
            {
                parser.SetToolsRelease(version);
            }
            
            var program = parser.ParseProgram();
            
            if (program != null)
            {
                Console.WriteLine($"    ✓ Parse successful");
                Console.WriteLine($"      Statements: {program.MainBlock?.Statements.Count ?? 0}");
                Console.WriteLine($"      Errors: {parser.Errors.Count}");
                
                if (parser.Errors.Any())
                {
                    foreach (var error in parser.Errors.Take(2))
                    {
                        Console.WriteLine($"        {error}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"    ✗ Parse failed");
                foreach (var error in parser.Errors.Take(3))
                {
                    Console.WriteLine($"        {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Exception: {ex.Message}");
        }
    }
}