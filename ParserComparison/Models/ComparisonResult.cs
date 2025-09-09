namespace ParserComparison.Models;

public class ComparisonResult
{
    public ParseResult AntlrResult { get; set; } = new();
    public ParseResult SelfHostedResult { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
    
    public double AntlrSpeedRatio => SelfHostedResult.TotalDuration.TotalMilliseconds > 0 
        ? AntlrResult.TotalDuration.TotalMilliseconds / SelfHostedResult.TotalDuration.TotalMilliseconds 
        : 0;
    
    public double MemoryRatio => SelfHostedResult.MemoryUsed > 0 
        ? (double)AntlrResult.MemoryUsed / SelfHostedResult.MemoryUsed 
        : 0;
    
    public bool BothSuccessful => AntlrResult.Success && SelfHostedResult.Success;
    public bool BothFailed => !AntlrResult.Success && !SelfHostedResult.Success;
}