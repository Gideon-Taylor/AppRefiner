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
    public class LanguageExtensionManager
    {
        private readonly List<BaseLanguageExtension> extensions = new();
        private readonly MainForm mainForm;
        private readonly DataGridView? extensionGrid;
        private readonly SettingsService settingsService;

        // Cached lookups for performance (indexed by lowercase type name)
        private Dictionary<string, List<BaseLanguageExtension>> propertyExtensionsByType = new();
        private Dictionary<string, List<BaseLanguageExtension>> methodExtensionsByType = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="form">The main form for UI operations</param>
        /// <param name="extensionOptionsGrid">The data grid view for displaying extensions (optional)</param>
        /// <param name="settings">The settings service for persistence</param>
        public LanguageExtensionManager(MainForm form, DataGridView? extensionOptionsGrid, SettingsService settings)
        {
            mainForm = form;
            extensionGrid = extensionOptionsGrid;
            settingsService = settings;
        }

        /// <summary>
        /// Gets all registered language extensions
        /// </summary>
        public IEnumerable<BaseLanguageExtension> Extensions => extensions;

        #region Discovery and Initialization

        /// <summary>
        /// Discovers and initializes all language extensions from the main assembly and plugins
        /// </summary>
        public void InitializeLanguageExtensions()
        {
            extensions.Clear();
            propertyExtensionsByType.Clear();
            methodExtensionsByType.Clear();

            if (extensionGrid != null)
            {
                extensionGrid.Rows.Clear();
            }

            // Discover from main assembly
            var coreTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => typeof(BaseLanguageExtension).IsAssignableFrom(p) &&
                           !p.IsAbstract && !p.IsInterface);

            // Discover from plugins
            var pluginTypes = PluginManager.DiscoverLanguageExtensionTypes();

            var allTypes = coreTypes.Concat(pluginTypes);

            // Instantiate and register all discovered types
            foreach (var type in allTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is BaseLanguageExtension extension)
                    {
                        extensions.Add(extension);
                        extension.Active = true;
                        if (extensionGrid != null)
                        {
                            // Grid columns: Active (checkbox), Target (TypeWithDimensionality), Type (Property/Method), Method/Property (Name)
                            string targetTypeStr = FormatTargetTypes(extension.TargetTypes);
                            string extensionTypeStr = extension.ExtensionType == LanguageExtensionType.Property ? "Property" : "Method";

                            int rowIndex = extensionGrid.Rows.Add(
                                extension.Active,           // Column 0: Active
                                targetTypeStr,              // Column 1: Target
                                extensionTypeStr,           // Column 2: Type
                                extension.Name              // Column 3: Method/Property
                            );
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

            Debug.Log($"Initialized {extensions.Count} language extensions");
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all active extensions that match the given target type.
        /// Uses both cached lookups and type assignability checking.
        /// </summary>
        /// <param name="targetType">The type to match against</param>
        /// <returns>List of matching active extensions</returns>
        public List<BaseLanguageExtension> GetExtensionsForType(TypeInfo targetType)
        {
            var results = new List<BaseLanguageExtension>();

            // Check cached lookups by type name
            var targetTypeName = targetType.Name.ToLowerInvariant();

            if (propertyExtensionsByType.TryGetValue(targetTypeName, out var propExtensions))
            {
                results.AddRange(propExtensions.Where(e => e.Active));
            }

            if (methodExtensionsByType.TryGetValue(targetTypeName, out var methodExtensions))
            {
                results.AddRange(methodExtensions.Where(e => e.Active));
            }

            // Also check for extensions that match via type assignability
            foreach (var extension in extensions.Where(e => e.Active))
            {
                foreach (var extTargetType in extension.TargetTypes)
                {
                    if (extTargetType.IsAssignableFrom(targetType))
                    {
                        if (!results.Contains(extension))
                        {
                            results.Add(extension);
                        }
                        break; // Don't add the same extension multiple times
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all active property extensions
        /// </summary>
        /// <returns>List of active property extensions</returns>
        public List<BaseLanguageExtension> GetPropertyExtensions()
        {
            return extensions.Where(e => e.Active && e.ExtensionType == LanguageExtensionType.Property).ToList();
        }

        /// <summary>
        /// Gets all active method extensions
        /// </summary>
        /// <returns>List of active method extensions</returns>
        public List<BaseLanguageExtension> GetMethodExtensions()
        {
            return extensions.Where(e => e.Active && e.ExtensionType == LanguageExtensionType.Method).ToList();
        }

        /// <summary>
        /// Gets extensions that match both the target type and member name
        /// </summary>
        /// <param name="targetType">The type to match against</param>
        /// <param name="memberName">The member name to match</param>
        /// <param name="extensionType">Property or method extension</param>
        /// <returns>List of matching active extensions</returns>
        public List<BaseLanguageExtension> GetExtensionsForTypeAndName(TypeInfo targetType, string memberName, LanguageExtensionType extensionType)
        {
            var candidates = GetExtensionsForType(targetType);
            return candidates.Where(e =>
                e.ExtensionType == extensionType &&
                e.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
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
                if (extensionGrid.Rows[e.RowIndex].Tag is BaseLanguageExtension extension)
                {
                    if (extensionGrid.Rows[e.RowIndex].Cells[0].Value is bool isActive)
                    {
                        extension.Active = isActive;
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

            foreach (var extension in extensions)
            {
                // Add extension to cache for each target type it supports
                foreach (var targetType in extension.TargetTypes)
                {
                    var targetTypeName = GetTypeName(targetType);

                    if (extension.ExtensionType == LanguageExtensionType.Property)
                    {
                        if (!propertyExtensionsByType.ContainsKey(targetTypeName))
                        {
                            propertyExtensionsByType[targetTypeName] = new List<BaseLanguageExtension>();
                        }
                        propertyExtensionsByType[targetTypeName].Add(extension);
                    }
                    else if (extension.ExtensionType == LanguageExtensionType.Method)
                    {
                        if (!methodExtensionsByType.ContainsKey(targetTypeName))
                        {
                            methodExtensionsByType[targetTypeName] = new List<BaseLanguageExtension>();
                        }
                        methodExtensionsByType[targetTypeName].Add(extension);
                    }
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
        /// Formats a list of target types for display in the grid
        /// </summary>
        private string FormatTargetTypes(List<TypeInfo> types)
        {
            if (types.Count == 1)
            {
                return FormatTypeInfo(types[0]);
            }

            return string.Join(", ", types.Select(FormatTypeInfo));
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
