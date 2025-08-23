namespace ParserComparison.Models;

public class TestConfiguration
{
    public string? SingleFilePath { get; set; }
    public string? DirectoryPath { get; set; }
    public bool StopOnFirstError { get; set; } = true;
    public bool VerboseOutput { get; set; } = false;
    public int MaxFiles { get; set; } = int.MaxValue;
    public bool IncludeMemoryAnalysis { get; set; } = true;
}