using AppRefiner.Database;
using AppRefiner.LanguageExtensions;
using DiffPlex.Model;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Manages tooltip providers and handles tooltip display.
    /// </summary>
    public static class TooltipManager
    {
        private static readonly List<BaseTooltipProvider> providers = new();
        private static bool initialized = false;
        public static bool IgnoreNextCallTip;

        /// <summary>
        /// Extension manager for checking extension method signatures.
        /// Set by MainForm during initialization.
        /// </summary>
        public static TypeExtensionManager? ExtensionManager { get; set; }

        /// <summary>
        /// Initializes the tooltip manager with discovered providers.
        /// </summary>
        public static void Initialize(bool force = false)
        {
            if (initialized && !force)
                return;

            // Clear existing providers
            providers.Clear();

            // First, register built-in tooltip providers by discovering them from the current assembly
            DiscoverAndRegisterBuiltInProviders();

            // Next, discover and register tooltip providers from plugins
            DiscoverAndRegisterPluginProviders();

            initialized = true;

            Debug.Log($"Initialized TooltipManager with {providers.Count} providers");
        }

        public static void ShowFunctionCallTooltip(ScintillaEditor editor, ProgramNode program, FunctionCallNode node, int cursorPosition = -1)
        {
            FunctionInfo? funcInfo = node.GetFunctionInfo();

            // If no FunctionInfo attached, check if this is an extension method
            if (funcInfo == null &&
                node.Function is MemberAccessNode memberAccess &&
                ExtensionManager != null)
            {
                // Get the target type from type inference
                var targetType = memberAccess.Target.GetInferredType();
                if (targetType != null)
                {
                    // Check if there's an active extension for this type and method name
                    var extensionMatches = ExtensionManager.GetExtensionsForTypeAndName(
                        targetType,
                        memberAccess.MemberName,
                        LanguageExtensionType.Method);

                    // Use first active extension match
                    var extension = extensionMatches.FirstOrDefault(e => e.Active);
                    if (extension != null)
                    {
                        funcInfo = extension.GetFunctionInfo();
                    }
                }
            }

            string toolTipText;
            int highlightStart;
            int highlightEnd;

            if (funcInfo != null && node.FirstToken != null)
            {

                var validator = new FunctionCallValidator(new NullTypeMetadataResolver());

                List<PeopleCodeTypeInfo.Types.TypeInfo> arguments = [];
                foreach (var a in node.Arguments)
                {
                    var inferredType = a.GetInferredType();
                    if (inferredType == null)
                    {
                        inferredType = UnknownTypeInfo.Instance;
                    }
                    // Note: IsAssignable flag should already be set during type inference
                    arguments.Add(inferredType);
                }

                // Determine which argument the cursor is inside by counting
                // how many arguments end before the cursor position.
                // This gives us the index of the argument currently being edited.
                var argumentsForHighlight = arguments;
                if (cursorPosition >= 0)
                {
                    int cursorArgIndex = 0;
                    for (int i = 0; i < node.Arguments.Count; i++)
                    {
                        if (node.Arguments[i].SourceSpan.End.ByteIndex <= cursorPosition)
                            cursorArgIndex = i + 1;
                        else
                            break;
                    }
                    // Only pass arguments before the cursor's position to highlight the current parameter
                    if (cursorArgIndex < arguments.Count)
                    {
                        argumentsForHighlight = arguments.Take(cursorArgIndex).ToList();
                    }
                }

                var allowedTypes = validator.GetAllowedNextTypes(funcInfo, argumentsForHighlight.ToArray());

                // Extract object type for polymorphic type resolution
                PeopleCodeTypeInfo.Types.TypeInfo? objectType = GetObjectTypeFromFunctionCall(node);

                List<(int start, int end)> highlightRanges = new List<(int start, int end)>();
                (var text, highlightRanges) = FormatFunctionCallTip(funcInfo, arguments, allowedTypes, objectType);

                ScintillaManager.ShowCallTipWithText(editor, editor.FunctionCallNode.FirstToken.SourceSpan.Start.ByteIndex + 1, text, true);
                foreach (var highlightRange in highlightRanges)
                {
                    ScintillaManager.SetCallTipHighlight(editor, highlightRange.start, highlightRange.end);
                }
            }

        }

        private static (string, List<(int start, int end)>) FormatFunctionCallTip(FunctionInfo funcInfo, List<PeopleCodeTypeInfo.Types.TypeInfo> arguments, List<FunctionCallValidator.ParameterTypeInfo> allowedTypes, PeopleCodeTypeInfo.Types.TypeInfo? objectType)
        {
            List<(int start, int end)> highlightRanges = new List<(int start, int end)>();
            StringBuilder sb = new StringBuilder();
            int paramStart = 0;
            int paramEnd = 0;
            IEnumerable<string> allowedNames = allowedTypes.Select(t => t.ParameterName);
            sb.Append($"{funcInfo.Name}(");
            for (var x = 0; x < funcInfo.Parameters.Count; x++)
            {

                if (allowedNames.Contains(funcInfo.Parameters[x].Name))
                {
                    /* Highlight the parameter name */
                    paramStart = sb.Length;
                    paramEnd = Encoding.UTF8.GetByteCount(funcInfo.Parameters[x].Name);
                    highlightRanges.Add((paramStart, paramStart + paramEnd));
                }

                var typeName = funcInfo.Parameters[x].ToString();
                // Resolve polymorphic type keywords in parameter type
                try
                {
                    typeName = ResolvePolymorphicTypeString(typeName, objectType, arguments);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error resolving parameter type: {ex.Message}");
                    // Keep original typeName if resolution fails
                }

                sb.Append(typeName);

                if (x < funcInfo.Parameters.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            // Resolve polymorphic return type
            string returnTypeStr;
            try
            {
                var resolvedReturnTypes = funcInfo.ResolveReturnTypes(objectType, arguments.ToArray());
                returnTypeStr = string.Join(" | ", resolvedReturnTypes.Select(t => t.ToString()));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resolving return type for {funcInfo.Name}: {ex.Message}");
                returnTypeStr = funcInfo.GetReturnTypeString(); // Fallback to original method
            }
            sb.Append($") -> {returnTypeStr}");
            sb.Append("\n\n");
            paramStart = sb.Length;
            sb.Append($"Next allowed type{(allowedTypes.Count > 1 ? "(s)" : "")}:\n");
            paramEnd = sb.Length;
            highlightRanges.Add((paramStart, paramEnd));
            foreach (var type in allowedTypes)
            {
                var typeName = type.TypeName;
                // Resolve polymorphic type keywords in allowed types
                try
                {
                    typeName = ResolvePolymorphicTypeString(typeName, objectType, arguments);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error resolving allowed type: {ex.Message}");
                    // Keep original typeName if resolution fails
                }

                if (!string.IsNullOrEmpty(type.ParameterName))
                {
                    sb.Append($"{type.ParameterName}: {typeName}\n");
                } else
                {
                    sb.Append($"{typeName}\n");
                }
            }
            return (sb.ToString(), highlightRanges);
        }

        /// <summary>
        /// Extracts the object type from a function call node if it's a member access.
        /// </summary>
        /// <param name="node">The function call node.</param>
        /// <returns>The inferred type of the target object, or null for non-member-access calls.</returns>
        private static PeopleCodeTypeInfo.Types.TypeInfo? GetObjectTypeFromFunctionCall(FunctionCallNode node)
        {
            if (node.Function is MemberAccessNode memberAccess)
            {
                return memberAccess.Target.GetInferredType();
            }
            return null;
        }

        /// <summary>
        /// Gets the element type from an object type (reduces array dimensionality by 1).
        /// Used for resolving "elementofobject" polymorphic type.
        /// </summary>
        /// <param name="objectType">The object type.</param>
        /// <returns>The element type, or null if not an array.</returns>
        private static PeopleCodeTypeInfo.Types.TypeInfo? GetElementTypeFromObject(PeopleCodeTypeInfo.Types.TypeInfo? objectType)
        {
            if (objectType == null) return null;

            if (objectType is ArrayTypeInfo arrayType)
            {
                if (arrayType.Dimensions == 1)
                {
                    // 1D array -> return element type
                    return arrayType.ElementType;
                }
                else
                {
                    // Multi-dimensional array -> reduce dimensionality by 1
                    return new ArrayTypeInfo(arrayType.Dimensions - 1, arrayType.ElementType);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets an array type from a parameter type (increases array dimensionality by 1).
        /// Used for resolving "arrayoffirstparameter" polymorphic type.
        /// </summary>
        /// <param name="paramType">The parameter type.</param>
        /// <returns>An array type with dimensionality increased by 1, or null if param is null.</returns>
        private static PeopleCodeTypeInfo.Types.TypeInfo? GetArrayTypeFromParameter(PeopleCodeTypeInfo.Types.TypeInfo? paramType)
        {
            if (paramType == null) return null;

            if (paramType is ArrayTypeInfo arrayType)
            {
                // Increase array dimensionality by 1
                return new ArrayTypeInfo(arrayType.Dimensions + 1, arrayType.ElementType);
            }
            else
            {
                // Wrap scalar type in 1D array
                return new ArrayTypeInfo(1, paramType);
            }
        }

        /// <summary>
        /// Resolves polymorphic type keywords in a type name string.
        /// </summary>
        /// <param name="typeName">The type name string that may contain polymorphic keywords.</param>
        /// <param name="objectType">The object type for resolving sameasobject/elementofobject.</param>
        /// <param name="arguments">The argument types for resolving sameasfirstparameter/arrayoffirstparameter.</param>
        /// <returns>The type name with polymorphic keywords replaced by actual types.</returns>
        private static string ResolvePolymorphicTypeString(string typeName, PeopleCodeTypeInfo.Types.TypeInfo? objectType, List<PeopleCodeTypeInfo.Types.TypeInfo> arguments)
        {
            // Replace sameasfirstparameter
            if (typeName.Contains("sameasfirstparameter"))
            {
                var replacement = arguments.Count > 0 ? (arguments[0]?.ToString() ?? "any") : "any";
                typeName = typeName.Replace("sameasfirstparameter", replacement);
            }

            // Replace sameasobject
            if (typeName.Contains("sameasobject"))
            {
                var replacement = objectType?.ToString() ?? "any";
                typeName = typeName.Replace("sameasobject", replacement);
            }

            // Replace elementofobject
            if (typeName.Contains("elementofobject"))
            {
                var elementType = GetElementTypeFromObject(objectType);
                var replacement = elementType?.ToString() ?? "any";
                typeName = typeName.Replace("elementofobject", replacement);
            }

            // Replace arrayoffirstparameter
            if (typeName.Contains("arrayoffirstparameter"))
            {
                var firstArg = arguments.Count > 0 ? arguments[0] : null;
                var arrayType = GetArrayTypeFromParameter(firstArg);
                var replacement = arrayType?.ToString() ?? "any";
                typeName = typeName.Replace("arrayoffirstparameter", replacement);
            }

            return typeName;
        }

        /// <summary>
        /// Discovers and registers tooltip providers from the current assembly
        /// </summary>
        private static void DiscoverAndRegisterBuiltInProviders()
        {
            try
            {
                var currentAssembly = Assembly.GetExecutingAssembly();

                // Find all non-abstract types that implement BaseTooltipProvider
                var tooltipProviderTypes = currentAssembly.GetTypes()
                    .Where(t => typeof(BaseTooltipProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var type in tooltipProviderTypes)
                {
                    try
                    {
                        // Create an instance of the tooltip provider and register it
                        var provider = Activator.CreateInstance(type) as BaseTooltipProvider;
                        if (provider != null)
                        {
                            RegisterProvider(provider);
                            Debug.Log($"Registered built-in tooltip provider: {provider.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other providers
                        Debug.LogError($"Failed to create instance of tooltip provider {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error discovering built-in tooltip providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers and registers tooltip providers from plugins
        /// </summary>
        private static void DiscoverAndRegisterPluginProviders()
        {
            try
            {
                // Get tooltip provider types from plugins
                var pluginProviderTypes = Plugins.PluginManager.DiscoverTooltipProviderTypes();

                foreach (var type in pluginProviderTypes)
                {
                    try
                    {
                        // Create an instance of the tooltip provider and register it
                        var provider = Activator.CreateInstance(type) as BaseTooltipProvider;
                        if (provider != null)
                        {
                            RegisterProvider(provider);
                            Debug.Log($"Registered plugin tooltip provider: {provider.Name} from {type.Assembly.GetName().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other providers
                        Debug.LogError($"Failed to create instance of plugin tooltip provider {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error discovering plugin tooltip providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a tooltip provider with the manager.
        /// </summary>
        /// <param name="provider">The tooltip provider to register.</param>
        public static void RegisterProvider(BaseTooltipProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            // Add provider and sort by priority
            providers.Add(provider);

            // Sort in descending order of priority
            providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Gets all registered tooltip providers.
        /// </summary>
        public static IReadOnlyList<BaseTooltipProvider> Providers => providers.AsReadOnly();

        /// <summary>
        /// Shows a tooltip for the given position in the editor by querying all active providers.
        /// </summary>
        /// <param name="editor">The Scintilla editor.</param>
        /// <param name="position">Position to show tooltip at.</param>
        /// <returns>True if a tooltip was shown, false otherwise.</returns>
        public static bool ShowTooltip(ScintillaEditor editor, int position)
        {
            return ShowTooltip(editor, position, -1);
        }

        /// <summary>
        /// Shows a tooltip for the given position and line in the editor by querying all active providers.
        /// </summary>
        /// <param name="editor">The Scintilla editor.</param>
        /// <param name="position">Position to show tooltip at.</param>
        /// <param name="lineNumber">The one-based line number at the position.</param>
        /// <returns>True if a tooltip was shown, false otherwise.</returns>
        public static bool ShowTooltip(ScintillaEditor editor, int position, int lineNumber)
        {
            if (editor == null || !editor.IsValid())
                return false;

            // Collect tooltips from all active providers
            var tooltips = new List<string>();

            var tooltipProviders = providers.Where(p => p.Active && p.GetType().IsAssignableTo(typeof(BaseTooltipProvider)))
                .ToList();

            // Then, handle AST providers if there are any and this is PeopleCode
            if (tooltipProviders.Count > 0 && editor.Type == EditorType.PeopleCode)
            {
                ProcessTooltipProviders(editor, position, lineNumber, tooltipProviders, tooltips);
            }

            // If no tooltips were found, return false
            if (tooltips.Count == 0)
                return false;

            // Combine all tooltips with blank lines between them
            var combinedTooltip = string.Join(Environment.NewLine + Environment.NewLine, tooltips);

            // Show the combined tooltip
            ScintillaManager.ShowCallTipWithText(editor, position, combinedTooltip);
            return true;
        }

        /// <summary>
        /// Process AST-based tooltip providers using the self-hosted parser.
        /// Handles both AstTooltipProvider and ScopedAstTooltipProvider types.
        /// </summary>
        private static void ProcessTooltipProviders(
            ScintillaEditor editor,
            int position,
            int lineNumber,
            List<BaseTooltipProvider> tooltipProviders,
            List<string> tooltips)
        {
            if (editor.ContentString == null)
                return;

            try
            {
                // Parse the content using self-hosted parser
                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(editor.ContentString);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                // Run type inference (works with or without database)
                // This populates type information on AST nodes for all tooltip providers
                RunTypeInference(editor, program);

                // Reset and configure each provider
                foreach (var provider in tooltipProviders)
                {
                    provider.Reset();
                    provider.DataManager = editor.DataManager;

                    if (provider.CanProvideTooltipAt(editor, program, tokens, position, lineNumber))
                    {
                        provider.ProcessProgram(program, position, lineNumber);
                    }
                }

                // Retrieve tooltips from each provider
                foreach (var provider in tooltipProviders)
                {
                    var tooltip = provider.GetTooltip(editor, position);
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        tooltips.Add(tooltip);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing AST tooltip providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Hides the current tooltip and notifies all providers.
        /// </summary>
        /// <param name="editor">The Scintilla editor.</param>
        public static void HideTooltip(ScintillaEditor editor)
        {
            if (editor == null || !editor.IsValid())
                return;

            // Notify all active providers
            foreach (var provider in providers.Where(p => p.Active))
            {
                provider.OnHideTooltip(editor);
            }

            // Hide the tooltip using the existing ScintillaManager method
            ScintillaManager.HideCallTip(editor);
        }

        /// <summary>
        /// Runs type inference on the program to populate type information on AST nodes.
        /// Works with or without database - with database can resolve custom app classes,
        /// without database still resolves builtins, literals, and local types.
        /// </summary>
        private static void RunTypeInference(ScintillaEditor editor, ProgramNode program)
        {
            try
            {
                // Extract metadata from current program
                string qualifiedName = DetermineQualifiedName(editor, program);
                var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                // Get type resolver (may be null if no database)
                var typeResolver = editor.AppDesignerProcess?.TypeResolver;

                // Run type inference (works even with null resolver)
                TypeInferenceVisitor.Run(
                    program,
                    metadata,
                    typeResolver,
                    defaultRecordName: null,
                    defaultFieldName: null,
                    inferAutoDeclaredTypes: false,
                    onUndefinedVariable: ExtensionManager != null ? ExtensionManager.HandleUndefinedVariable : null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error running type inference for tooltips: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the qualified name for the current program for type inference.
        /// </summary>
        private static string DetermineQualifiedName(ScintillaEditor editor, ProgramNode node)
        {
            // Try to extract from AST structure first
            if (node.AppClass != null)
            {
                // For app classes/interfaces, try to build qualified name from imports or use simple name
                var className = node.AppClass.Name;

                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    // Parse caption to get program identifier
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                        openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                        openTarget.ObjectValues[methodIndex] = null;
                        return openTarget.Path;
                    }
                    else
                    {
                        /* probably never what you want but we have to return something? */
                        return className;
                    }
                }
                else
                {
                    /* probably never what you want but we have to return something? */
                    return className;
                }
            }
            else
            {
                // For function libraries or other programs, use a generic name
                // Try to extract from editor caption if available
                if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
                {
                    // Parse caption to get program identifier
                    var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                    if (openTarget != null)
                    {
                        return openTarget.Path;
                    }
                }

                // Fallback to generic name
                return "Program";
            }
        }
    }
}