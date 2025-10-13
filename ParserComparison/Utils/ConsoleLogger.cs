using ParserComparison.Models;
using ParserComparison.Parsers;
using System.Diagnostics;

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

    public static void WriteSingleFileResult(SelfHostedOnlyResult result)
    {
        Console.WriteLine($"File: {Path.GetFileName(result.FilePath)} ({FormatFileSize(result.FileSize)})");
        Console.WriteLine();

        // Results table
        Console.WriteLine($"{"Parser",-15} {"Success",-10} {"Total (ms)",-12} {"Lexer (ms)",-12} {"Parser (ms)",-12} {"Memory (KB)",-12} {"Visitor (ms)",-12} {"Errors",-8}");
        Console.WriteLine(new string('-', 95));

        WriteParseResult(result.SelfHostedResult);

        Console.WriteLine();

        // Performance metrics
        if (result.SelfHostedResult.Success)
        {
            Console.WriteLine("Performance Metrics:");
            Console.WriteLine($"  Total Parse Time: {result.SelfHostedResult.TotalDuration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Lexer Time: {result.SelfHostedResult.LexerDuration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Parser Time: {result.SelfHostedResult.ParserDuration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Memory Used: {result.SelfHostedResult.MemoryUsed / 1024.0:F1}KB");

            if (result.SelfHostedResult.VisitorWalkDuration.HasValue)
            {
                Console.WriteLine($"  Visitor Walk Time: {result.SelfHostedResult.VisitorWalkDuration.Value.TotalMilliseconds:F2}ms");
            }
        }
        else
        {
            Console.WriteLine("Parse Error:");
            Console.WriteLine($"  Error: {result.SelfHostedResult.ErrorMessage}");
        }
    }

    private static void WriteParseResult(ParseResult result)
    {
        var success = result.Success ? "✓" : "✗";
        var totalMs = result.TotalDuration.TotalMilliseconds;
        var lexerMs = result.LexerDuration.TotalMilliseconds;
        var parserMs = result.ParserDuration.TotalMilliseconds;
        var memoryKb = result.MemoryUsed / 1024.0;
        var visitorMs = result.VisitorWalkDuration?.TotalMilliseconds.ToString("F3") ?? "N/A";

        Console.WriteLine($"{result.ParserType,-15} {success,-10} {totalMs,-12:F3} {lexerMs,-12:F3} {parserMs,-12:F3} {memoryKb,-12:F1} {visitorMs,-12} {result.ErrorCount,-8}");
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

            if (successful.Any(r => r.SelfHostedResult.VisitorWalkDuration.HasValue))
            {
                var withVisitorTiming = successful.Where(r => r.SelfHostedResult.VisitorWalkDuration.HasValue).ToList();
                var avgVisitorTime = withVisitorTiming.Average(r => r.SelfHostedResult.VisitorWalkDuration!.Value.TotalMilliseconds);
                Console.WriteLine($"Average Visitor Walk Time: {avgVisitorTime:F2}ms");
            }

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

            if (successful.Any(r => r.SelfHostedResult.VisitorWalkDuration.HasValue))
            {
                var withVisitorTiming = successful.Where(r => r.SelfHostedResult.VisitorWalkDuration.HasValue).ToList();
                var totalVisitorTime = withVisitorTiming.Sum(r => r.SelfHostedResult.VisitorWalkDuration!.Value.TotalMilliseconds);
                var avgVisitorTime = withVisitorTiming.Average(r => r.SelfHostedResult.VisitorWalkDuration!.Value.TotalMilliseconds);
                Console.WriteLine($"  Total Visitor Walk Time: {totalVisitorTime:F0}ms ({totalVisitorTime/1000:F1}s)");
                Console.WriteLine($"  Average Visitor Walk Time: {avgVisitorTime:F2}ms");
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

    // Interactive Debugging Methods
    public static bool AskForDebug(string filePath, string errorMessage)
    {
        Console.WriteLine();
        Console.WriteLine($"🔍 DEBUG: File failed to parse: {Path.GetFileName(filePath)}");
        Console.WriteLine($"Error: {errorMessage}");
        Console.WriteLine();
        Console.Write("Would you like to debug this file? (y/n): ");

        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    public static bool AskForValidation()
    {
        Console.WriteLine();
        Console.Write("Would you like to validate the fix? (y/n): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    public static void DebugFile(string filePath, string sourceCode)
    {
        Console.WriteLine();
        Console.WriteLine($"🐛 Starting debugger for: {Path.GetFileName(filePath)}");
        Console.WriteLine("Attach your debugger now (Visual Studio, VS Code, etc.)");
        Console.WriteLine("The debugger will break after you press Enter...");
        Console.WriteLine();

        Console.Write("Press Enter to trigger debugger break: ");
        Console.ReadLine();

        // Trigger debugger break
        Debugger.Break();

        // Re-parse the file for debugging
        Console.WriteLine("🔄 Re-parsing file for debugging...");
        var parser = new SelfHostedParserWrapper();
        var result = parser.Parse(sourceCode, filePath);

        Console.WriteLine();
        Console.WriteLine("📊 Debug Parse Results:");
        Console.WriteLine($"Success: {(result.Success ? "✅" : "❌")}");
        Console.WriteLine($"Total Time: {result.TotalDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Errors: {result.ErrorCount}");

        if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    public static void ValidateFix(string filePath, string sourceCode)
    {
        Console.WriteLine();
        Console.WriteLine($"🔍 Validating fix for: {Path.GetFileName(filePath)}");

        // Re-parse to validate the fix
        var parser = new SelfHostedParserWrapper();
        var result = parser.Parse(sourceCode, filePath);

        Console.WriteLine();
        Console.WriteLine("📊 Validation Results:");
        Console.WriteLine($"Success: {(result.Success ? "✅" : "❌")}");
        Console.WriteLine($"Total Time: {result.TotalDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Errors: {result.ErrorCount}");

        if (result.Success)
        {
            Console.WriteLine("🎉 Fix validated successfully!");
        }
        else
        {
            Console.WriteLine("❌ Fix validation failed. File still has errors:");
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"Error: {result.ErrorMessage}");
                DebugFile(filePath, sourceCode);
            }
        }
    }
}