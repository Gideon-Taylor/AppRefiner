namespace ParserComparison.Models;

public class SelfHostedOnlyResult
{
    public ParseResult SelfHostedResult { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public bool WasCopiedToFailed { get; set; }
    public string? FailedFilePath { get; set; }
}