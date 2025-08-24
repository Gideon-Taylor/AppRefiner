namespace ParserComparison.Models;

public class AntlrOnlyResult
{
    public ParseResult AntlrResult { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public bool WasCopiedToFailed { get; set; }
    public string? FailedFilePath { get; set; }
}