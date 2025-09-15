using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Result of a type inference operation, containing analysis results and any issues found
/// </summary>
public class TypeInferenceResult
{
    /// <summary>
    /// Whether the type inference operation completed successfully
    /// True if no critical errors prevented analysis, false if analysis was aborted
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// The mode that was used for this type inference operation
    /// </summary>
    public TypeInferenceMode Mode { get; set; }

    /// <summary>
    /// The program that was analyzed
    /// </summary>
    public ProgramNode Program { get; set; }

    /// <summary>
    /// Total number of AST nodes that were analyzed
    /// </summary>
    public int NodesAnalyzed { get; set; }

    /// <summary>
    /// Number of nodes that had types successfully inferred
    /// </summary>
    public int TypesInferred { get; set; }

    /// <summary>
    /// Number of nodes where type inference failed or returned Unknown
    /// </summary>
    public int UnresolvedTypes { get; set; }

    /// <summary>
    /// Type errors found during analysis
    /// </summary>
    public List<TypeError> Errors { get; set; } = new();

    /// <summary>
    /// Type warnings found during analysis
    /// </summary>
    public List<TypeWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Time taken to perform the type inference
    /// </summary>
    public TimeSpan AnalysisTime { get; set; }

    /// <summary>
    /// Number of external programs that were resolved during thorough mode analysis
    /// </summary>
    public int ExternalProgramsResolved { get; set; }

    /// <summary>
    /// Number of cache hits during the analysis
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Whether the analysis was terminated due to a timeout
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Additional context or debug information about the analysis
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Gets the success rate as a percentage of nodes that were successfully typed
    /// </summary>
    public double SuccessRate =>
        NodesAnalyzed > 0 ? (double)TypesInferred / NodesAnalyzed * 100 : 0;

    /// <summary>
    /// Gets whether any issues (errors or warnings) were found
    /// </summary>
    public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

    /// <summary>
    /// Gets whether any errors were found (not including warnings)
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Gets whether any warnings were found
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Empty result for when type checking is disabled
    /// </summary>
    public static readonly TypeInferenceResult Empty = new()
    {
        Success = true,
        Mode = TypeInferenceMode.Disabled,
        NodesAnalyzed = 0,
        TypesInferred = 0,
        UnresolvedTypes = 0,
        AnalysisTime = TimeSpan.Zero
    };

    /// <summary>
    /// Creates a failed result for when analysis could not be completed
    /// </summary>
    public static TypeInferenceResult Failed(ProgramNode program, TypeInferenceMode mode, string reason)
    {
        return new TypeInferenceResult
        {
            Success = false,
            Mode = mode,
            Program = program,
            Errors = { new TypeError(reason, program.SourceSpan, TypeErrorKind.General) }
        };
    }

    /// <summary>
    /// Creates a timeout result
    /// </summary>
    public static TypeInferenceResult TimedOutResult(ProgramNode program, TypeInferenceMode mode, TimeSpan elapsed)
    {
        return new TypeInferenceResult
        {
            Success = false,
            Mode = mode,
            Program = program,
            TimedOut = true,
            AnalysisTime = elapsed,
            Errors = { new TypeError($"Type inference timed out after {elapsed.TotalSeconds:F1} seconds",
                                   program.SourceSpan, TypeErrorKind.General) }
        };
    }

    public TypeInferenceResult()
    {
        // Default constructor for normal initialization
    }

    public TypeInferenceResult(ProgramNode program)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
    }

    /// <summary>
    /// Gets all issues (errors and warnings) found during analysis
    /// </summary>
    public IEnumerable<object> GetAllIssues()
    {
        return Errors.Cast<object>().Concat(Warnings.Cast<object>());
    }

    /// <summary>
    /// Gets a summary string of the analysis results
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>
        {
            $"Mode: {Mode}",
            $"Success: {Success}",
            $"Nodes: {NodesAnalyzed} analyzed, {TypesInferred} typed ({SuccessRate:F1}%)",
            $"Issues: {Errors.Count} errors, {Warnings.Count} warnings",
            $"Time: {AnalysisTime.TotalMilliseconds:F1}ms"
        };

        if (Mode == TypeInferenceMode.Thorough && ExternalProgramsResolved > 0)
        {
            parts.Add($"External programs: {ExternalProgramsResolved}");
        }

        if (CacheHits > 0)
        {
            parts.Add($"Cache hits: {CacheHits}");
        }

        if (TimedOut)
        {
            parts.Add("TIMED OUT");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Gets a detailed report of all errors and warnings
    /// </summary>
    public string GetDetailedReport()
    {
        var report = new List<string>
        {
            $"Type Inference Report - {GetSummary()}",
            ""
        };

        if (Errors.Count > 0)
        {
            report.Add("ERRORS:");
            foreach (var error in Errors.OrderBy(e => e.Location.Start.Line).ThenBy(e => e.Location.Start.Column))
            {
                report.Add($"  {error}");
            }
            report.Add("");
        }

        if (Warnings.Count > 0)
        {
            report.Add("WARNINGS:");
            foreach (var warning in Warnings.OrderBy(w => w.Location.Start.Line).ThenBy(w => w.Location.Start.Column))
            {
                report.Add($"  {warning}");
            }
            report.Add("");
        }

        if (!HasIssues)
        {
            report.Add("No issues found.");
        }

        return string.Join(Environment.NewLine, report);
    }

    public override string ToString()
    {
        return GetSummary();
    }
}