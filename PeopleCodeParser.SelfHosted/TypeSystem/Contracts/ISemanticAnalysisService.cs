using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Main interface for semantic analysis services including type inference, type checking,
/// and other semantic validation operations.
/// This is the primary interface that consuming applications should use.
/// </summary>
public interface ISemanticAnalysisService
{
    /// <summary>
    /// Gets the current semantic analysis configuration
    /// </summary>
    SemanticAnalysisConfiguration Configuration { get; }

    /// <summary>
    /// Updates the semantic analysis configuration
    /// </summary>
    /// <param name="configuration">New configuration to apply</param>
    void UpdateConfiguration(SemanticAnalysisConfiguration configuration);

    /// <summary>
    /// Performs comprehensive semantic analysis on a program including type inference
    /// </summary>
    /// <param name="program">The program to analyze</param>
    /// <param name="options">Analysis options (optional)</param>
    /// <returns>Complete semantic analysis results</returns>
    Task<SemanticAnalysisResult> AnalyzeProgramAsync(ProgramNode program, SemanticAnalysisOptions? options = null);

    /// <summary>
    /// Performs only type inference without other semantic validations (faster)
    /// </summary>
    /// <param name="program">The program to analyze</param>
    /// <param name="options">Type inference options (optional)</param>
    /// <returns>Type inference results</returns>
    Task<TypeInferenceResult> InferTypesAsync(ProgramNode program, TypeInferenceOptions? options = null);

    /// <summary>
    /// Gets the inferred type for a specific AST node from the last analysis
    /// </summary>
    /// <param name="node">The AST node to get type information for</param>
    /// <returns>The inferred type, or null if no type information is available</returns>
    TypeInfo? GetNodeType(AstNode node);

    /// <summary>
    /// Validates type compatibility for assignment operations
    /// </summary>
    /// <param name="sourceType">Type being assigned from</param>
    /// <param name="targetType">Type being assigned to</param>
    /// <returns>True if assignment is type-safe</returns>
    bool IsAssignmentValid(TypeInfo sourceType, TypeInfo targetType);

    /// <summary>
    /// Registers a program resolver for cross-program analysis
    /// </summary>
    /// <param name="resolver">Program resolver to register</param>
    void RegisterProgramResolver(IProgramResolver resolver);

    /// <summary>
    /// Registers a program source provider for external class resolution
    /// </summary>
    /// <param name="sourceProvider">Source provider to register</param>
    void RegisterProgramSourceProvider(IProgramSourceProvider sourceProvider);

    /// <summary>
    /// Clears all cached analysis results and internal state
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets performance and usage statistics
    /// </summary>
    /// <returns>Analysis statistics</returns>
    SemanticAnalysisStatistics GetStatistics();
}

/// <summary>
/// Configuration for semantic analysis operations
/// </summary>
public class SemanticAnalysisConfiguration
{
    /// <summary>
    /// Whether semantic analysis is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default type inference mode for new analyses
    /// </summary>
    public TypeInferenceMode DefaultTypeInferenceMode { get; set; } = TypeInferenceMode.Quick;

    /// <summary>
    /// Whether to include detailed error information
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;

    /// <summary>
    /// Whether to include warning messages
    /// </summary>
    public bool IncludeWarnings { get; set; } = true;

    /// <summary>
    /// Default timeout for analysis operations
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Whether to use caching for improved performance
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Whether to support incremental analysis
    /// </summary>
    public bool EnableIncrementalAnalysis { get; set; } = false;
}

/// <summary>
/// Options for semantic analysis operations
/// </summary>
public class SemanticAnalysisOptions
{
    /// <summary>
    /// Type inference options to use
    /// </summary>
    public TypeInferenceOptions? TypeInferenceOptions { get; set; }

    /// <summary>
    /// Whether to perform additional semantic validations beyond type checking
    /// </summary>
    public bool PerformExtendedValidation { get; set; } = true;

    /// <summary>
    /// Whether to analyze dead code
    /// </summary>
    public bool DetectDeadCode { get; set; } = false;

