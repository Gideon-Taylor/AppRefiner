using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Modes for type inference operations
/// </summary>
public enum TypeInferenceMode
{
    /// <summary>
    /// Type checking is disabled - no type inference is performed
    /// </summary>
    Disabled,

    /// <summary>
    /// Quick mode - only analyze types within the current program
    /// Fast but limited to local context
    /// </summary>
    Quick,

    /// <summary>
    /// Thorough mode - analyze types across program boundaries
    /// Slower but provides complete type information including cross-program references
    /// </summary>
    Thorough
}

/// <summary>
/// Options for controlling type inference behavior
/// </summary>
public class TypeInferenceOptions
{
    /// <summary>
    /// The inference mode to use
    /// </summary>
    public TypeInferenceMode Mode { get; set; } = TypeInferenceMode.Quick;

    /// <summary>
    /// Whether to include detailed error messages and location information
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;

    /// <summary>
    /// Whether to collect type warnings in addition to errors
    /// </summary>
    public bool IncludeWarnings { get; set; } = true;

    /// <summary>
    /// Maximum time to spend on type inference before timing out
    /// Useful for preventing hangs during thorough mode analysis
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether to use cached results from previous type inference runs
    /// </summary>
    public bool UseCaching { get; set; } = true;

    /// <summary>
    /// Whether to perform incremental analysis (only re-analyze changed nodes)
    /// </summary>
    public bool IncrementalAnalysis { get; set; } = false;

    /// <summary>
    /// When true, unknown types are treated permissively (similar to 'any').
    /// Type mismatches involving unknown types will not be reported as errors.
    /// Instead, they may be reported as warnings if <see cref="IncludeWarnings"/> is true.
    ///
    /// This is useful for IDE scenarios where incomplete type resolution (e.g., unimplemented
    /// built-in functions) shouldn't block users with false positive errors.
    ///
    /// When false (default), unknown types cause strict errors, which helps identify
    /// gaps in the type system implementation during testing.
    /// </summary>
    public bool TreatUnknownAsAny { get; set; } = false;

    /// <summary>
    /// Optional provider that can supply PeopleCode program source when class metadata needs to be resolved.
    /// </summary>
    public IProgramSourceProvider? ProgramSourceProvider { get; set; }
}

/// <summary>
/// Main interface for type inference and type checking services
/// </summary>
public interface ITypeService
{
    /// <summary>
    /// Current type inference mode
    /// </summary>
    TypeInferenceMode Mode { get; set; }

    /// <summary>
    /// Whether type checking is enabled
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Performs type inference on a program AST
    /// </summary>
    /// <param name="program">The program to analyze</param>
    /// <param name="options">Options controlling the analysis behavior</param>
    /// <returns>Results of the type inference including any errors found</returns>
    Task<TypeInferenceResult> InferTypesAsync(ProgramNode program, TypeInferenceOptions? options = null);

    /// <summary>
    /// Gets the inferred type information for a specific AST node
    /// </summary>
    /// <param name="node">The AST node to get type information for</param>
    /// <returns>The type information, or null if no type has been inferred</returns>
    TypeInfo? GetTypeInfo(AstNode node);

    /// <summary>
    /// Checks if two types are compatible for assignment
    /// </summary>
    /// <param name="source">The source type being assigned</param>
    /// <param name="target">The target type being assigned to</param>
    /// <returns>True if the assignment is valid, false otherwise</returns>
    bool IsTypeCompatible(TypeInfo source, TypeInfo target);

    /// <summary>
    /// Gets the most specific common type between two types
    /// </summary>
    /// <param name="type1">First type</param>
    /// <param name="type2">Second type</param>
    /// <returns>The most specific common type, or Any if no common type exists</returns>
    TypeInfo GetCommonType(TypeInfo type1, TypeInfo type2);

    /// <summary>
    /// Registers a program resolver for thorough mode analysis
    /// </summary>
    /// <param name="resolver">The resolver to register</param>
    void RegisterProgramResolver(IProgramResolver resolver);

    /// <summary>
    /// Gets the currently registered program resolver
    /// </summary>
    /// <returns>The program resolver, or null if none is registered</returns>
    IProgramResolver? GetProgramResolver();

    /// <summary>
    /// Clears all cached type information and analysis results
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets statistics about type inference performance
    /// </summary>
    /// <returns>Performance and usage statistics</returns>
    TypeInferenceStatistics GetStatistics();
}

/// <summary>
/// Statistics about type inference performance and results
/// </summary>
public class TypeInferenceStatistics
{
    /// <summary>
    /// Number of type inference operations performed
    /// </summary>
    public int InferenceOperations { get; set; }

    /// <summary>
    /// Number of nodes that had types successfully inferred
    /// </summary>
    public int TypesInferred { get; set; }

    /// <summary>
    /// Number of nodes that could not be typed
    /// </summary>
    public int UnresolvedTypes { get; set; }

    /// <summary>
    /// Total time spent on type inference
    /// </summary>
    public TimeSpan TotalInferenceTime { get; set; }

    /// <summary>
    /// Number of type errors found across all operations
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Number of type warnings found across all operations
    /// </summary>
    public int TotalWarnings { get; set; }

    /// <summary>
    /// Number of cache hits during analysis
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Number of external program resolutions performed
    /// </summary>
    public int ExternalResolutions { get; set; }

    /// <summary>
    /// Average time per type inference operation
    /// </summary>
    public TimeSpan AverageInferenceTime =>
        InferenceOperations > 0 ? TimeSpan.FromTicks(TotalInferenceTime.Ticks / InferenceOperations) : TimeSpan.Zero;

    /// <summary>
    /// Percentage of nodes that were successfully typed
    /// </summary>
    public double SuccessRate =>
        (TypesInferred + UnresolvedTypes) > 0 ? (double)TypesInferred / (TypesInferred + UnresolvedTypes) * 100 : 0;

    public override string ToString()
    {
        return $"Inference: {InferenceOperations} ops, {SuccessRate:F1}% success rate | " +
               $"Results: {TypesInferred} typed, {UnresolvedTypes} unresolved | " +
               $"Issues: {TotalErrors} errors, {TotalWarnings} warnings | " +
               $"Avg time: {AverageInferenceTime.TotalMilliseconds:F1}ms";
    }
}
