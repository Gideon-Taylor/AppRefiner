using AppRefiner.Plugins;
using AppRefiner.Services;
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

                            // Add to grid (one row per transform)
                            if (extensionGrid != null)
                            {
                                // Grid columns: Active (checkbox), Target (TypeWithDimensionality), Type (Property/Method), Method/Property (Name)
                                string targetTypeStr = FormatTypeInfo(extension.TargetType);
                                string extensionTypeStr = transform.ExtensionType == LanguageExtensionType.Property
                                    ? "Property" : "Method";

                                int rowIndex = extensionGrid.Rows.Add(
                                    extension.Active,           // Column 0: Active
                                    targetTypeStr,              // Column 1: Target
                                    extensionTypeStr,           // Column 2: Type
                                    transform.GetName()         // Column 3: Method/Property (individual transform name)
                                );
                                // Store transform reference (not extension)
                                extensionGrid.Rows[rowIndex].Tag = transform;
                            }
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
                t.GetName().Equals(memberName, StringComparison.OrdinalIgnoreCase))
                .ToList();
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
                if (extensionGrid.Rows[e.RowIndex].Tag is ExtensionTransform transform)
                {
                    if (extensionGrid.Rows[e.RowIndex].Cells[0].Value is bool isActive)
                    {
                        // Update parent extension's active state
                        if (transform.ParentExtension != null)
                        {
                            transform.ParentExtension.Active = isActive;

                            // Update ALL rows for this parent extension
                            foreach (DataGridViewRow row in extensionGrid.Rows)
                            {
                                if (row.Tag is ExtensionTransform t &&
                                    t.ParentExtension == transform.ParentExtension)
                                {
                                    row.Cells[0].Value = isActive;
                                }
                            }
                        }

                        // Settings saved centrally on form close

                        // Rebuild caches when active state changes
                        RebuildLookupCaches();
                    }
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
                return arrayPrefix + elementTypeName;
            }

            if (typeInfo is AppClassTypeInfo appClassType)
            {
                return appClassType.Name;
            }

            return typeInfo.Name;
        }

        #endregion
    }
}