    /// <summary>
    /// Whether to analyze unused variables
    /// </summary>
    public bool DetectUnusedVariables { get; set; } = false;

    /// <summary>
    /// Custom validation rules to apply
    /// </summary>
    public IList<string> CustomValidationRules { get; set; } = new List<string>();
}

/// <summary>
/// Comprehensive results from semantic analysis
/// </summary>
public class SemanticAnalysisResult
{
    /// <summary>
    /// The analyzed program
    /// </summary>
    public ProgramNode Program { get; set; } = null!;

    /// <summary>
    /// Type inference results
    /// </summary>
    public TypeInferenceResult TypeInferenceResult { get; set; } = null!;

    /// <summary>
    /// Additional semantic errors found (beyond type errors)
    /// </summary>
    public IList<SemanticError> SemanticErrors { get; set; } = new List<SemanticError>();

    /// <summary>
    /// Semantic warnings
    /// </summary>
    public IList<SemanticWarning> SemanticWarnings { get; set; } = new List<SemanticWarning>();

    /// <summary>
    /// Whether the analysis completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Total time spent on analysis
    /// </summary>
    public TimeSpan AnalysisTime { get; set; }

    /// <summary>
    /// Whether the analysis was cancelled due to timeout
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Total number of errors (type errors + semantic errors)
    /// </summary>
    public int TotalErrors => TypeInferenceResult.Errors.Count + SemanticErrors.Count;

    /// <summary>
    /// Total number of warnings (type warnings + semantic warnings)
    /// </summary>
    public int TotalWarnings => TypeInferenceResult.Warnings.Count + SemanticWarnings.Count;
}

/// <summary>
/// Represents a semantic error beyond type checking
/// </summary>
public class SemanticError
{
    public string Message { get; set; } = string.Empty;
    public SourceSpan Location { get; set; }
    public SemanticErrorKind Kind { get; set; }
    public string RuleName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a semantic warning
/// </summary>
public class SemanticWarning
{
    public string Message { get; set; } = string.Empty;
    public SourceSpan Location { get; set; }
    public SemanticWarningKind Kind { get; set; }
    public string RuleName { get; set; } = string.Empty;
}

/// <summary>
/// Categories of semantic errors
/// </summary>
public enum SemanticErrorKind
{
    UnreachableCode,
    UnusedVariable,
    UninitializedVariable,
    InvalidOperation,
    ConstraintViolation,
    Custom
}

/// <summary>
/// Categories of semantic warnings
/// </summary>
public enum SemanticWarningKind
{
    UnusedImport,
    PotentialNullReference,
    PerformanceWarning,
    StyleViolation,
    Deprecated,
    Custom
}

/// <summary>
/// Performance and usage statistics for semantic analysis
/// </summary>
public class SemanticAnalysisStatistics
{
    /// <summary>
    /// Number of semantic analysis operations performed
    /// </summary>
    public int AnalysisOperations { get; set; }

    /// <summary>
    /// Type inference statistics
    /// </summary>
    public TypeInferenceStatistics TypeInferenceStats { get; set; } = new();

    /// <summary>
    /// Total time spent on all semantic analysis operations
    /// </summary>
    public TimeSpan TotalAnalysisTime { get; set; }

    /// <summary>
    /// Number of semantic errors found (excluding type errors)
    /// </summary>
    public int SemanticErrors { get; set; }

    /// <summary>
    /// Number of semantic warnings found (excluding type warnings)
    /// </summary>
    public int SemanticWarnings { get; set; }

    /// <summary>
    /// Average time per analysis operation
    /// </summary>
    public TimeSpan AverageAnalysisTime =>
        AnalysisOperations > 0 ? TimeSpan.FromTicks(TotalAnalysisTime.Ticks / AnalysisOperations) : TimeSpan.Zero;

    public override string ToString()
    {
        return $"Semantic Analysis: {AnalysisOperations} ops, avg {AverageAnalysisTime.TotalMilliseconds:F1}ms | " +
               $"Issues: {SemanticErrors} errors, {SemanticWarnings} warnings | " +
               $"Type Analysis: {TypeInferenceStats}";
    }
}