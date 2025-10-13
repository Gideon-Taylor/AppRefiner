using ParserComparison.Models;

namespace ParserComparison.Parsers;

public interface IParser
{
    string Name { get; }
    ParseResult Parse(string sourceCode, string filePath, bool skipGarbageCollection = false);
}