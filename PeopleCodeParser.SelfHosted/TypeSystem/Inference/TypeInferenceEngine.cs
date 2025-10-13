using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Extensions;

namespace PeopleCodeParser.SelfHosted.TypeSystem;

/// <summary>
/// Main engine for performing type inference on PeopleCode ASTs
/// Orchestrates the type inference process and manages the overall workflow
/// </summary>
public class TypeInferenceEngine : IExtendedTypeInferenceEngine
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
    public async Task<TypeInferenceResult> InferTypesAsync(
        ProgramNode program,
        TypeInferenceMode mode,
        IProgramResolver? resolver = null,
        TypeInferenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (program == null)
            throw new ArgumentNullException(nameof(program));

        // If type checking is disabled, return empty result
        if (mode == TypeInferenceMode.Disabled)
            return TypeInferenceResult.Empty;

        // Set up options with defaults
        options ??= new TypeInferenceOptions { Mode = mode };
        if (options.Mode != mode) options.Mode = mode;

        // Create context for this analysis
        var context = new TypeInferenceContext(program, mode, options, resolver, cancellationToken)
        {
            ProgramSourceProvider = options.ProgramSourceProvider
        };

        try
        {
            // Set up timeout if specified
            using var timeoutCts = options.Timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeoutCts != null)
            {
                timeoutCts.CancelAfter(options.Timeout.Value);
                context = new TypeInferenceContext(program, mode, options, resolver, timeoutCts.Token)
                {
                    ProgramSourceProvider = options.ProgramSourceProvider
                };
            }

            // Perform the analysis
            await PerformTypeInferenceAsync(context);

            return context.CreateResult();
        }
        catch (OperationCanceledException) when (options.Timeout.HasValue)
        {
            return TypeInferenceResult.TimedOutResult(program, mode, context.Stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return TypeInferenceResult.Failed(program, mode, $"Type inference failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs the main type inference workflow
    /// </summary>
    private async Task PerformTypeInferenceAsync(TypeInferenceContext context)
    {
        context.EnterResolution("TypeInferenceEngine");

        try
        {
            // Phase 1: Clear any existing type information if not using incremental analysis
            if (!context.Options.IncrementalAnalysis)
            {
                ClearExistingTypeInformation(context.RootProgram);
            }

            // Phase 2: Collect global declarations and build initial symbol table
            await CollectGlobalDeclarationsAsync(context);
            context.ThrowIfCancelled();

            // Phase 3: Perform type inference using visitor pattern
            await PerformVisitorBasedInferenceAsync(context);
            context.ThrowIfCancelled();

            // Phase 4: Validate and post-process results
            await ValidateAndPostProcessAsync(context);
        }
        finally
        {
            context.ExitResolution();
        }
    }

    /// <summary>
    /// Phase 1: Clears existing type information from the AST
    /// </summary>
    private void ClearExistingTypeInformation(ProgramNode program)
    {
        program.ClearTypeInformationRecursively();
    }

    /// <summary>
    /// Phase 2: Collects global declarations and builds initial symbol table
    /// </summary>
    private async Task CollectGlobalDeclarationsAsync(TypeInferenceContext context)
    {
        context.EnterResolution("CollectGlobalDeclarations");

        try
        {
            var program = context.RootProgram;

            // Collect class definitions
            if (program.AppClass != null)
            {
                var classType = new AppClassTypeInfo(program.AppClass.Name);
                context.AddGlobalType(program.AppClass.Name, classType);
            }

            // Collect interface definitions
            if (program.Interface != null)
            {
                var interfaceType = new AppClassTypeInfo(program.Interface.Name);
                context.AddGlobalType(program.Interface.Name, interfaceType);
            }

            // Collect function declarations
            foreach (var function in program.Functions)
            {
                // For now, assume functions return Any unless we can determine otherwise
                // This will be refined when we have more complete type information
                var returnType = DetermineReturnType(function) ?? AnyTypeInfo.Instance;
                context.AddGlobalType($"function:{function.Name}", returnType);
            }

            // Collect global and component variable declarations
            foreach (var variable in program.ComponentAndGlobalVariables)
            {
                var variableType = DetermineVariableType(variable);
                context.AddGlobalType(variable.Name, variableType);
            }

            // Collect constant declarations
            foreach (var constant in program.Constants)
            {
                var constantType = DetermineConstantType(constant);
                context.AddGlobalType(constant.Name, constantType);
            }
        }
        finally
        {
            context.ExitResolution();
        }
    }

    /// <summary>
    /// Phase 3: Performs the main type inference using the visitor pattern
    /// </summary>
    private async Task PerformVisitorBasedInferenceAsync(TypeInferenceContext context)
    {
        context.EnterResolution("VisitorBasedInference");

        try
        {
            var visitor = new SimpleTypeInferenceVisitor(context);

            // Visit the entire program tree
            context.RootProgram.Accept(visitor);

            // If we're in thorough mode, we might need to resolve additional references
            if (context.CanResolveExternalPrograms)
            {
                // External references resolution would go here in thorough mode
        // await ResolveExternalReferencesAsync(context, visitor);
            }
        }
        finally
        {
            context.ExitResolution();
        }
    }

    /// <summary>
    /// Phase 4: Validates results and performs post-processing
    /// </summary>
    private async Task ValidateAndPostProcessAsync(TypeInferenceContext context)
    {
        context.EnterResolution("ValidateAndPostProcess");

        try
        {
            // Validate type compatibility in assignments
            ValidateTypeCompatibility(context);

            // Check for common type errors
            ValidateCommonErrors(context);

            // Perform any final cleanup
            await Task.CompletedTask; // Placeholder for future async validation
        }
        finally
        {
            context.ExitResolution();
        }
    }


    /// <summary>
    /// Determines the return type of a function
    /// </summary>
    private TypeInfo? DetermineReturnType(FunctionNode function)
    {
        // Check if function has explicit return type annotation
        if (function.ReturnType != null)
        {
            return ConvertTypeNodeToTypeInfo(function.ReturnType);
        }

        // For now, return null to indicate unknown - visitor will determine from return statements
        return null;
    }

    /// <summary>
    /// Determines the type of a variable declaration
    /// </summary>
    private TypeInfo DetermineVariableType(ProgramVariableNode variable)
    {
        // Check if variable has explicit type
        if (variable.Type != null)
        {
            var explicitType = ConvertTypeNodeToTypeInfo(variable.Type);
            if (explicitType != null) return explicitType;
        }

        // For now, default to Any - visitor will handle initializer inference
        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Determines the type of a constant declaration
    /// </summary>
    private TypeInfo DetermineConstantType(ConstantNode constant)
    {
        // Constants should have initializer values we can analyze
        if (constant.Value != null)
        {
            return InferLiteralType(constant.Value);
        }

        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Converts a TypeNode from the AST to a TypeInfo instance
    /// </summary>
    private TypeInfo? ConvertTypeNodeToTypeInfo(TypeNode typeNode)
    {
        return typeNode switch
        {
            BuiltInTypeNode builtin => ConvertBuiltinType(builtin),
            ArrayTypeNode array => ConvertArrayType(array),
            AppClassTypeNode appClass => new AppClassTypeInfo(appClass.QualifiedName),
            _ => null
        };
    }

    /// <summary>
    /// Converts a builtin TypeNode to TypeInfo
    /// </summary>
    private TypeInfo ConvertBuiltinType(BuiltInTypeNode builtin)
    {
        // Since AST and type system now use the same PeopleCodeType enum,
        // we can directly use the registry for lookup
        return PeopleCodeTypeRegistry.GetTypeByName(builtin.Type.GetTypeName()) ?? AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Converts an array TypeNode to TypeInfo
    /// </summary>
    private TypeInfo ConvertArrayType(ArrayTypeNode array)
    {
        var elementType = array.ElementType != null ? ConvertTypeNodeToTypeInfo(array.ElementType) : null;
        return new ArrayTypeInfo(array.Dimensions, elementType);
    }

    /// <summary>
    /// Infers the type of a literal value
    /// </summary>
    private TypeInfo InferLiteralType(AstNode literalNode)
    {
        if (literalNode is LiteralNode literal)
        {
            return literal.LiteralType switch
            {
                LiteralType.String => PrimitiveTypeInfo.String,
                LiteralType.Integer => PrimitiveTypeInfo.Integer,
                LiteralType.Decimal => PrimitiveTypeInfo.Number,
                LiteralType.Boolean => PrimitiveTypeInfo.Boolean,
                LiteralType.Null => AnyTypeInfo.Instance, // Null can be any type
                _ => AnyTypeInfo.Instance
            };
        }

        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Validates type compatibility throughout the program
    /// </summary>
    private void ValidateTypeCompatibility(TypeInferenceContext context)
    {
        // This will be expanded to check assignments, function calls, etc.
        // For now, just a placeholder
    }

    /// <summary>
    /// Validates common type errors
    /// </summary>
    private void ValidateCommonErrors(TypeInferenceContext context)
    {
        // Check for common issues like:
        // - Undefined variables
        // - Type mismatches
        // - Invalid operations
        // For now, just a placeholder
    }

    #region ITypeInferenceEngine Implementation

    /// <summary>
    /// Validates the type compatibility between two types for assignment operations
    /// </summary>
    public bool ValidateTypeCompatibility(TypeInfo sourceType, TypeInfo targetType)
    {
        // For now, use a simple compatibility check
        // This should be enhanced with proper type hierarchy checking
        return sourceType.Equals(targetType) ||
               targetType.Kind == TypeKind.Any ||
               sourceType.Kind == TypeKind.Any;
    }

    /// <summary>
    /// Determines the most specific common type between two types
    /// </summary>
    public TypeInfo ResolveCommonType(TypeInfo type1, TypeInfo type2)
    {
        if (type1.Equals(type2))
            return type1;

        // If either is Any, return Any
        if (type1.Kind == TypeKind.Any || type2.Kind == TypeKind.Any)
            return AnyTypeInfo.Instance;

        // Check if one is assignable to the other
        if (ValidateTypeCompatibility(type1, type2))
            return type2;
        if (ValidateTypeCompatibility(type2, type1))
            return type1;

        // Default to Any if no common type found
        return AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Infers the type of a literal value or expression without full program context
    /// </summary>
    public TypeInfo InferNodeType(AstNode node)
    {
        return node switch
        {
            LiteralNode literal => InferLiteralType(literal),
            ProgramVariableNode => node.GetInferredType() ?? AnyTypeInfo.Instance,
            _ => node.GetInferredType() ?? UnknownTypeInfo.Instance
        };
    }

    /// <summary>
    /// Clears any internal caching or state to ensure fresh analysis
    /// </summary>
    public void Reset()
    {
        // Clear any internal state if needed
        // For now, no internal state to clear beyond what's in context
    }

    #endregion

    #region IExtendedTypeInferenceEngine Implementation

    /// <summary>
    /// Resolves type information for a class or interface from external program sources
    /// </summary>
    public async Task<ClassTypeInfo?> ResolveExternalTypeAsync(
        string qualifiedName,
        IProgramSourceProvider sourceProvider,
        CancellationToken cancellationToken = default)
    {
        // Create a temporary context for resolution
        var tempProgram = new ProgramNode(); // Placeholder program
        var tempContext = new TypeInferenceContext(
            tempProgram,
            TypeInferenceMode.Thorough,
            new TypeInferenceOptions { ProgramSourceProvider = sourceProvider },
            cancellationToken: cancellationToken);

        return await tempContext.ResolveClassInfoAsync(qualifiedName);
    }

    /// <summary>
    /// Performs incremental type inference, only re-analyzing nodes that have changed
    /// </summary>
    public async Task<TypeInferenceResult> InferTypesIncrementalAsync(
        ProgramNode program,
        ISet<AstNode> changedNodes,
        TypeInferenceResult previousResult,
        TypeInferenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Enable incremental analysis in options
        var incrementalOptions = options ?? new TypeInferenceOptions();
        incrementalOptions.IncrementalAnalysis = true;

        // For now, fall back to full analysis
        // Future enhancement would track changed nodes and only re-analyze affected parts
        return await InferTypesAsync(program, TypeInferenceMode.Quick, null, incrementalOptions, cancellationToken);
    }

    #endregion
}
