using AppRefiner.Plugins;
using AppRefiner.Services;
using PeopleCodeParser.SelfHosted;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using System.Reflection;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions
{
    /// <summary>
    /// Manages discovery, registration, and querying of language extensions
    /// </summary>
    public class TypeExtensionManager
    {
        private readonly List<BaseTypeExtension> extensions = new();
        private readonly List<ExtensionTransform> allTransforms = new();
        private readonly MainForm mainForm;
        private readonly DataGridView? extensionGrid;
        private readonly SettingsService settingsService;

        // Cached lookups for performance (indexed by lowercase type name)
        private Dictionary<string, List<ExtensionTransform>> propertyExtensionsByType = new();
        private Dictionary<string, List<ExtensionTransform>> methodExtensionsByType = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="form">The main form for UI operations</param>
        /// <param name="extensionOptionsGrid">The data grid view for displaying extensions (optional)</param>
        /// <param name="settings">The settings service for persistence</param>
        public TypeExtensionManager(MainForm form, DataGridView? extensionOptionsGrid, SettingsService settings)
        {
            mainForm = form;
            extensionGrid = extensionOptionsGrid;
            settingsService = settings;
        }

        /// <summary>
        /// Gets all registered language extensions
        /// </summary>
        public IEnumerable<BaseTypeExtension> Extensions => extensions;

        #region Discovery and Initialization

        /// <summary>
        /// Discovers and initializes all language extensions from the main assembly and plugins
        /// </summary>
        public void InitializeLanguageExtensions()
        {
            extensions.Clear();
            allTransforms.Clear();
            propertyExtensionsByType.Clear();
            methodExtensionsByType.Clear();

            if (extensionGrid != null)
            {
                extensionGrid.Rows.Clear();
            }

            // Discover from main assembly
            var coreTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => typeof(BaseTypeExtension).IsAssignableFrom(p) &&
                           !p.IsAbstract && !p.IsInterface);

            // Discover from plugins
            var pluginTypes = PluginManager.DiscoverLanguageExtensionTypes();

            var allTypes = coreTypes.Concat(pluginTypes);

            // Instantiate and register all discovered types
            foreach (var type in allTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is BaseTypeExtension extension)
                    {
                        extensions.Add(extension);
                        extension.Active = true;

                        // Extract and register all transforms from this extension
                        foreach (var transform in extension.Transforms)
                        {
                            // Link transform to parent extension
                            transform.ParentExtension = extension;
                            allTransforms.Add(transform);
                        }

                        // Add to grid (one row per extension)
                        if (extensionGrid != null)
                        {
                            // Grid columns: Active (checkbox), Target (type), Transforms (count summary), Extension Name (class name)
                            string targetTypeStr = FormatTypeInfo(extension.TargetType);
                            string transformsSummary = GetTransformCountSummary(extension);
                            //string extensionName = extension.GetType().Name;

                            int rowIndex = extensionGrid.Rows.Add(
                                extension.Active,           // Column 0: Active
                                targetTypeStr,              // Column 1: Target
                                transformsSummary,          // Column 2: Transforms (count summary)
                                "Inspect"
                                //extensionName               // Column 3: Extension Name (class name)
                            );
                            // Store extension reference (not individual transform)
                            extensionGrid.Rows[rowIndex].Tag = extension;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Failed to instantiate language extension: {type.FullName}");
                }
            }

            // Load saved active states and configurations
            settingsService.LoadLanguageExtensionStates(extensions, extensionGrid);
            settingsService.LoadLanguageExtensionConfigs(extensions);

            // Build lookup caches
            RebuildLookupCaches();

            Debug.Log($"Initialized {extensions.Count} language extensions with {allTransforms.Count} transforms");
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all active transforms that match the given target type.
        /// Uses both cached lookups and type assignability checking.
        /// </summary>
        /// <param name="targetType">The type to match against</param>
        /// <returns>List of matching active transforms</returns>
        public List<ExtensionTransform> GetExtensionsForType(TypeInfo targetType)
        {
            var results = new List<ExtensionTransform>();

            // Check cached lookups by type name
            var targetTypeName = targetType.Name.ToLowerInvariant();

            if (propertyExtensionsByType.TryGetValue(targetTypeName, out var propTransforms))
            {
                results.AddRange(propTransforms.Where(t => t.Active));
            }

            if (methodExtensionsByType.TryGetValue(targetTypeName, out var methodTransforms))
            {
                results.AddRange(methodTransforms.Where(t => t.Active));
            }

            // Also check for transforms that match via type assignability
            foreach (var transform in allTransforms.Where(t => t.Active))
            {
                if (transform.ParentExtension == null) continue;

                if (transform.ParentExtension.TargetType.IsAssignableFrom(targetType))
                {
                    if (!results.Contains(transform))
                    {
                        results.Add(transform);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all active property transforms
        /// </summary>
        /// <returns>List of active property transforms</returns>
        public List<ExtensionTransform> GetPropertyExtensions()
        {
            return allTransforms.Where(t => t.Active && t.ExtensionType == LanguageExtensionType.Property).ToList();
        }

        /// <summary>
        /// Gets all active method transforms
        /// </summary>
        /// <returns>List of active method transforms</returns>
        public List<ExtensionTransform> GetMethodExtensions()
        {
            return allTransforms.Where(t => t.Active && t.ExtensionType == LanguageExtensionType.Method).ToList();
        }

        /// <summary>
        /// Gets transforms that match both the target type and member name
        /// </summary>
        /// <param name="targetType">The type to match against</param>
        /// <param name="memberName">The member name to match</param>
        /// <param name="extensionType">Property or method extension</param>
        /// <returns>List of matching active transforms</returns>
        public List<ExtensionTransform> GetExtensionsForTypeAndName(TypeInfo targetType, string memberName, LanguageExtensionType extensionType)
        {
            var candidates = GetExtensionsForType(targetType);
            return candidates.Where(t =>
                t.ExtensionType == extensionType &&
                t.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Handler for undefined variable resolution during type inference.
        /// Called when TypeInferenceVisitor encounters an undefined identifier.
        /// Checks if the identifier is an implicit parameter introduced by a language extension.
        /// </summary>
        /// <param name="node">The identifier node for the undefined variable</param>
        /// <param name="scope">The current scope context</param>
        /// <param name="registry">The variable registry</param>
        /// <returns>TypeInfo for the implicit parameter, or null if not an extension parameter</returns>
        public TypeInfo? HandleUndefinedVariable(
            PeopleCodeParser.SelfHosted.Nodes.IdentifierNode node,
            PeopleCodeParser.SelfHosted.Visitors.Models.ScopeContext? scope,
            PeopleCodeParser.SelfHosted.Visitors.Models.VariableRegistry? registry)
        {
            // Walk up AST to find containing function call
            var functionCall = node.FindAncestor<PeopleCodeParser.SelfHosted.Nodes.FunctionCallNode>();
            if (functionCall == null) return null;

            // Check if the function call is a member access (e.g., &students.Map(...))
            if (functionCall.Function is not PeopleCodeParser.SelfHosted.Nodes.MemberAccessNode memberAccess)
                return null;

            // Get the already-inferred type of the target (&students)
            var targetType = memberAccess.Target.GetInferredType();
            if (targetType == null) return null;

            // Check if this method is an active language extension
            var matchingTransforms = GetExtensionsForTypeAndName(
                targetType,
                memberAccess.MemberName,
                LanguageExtensionType.Method);

            if (!matchingTransforms.Any()) return null;

            // For each matching transform, check if it has implicit parameters matching this identifier
            foreach (var transform in matchingTransforms)
            {
                if (transform.ImplicitParameters == null) continue;

                var implicitParam = transform.ImplicitParameters
                    .FirstOrDefault(p => p.ParameterName.Equals(node.Name, StringComparison.OrdinalIgnoreCase));

                if (implicitParam != null)
                {
                    // Resolve the type using the transform's type resolver
                    return implicitParam.TypeResolver(targetType);
                }
            }

            return null;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles cell value changed events (Active checkbox in column 0)
        /// </summary>
        public void HandleExtensionGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (extensionGrid == null) return;

            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                if (extensionGrid.Rows[e.RowIndex].Tag is BaseTypeExtension extension)
                {
                    if (extensionGrid.Rows[e.RowIndex].Cells[0].Value is bool isActive)
                    {
                        // Update extension's active state
                        extension.Active = isActive;

                        // Settings saved centrally on form close

                        // Rebuild caches when active state changes
                        RebuildLookupCaches();
                    }
                }
            }
        }

        /// <summary>
        /// Handles cell content click events (Inspect button in column 3)
        /// </summary>
        public void HandleExtensionGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (extensionGrid == null) return;

            // Commit any pending edits, especially for checkboxes
            extensionGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            // Column 3 is the Inspect button
            if (e.RowIndex >= 0 && e.ColumnIndex == 3)
            {
                if (extensionGrid.Rows[e.RowIndex].Tag is BaseTypeExtension extension)
                {
                    // Show the inspect dialog
                    var dialog = new Dialogs.TypeExtensionInspectDialog(extension);
                    dialog.ShowDialog(mainForm);
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Rebuilds the type-indexed lookup caches for performance
        /// </summary>
        private void RebuildLookupCaches()
        {
            propertyExtensionsByType.Clear();
            methodExtensionsByType.Clear();

            foreach (var transform in allTransforms)
            {
                if (transform.ParentExtension == null) continue;

                // Add transform to cache for the target type
                var targetTypeName = GetTypeName(transform.ParentExtension.TargetType);

                if (transform.ExtensionType == LanguageExtensionType.Property)
                {
                    if (!propertyExtensionsByType.ContainsKey(targetTypeName))
                    {
                        propertyExtensionsByType[targetTypeName] = new List<ExtensionTransform>();
                    }
                    propertyExtensionsByType[targetTypeName].Add(transform);
                }
                else if (transform.ExtensionType == LanguageExtensionType.Method)
                {
                    if (!methodExtensionsByType.ContainsKey(targetTypeName))
                    {
                        methodExtensionsByType[targetTypeName] = new List<ExtensionTransform>();
                    }
                    methodExtensionsByType[targetTypeName].Add(transform);
                }
            }
        }

        /// <summary>
        /// Gets the type name from a TypeInfo for indexing
        /// </summary>
        private string GetTypeName(TypeInfo typeInfo)
        {
            if (typeInfo is ArrayTypeInfo arrayType)
            {
                string arrayPrefix = string.Join("", Enumerable.Repeat("array of ", arrayType.Dimensions));
                string elementTypeName = GetTypeName(arrayType.ElementType ?? AnyTypeInfo.Instance);
                return (arrayPrefix + elementTypeName).ToLowerInvariant();
            }

            if (typeInfo is AppClassTypeInfo appClassType)
            {
                return appClassType.Name.ToLowerInvariant();
            }

            return typeInfo.Name.ToLowerInvariant();
        }

        /// <summary>
        /// Formats a TypeInfo for display in the grid
        /// </summary>
        private string FormatTypeInfo(TypeInfo typeInfo)
        {
            if (typeInfo is ArrayTypeInfo arrayType)
            {
                string arrayPrefix = string.Join("", Enumerable.Repeat("array of ", arrayType.Dimensions));
                string elementTypeName = FormatTypeInfo(arrayType.ElementType ?? AnyTypeInfo.Instance);
                if (arrayType.Dimensions == 0)
                {
                    if (elementTypeName == "any")
                    {
                        return "Arrays";
                    } else
                    {
                        return $"{elementTypeName} Arrays";
                    }
                }
                return arrayPrefix + elementTypeName;
            }

            if (typeInfo is AppClassTypeInfo appClassType)
            {
                return appClassType.Name;
            }

            return typeInfo.Name;
        }

        /// <summary>
        /// Gets a summary of transform counts for an extension (e.g., "3 properties, 2 methods")
        /// </summary>
        private string GetTransformCountSummary(BaseTypeExtension extension)
        {
            var propertyCount = extension.Transforms.Count(t => t.ExtensionType == LanguageExtensionType.Property);
            var methodCount = extension.Transforms.Count(t => t.ExtensionType == LanguageExtensionType.Method);

            var parts = new List<string>();

            if (propertyCount > 0)
            {
                parts.Add(propertyCount == 1 ? "1 property" : $"{propertyCount} properties");
            }

            if (methodCount > 0)
            {
                parts.Add(methodCount == 1 ? "1 method" : $"{methodCount} methods");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "No transforms";
        }

        #endregion
    }
}
