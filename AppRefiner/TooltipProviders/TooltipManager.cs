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

        /// <summary>
        /// Initializes the tooltip manager with discovered providers.
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
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

        public static void ShowFunctionCallTooltip(ScintillaEditor editor, ProgramNode program, FunctionCallNode node)
        {
            FunctionInfo? funcInfo = node.GetFunctionInfo();
            string toolTipText;
            int highlightStart;
            int highlightEnd;


            if (funcInfo != null && node.FirstToken != null)
            {

                var validator = new FunctionCallValidator(new NullTypeMetadataResolver());

                List<ArgumentInfo> arguments = [];
                foreach (var a in node.Arguments)
                {
                    if (a is IdentifierNode ident && ident.Name.StartsWith("&"))
                    {
                        var autoDeclareCheck = program.AutoDeclaredVariables.Where(v => v.Name == ident.Name).FirstOrDefault();

                        if (autoDeclareCheck is not null)
                        {
                            arguments.Add(ArgumentInfo.Variable(AnyTypeInfo.Instance));
                        }
                        else
                        {
                            arguments.Add(ArgumentInfo.Variable(a.GetInferredType()));
                        }
                    }
                    else
                    {
                        arguments.Add(ArgumentInfo.NonVariable(a.GetInferredType()));
                    }
                }

                var allowedTypes = validator.GetAllowedNextTypes(funcInfo, arguments.ToArray());

                (var text, var start, var end) = FormatFunctionCallTip(funcInfo, allowedTypes);

                ScintillaManager.ShowCallTipWithText(editor, editor.FunctionCallNode.FirstToken.SourceSpan.Start.ByteIndex + 1, text, true);
                ScintillaManager.SetCallTipHighlight(editor, start, end);
            }

        }

        private static (string, int, int) FormatFunctionCallTip(FunctionInfo funcInfo, List<FunctionCallValidator.ParameterTypeInfo> allowedTypes)
        {
            StringBuilder sb = new StringBuilder();
            int paramStart = 0;
            int paramEnd = 0;

            sb.Append($"{funcInfo.Name}(");
            for (var x = 0; x < funcInfo.Parameters.Count; x++)
            {
                sb.Append(funcInfo.Parameters[x].ToString());

                if (x < funcInfo.Parameters.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            var returnTypeStr = funcInfo.GetReturnTypeString();
            sb.Append($") -> {returnTypeStr}");
            sb.Append("\n\n");
            paramStart = sb.Length;
            sb.Append($"Next allowed type{(allowedTypes.Count > 1 ? "(s)" : "")}:\n");
            paramEnd = sb.Length;
            foreach(var type in allowedTypes)
            {
                if (!string.IsNullOrEmpty(type.ParameterName))
                {
                    sb.Append($"{type.ParameterName}: {type.TypeName}\n");
                } else
                {
                    sb.Append($"{type.TypeName}\n");
                }
            }
            return (sb.ToString(), paramStart, paramEnd);
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
                TypeInferenceVisitor.Run(program, metadata, typeResolver);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error running type inference for tooltips: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the qualified name for the current program for type inference.
        /// </summary>
        private static string DetermineQualifiedName(ScintillaEditor editor, ProgramNode program)
        {
            // Try to parse from editor caption
            var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
            if (openTarget != null)
            {
                return openTarget.ToQualifiedName();
            }

            // Fall back to program structure
            if (program.AppClass != null) return program.AppClass.Name;
            if (program.Interface != null) return program.Interface.Name;

            return "Program";
        }
    }
}