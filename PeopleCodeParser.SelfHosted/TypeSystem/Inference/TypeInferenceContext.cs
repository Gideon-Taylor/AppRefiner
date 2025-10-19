using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Extensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Context object that holds state during type inference operations
/// </summary>
public class TypeInferenceContext
{
    /// <summary>
    /// The root program being analyzed
    /// </summary>
    public ProgramNode RootProgram { get; }

    /// <summary>
    /// The type inference mode being used
    /// </summary>
    public TypeInferenceMode Mode { get; }

    /// <summary>
    /// Program resolver for thorough mode analysis (nullable)
    /// </summary>
    public IProgramResolver? ProgramResolver { get; }

    /// <summary>
    /// Options controlling the type inference behavior
    /// </summary>
    public TypeInferenceOptions Options { get; }

    /// <summary>
    /// Global type definitions (classes, interfaces, etc.) found in the root program
    /// </summary>
    public Dictionary<string, TypeInfo> GlobalTypes { get; }

    /// <summary>
    /// Local type information per scope context
    /// Maps scope contexts to their local variable types
    /// </summary>
    public Dictionary<ScopeContext, Dictionary<string, TypeInfo>> LocalTypes { get; }

    /// <summary>
    /// Cache of resolved external programs (thorough mode only)
    /// Thread-safe for concurrent access
    /// </summary>
    public ConcurrentDictionary<string, ProgramNode> ResolvedPrograms { get; }

    /// <summary>
    /// Cache of resolved types for performance
    /// Thread-safe for concurrent access
    /// </summary>
    public ConcurrentDictionary<string, TypeInfo> TypeCache { get; }

    /// <summary>
    /// Errors found during type inference
    /// Thread-safe collection for concurrent error reporting
    /// </summary>
    public ConcurrentBag<TypeError> Errors { get; }

    /// <summary>
    /// Warnings found during type inference
    /// Thread-safe collection for concurrent warning reporting
    /// </summary>
    public ConcurrentBag<TypeWarning> Warnings { get; }

    /// <summary>
    /// Statistics tracking for the current analysis
    /// </summary>
    public TypeInferenceStats Stats { get; }

    /// <summary>
    /// Cancellation token for timeout support
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Stopwatch for tracking analysis time
    /// </summary>
    public System.Diagnostics.Stopwatch Stopwatch { get; }

    /// <summary>
    /// Optional provider capable of retrieving PeopleCode program sources for external resolution.
    /// </summary>
    public IProgramSourceProvider? ProgramSourceProvider { get; set; }

    /// <summary>
    /// Working set of nodes that have been visited to prevent infinite recursion
    /// </summary>
    public HashSet<AstNode> VisitedNodes { get; }

    /// <summary>
    /// Stack of current type resolution context for debugging
    /// </summary>
    public Stack<string> ResolutionStack { get; }

    public TypeInferenceContext(ProgramNode rootProgram, TypeInferenceMode mode,
                              TypeInferenceOptions options, IProgramResolver? programResolver = null,
                              CancellationToken cancellationToken = default)
    {
        RootProgram = rootProgram ?? throw new ArgumentNullException(nameof(rootProgram));
        Mode = mode;
        Options = options ?? new TypeInferenceOptions { Mode = mode };
        ProgramResolver = programResolver;
        CancellationToken = cancellationToken;

        GlobalTypes = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
        LocalTypes = new Dictionary<ScopeContext, Dictionary<string, TypeInfo>>();
        ResolvedPrograms = new ConcurrentDictionary<string, ProgramNode>(StringComparer.OrdinalIgnoreCase);
        TypeCache = new ConcurrentDictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
        Errors = new ConcurrentBag<TypeError>();
        Warnings = new ConcurrentBag<TypeWarning>();
        Stats = new TypeInferenceStats();
        Stopwatch = System.Diagnostics.Stopwatch.StartNew();
        VisitedNodes = new HashSet<AstNode>();
        ResolutionStack = new Stack<string>();
    }

