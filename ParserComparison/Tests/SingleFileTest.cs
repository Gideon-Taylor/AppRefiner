using ParserComparison.Models;
using ParserComparison.Parsers;
using ParserComparison.Utils;
using System.Text.RegularExpressions;

namespace ParserComparison.Tests;

public class SingleFileTest
{
    public static SelfHostedOnlyResult RunTest(string filePath, bool debugOnError = false)
    {
        ConsoleLogger.WriteSubHeader($"Single File Test: {Path.GetFileName(filePath)}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var sourceCode = File.ReadAllText(filePath);
        var fileSize = sourceCode.Length;

        Console.WriteLine($"File size: {fileSize:N0} characters");
        Console.WriteLine("Parsing with self-hosted parser...");
        Console.WriteLine();

        var selfHostedParser = new SelfHostedParserWrapper();
        var selfHostedResult = selfHostedParser.Parse(sourceCode, filePath);

        var result = new SelfHostedOnlyResult
        {
            SelfHostedResult = selfHostedResult,
            FilePath = filePath,
            FileSize = fileSize
        };

        ConsoleLogger.WriteSingleFileResult(result);

        // Interactive debugging if enabled and file failed
        if (debugOnError && !selfHostedResult.Success)
        {
            var debug = ConsoleLogger.AskForDebug(filePath, selfHostedResult.ErrorMessage ?? "Unknown error");
            if (debug)
            {
                ConsoleLogger.DebugFile(filePath, sourceCode);

                var validate = ConsoleLogger.AskForValidation();
                if (validate)
                {
                    ConsoleLogger.ValidateFix(filePath, sourceCode);
                }
            }
        }

        return result;
    }
}