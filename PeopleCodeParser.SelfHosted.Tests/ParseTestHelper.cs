using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using ParserImpl = PeopleCodeParser.SelfHosted.PeopleCodeParser;

namespace PeopleCodeParser.SelfHosted.Tests;

internal static class ParseTestHelper
{
    public static (ProgramNode Program, IReadOnlyList<ParseError> Errors) Parse(string source)
    {
        var lexer = new PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserImpl(tokens);
        var program = parser.ParseProgram();
        return (program, parser.Errors);
    }
}
