using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Provides factory methods for creating TypeSystem services
/// This approach avoids dependency on Microsoft.Extensions.DependencyInjection
/// which may not be available in all consuming applications
/// </summary>
public static class TypeSystemServiceFactory
{
    /// <summary>
    /// Creates a configured semantic analysis service with default settings
    /// </summary>
    /// <param name="configuration">Optional configuration for semantic analysis</param>
    /// <returns>A fully configured semantic analysis service</returns>
    public static ISemanticAnalysisService CreateSemanticAnalysisService(
        SemanticAnalysisConfiguration? configuration = null)
    {
        var config = configuration ?? new SemanticAnalysisConfiguration();
        var typeInferenceEngine = new TypeInferenceEngine();
        var typeService = new TypeService();

        return new SemanticAnalysisService(typeInferenceEngine, typeService, config);
    }

    /// <summary>
    /// Creates a type inference engine
    /// </summary>
    /// <returns>A type inference engine instance</returns>
    public static ITypeInferenceEngine CreateTypeInferenceEngine()
    {
        return new TypeInferenceEngine();
    }

    /// <summary>
    /// Creates a type service
    /// </summary>
    /// <returns>A type service instance</returns>
    public static ITypeService CreateTypeService()
    {
        return new TypeService();
    }

    /// <summary>
    /// Creates a semantic analysis service with custom dependencies
    /// </summary>
    /// <param name="typeInferenceEngine">Custom type inference engine</param>
    /// <param name="typeService">Custom type service</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Configured semantic analysis service</returns>
    public static ISemanticAnalysisService CreateSemanticAnalysisService(
        ITypeInferenceEngine typeInferenceEngine,
        ITypeService typeService,
        SemanticAnalysisConfiguration configuration)
    {
        return new SemanticAnalysisService(typeInferenceEngine, typeService, configuration);
    }
}

/// <summary>
/// Default implementation of ISemanticAnalysisService that orchestrates type inference and other semantic analysis
/// </summary>
public class SemanticAnalysisService : ISemanticAnalysisService
{
    private readonly ITypeInferenceEngine _typeInferenceEngine;
    private readonly ITypeService _typeService;
    private SemanticAnalysisConfiguration _configuration;
    private IProgramResolver? _programResolver;
    private IProgramSourceProvider? _programSourceProvider;
    private readonly SemanticAnalysisStatistics _statistics = new();

    public SemanticAnalysisService(
        ITypeInferenceEngine typeInferenceEngine,
        ITypeService typeService,
        SemanticAnalysisConfiguration configuration)
    {
        _typeInferenceEngine = typeInferenceEngine;
        _typeService = typeService;
        _configuration = configuration;
    }

    public SemanticAnalysisConfiguration Configuration => _configuration;

