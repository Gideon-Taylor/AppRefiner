using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Collections.Concurrent;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Main implementation of the type service for PeopleCode type inference and checking
/// </summary>
public class TypeService : ITypeService
{
    private readonly TypeInferenceEngine _inferenceEngine;
    private readonly List<IProgramResolver> _resolvers = new();
    private readonly ConcurrentDictionary<ProgramNode, TypeInferenceResult> _resultCache = new();
    private readonly object _lock = new();
    private TypeInferenceStatistics _statistics = new();

    /// <summary>
    /// Current type inference mode
    /// </summary>
    public TypeInferenceMode Mode { get; set; } = TypeInferenceMode.Disabled;

    /// <summary>
    /// Whether type checking is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    public TypeService() : this(new TypeInferenceEngine())
    {
    }

    public TypeService(TypeInferenceEngine inferenceEngine)
    {
        _inferenceEngine = inferenceEngine ?? throw new ArgumentNullException(nameof(inferenceEngine));
    }

    /// <summary>
    /// Performs type inference on a program AST
    /// </summary>
    public async Task<TypeInferenceResult> InferTypesAsync(ProgramNode program, TypeInferenceOptions? options = null)
    {
        if (program == null)
            throw new ArgumentNullException(nameof(program));

        if (!IsEnabled || Mode == TypeInferenceMode.Disabled)
        {
            return TypeInferenceResult.Empty;
        }

        // Set up options
        options ??= new TypeInferenceOptions { Mode = Mode };
        if (options.Mode == TypeInferenceMode.Disabled)
            options.Mode = Mode;

        // Check cache if enabled
        if (options.UseCaching && _resultCache.TryGetValue(program, out var cachedResult))
        {
            lock (_lock)
            {
                _statistics.CacheHits++;
            }
            return cachedResult;
        }

        var startTime = DateTime.UtcNow;

        try
        {
            // Get appropriate program resolver
            var resolver = options.Mode == TypeInferenceMode.Thorough ? GetPrimaryResolver() : null;

            // Perform type inference
            var result = await _inferenceEngine.InferTypesAsync(program, options.Mode, resolver, options);

            // Update statistics
            lock (_lock)
            {
                _statistics.InferenceOperations++;
                _statistics.TypesInferred += result.TypesInferred;
                _statistics.UnresolvedTypes += result.UnresolvedTypes;
                _statistics.TotalInferenceTime = _statistics.TotalInferenceTime.Add(result.AnalysisTime);
                _statistics.TotalErrors += result.Errors.Count;
                _statistics.TotalWarnings += result.Warnings.Count;
                _statistics.ExternalResolutions += result.ExternalProgramsResolved;
            }

            // Cache result if successful and caching is enabled
            if (options.UseCaching && result.Success)
            {
                _resultCache.TryAdd(program, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            return TypeInferenceResult.Failed(program, options.Mode, $"Type inference failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the inferred type information for a specific AST node
    /// </summary>
    public TypeInfo? GetTypeInfo(AstNode node)
    {
        return node?.GetInferredType();
        //return null;
    }

    /// <summary>
    /// Checks if two types are compatible for assignment
    /// </summary>
    public bool IsTypeCompatible(TypeInfo source, TypeInfo target)
    {
        if (source == null || target == null)
            return false;

        return target.IsAssignableFrom(source);
    }

    /// <summary>
    /// Gets the most specific common type between two types
    /// </summary>
    public TypeInfo GetCommonType(TypeInfo type1, TypeInfo type2)
    {
        if (type1 == null || type2 == null)
            return AnyTypeInfo.Instance;

        return type1.GetCommonType(type2);
    }

    /// <summary>
    /// Registers a program resolver for thorough mode analysis
    /// </summary>
    public void RegisterProgramResolver(IProgramResolver resolver)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));

        lock (_lock)
        {
            if (!_resolvers.Contains(resolver))
            {
                _resolvers.Add(resolver);
            }
        }
    }

    /// <summary>
    /// Gets the currently registered program resolver (first one if multiple)
    /// </summary>
    public IProgramResolver? GetProgramResolver()
    {
        return GetPrimaryResolver();
    }

    /// <summary>
    /// Clears all cached type information and analysis results
    /// </summary>
    public void ClearCache()
    {
        _resultCache.Clear();

        lock (_lock)
        {
            foreach (var resolver in _resolvers)
            {
                resolver.ClearCache();
            }
        }
    }

    /// <summary>
    /// Gets statistics about type inference performance
    /// </summary>
    public TypeInferenceStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new TypeInferenceStatistics
            {
                InferenceOperations = _statistics.InferenceOperations,
                TypesInferred = _statistics.TypesInferred,
                UnresolvedTypes = _statistics.UnresolvedTypes,
                TotalInferenceTime = _statistics.TotalInferenceTime,
                TotalErrors = _statistics.TotalErrors,
                TotalWarnings = _statistics.TotalWarnings,
                CacheHits = _statistics.CacheHits,
                ExternalResolutions = _statistics.ExternalResolutions
            };
        }
    }

