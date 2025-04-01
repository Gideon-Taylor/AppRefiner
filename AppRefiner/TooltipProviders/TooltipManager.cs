using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Antlr4.Runtime;
using AppRefiner;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Manages tooltip providers and handles tooltip display.
    /// </summary>
    public static class TooltipManager
    {
        private static readonly List<ITooltipProvider> providers = new List<ITooltipProvider>();
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
            
            // Separate parse tree providers from regular providers
            var parseTreeProviders = providers.Where(p => p.Active && p is ParseTreeTooltipProvider)
                .Cast<ParseTreeTooltipProvider>().ToList();
            var regularProviders = providers.Where(p => p.Active && !(p is ParseTreeTooltipProvider));
            
            // First, query regular providers which don't need parse tree analysis
            foreach (var provider in regularProviders)
            {
                var tooltip = provider.GetTooltip(editor, position);
                if (!string.IsNullOrEmpty(tooltip))
                {
                    tooltips.Add(tooltip);
                }
            }
            
            // Then, handle parse tree providers if there are any
            if (parseTreeProviders.Count > 0 && editor.Type == EditorType.PeopleCode)
            {
                var applicableProviders = FilterParseTreeProviders(editor, position, parseTreeProviders);
                
                if (applicableProviders.Count > 0)
                {
                    // Process applicable parse tree providers
                    ProcessParseTreeTooltipProviders(editor, position, lineNumber, applicableProviders, tooltips);
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
        /// Filters parse tree providers based on the token at the given position.
        /// </summary>
        /// <param name="editor">The editor to check</param>
        /// <param name="position">The position in the document</param>
        /// <param name="providers">The list of parse tree providers to filter</param>
        /// <returns>List of applicable providers that match tokens at the position</returns>
        private static List<ParseTreeTooltipProvider> FilterParseTreeProviders(
            ScintillaEditor editor, 
            int position, 
            List<ParseTreeTooltipProvider> providers)
        {
            var applicableProviders = new List<ParseTreeTooltipProvider>();
            
            try
            {
                // Ensure we have editor content
                if (editor.ContentString == null)
                {
                    editor.ContentString = ScintillaManager.GetScintillaText(editor);
                }
                
                // Create lexer and token stream
                var lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(editor.ContentString));
                var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
                
                // Get all tokens including those on hidden channels
                tokenStream.Fill();
                var tokens = tokenStream.GetTokens();
                
                // Check providers against tokens
                foreach (var provider in providers)
                {
                    // If provider specifies no token types, it should be run for any token
                    if (provider.TokenTypes == null || provider.TokenTypes.Length == 0)
                    {
                        applicableProviders.Add(provider);
                        continue;
                    }
                    
                    // Check if any token at the position matches the provider's token types
                    bool applicable = false;
                    foreach (var token in tokens)
                    {
                        // Check if token contains or is adjacent to the position
                        if (position >= token.StartIndex && position <= token.StopIndex + 1)
                        {
                            if (provider.CanProvideTooltipForToken(token, position))
                            {
                                applicable = true;
                                break;
                            }
                        }
                    }
                    
                    if (applicable)
                    {
                        applicableProviders.Add(provider);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error filtering parse tree tooltip providers: {ex.Message}");
            }
            
            return applicableProviders;
        }
        
        /// <summary>
        /// Process parse tree tooltip providers by creating a MultiParseTreeWalker.
        /// </summary>
        private static void ProcessParseTreeTooltipProviders(
            ScintillaEditor editor, 
            int position,
            List<ParseTreeTooltipProvider> applicableProviders,
            List<string> tooltips)
        {
            ProcessParseTreeTooltipProviders(editor, position, -1, applicableProviders, tooltips);
        }

        /// <summary>
        /// Process parse tree tooltip providers by creating a MultiParseTreeWalker.
        /// </summary>
        private static void ProcessParseTreeTooltipProviders(
            ScintillaEditor editor, 
            int position,
            int lineNumber,
            List<ParseTreeTooltipProvider> applicableProviders,
            List<string> tooltips)
        {
            try
            {
                // Ensure we have editor content
                if (editor.ContentString == null)
                {
                    editor.ContentString = ScintillaManager.GetScintillaText(editor);
                }
                
                // Get the content string for preprocessing
                string content = editor.ContentString;
                if (string.IsNullOrEmpty(content))
                    return;
                    
                // Create lexer, token stream, and parser
                var lexer = new PeopleCodeLexer(new Antlr4.Runtime.AntlrInputStream(content));
                var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
                
                // Get all tokens including those on hidden channels
                tokenStream.Fill();
                
                // Collect comments from both comment channels
                var comments = tokenStream.GetTokens()
                    .Where(token => token.Channel == PeopleCodeLexer.COMMENTS || token.Channel == PeopleCodeLexer.API_COMMENTS)
                    .ToList();
                
                var parser = new PeopleCodeParser(tokenStream);
                var program = parser.program();
                
                // Create and configure the parse tree walker
                var walker = new MultiParseTreeWalker();
                
                // Reset and configure each provider
                foreach (var provider in applicableProviders)
                {
                    provider.Reset();
                    provider.Comments = comments;
                    provider.DataManager = editor.DataManager;
                    
                    // Initialize content for ScopeTooltipProvider before walking the parse tree
                    if (provider is ScopeTooltipProvider scopeProvider)
                    {
                        scopeProvider.InitializeWithContent(content);
                        
                        // Set the line number if available
                        if (lineNumber > 0)
                        {
                            scopeProvider.LineNumber = lineNumber;
                        }
                        
                        // Initialize with token stream to identify first tokens on each line
                        scopeProvider.InitializeWithTokenStream(tokenStream);
                    }
                    
                    walker.AddListener(provider);
                }
                
                // Walk the parse tree to collect tooltip information
                walker.Walk(program);
                
                // Retrieve tooltips from each provider
                foreach (var provider in applicableProviders)
                {
                    var tooltip = provider.GetTooltip(editor, position);
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        tooltips.Add(tooltip);
                    }
                }
                
                // Clean up resources
                parser.Interpreter.ClearDFA();
                GC.Collect();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing parse tree tooltip providers: {ex.Message}");
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