namespace ParserComparison.Models;

public class ParseResult
{
    public bool Success { get; set; }
    public TimeSpan LexerDuration { get; set; }
    public TimeSpan ParserDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public long MemoryBefore { get; set; }
    public long MemoryAfter { get; set; }
    public long MemoryUsed => MemoryAfter - MemoryBefore;
    public int ErrorCount { get; set; }
    public TimeSpan? VisitorWalkDuration { get; set; }
    public string? ErrorMessage { get; set; }
    public string ParserType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
}