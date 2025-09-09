using System.Diagnostics;

namespace ParserComparison.Utils;

public class PerformanceMonitor
{
    private Stopwatch? _stopwatch;
    public long MemoryBefore { get; private set; }
    public long MemoryAfter { get; private set; }

    public void StartMonitoring()
    {
        // Force garbage collection to get a clean memory reading
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        MemoryBefore = GC.GetTotalMemory(false);
        _stopwatch = Stopwatch.StartNew();
    }

    public Stopwatch StopMonitoring()
    {
        _stopwatch?.Stop();
        
        // Get memory after parsing
        MemoryAfter = GC.GetTotalMemory(false);
        
        return _stopwatch ?? throw new InvalidOperationException("Monitoring was not started");
    }
}