using ParserComparison.Models;
using ParserComparison.Parsers;
using ParserComparison.Utils;
using System.Text.RegularExpressions;

namespace ParserComparison.Tests;

public class BulkDirectoryTest
{
    public static List<ComparisonResult> RunTest(TestConfiguration config)
    {
        if (string.IsNullOrEmpty(config.DirectoryPath))
            throw new ArgumentException("Directory path is required");

        ConsoleLogger.WriteSubHeader($"Bulk Directory Test: {config.DirectoryPath}");

        if (!Directory.Exists(config.DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {config.DirectoryPath}");
        }

        var pcodeFiles = Directory.GetFiles(config.DirectoryPath, "*.pcode", SearchOption.AllDirectories)
                                 .Take(config.MaxFiles)
                                 .ToList();

        Console.WriteLine($"Found {pcodeFiles.Count} .pcode files");
        Console.WriteLine();

        var results = new List<ComparisonResult>();
        var antlrParser = new AntlrParserWrapper();
        var selfHostedParser = new SelfHostedParserWrapper();

        for (int i = 0; i < pcodeFiles.Count; i++)
        {
            var filePath = pcodeFiles[i];
            
            if (config.VerboseOutput)
            {
                ConsoleLogger.WriteProgress(i + 1, pcodeFiles.Count, filePath);
            }

            try
            {
                var sourceCode = File.ReadAllText(filePath);

                /* For now, we are going to strip out any directive peoplecode */

                var fileSize = sourceCode.Length;

                var antlrResult = antlrParser.Parse(sourceCode, filePath);
                if (antlrResult.ErrorCount > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"ANTLR failed for file: {filePath}");
                }

                var selfHostedResult = selfHostedParser.Parse(sourceCode, filePath);

                var comparison = new ComparisonResult
                {
                    AntlrResult = antlrResult,
                    SelfHostedResult = selfHostedResult,
                    FilePath = filePath,
                    FileSize = fileSize
                };

                results.Add(comparison);

                // Show periodic status update
                if ((i + 1) % config.ProgressInterval == 0)
                {
                    ConsoleLogger.WritePeriodicStatus(results, i + 1, pcodeFiles.Count);
                }

                // Stop on first self-hosted parser failure if configured
                if (config.StopOnFirstError && !selfHostedResult.Success)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine($"STOPPING: Self-hosted parser failed on file: {filePath}");
                    Console.WriteLine($"Error: {selfHostedResult.ErrorMessage}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Failed to process file {filePath}: {ex.Message}");
                
                if (config.StopOnFirstError)
                {
                    Console.WriteLine("Stopping due to file processing error.");
                    break;
                }
            }
        }

        if (config.VerboseOutput)
        {
            Console.WriteLine();
            Console.WriteLine();
        }

        ConsoleLogger.WriteBulkResults(results);

        return results;
    }
}