    /// <summary>
    /// Resolves metadata for an application class, optionally fetching the program source if not already cached.
    /// </summary>
    public async Task<ClassTypeInfo?> ResolveClassInfoAsync(string qualifiedClassName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedClassName))
        {
            return null;
        }

        if (PeopleCodeTypeRegistry.TryGetClassInfo(qualifiedClassName, out var cached))
        {
            Stats.CacheHits++;
            return cached;
        }

        if (ProgramSourceProvider == null)
        {
            return null;
        }

        var (found, source) = await ProgramSourceProvider.TryGetProgramSourceAsync(qualifiedClassName).ConfigureAwait(false);
        if (!found || string.IsNullOrEmpty(source))
        {
            return null;
        }

        var program = ParseProgram(source);
        if (program == null)
        {
            return null;
        }

        if (program.AppClass != null)
        {
            var implementedInterfaceName = GetQualifiedTypeName(program.AppClass.ImplementedInterface);
            if (!string.IsNullOrWhiteSpace(implementedInterfaceName))
            {
                await ResolveClassInfoAsync(implementedInterfaceName!).ConfigureAwait(false);
            }

            var classInfo = ClassMetadataBuilder.Build(program.AppClass, qualifiedClassName);
            PeopleCodeTypeRegistry.CacheClassInfo(classInfo);

            Stats.ExternalResolutions++;
            return classInfo;
        }

        if (program.Interface != null)
        {
            var baseInterfaceName = GetQualifiedTypeName(program.Interface.BaseInterface);
            if (!string.IsNullOrWhiteSpace(baseInterfaceName))
            {
                await ResolveClassInfoAsync(baseInterfaceName!).ConfigureAwait(false);
            }

            var interfaceInfo = ClassMetadataBuilder.Build(program.Interface, qualifiedClassName);
            PeopleCodeTypeRegistry.CacheClassInfo(interfaceInfo);

            Stats.ExternalResolutions++;
            return interfaceInfo;
        }

        return null;
    }

    private static ProgramNode? ParseProgram(string source)
    {
        var lexer = new Lexing.PeopleCodeLexer(source);
        var tokens = lexer.TokenizeAll();

        if (PeopleCodeParser.ToolsRelease == null)
        {
            PeopleCodeParser.ToolsRelease = new ToolsVersion("99.99.99");
        }

        var parser = new PeopleCodeParser(tokens);
        var program = parser.ParseProgram();

        return parser.Errors.Any() ? null : program;
    }

    private static string? GetQualifiedTypeName(TypeNode? typeNode)
    {
        return typeNode switch
        {
            AppClassTypeNode appClass => appClass.QualifiedName,
            _ => typeNode?.TypeName
        };
    }

    /// <summary>
    /// Reports an error during type inference
    /// </summary>
    public void ReportError(string message, AstNode node, TypeErrorKind kind = TypeErrorKind.General,
                           TypeInfo? expectedType = null, TypeInfo? actualType = null)
    {
        if (!Options.IncludeDetailedErrors) return;

        // In lenient mode, suppress errors involving unknown types
        // This prevents false positives in IDE scenarios where the type system
        // hasn't fully resolved built-in functions yet
        if (Options.TreatUnknownAsAny &&
            (expectedType?.Kind == TypeKind.Unknown || actualType?.Kind == TypeKind.Unknown))
        {
            // Downgrade to warning instead of error
            if (Options.IncludeWarnings)
            {
                ReportWarning($"Type could not be fully determined: {message}", node);
            }
            return;
        }

        var error = new TypeError(message, node.SourceSpan, kind, expectedType, actualType);
        Errors.Add(error);
        node.AddTypeError(error);
        Stats.ErrorCount++;
    }

    /// <summary>
    /// Reports a warning during type inference
    /// </summary>
    public void ReportWarning(string message, AstNode node)
    {
        if (!Options.IncludeWarnings) return;

        var warning = new TypeWarning(message, node.SourceSpan);
        Warnings.Add(warning);
        node.AddTypeWarning(warning);
        Stats.WarningCount++;
    }

    /// <summary>
    /// Records that a type was successfully inferred for a node
    /// </summary>
    public void RecordTypeInference(AstNode node, TypeInfo type)
    {
        node.SetInferredType(type);
        Stats.TypesInferred++;

        // Cache the type if it's a named type
        if (type.Kind != TypeKind.Unknown && type.Kind != TypeKind.Any)
        {
            var cacheKey = GetTypeCacheKey(node, type);
            if (!string.IsNullOrEmpty(cacheKey))
            {
                TypeCache.TryAdd(cacheKey, type);
            }
        }
    }

    /// <summary>
    /// Records that a node was analyzed but type could not be determined
    /// </summary>
    public void RecordUnresolvedType(AstNode node, string reason = "")
    {
        node.SetInferredType(UnknownTypeInfo.Instance);
        Stats.UnresolvedTypes++;

        if (!string.IsNullOrEmpty(reason))
        {
            ReportWarning($"Could not determine type: {reason}", node);
        }
    }

    /// <summary>
    /// Gets the type for a variable in a specific scope
    /// </summary>
    public TypeInfo? GetVariableType(string variableName, ScopeContext scope)
    {
        // Check local scope first
        if (LocalTypes.TryGetValue(scope, out var localVars) &&
            localVars.TryGetValue(variableName, out var localType))
        {
            return localType;
        }

        if (variableName.StartsWith("&", StringComparison.Ordinal) &&
            localVars != null &&
            localVars.TryGetValue(variableName.TrimStart('&'), out localType))
        {
            return localType;
        }

        // Check parent scopes
        var currentScope = scope.Parent;
        while (currentScope != null)
        {
            if (LocalTypes.TryGetValue(currentScope, out var parentVars) &&
                parentVars.TryGetValue(variableName, out var parentType))
            {
                return parentType;
            }
            if (variableName.StartsWith("&", StringComparison.Ordinal) &&
                parentVars != null &&
                parentVars.TryGetValue(variableName.TrimStart('&'), out parentType))
            {
                return parentType;
            }
            currentScope = currentScope.Parent;
        }

        // Check global types
        if (GlobalTypes.TryGetValue(variableName, out var globalType))
        {
            return globalType;
        }

        if (variableName.StartsWith("&", StringComparison.Ordinal) &&
            GlobalTypes.TryGetValue(variableName.TrimStart('&'), out globalType))
        {
            return globalType;
        }

        return null;
    }

    /// <summary>
    /// Sets the type for a variable in a specific scope
    /// </summary>
    public void SetVariableType(string variableName, TypeInfo type, ScopeContext scope)
    {
        if (!LocalTypes.TryGetValue(scope, out var scopeVars))
        {
            scopeVars = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
            LocalTypes[scope] = scopeVars;
        }

        scopeVars[variableName] = type;
        if (variableName.StartsWith("&", StringComparison.Ordinal))
        {
            scopeVars[variableName.TrimStart('&')] = type;
        }
    }

    /// <summary>
    /// Adds a global type definition
    /// </summary>
    public void AddGlobalType(string name, TypeInfo type)
    {
        GlobalTypes[name] = type;
    }

    /// <summary>
    /// Checks if we're in thorough mode and have a program resolver
    /// </summary>
    public bool CanResolveExternalPrograms => Mode == TypeInferenceMode.Thorough && ProgramResolver != null;

    /// <summary>
    /// Attempts to resolve an external program
    /// </summary>
    public async Task<ProgramNode?> ResolveExternalProgramAsync(string programName)
    {
        if (!CanResolveExternalPrograms) return null;

        // Check cache first
        if (ResolvedPrograms.TryGetValue(programName, out var cachedProgram))
        {
            Stats.CacheHits++;
            return cachedProgram;
        }

        try
        {
            var program = await ProgramResolver!.ResolveProgramAsync(programName);
            if (program != null)
            {
                ResolvedPrograms.TryAdd(programName, program);
                Stats.ExternalResolutions++;
            }
            return program;
        }
        catch (Exception ex)
        {
            ReportError($"Failed to resolve external program '{programName}': {ex.Message}",
                       RootProgram, TypeErrorKind.UnresolvableReference);
            return null;
        }
    }

    /// <summary>
    /// Enters a resolution context for debugging/tracking
    /// </summary>
    public void EnterResolution(string context)
    {
        ResolutionStack.Push(context);
    }

    /// <summary>
    /// Exits the current resolution context
    /// </summary>
    public void ExitResolution()
    {
        if (ResolutionStack.Count > 0)
        {
            ResolutionStack.Pop();
        }
    }

    /// <summary>
    /// Gets the current resolution context path for debugging
    /// </summary>
    public string GetResolutionPath()
    {
        return string.Join(" -> ", ResolutionStack.Reverse());
    }

    /// <summary>
    /// Checks if the operation should be cancelled due to timeout
    /// </summary>
    public void ThrowIfCancelled()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Generates a cache key for a type inference result
    /// </summary>
    private string GetTypeCacheKey(AstNode node, TypeInfo type)
    {
        // Create a cache key based on node type and context
        return $"{node.GetType().Name}:{type.Name}:{node.SourceSpan}";
    }

    /// <summary>
    /// Creates the final result object from this context
    /// </summary>
    public TypeInferenceResult CreateResult()
    {
        Stopwatch.Stop();

        return new TypeInferenceResult(RootProgram)
        {
            Success = Errors.IsEmpty,
            Mode = Mode,
            NodesAnalyzed = Stats.NodesAnalyzed,
            TypesInferred = Stats.TypesInferred,
            UnresolvedTypes = Stats.UnresolvedTypes,
            Errors = Errors.ToList(),
            Warnings = Warnings.ToList(),
            AnalysisTime = Stopwatch.Elapsed,
            ExternalProgramsResolved = Stats.ExternalResolutions,
            CacheHits = Stats.CacheHits,
            TimedOut = CancellationToken.IsCancellationRequested
        };
    }
}

/// <summary>
/// Statistics tracking for type inference operations
/// </summary>
public class TypeInferenceStats
{
    /// <summary>
    /// Number of AST nodes analyzed
    /// </summary>
    private int _nodesAnalyzed;
    public int NodesAnalyzed => _nodesAnalyzed;

    /// <summary>
    /// Number of types successfully inferred
    /// </summary>
    public int TypesInferred { get; set; }

    /// <summary>
    /// Number of unresolved type references
    /// </summary>
    public int UnresolvedTypes { get; set; }

    /// <summary>
    /// Number of errors encountered
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warnings generated
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Number of external program resolutions performed
    /// </summary>
    public int ExternalResolutions { get; set; }

    /// <summary>
    /// Number of cache hits during analysis
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Increments the node analyzed counter
    /// </summary>
    public void IncrementNodesAnalyzed()
    {
        Interlocked.Increment(ref _nodesAnalyzed);
    }
}