    /// <summary>
    /// Gets the primary program resolver
    /// </summary>
    private IProgramResolver? GetPrimaryResolver()
    {
        lock (_lock)
        {
            return _resolvers.FirstOrDefault();
        }
    }

    /// <summary>
    /// Removes a program resolver
    /// </summary>
    public void UnregisterProgramResolver(IProgramResolver resolver)
    {
        if (resolver == null) return;

        lock (_lock)
        {
            _resolvers.Remove(resolver);
        }
    }

    /// <summary>
    /// Gets all registered program resolvers
    /// </summary>
    public IReadOnlyList<IProgramResolver> GetProgramResolvers()
    {
        lock (_lock)
        {
            return _resolvers.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Enables type checking with the specified mode
    /// </summary>
    public void Enable(TypeInferenceMode mode = TypeInferenceMode.Quick)
    {
        IsEnabled = true;
        Mode = mode;
    }

    /// <summary>
    /// Disables type checking
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        Mode = TypeInferenceMode.Disabled;
    }

    /// <summary>
    /// Resets all statistics
    /// </summary>
    public void ResetStatistics()
    {
        lock (_lock)
        {
            _statistics = new TypeInferenceStatistics();
        }
    }

    /// <summary>
    /// Validates a program's type correctness without storing results
    /// Useful for quick validation without affecting caches
    /// </summary>
    public async Task<bool> ValidateProgramAsync(ProgramNode program, TypeInferenceMode mode = TypeInferenceMode.Quick)
    {
        var options = new TypeInferenceOptions
        {
            Mode = mode,
            UseCaching = false, // Don't cache validation results
            IncludeDetailedErrors = false,
            IncludeWarnings = false
        };

        var result = await InferTypesAsync(program, options);
        return result.Success && result.Errors.Count == 0;
    }

    /// <summary>
    /// Gets a summary of type information for a program
    /// </summary>
    public async Task<string> GetProgramTypeSummaryAsync(ProgramNode program)
    {
        var result = await InferTypesAsync(program);

        if (!result.Success)
        {
            return $"Type analysis failed: {string.Join(", ", result.Errors.Take(3).Select(e => e.Message))}";
        }

        var summary = new List<string>
        {
            $"{result.TypesInferred}/{result.NodesAnalyzed} nodes typed ({result.SuccessRate:F1}%)"
        };

        if (result.Errors.Count > 0)
        {
            summary.Add($"{result.Errors.Count} errors");
        }

        if (result.Warnings.Count > 0)
        {
            summary.Add($"{result.Warnings.Count} warnings");
        }

        if (result.Mode == TypeInferenceMode.Thorough && result.ExternalProgramsResolved > 0)
        {
            summary.Add($"{result.ExternalProgramsResolved} external programs resolved");
        }

        return string.Join(" | ", summary);
    }

    /// <summary>
    /// Disposes of resources
    /// </summary>
    public void Dispose()
    {
        _resultCache.Clear();
        lock (_lock)
        {
            _resolvers.Clear();
        }
    }
}
