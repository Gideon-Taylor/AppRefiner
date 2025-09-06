using AppRefiner.Linters;
using AppRefiner.Stylers;
using AppRefiner.TooltipProviders;
using System.Reflection;

namespace AppRefiner.Plugins
{
    /// <summary>
    /// Manages the loading and discovery of plugins for AppRefiner
    /// </summary>
    public class PluginManager
    {
        private static readonly List<Assembly> LoadedPluginAssemblies = new();

        /// <summary>
        /// Loads all plugin assemblies from the specified directory
        /// </summary>
        /// <param name="pluginDirectory">Directory containing plugin DLLs</param>
        /// <returns>Number of plugin assemblies loaded</returns>
        public static int LoadPlugins(string pluginDirectory)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                return 0;
            }

            // Clear previously loaded plugin assemblies
            LoadedPluginAssemblies.Clear();

            // Find all DLL files in the plugin directory
            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    // Load the assembly
                    var assembly = Assembly.LoadFrom(dllFile);
                    LoadedPluginAssemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other plugins
                    Debug.Log($"Failed to load plugin: {dllFile}. Error: {ex.Message}");
                }
            }

            return LoadedPluginAssemblies.Count;
        }

        /// <summary>
        /// Discovers all linter types from loaded plugin assemblies
        /// </summary>
        /// <returns>Collection of linter types from plugins</returns>
        public static IEnumerable<Type> DiscoverLinterTypes()
        {
            var linterTypes = new List<Type>();

            foreach (var assembly in LoadedPluginAssemblies)
            {
                try
                {
                    // Find all non-abstract types that inherit from BaseLintRule
                    var types = assembly.GetTypes()
                        .Where(t => typeof(BaseLintRule).IsAssignableFrom(t) && !t.IsAbstract);

                    linterTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    Debug.Log($"Error discovering linter types in assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return linterTypes;
        }

        /// <summary>
        /// Discovers all styler types from loaded plugin assemblies
        /// </summary>
        /// <returns>Collection of styler types from plugins</returns>
        public static IEnumerable<Type> DiscoverStylerTypes()
        {
            var stylerTypes = new List<Type>();

            foreach (var assembly in LoadedPluginAssemblies)
            {
                try
                {
                    // Find all non-abstract types that implement IStyler
                    var types = assembly.GetTypes()
                        .Where(t => typeof(IStyler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                    stylerTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    Debug.Log($"Error discovering styler types in assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return stylerTypes;
        }

        /// <summary>
        /// Discovers all refactor types from loaded plugin assemblies
        /// </summary>
        /// <returns>Collection of refactor types from plugins</returns>
        public static IEnumerable<Type> DiscoverRefactorTypes()
        {
            var refactorTypes = new List<Type>();

            foreach (var assembly in LoadedPluginAssemblies)
            {
                try
                {
                    // Find all non-abstract types that inherit from ScopedRefactor
                    var types = assembly.GetTypes()
                        .Where(t => typeof(Refactors.BaseRefactor).IsAssignableFrom(t) &&
                               !t.IsAbstract);

                    refactorTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    Debug.Log($"Error discovering refactor types in assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return refactorTypes;
        }

        /// <summary>
        /// Discovers all tooltip provider types from loaded plugin assemblies
        /// </summary>
        /// <returns>Collection of tooltip provider types from plugins</returns>
        public static IEnumerable<Type> DiscoverTooltipProviderTypes()
        {
            var tooltipProviderTypes = new List<Type>();

            foreach (var assembly in LoadedPluginAssemblies)
            {
                try
                {
                    // Find all non-abstract types that implement BaseTooltipProvider
                    var types = assembly.GetTypes()
                        .Where(t => typeof(BaseTooltipProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                    tooltipProviderTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    Debug.Log($"Error discovering tooltip provider types in assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return tooltipProviderTypes;
        }

        /// <summary>
        /// Gets metadata about all loaded plugin assemblies
        /// </summary>
        /// <returns>List of plugin metadata</returns>
        public static List<PluginMetadata> GetLoadedPluginMetadata()
        {
            var metadata = new List<PluginMetadata>();

            foreach (var assembly in LoadedPluginAssemblies)
            {
                try
                {
                    var linterCount = assembly.GetTypes()
                        .Count(t => typeof(BaseLintRule).IsAssignableFrom(t) && !t.IsAbstract);

                    var stylerCount = assembly.GetTypes()
                        .Count(t => typeof(BaseStyler).IsAssignableFrom(t) && !t.IsAbstract);

                    var refactorCount = assembly.GetTypes()
                        .Count(t => typeof(Refactors.BaseRefactor).IsAssignableFrom(t) &&
                               !t.IsAbstract);

                    var tooltipProviderCount = assembly.GetTypes()
                        .Count(t => typeof(BaseTooltipProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                    var plugin = new PluginMetadata
                    {
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Version = assembly.GetName().Version?.ToString() ?? "Unknown",
                        FilePath = assembly.Location,
                        LinterCount = linterCount,
                        StylerCount = stylerCount,
                        RefactorCount = refactorCount,
                        TooltipProviderCount = tooltipProviderCount
                    };

                    metadata.Add(plugin);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    Debug.Log($"Error getting metadata for assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return metadata;
        }
    }

    /// <summary>
    /// Represents metadata about a loaded plugin
    /// </summary>
    public class PluginMetadata
    {
        public string AssemblyName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LinterCount { get; set; }
        public int StylerCount { get; set; }
        public int RefactorCount { get; set; }
        public int TooltipProviderCount { get; set; }

        public override string ToString()
        {
            return $"{AssemblyName} v{Version} ({LinterCount} linters, {StylerCount} stylers, {RefactorCount} refactors, {TooltipProviderCount} tooltip providers)";
        }
    }
}
