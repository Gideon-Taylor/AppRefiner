using ParserComparison.Models;
using ParserComparison.Parsers;
using ParserComparison.Utils;
using System.Text.RegularExpressions;

namespace ParserComparison.Tests;

public class SingleFileTest
{
    public static ComparisonResult RunTest(string filePath)
    {
        ConsoleLogger.WriteSubHeader($"Single File Test: {Path.GetFileName(filePath)}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var sourceCode = File.ReadAllText(filePath);
        var fileSize = sourceCode.Length;

        Console.WriteLine($"File size: {fileSize:N0} characters");
        Console.WriteLine("Parsing with both parsers...");
        Console.WriteLine();

        var antlrParser = new AntlrParserWrapper();
        var selfHostedParser = new SelfHostedParserWrapper();

        var antlrResult = antlrParser.Parse(sourceCode, filePath);
        var selfHostedResult = selfHostedParser.Parse(sourceCode, filePath);

        var comparison = new ComparisonResult
        {
            AntlrResult = antlrResult,
            SelfHostedResult = selfHostedResult,
            FilePath = filePath,
            FileSize = fileSize
        };

        ConsoleLogger.WriteComparisonResult(comparison);

        return comparison;
    }
}