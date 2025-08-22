using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted;

public class QuickTest
{
    public static void DebugExpression(string code)
    {
        Console.WriteLine($"Parsing: {code}");
        
        var lexer = new PeopleCodeLexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser(tokens);
        var result = parser.ParseExpression();
        
        Console.WriteLine($"Result type: {result?.GetType().Name}");
        Console.WriteLine($"Result: {result}");
        Console.WriteLine($"Errors: {parser.Errors.Count}");
        foreach (var error in parser.Errors)
        {
            Console.WriteLine($"  - {error.Message}");
        }
        Console.WriteLine();
    }
    
    public static void Main()
    {
        DebugExpression("&a + &b AS MyClass");
        DebugExpression("&obj AS MyClass.method()");
        DebugExpression("&result = &obj AS MyClass");
    }
}