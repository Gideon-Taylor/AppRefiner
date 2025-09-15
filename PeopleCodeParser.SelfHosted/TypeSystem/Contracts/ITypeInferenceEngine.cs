using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Core interface for type inference engine operations.
/// This interface represents the foundational type inference capabilities,
/// separate from higher-level type service operations.
/// </summary>
public interface ITypeInferenceEngine
{
    /// <summary>
    /// Performs type inference on a program AST
    /// </summary>
    /// <param name="program">The program to analyze</param>
    /// <param name="mode">The inference mode to use</param>
    /// <param name="resolver">Program resolver for thorough mode (optional)</param>
    /// <param name="options">Analysis options (optional)</param>
    /// <param name="cancellationToken">Cancellation token for timeout support</param>
    /// <returns>The results of the type inference operation</returns>
    Task<TypeInferenceResult> InferTypesAsync(
        ProgramNode program,
        TypeInferenceMode mode,
        IProgramResolver? resolver = null,
        TypeInferenceOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the type compatibility between two types for assignment operations
    /// </summary>
    /// <param name="sourceType">The type being assigned from</param>
    /// <param name="targetType">The type being assigned to</param>
    /// <returns>True if the assignment is type-safe, false otherwise</returns>
    bool ValidateTypeCompatibility(TypeInfo sourceType, TypeInfo targetType);

    /// <summary>
    /// Determines the most specific common type between two types
    /// This is useful for scenarios like conditional expressions where both branches must have a common type
    /// </summary>
    /// <param name="type1">First type to compare</param>
    /// <param name="type2">Second type to compare</param>
    /// <returns>The most specific common type, or Any if no common type exists</returns>
    TypeInfo ResolveCommonType(TypeInfo type1, TypeInfo type2);

    /// <summary>
    /// Infers the type of a literal value or expression without full program context
    /// This is useful for lightweight type inference scenarios
    /// </summary>
    /// <param name="node">The AST node to infer the type for</param>
    /// <returns>The inferred type, or Unknown if inference is not possible</returns>
    TypeInfo InferNodeType(AstNode node);

    /// <summary>
    /// Clears any internal caching or state to ensure fresh analysis
    /// </summary>
    void Reset();
}

/// <summary>
/// Extended interface for type inference engines that support external program resolution
/// </summary>
public interface IExtendedTypeInferenceEngine : ITypeInferenceEngine
{
    /// <summary>
    /// Resolves type information for a class or interface from external program sources
    /// </summary>
    /// <param name="qualifiedName">The fully qualified name of the class/interface</param>
    /// <param name="sourceProvider">Provider for accessing external program sources</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The resolved class type information, or null if not found</returns>
    Task<ClassTypeInfo?> ResolveExternalTypeAsync(
        string qualifiedName,
        IProgramSourceProvider sourceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs incremental type inference, only re-analyzing nodes that have changed
    /// </summary>
    /// <param name="program">The program to analyze</param>
    /// <param name="changedNodes">Set of nodes that have changed since last analysis</param>
    /// <param name="previousResult">Previous analysis result to build upon</param>
    /// <param name="options">Analysis options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated type inference results</returns>
    Task<TypeInferenceResult> InferTypesIncrementalAsync(
        ProgramNode program,
        ISet<AstNode> changedNodes,
        TypeInferenceResult previousResult,
        TypeInferenceOptions? options = null,
        CancellationToken cancellationToken = default);
}