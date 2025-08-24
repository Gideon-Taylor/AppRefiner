using ParserComparison.Models;
using ParserComparison.Parsers;
using ParserComparison.Utils;
using System.Text.RegularExpressions;

namespace ParserComparison.Tests;

public class AntlrOnlyBulkTest
{
    public static List<AntlrOnlyResult> RunTest(TestConfiguration config)
    {
        if (string.IsNullOrEmpty(config.DirectoryPath))
            throw new ArgumentException("Directory path is required");

        ConsoleLogger.WriteSubHeader($"ANTLR Only Bulk Test: {config.DirectoryPath}");

        if (!Directory.Exists(config.DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {config.DirectoryPath}");
        }

        // Create failed files directory if it doesn't exist
        var failedDir = Path.Combine(Directory.GetCurrentDirectory(), config.FailedFilesDirectory ?? "failed");
        if (Directory.Exists(failedDir))
        {
            // Clear existing failed files
            var existingFiles = Directory.GetFiles(failedDir, "*.pcode");
            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }
            Console.WriteLine($"Cleared {existingFiles.Length} existing files from failed directory: {failedDir}");
        }
        else
        {
            Directory.CreateDirectory(failedDir);
            Console.WriteLine($"Created failed files directory: {failedDir}");
        }

        var pcodeFiles = Directory.GetFiles(config.DirectoryPath, "*.pcode", SearchOption.AllDirectories)
                                 .Take(config.MaxFiles)
                                 .ToList();

        Console.WriteLine($"Found {pcodeFiles.Count} .pcode files");
        Console.WriteLine($"Failed files will be copied to: {failedDir}");
        Console.WriteLine();

        var results = new List<AntlrOnlyResult>();
        var antlrParser = new AntlrParserWrapper();
        var failedCount = 0;

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

                var result = new AntlrOnlyResult
                {
                    AntlrResult = antlrResult,
                    FilePath = filePath,
                    FileSize = fileSize,
                    WasCopiedToFailed = false
                };

                // If parsing failed, copy file to failed directory
                if (!antlrResult.Success)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var failedFilePath = Path.Combine(failedDir, fileName);
                        
                        // Handle duplicate file names by appending a number
                        var counter = 1;
                        var originalFailedFilePath = failedFilePath;
                        while (File.Exists(failedFilePath))
                        {
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFailedFilePath);
                            var extension = Path.GetExtension(originalFailedFilePath);
                            failedFilePath = Path.Combine(failedDir, $"{nameWithoutExt}_{counter}{extension}");
                            counter++;
                        }

                        File.Copy(filePath, failedFilePath);
                        result.WasCopiedToFailed = true;
                        result.FailedFilePath = failedFilePath;
                        failedCount++;

                        if (config.VerboseOutput)
                        {
                            Console.WriteLine();
                            Console.WriteLine($"FAILED: Copied {fileName} to failed directory");
                        }
                    }
                    catch (Exception copyEx)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Failed to copy file {filePath} to failed directory: {copyEx.Message}");
                    }
                }

                results.Add(result);

                // Show periodic status update
                if ((i + 1) % config.ProgressInterval == 0)
                {
                    ConsoleLogger.WriteAntlrOnlyPeriodicStatus(results, i + 1, pcodeFiles.Count, failedCount);
                }

                // Stop on first ANTLR parser failure if configured
                if (config.StopOnFirstError && !antlrResult.Success)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine($"STOPPING: ANTLR parser failed on file: {filePath}");
                    Console.WriteLine($"Error: {antlrResult.ErrorMessage}");
                    Console.WriteLine($"File copied to: {result.FailedFilePath}");
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

        ConsoleLogger.WriteAntlrOnlyResults(results, failedDir, failedCount);

        return results;
    }
}