    public void UpdateConfiguration(SemanticAnalysisConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<SemanticAnalysisResult> AnalyzeProgramAsync(ProgramNode program, SemanticAnalysisOptions? options = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _statistics.AnalysisOperations++;

        try
        {
            // Perform type inference
            var typeOptions = options?.TypeInferenceOptions ?? new TypeInferenceOptions
            {
                Mode = _configuration.DefaultTypeInferenceMode,
                IncludeDetailedErrors = _configuration.IncludeDetailedErrors,
                IncludeWarnings = _configuration.IncludeWarnings,
                Timeout = _configuration.DefaultTimeout,
                UseCaching = _configuration.EnableCaching,
                IncrementalAnalysis = _configuration.EnableIncrementalAnalysis,
                ProgramSourceProvider = _programSourceProvider
            };

            var typeResult = await _typeInferenceEngine.InferTypesAsync(
                program,
                typeOptions.Mode,
                _programResolver,
                typeOptions);

            // Create comprehensive result
            var result = new SemanticAnalysisResult
            {
                Program = program,
                TypeInferenceResult = typeResult,
                Success = typeResult.Success,
                AnalysisTime = stopwatch.Elapsed,
                TimedOut = typeResult.TimedOut
            };

            // Perform additional semantic analysis if requested
            if (options?.PerformExtendedValidation == true)
            {
                await PerformExtendedValidation(result, options);
            }

            _statistics.TotalAnalysisTime = _statistics.TotalAnalysisTime.Add(stopwatch.Elapsed);
            _statistics.TypeInferenceStats = _typeService.GetStatistics();

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new SemanticAnalysisResult
            {
                Program = program,
                TypeInferenceResult = TypeInferenceResult.Failed(program, _configuration.DefaultTypeInferenceMode, ex.Message),
                Success = false,
                AnalysisTime = stopwatch.Elapsed,
                SemanticErrors = new List<SemanticError>
                {
                    new() { Message = $"Analysis failed: {ex.Message}", Kind = SemanticErrorKind.Custom }
                }
            };
        }
    }

    public Task<TypeInferenceResult> InferTypesAsync(ProgramNode program, TypeInferenceOptions? options = null)
    {
        return _typeService.InferTypesAsync(program, options);
    }

    public TypeInfo? GetNodeType(AstNode node)
    {
        return _typeService.GetTypeInfo(node);
    }

    public bool IsAssignmentValid(TypeInfo sourceType, TypeInfo targetType)
    {
        return _typeService.IsTypeCompatible(sourceType, targetType);
    }

    public void RegisterProgramResolver(IProgramResolver resolver)
    {
        _programResolver = resolver;
        _typeService.RegisterProgramResolver(resolver);
    }

    public void RegisterProgramSourceProvider(IProgramSourceProvider sourceProvider)
    {
        _programSourceProvider = sourceProvider;
    }

    public void ClearCache()
    {
        _typeService.ClearCache();
        _typeInferenceEngine.Reset();
    }

    public SemanticAnalysisStatistics GetStatistics()
    {
        return _statistics;
    }

    private async Task PerformExtendedValidation(SemanticAnalysisResult result, SemanticAnalysisOptions options)
    {
        // Placeholder for additional semantic validations beyond type checking
        // This could include:
        // - Dead code detection
        // - Unused variable detection
        // - Custom validation rules
        // - Performance analysis

        await Task.CompletedTask; // Remove when actual validation is implemented
    }
}

/// <summary>
/// Example usage documentation for consuming applications
/// </summary>
public static class TypeSystemUsageExamples
{
    /// <summary>
    /// Example: Basic semantic analysis
    /// </summary>
    public static async Task<SemanticAnalysisResult> PerformBasicAnalysis(ProgramNode program)
    {
        var service = TypeSystemServiceFactory.CreateSemanticAnalysisService();
        return await service.AnalyzeProgramAsync(program);
    }

    /// <summary>
    /// Example: Advanced analysis with external program resolution
    /// </summary>
    public static async Task<SemanticAnalysisResult> PerformAdvancedAnalysis(
        ProgramNode program,
        IProgramResolver programResolver,
        IProgramSourceProvider sourceProvider)
    {
        var config = new SemanticAnalysisConfiguration
        {
            DefaultTypeInferenceMode = TypeInferenceMode.Thorough,
            EnableCaching = true
        };

        var service = TypeSystemServiceFactory.CreateSemanticAnalysisService(config);
        service.RegisterProgramResolver(programResolver);
        service.RegisterProgramSourceProvider(sourceProvider);

        var options = new SemanticAnalysisOptions
        {
            PerformExtendedValidation = true,
            DetectUnusedVariables = true
        };

        return await service.AnalyzeProgramAsync(program, options);
    }

    /// <summary>
    /// Example: Type inference only
    /// </summary>
    public static async Task<TypeInferenceResult> PerformTypeInference(ProgramNode program)
    {
        var engine = TypeSystemServiceFactory.CreateTypeInferenceEngine();
        return await engine.InferTypesAsync(program, TypeInferenceMode.Quick);
    }
}