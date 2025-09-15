using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Interface for resolving external PeopleCode programs and their type information
/// This enables "thorough" mode type checking that can analyze cross-program references
/// </summary>
public interface IProgramResolver
{
    /// <summary>
    /// Resolves a PeopleCode program by its name/identifier
    /// </summary>
    /// <param name="programName">The name or identifier of the program to resolve</param>
    /// <returns>The parsed program AST, or null if not found</returns>
    Task<ProgramNode?> ResolveProgramAsync(string programName);

    /// <summary>
    /// Resolves the program that contains a specific function
    /// Used for resolving function calls that might be defined in other programs
    /// </summary>
    /// <param name="functionName">The name of the function to find</param>
    /// <returns>The program containing the function, or null if not found</returns>
    Task<ProgramNode?> ResolveFunctionProgramAsync(string functionName);

    /// <summary>
    /// Resolves type information for an application class
    /// </summary>
    /// <param name="className">The fully qualified class name (e.g., "MyPackage:MyClass")</param>
    /// <returns>Type information for the class, or null if not found</returns>
    Task<AppClassTypeInfo?> ResolveClassAsync(string className);

    /// <summary>
    /// Resolves type information for an interface
    /// </summary>
    /// <param name="interfaceName">The fully qualified interface name</param>
    /// <returns>Type information for the interface, or null if not found</returns>
    Task<AppClassTypeInfo?> ResolveInterfaceAsync(string interfaceName);

    /// <summary>
    /// Checks if a program is available for resolution without actually loading it
    /// Useful for performance optimization and availability checking
    /// </summary>
    /// <param name="programName">The name of the program to check</param>
    /// <returns>True if the program can be resolved, false otherwise</returns>
    Task<bool> IsProgramAvailableAsync(string programName);

    /// <summary>
    /// Clears any internal caching to free memory or force fresh resolution
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Gets statistics about resolution performance and cache usage
    /// Useful for debugging and monitoring
    /// </summary>
    ResolutionStatistics GetStatistics();
}

/// <summary>
/// Statistics about program resolution performance
/// </summary>
public class ResolutionStatistics
{
    /// <summary>
    /// Number of successful program resolutions
    /// </summary>
    public int SuccessfulResolutions { get; set; }

    /// <summary>
    /// Number of failed resolution attempts
    /// </summary>
    public int FailedResolutions { get; set; }

    /// <summary>
    /// Number of cached hits (resolved from cache rather than source)
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Number of programs currently in cache
    /// </summary>
    public int CachedPrograms { get; set; }

    /// <summary>
    /// Total time spent on resolution operations
    /// </summary>
    public TimeSpan TotalResolutionTime { get; set; }

    /// <summary>
    /// Average time per resolution operation
    /// </summary>
    public TimeSpan AverageResolutionTime =>
        SuccessfulResolutions > 0 ? TimeSpan.FromTicks(TotalResolutionTime.Ticks / SuccessfulResolutions) : TimeSpan.Zero;

    /// <summary>
    /// Cache hit ratio as a percentage
    /// </summary>
    public double CacheHitRatio =>
        (SuccessfulResolutions + CacheHits) > 0 ? (double)CacheHits / (SuccessfulResolutions + CacheHits) * 100 : 0;

    public override string ToString()
    {
        return $"Resolutions: {SuccessfulResolutions} successful, {FailedResolutions} failed | " +
               $"Cache: {CachedPrograms} programs, {CacheHitRatio:F1}% hit ratio | " +
               $"Avg time: {AverageResolutionTime.TotalMilliseconds:F1}ms";
    }
}