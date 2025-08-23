using ParserComparison.Models;

namespace ParserComparison.Utils;

public static class ConsoleLogger
{
    public static void WriteHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine();
    }

    public static void WriteSubHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} ---");
        Console.WriteLine();
    }

    public static void WriteComparisonResult(ComparisonResult comparison)
    {
        Console.WriteLine($"File: {Path.GetFileName(comparison.FilePath)} ({FormatFileSize(comparison.FileSize)})");
        Console.WriteLine();

        // Results table
        Console.WriteLine($"{"Parser",-15} {"Success",-10} {"Total (ms)",-12} {"Lexer (ms)",-12} {"Parser (ms)",-12} {"Memory (KB)",-12} {"Nodes",-8} {"Errors",-8}");
        Console.WriteLine(new string('-', 95));
        
        WriteParseResult(comparison.AntlrResult);
        WriteParseResult(comparison.SelfHostedResult);
        
        Console.WriteLine();
        
        // Comparison metrics
        if (comparison.BothSuccessful)
        {
            Console.WriteLine("Performance Comparison:");
            Console.WriteLine($"  Speed Ratio (ANTLR/Self-Hosted): {comparison.AntlrSpeedRatio:F2}x");
            Console.WriteLine($"  Memory Ratio (ANTLR/Self-Hosted): {comparison.MemoryRatio:F2}x");
            
            if (comparison.AntlrResult.NodeCount.HasValue && comparison.SelfHostedResult.NodeCount.HasValue)
            {
                var nodeRatio = (double)comparison.AntlrResult.NodeCount.Value / comparison.SelfHostedResult.NodeCount.Value;
                Console.WriteLine($"  Node Count Ratio (ANTLR/Self-Hosted): {nodeRatio:F2}x");
                Console.WriteLine($"  AST Nodes: ANTLR={comparison.AntlrResult.NodeCount:N0}, Self-Hosted={comparison.SelfHostedResult.NodeCount:N0}");
            }
            
            var winner = comparison.AntlrSpeedRatio > 1 ? "Self-Hosted" : "ANTLR";
            var speedImprovement = Math.Max(comparison.AntlrSpeedRatio, 1.0 / comparison.AntlrSpeedRatio);
            Console.WriteLine($"  Winner: {winner} is {speedImprovement:F2}x faster");
        }
        else if (!comparison.BothSuccessful)
        {
            Console.WriteLine("Parse Issues:");
            if (!comparison.AntlrResult.Success)
                Console.WriteLine($"  ANTLR Error: {comparison.AntlrResult.ErrorMessage}");
            if (!comparison.SelfHostedResult.Success)
                Console.WriteLine($"  Self-Hosted Error: {comparison.SelfHostedResult.ErrorMessage}");
        }
    }

    private static void WriteParseResult(ParseResult result)
    {
        var success = result.Success ? "✓" : "✗";
        var totalMs = result.TotalDuration.TotalMilliseconds;
        var lexerMs = result.LexerDuration.TotalMilliseconds;
        var parserMs = result.ParserDuration.TotalMilliseconds;
        var memoryKb = result.MemoryUsed / 1024.0;
        var nodes = result.NodeCount?.ToString() ?? "N/A";
        
        Console.WriteLine($"{result.ParserType,-15} {success,-10} {totalMs,-12:F3} {lexerMs,-12:F3} {parserMs,-12:F3} {memoryKb,-12:F1} {nodes,-8} {result.ErrorCount,-8}");
    }

    public static void WriteBulkResults(List<ComparisonResult> results)
    {
        var totalFiles = results.Count;
        var antlrSuccesses = results.Count(r => r.AntlrResult.Success);
        var selfHostedSuccesses = results.Count(r => r.SelfHostedResult.Success);
        
        Console.WriteLine("Bulk Parsing Results:");
        Console.WriteLine($"  Total Files: {totalFiles}");
        Console.WriteLine($"  ANTLR Success Rate: {antlrSuccesses}/{totalFiles} ({(double)antlrSuccesses/totalFiles*100:F1}%)");
        Console.WriteLine($"  Self-Hosted Success Rate: {selfHostedSuccesses}/{totalFiles} ({(double)selfHostedSuccesses/totalFiles*100:F1}%)");
        
        var bothSuccessful = results.Where(r => r.BothSuccessful).ToList();
        if (bothSuccessful.Any())
        {
            var avgSpeedRatio = bothSuccessful.Average(r => r.AntlrSpeedRatio);
            var avgMemoryRatio = bothSuccessful.Average(r => r.MemoryRatio);
            
            Console.WriteLine();
            Console.WriteLine("Average Performance (successful parses only):");
            Console.WriteLine($"  Average Speed Ratio: {avgSpeedRatio:F2}x");
            Console.WriteLine($"  Average Memory Ratio: {avgMemoryRatio:F2}x");
        }
        
        // Show first failure if any
        var firstFailure = results.FirstOrDefault(r => !r.SelfHostedResult.Success);
        if (firstFailure != null)
        {
            Console.WriteLine();
            Console.WriteLine($"First Self-Hosted Parser Failure: {firstFailure.FilePath}");
            Console.WriteLine($"  Error: {firstFailure.SelfHostedResult.ErrorMessage}");
        }
    }

    private static string FormatFileSize(int bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public static void WriteProgress(int current, int total, string currentFile)
    {
        var percentage = (double)current / total * 100;
        var fileName = Path.GetFileName(currentFile);
        Console.Write($"\rProgress: {current}/{total} ({percentage:F1}%) - {fileName}".PadRight(80));
    }
}