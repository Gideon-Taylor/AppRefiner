using System.Diagnostics;
using ParserComparison.Models;
using ParserComparison.Utils;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Nodes;

namespace ParserComparison.Parsers;

public class SelfHostedParserWrapper : IParser
{
    /// <summary>
    /// Simple visitor for benchmarking AST traversal performance.
    /// Does not perform any custom logic - just walks the tree with scope tracking.
    /// </summary>
    private class BenchmarkVisitor : ScopedAstVisitor<object>
    {
        // No custom implementation needed - uses default walk behavior from ScopedAstVisitor
    }

    public string Name => "Self-Hosted";
    public ParseResult Parse(string sourceCode, string filePath, bool skipGarbageCollection = false)
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
            monitor.StartMonitoring(skipGarbageCollection);

            // Lexing phase
            var lexerStopwatch = Stopwatch.StartNew();
            var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
            var tokens = lexer.TokenizeAll();
            lexerStopwatch.Stop();

            // Parsing phase
            var parserStopwatch = Stopwatch.StartNew();
            PeopleCodeParser.SelfHosted.PeopleCodeParser.ToolsRelease = new PeopleCodeParser.SelfHosted.ToolsVersion("8.61");
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
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

            // Time the ScopedAstVisitor walk
            if (parseTree != null)
            {
                var visitorStopwatch = Stopwatch.StartNew();
                var visitor = new BenchmarkVisitor();
                visitor.VisitProgram(parseTree);
                visitorStopwatch.Stop();
                result.VisitorWalkDuration = visitorStopwatch.Elapsed;
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
}