using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted.Test;

/// <summary>
/// Simple test class for validating compiler directive functionality
/// </summary>
public static class DirectiveTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== PeopleCode Compiler Directive Tests ===\n");
        
        TestToolsVersionParsing();
        TestToolsVersionComparison();
        TestDirectiveLexing();
        TestDirectiveParsingWithVersion();
        TestDirectiveParsingWithoutVersion();
        TestNestedDirectives();
        
        // Run enhanced directive tests
        EnhancedDirectiveTest.RunEnhancedTests();
        
        // Run real-world tests
        RealDirectiveTest.RunRealWorldTest();
        
        Console.WriteLine("All tests completed!");
    }

    private static void TestToolsVersionParsing()
    {
        Console.WriteLine("Testing ToolsVersion parsing...");
        
        // Test valid version strings
        var version1 = new ToolsVersion("8.55.13");
        Console.WriteLine($"  ✓ Parsed '8.55.13': {version1}");
        
        var version2 = new ToolsVersion("8.54");
        Console.WriteLine($"  ✓ Parsed '8.54': {version2}");
        
        try
        {
            var invalidVersion = new ToolsVersion("invalid");
            Console.WriteLine($"  ✗ Should have failed for 'invalid'");
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"  ✓ Correctly rejected 'invalid'");
        }
        
        Console.WriteLine();
    }

    private static void TestToolsVersionComparison()
    {
        Console.WriteLine("Testing ToolsVersion comparison...");
        
        var v855 = new ToolsVersion("8.55");
        var v85513 = new ToolsVersion("8.55.13");
        var v854 = new ToolsVersion("8.54");
        
        Console.WriteLine($"  ✓ 8.55 == 8.55.13: {v855 == v85513} (should be True - release level comparison)");
        Console.WriteLine($"  ✓ 8.54 < 8.55: {v854 < v855} (should be True)");
        Console.WriteLine($"  ✓ 8.55 > 8.54: {v855 > v854} (should be True)");
        
        var v85512 = new ToolsVersion("8.55.12");
        Console.WriteLine($"  ✓ 8.55.12 < 8.55.13: {v85512 < v85513} (should be True - patch level comparison)");
        
        Console.WriteLine();
    }

    private static void TestDirectiveLexing()
    {
        Console.WriteLine("Testing directive lexing...");
        
        var source = "#If #ToolsRel < \"8.55\" #Then\nLocal string &test;\n#Else\nLocal string &newTest;\n#End-If";
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        
        var directiveTokens = tokens.Where(t => t.Type.IsTrivia() && 
            (t.Type == TokenType.DirectiveIf || t.Type == TokenType.DirectiveToolsRel || 
             t.Type == TokenType.DirectiveThen || t.Type == TokenType.DirectiveElse || 
             t.Type == TokenType.DirectiveEndIf)).ToList();
        
        Console.WriteLine($"  Found {directiveTokens.Count} directive tokens:");
        foreach (var token in directiveTokens)
        {
            Console.WriteLine($"    {token.Type}: '{token.Text}'");
        }
        
        Console.WriteLine($"  ✓ Expected 5 directive tokens, found {directiveTokens.Count}");
        Console.WriteLine();
    }

    private static void TestDirectiveParsingWithVersion()
    {
        Console.WriteLine("Testing directive parsing WITH ToolsRelease version...");
        
        var source = @"
            #If #ToolsRel < ""8.55"" #Then
                Local string &oldCode;
                &oldCode = ""Old implementation"";
            #Else
                Local string &newCode;
                &newCode = ""New implementation"";
            #End-If";
        
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll().Where(t => !t.Type.IsTrivia()).ToList();
        
        var parser = new PeopleCodeParser(tokens);
        parser.SetToolsRelease("8.54.05"); // Should trigger THEN branch
        
        var program = parser.ParseProgram();
        
        Console.WriteLine($"  Parse result with version '8.54.05' (should use THEN branch):");
        Console.WriteLine($"    Errors: {parser.Errors.Count}");
        Console.WriteLine($"    Program has {program.MainBlock?.Statements.Count ?? 0} statements");
        
        // Test with newer version
        parser = new PeopleCodeParser(tokens);
        parser.SetToolsRelease("8.56.00"); // Should trigger ELSE branch
        
        program = parser.ParseProgram();
        Console.WriteLine($"  Parse result with version '8.56.00' (should use ELSE branch):");
        Console.WriteLine($"    Errors: {parser.Errors.Count}");
        Console.WriteLine($"    Program has {program.MainBlock?.Statements.Count ?? 0} statements");
        
        Console.WriteLine();
    }

    private static void TestDirectiveParsingWithoutVersion()
    {
        Console.WriteLine("Testing directive parsing WITHOUT ToolsRelease version...");
        
        var source = @"
            #If #ToolsRel < ""8.55"" #Then
                Local string &oldCode;
            #Else
                Local string &newCode;
            #End-If";
        
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll().Where(t => !t.Type.IsTrivia()).ToList();
        
        var parser = new PeopleCodeParser(tokens);
        // Don't set ToolsRelease - should use "newer branch" policy (ELSE)
        
        var program = parser.ParseProgram();
        
        Console.WriteLine($"  Parse result WITHOUT version (should prefer ELSE 'newer' branch):");
        Console.WriteLine($"    Errors: {parser.Errors.Count}");
        Console.WriteLine($"    Program has {program.MainBlock?.Statements.Count ?? 0} statements");
        
        Console.WriteLine();
    }

    private static void TestNestedDirectives()
    {
        Console.WriteLine("Testing nested directives...");
        
        var source = @"
            #If #ToolsRel >= ""8.54"" #Then
                Local string &outerThen;
                #If #ToolsRel >= ""8.55"" #Then
                    Local string &innerThen;
                #Else
                    Local string &innerElse;
                #End-If
            #Else
                Local string &outerElse;
            #End-If";
        
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll().Where(t => !t.Type.IsTrivia()).ToList();
        
        var parser = new PeopleCodeParser(tokens);
        parser.SetToolsRelease("8.54.10"); // Should trigger outer THEN, inner ELSE
        
        var program = parser.ParseProgram();
        
        Console.WriteLine($"  Parse result with nested directives:");
        Console.WriteLine($"    Errors: {parser.Errors.Count}");
        Console.WriteLine($"    Program has {program.MainBlock?.Statements.Count ?? 0} statements");
        
        if (parser.Errors.Any())
        {
            Console.WriteLine("    Errors found:");
            foreach (var error in parser.Errors)
            {
                Console.WriteLine($"      {error}");
            }
        }
        
        Console.WriteLine();
    }
}