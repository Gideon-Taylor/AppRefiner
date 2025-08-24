using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;
using ParserComparison.Models;
using ParserComparison.Utils;
using System.Diagnostics;

namespace ParserComparison.Parsers;

public class AntlrParserWrapper : IParser
{
    public string Name => "ANTLR";

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

            var lexerStopwatch = Stopwatch.StartNew();

            // Parse with ANTLR using ProgramParser (simplified approach)
            // Create the ANTLR stream and lexer
            ByteTrackingCharStream inputStream = new(sourceCode);
            PeopleCodeLexer lexer = new(inputStream);

            // Reset lexer state if it holds any from previous runs (optional but good practice)
            lexer.Reset();

            // Create the token stream and parser
            CommonTokenStream tokenStream = new(lexer);
            AppRefiner.PeopleCode.PeopleCodeParser parser = new(tokenStream);
            parser.ErrorHandler = new BailErrorStrategy();
            // Reset parser state (optional but good practice)
            parser.Reset();

            // Remove error listeners to suppress console output
            parser.RemoveErrorListeners();
            bool errorDuringParse = false;
            AppRefiner.PeopleCode.PeopleCodeParser.ProgramContext? parseTree = null;
            try
            {
                // Parse the program starting from the 'program' rule
                parseTree = parser.program();
            } catch (Antlr4.Runtime.Misc.ParseCanceledException e)
            {
                errorDuringParse = true;
            }
            finally
            {
                // Clear the DFA cache to release memory, as shown in the example
                // This is important if parsing many files sequentially.
                parser.Interpreter.ClearDFA();
            }

            // Consider adding basic error checking here, e.g., checking parser.NumberOfSyntaxErrors
            // if (parser.NumberOfSyntaxErrors > 0) { // Handle or log errors }



            lexerStopwatch.Stop();
            var totalStopwatch = monitor.StopMonitoring();

            result.Success = parseTree != null;
            result.LexerDuration = lexerStopwatch.Elapsed;
            result.ParserDuration = totalStopwatch.Elapsed - lexerStopwatch.Elapsed;
            result.TotalDuration = totalStopwatch.Elapsed;
            result.MemoryBefore = monitor.MemoryBefore;
            result.MemoryAfter = monitor.MemoryAfter;
            result.ErrorCount = errorDuringParse ? 1 : 0; // Will be improved in future iteration
            
            // Count nodes
            if (parseTree != null)
                result.NodeCount = CountAntlrNodes(parseTree);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ErrorCount = 1;
        }

        return result;
    }

    private int CountAntlrNodes(IParseTree node)
    {
        if (node == null) return 0;
        
        int count = 1; // Count this node
        
        // Recursively count all children
        for (int i = 0; i < node.ChildCount; i++)
        {
            count += CountAntlrNodes(node.GetChild(i));
        }
        
        return count;
    }
}