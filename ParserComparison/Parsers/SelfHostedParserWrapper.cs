using System.Diagnostics;
using ParserComparison.Models;
using ParserComparison.Utils;

namespace ParserComparison.Parsers;

public class SelfHostedParserWrapper : IParser
{
    public string Name => "Self-Hosted";

    public ParseResult Parse(string sourceCode, string filePath)
    {
        var result = new ParseResult
        {
            ParserType = Name,
            FilePath = filePath,
            FileSize = sourceCode.Length
        };

        try
        {
            var monitor = new PerformanceMonitor();
            monitor.StartMonitoring();

            // Lexing phase
            var lexerStopwatch = Stopwatch.StartNew();
            var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
            var tokens = lexer.TokenizeAll();
            lexerStopwatch.Stop();

            // Parsing phase
            var parserStopwatch = Stopwatch.StartNew();
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens,"8.61");
            var parseTree = parser.ParseProgram();
            parserStopwatch.Stop();
            var totalStopwatch = monitor.StopMonitoring();

            result.Success = parseTree != null && !parser.Errors.Any();
            result.LexerDuration = lexerStopwatch.Elapsed;
            result.ParserDuration = parserStopwatch.Elapsed;
            result.TotalDuration = totalStopwatch.Elapsed;
            result.MemoryBefore = monitor.MemoryBefore;
            result.MemoryAfter = monitor.MemoryAfter;
            result.ErrorCount = parser.Errors.Count;
            
            if (parseTree != null)
            {
                result.NodeCount = CountSelfHostedNodes(parseTree);
            }

            if (!result.Success && parser.Errors.Any())
            {
                result.ErrorMessage = string.Join("; ", parser.Errors.Take(3).Select(e => $"({e.Location}){e.Message}"));
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ErrorCount = 1;
        }

        return result;
    }

    private int CountSelfHostedNodes(PeopleCodeParser.SelfHosted.AstNode node)
    {
        if (node == null) return 0;
        
        int count = 1; // Count this node
        
        // Recursively count all children using the AstNode's Children property
        foreach (var child in node.Children)
        {
            count += CountSelfHostedNodes(child);
        }
        
        return count;
    }
}