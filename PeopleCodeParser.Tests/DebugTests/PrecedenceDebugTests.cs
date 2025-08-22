using FluentAssertions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;
using Xunit.Abstractions;

namespace PeopleCodeParser.Tests.DebugTests;

public class PrecedenceDebugTests
{
    private readonly ITestOutputHelper _output;

    public PrecedenceDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_Arithmetic_Type_Cast_Precedence()
    {
        // Test: &a + &b AS MyClass
        DebugExpression("&a + &b AS MyClass");
    }
    
    [Fact]
    public void Debug_Dot_Access_Type_Cast_Precedence()
    {
        // Test: &obj AS MyClass.method()
        DebugExpression("&obj AS MyClass.method()");
    }
    
    [Fact]
    public void Debug_Assignment_Type_Cast()
    {
        // Test: &result = &obj AS MyClass
        DebugExpression("&result = &obj AS MyClass");
    }

    private void DebugExpression(string code)
    {
        _output.WriteLine($"Parsing: {code}");
        
        var lexer = new PeopleCodeLexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        _output.WriteLine($"Result type: {result?.GetType().Name}");
        _output.WriteLine($"Result: {result}");
        _output.WriteLine($"Errors: {parser.Errors.Count}");
        foreach (var error in parser.Errors)
        {
            _output.WriteLine($"  - {error.Message}");
        }
        _output.WriteLine("");
        
        // Print AST structure
        if (result != null)
        {
            var astPrinter = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
            var astStructure = astPrinter.PrintAstStructure(result, useTreeCharacters: true);
            _output.WriteLine("AST Structure:");
            _output.WriteLine(astStructure);
        }
    }
}