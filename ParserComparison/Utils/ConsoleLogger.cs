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

    public static void WritePeriodicStatus(List<ComparisonResult> results, int currentIndex, int totalFiles)
    {
        Console.WriteLine(); // Clear the progress line
        Console.WriteLine();
        
        var processedFiles = results.Count;
        var antlrSuccesses = results.Count(r => r.AntlrResult.Success);
        var selfHostedSuccesses = results.Count(r => r.SelfHostedResult.Success);
        var percentage = (double)currentIndex / totalFiles * 100;
        
        Console.WriteLine($"--- Progress Update ({currentIndex:N0}/{totalFiles:N0} - {percentage:F1}%) ---");
        Console.WriteLine($"Files Processed: {processedFiles:N0}");
        Console.WriteLine($"ANTLR Success Rate: {antlrSuccesses:N0}/{processedFiles:N0} ({(processedFiles > 0 ? (double)antlrSuccesses/processedFiles*100 : 0):F1}%)");
        Console.WriteLine($"Self-Hosted Success Rate: {selfHostedSuccesses:N0}/{processedFiles:N0} ({(processedFiles > 0 ? (double)selfHostedSuccesses/processedFiles*100 : 0):F1}%)");
        
        var bothSuccessful = results.Where(r => r.BothSuccessful).ToList();
        if (bothSuccessful.Any())
        {
            var avgSpeedRatio = bothSuccessful.Average(r => r.AntlrSpeedRatio);
            var avgMemoryRatio = bothSuccessful.Average(r => r.MemoryRatio);
            
            Console.WriteLine($"Average Speed Ratio (ANTLR/Self-Hosted): {avgSpeedRatio:F2}x");
            Console.WriteLine($"Average Memory Ratio (ANTLR/Self-Hosted): {avgMemoryRatio:F2}x");
            
            var winner = avgSpeedRatio > 1 ? "Self-Hosted" : "ANTLR";
            var speedImprovement = Math.Max(avgSpeedRatio, 1.0 / avgSpeedRatio);
            Console.WriteLine($"Current Winner: {winner} is {speedImprovement:F2}x faster on average");
        }
        
        // Show latest failure if any
        var latestFailure = results.LastOrDefault(r => !r.SelfHostedResult.Success);
        if (latestFailure != null)
        {
            Console.WriteLine($"Latest Self-Hosted Parser Failure: {Path.GetFileName(latestFailure.FilePath)}");
        }
        
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
    }

    public static void WriteSelfHostedOnlyPeriodicStatus(List<SelfHostedOnlyResult> results, int currentIndex, int totalFiles, int failedCount)
    {
        Console.WriteLine(); // Clear the progress line
        Console.WriteLine();
        
        var processedFiles = results.Count;
        var successCount = results.Count(r => r.SelfHostedResult.Success);
        var percentage = (double)currentIndex / totalFiles * 100;
        
        Console.WriteLine($"--- Self-Hosted Only Progress Update ({currentIndex:N0}/{totalFiles:N0} - {percentage:F1}%) ---");
        Console.WriteLine($"Files Processed: {processedFiles:N0}");
        Console.WriteLine($"Self-Hosted Success Rate: {successCount:N0}/{processedFiles:N0} ({(processedFiles > 0 ? (double)successCount/processedFiles*100 : 0):F1}%)");
        Console.WriteLine($"Failed Files Copied: {failedCount:N0}");
        
        var successful = results.Where(r => r.SelfHostedResult.Success).ToList();
        if (successful.Any())
        {
            var avgTotalTime = successful.Average(r => r.SelfHostedResult.TotalDuration.TotalMilliseconds);
            var avgMemory = successful.Average(r => r.SelfHostedResult.MemoryUsed / 1024.0);
            
            Console.WriteLine($"Average Parse Time: {avgTotalTime:F2}ms");
            Console.WriteLine($"Average Memory Usage: {avgMemory:F1}KB");
        }
        
        // Show latest failure if any
        var latestFailure = results.LastOrDefault(r => !r.SelfHostedResult.Success);
        if (latestFailure != null)
        {
            Console.WriteLine($"Latest Parser Failure: {Path.GetFileName(latestFailure.FilePath)}");
            Console.WriteLine($"  Error: {latestFailure.SelfHostedResult.ErrorMessage}");
        }
        
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
    }

    public static void WriteSelfHostedOnlyResults(List<SelfHostedOnlyResult> results, string failedDir, int failedCount)
    {
        var totalFiles = results.Count;
        var successCount = results.Count(r => r.SelfHostedResult.Success);
        
        Console.WriteLine("Self-Hosted Only Parsing Results:");
        Console.WriteLine($"  Total Files: {totalFiles:N0}");
        Console.WriteLine($"  Success Rate: {successCount:N0}/{totalFiles:N0} ({(totalFiles > 0 ? (double)successCount/totalFiles*100 : 0):F1}%)");
        Console.WriteLine($"  Failed Files: {failedCount:N0}");
        Console.WriteLine($"  Failed Files Directory: {failedDir}");
        
        var successful = results.Where(r => r.SelfHostedResult.Success).ToList();
        if (successful.Any())
        {
            var totalParseTime = successful.Sum(r => r.SelfHostedResult.TotalDuration.TotalMilliseconds);
            var avgParseTime = successful.Average(r => r.SelfHostedResult.TotalDuration.TotalMilliseconds);
            var totalMemory = successful.Sum(r => r.SelfHostedResult.MemoryUsed / 1024.0);
            var avgMemory = successful.Average(r => r.SelfHostedResult.MemoryUsed / 1024.0);
            var totalFileSize = successful.Sum(r => r.FileSize);
            
            Console.WriteLine();
            Console.WriteLine("Performance Summary (successful parses only):");
            Console.WriteLine($"  Total Parse Time: {totalParseTime:F0}ms ({totalParseTime/1000:F1}s)");
            Console.WriteLine($"  Average Parse Time: {avgParseTime:F2}ms");
            Console.WriteLine($"  Total Memory Used: {totalMemory:F0}KB ({totalMemory/1024:F1}MB)");
            Console.WriteLine($"  Average Memory Usage: {avgMemory:F1}KB");
            Console.WriteLine($"  Total Source Code: {FormatFileSize(totalFileSize)}");
            Console.WriteLine($"  Parse Speed: {totalFileSize/totalParseTime*1000:F0} bytes/second");

            if (successful.Any(r => r.SelfHostedResult.NodeCount.HasValue))
            {
                var withNodeCounts = successful.Where(r => r.SelfHostedResult.NodeCount.HasValue).ToList();
                var totalNodes = withNodeCounts.Sum(r => r.SelfHostedResult.NodeCount!.Value);
                var avgNodes = withNodeCounts.Average(r => r.SelfHostedResult.NodeCount!.Value);
                Console.WriteLine($"  Total AST Nodes: {totalNodes:N0}");
                Console.WriteLine($"  Average AST Nodes: {avgNodes:F0}");
            }
        }
        
        // Show first few failures
        var failures = results.Where(r => !r.SelfHostedResult.Success).Take(5).ToList();
        if (failures.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Sample Parse Failures:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  {Path.GetFileName(failure.FilePath)}: {failure.SelfHostedResult.ErrorMessage}");
            }
            
            if (failedCount > 5)
            {
                Console.WriteLine($"  ... and {failedCount - 5} more failures (see failed directory for all files)");
            }
        }
    }

    public static void WriteAntlrOnlyPeriodicStatus(List<AntlrOnlyResult> results, int currentIndex, int totalFiles, int failedCount)
    {
        Console.WriteLine(); // Clear the progress line
        Console.WriteLine();
        
        var processedFiles = results.Count;
        var successCount = results.Count(r => r.AntlrResult.Success);
        var percentage = (double)currentIndex / totalFiles * 100;
        
        Console.WriteLine($"--- ANTLR Only Progress Update ({currentIndex:N0}/{totalFiles:N0} - {percentage:F1}%) ---");
        Console.WriteLine($"Files Processed: {processedFiles:N0}");
        Console.WriteLine($"ANTLR Success Rate: {successCount:N0}/{processedFiles:N0} ({(processedFiles > 0 ? (double)successCount/processedFiles*100 : 0):F1}%)");
        Console.WriteLine($"Failed Files Copied: {failedCount:N0}");
        
        var successful = results.Where(r => r.AntlrResult.Success).ToList();
        if (successful.Any())
        {
            var avgTotalTime = successful.Average(r => r.AntlrResult.TotalDuration.TotalMilliseconds);
            var avgMemory = successful.Average(r => r.AntlrResult.MemoryUsed / 1024.0);
            
            Console.WriteLine($"Average Parse Time: {avgTotalTime:F2}ms");
            Console.WriteLine($"Average Memory Usage: {avgMemory:F1}KB");
        }
        
        // Show latest failure if any
        var latestFailure = results.LastOrDefault(r => !r.AntlrResult.Success);
        if (latestFailure != null)
        {
            Console.WriteLine($"Latest Parser Failure: {Path.GetFileName(latestFailure.FilePath)}");
            Console.WriteLine($"  Error: {latestFailure.AntlrResult.ErrorMessage}");
        }
        
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
    }

    public static void WriteAntlrOnlyResults(List<AntlrOnlyResult> results, string failedDir, int failedCount)
    {
        var totalFiles = results.Count;
        var successCount = results.Count(r => r.AntlrResult.Success);
        
        Console.WriteLine("ANTLR Only Parsing Results:");
        Console.WriteLine($"  Total Files: {totalFiles:N0}");
        Console.WriteLine($"  Success Rate: {successCount:N0}/{totalFiles:N0} ({(totalFiles > 0 ? (double)successCount/totalFiles*100 : 0):F1}%)");
        Console.WriteLine($"  Failed Files: {failedCount:N0}");
        Console.WriteLine($"  Failed Files Directory: {failedDir}");
        
        var successful = results.Where(r => r.AntlrResult.Success).ToList();
        if (successful.Any())
        {
            var totalParseTime = successful.Sum(r => r.AntlrResult.TotalDuration.TotalMilliseconds);
            var avgParseTime = successful.Average(r => r.AntlrResult.TotalDuration.TotalMilliseconds);
            var totalMemory = successful.Sum(r => r.AntlrResult.MemoryUsed / 1024.0);
            var avgMemory = successful.Average(r => r.AntlrResult.MemoryUsed / 1024.0);
            var totalFileSize = successful.Sum(r => r.FileSize);
            
            Console.WriteLine();
            Console.WriteLine("Performance Summary (successful parses only):");
            Console.WriteLine($"  Total Parse Time: {totalParseTime:F0}ms ({totalParseTime/1000:F1}s)");
            Console.WriteLine($"  Average Parse Time: {avgParseTime:F2}ms");
            Console.WriteLine($"  Total Memory Used: {totalMemory:F0}KB ({totalMemory/1024:F1}MB)");
            Console.WriteLine($"  Average Memory Usage: {avgMemory:F1}KB");
            Console.WriteLine($"  Total Source Code: {FormatFileSize(totalFileSize)}");
            Console.WriteLine($"  Parse Speed: {totalFileSize/totalParseTime*1000:F0} bytes/second");

            if (successful.Any(r => r.AntlrResult.NodeCount.HasValue))
            {
                var withNodeCounts = successful.Where(r => r.AntlrResult.NodeCount.HasValue).ToList();
                var totalNodes = withNodeCounts.Sum(r => r.AntlrResult.NodeCount!.Value);
                var avgNodes = withNodeCounts.Average(r => r.AntlrResult.NodeCount!.Value);
                Console.WriteLine($"  Total AST Nodes: {totalNodes:N0}");
                Console.WriteLine($"  Average AST Nodes: {avgNodes:F0}");
            }
        }
        
        // Show first few failures
        var failures = results.Where(r => !r.AntlrResult.Success).Take(5).ToList();
        if (failures.Any())
        {
            Console.WriteLine();
            Console.WriteLine("Sample Parse Failures:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  {Path.GetFileName(failure.FilePath)}: {failure.AntlrResult.ErrorMessage}");
            }
            
            if (failedCount > 5)
            {
                Console.WriteLine($"  ... and {failedCount - 5} more failures (see failed directory for all files)");
            }
        }
    }
}