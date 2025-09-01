using System.Reflection;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Manages tooltip providers and handles tooltip display.
    /// </summary>
    public static class TooltipManager
    {
        private static readonly List<ITooltipProvider> providers = new();
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

        /// <summary>
        /// Discovers and registers tooltip providers from the current assembly
        /// </summary>
        private static void DiscoverAndRegisterBuiltInProviders()
        {
            try
            {
                var currentAssembly = Assembly.GetExecutingAssembly();

                // Find all non-abstract types that implement ITooltipProvider
                var tooltipProviderTypes = currentAssembly.GetTypes()
                    .Where(t => typeof(ITooltipProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var type in tooltipProviderTypes)
                {
                    try
                    {
                        // Create an instance of the tooltip provider and register it
                        var provider = Activator.CreateInstance(type) as ITooltipProvider;
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
                        var provider = Activator.CreateInstance(type) as ITooltipProvider;
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
        public static void RegisterProvider(ITooltipProvider provider)
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
        public static IReadOnlyList<ITooltipProvider> Providers => providers.AsReadOnly();

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

            // Separate self-hosted AST providers from regular providers
            var selfHostedProviders = providers.Where(p => p.Active && p is ScopedAstTooltipProvider)
                .Cast<ScopedAstTooltipProvider>().ToList();
            var regularProviders = providers.Where(p => p.Active && !(p is ScopedAstTooltipProvider));

            // First, query regular providers which don't need parse tree analysis
            foreach (var provider in regularProviders)
            {
                var tooltip = provider.GetTooltip(editor, position);
                if (!string.IsNullOrEmpty(tooltip))
                {
                    tooltips.Add(tooltip);
                }
            }

            // Then, handle self-hosted AST providers if there are any
            if (selfHostedProviders.Count > 0 && editor.Type == EditorType.PeopleCode)
            {
                var applicableProviders = FilterSelfHostedProviders(editor, position, selfHostedProviders);

                if (applicableProviders.Count > 0)
                {
                    // Process applicable self-hosted providers
                    ProcessSelfHostedTooltipProviders(editor, position, lineNumber, applicableProviders, tooltips);
                }
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
        /// Filters self-hosted AST providers based on the AST node at the given position.
        /// </summary>
        /// <param name="editor">The editor to check</param>
        /// <param name="position">The position in the document</param>
        /// <param name="providers">The list of self-hosted providers to filter</param>
        /// <returns>List of applicable providers that can provide tooltips at the position</returns>
        private static List<ScopedAstTooltipProvider> FilterSelfHostedProviders(
            ScintillaEditor editor,
            int position,
            List<ScopedAstTooltipProvider> providers)
        {
            var applicableProviders = new List<ScopedAstTooltipProvider>();

            if (editor.ContentString == null) return [];

            try
            {
                // For self-hosted providers, we parse the content and check if providers can handle the position
                // All self-hosted providers are considered applicable for now since they work with AST nodes
                // The actual filtering will happen during AST traversal based on the position
                applicableProviders.AddRange(providers);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error filtering self-hosted tooltip providers: {ex.Message}");
            }

            return applicableProviders;
        }

        /// <summary>
        /// Process self-hosted AST tooltip providers.
        /// </summary>
        private static void ProcessSelfHostedTooltipProviders(
            ScintillaEditor editor,
            int position,
            List<ScopedAstTooltipProvider> applicableProviders,
            List<string> tooltips)
        {
            ProcessSelfHostedTooltipProviders(editor, position, -1, applicableProviders, tooltips);
        }

        /// <summary>
        /// Process self-hosted AST tooltip providers using the self-hosted parser.
        /// </summary>
        private static void ProcessSelfHostedTooltipProviders(
            ScintillaEditor editor,
            int position,
            int lineNumber,
            List<ScopedAstTooltipProvider> applicableProviders,
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

                // Reset and configure each provider
                foreach (var provider in applicableProviders)
                {
                    provider.Reset();
                    provider.DataManager = editor.DataManager;

                    // Note: CurrentPosition is set internally by the base GetTooltip method

                    // Initialize line number for ScopeTooltipProvider
                    if (provider is ScopeTooltipProvider scopeProvider)
                    {
                        if (lineNumber > 0)
                        {
                            scopeProvider.LineNumber = lineNumber;
                        }
                    }

                    // Process the AST program to collect tooltip information
                    provider.ProcessProgram(program);
                }

                // Retrieve tooltips from each provider
                foreach (var provider in applicableProviders)
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
                Debug.LogError($"Error processing self-hosted tooltip providers: {ex.Message}");
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
    }
}