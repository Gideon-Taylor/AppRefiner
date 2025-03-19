using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AppRefiner.Linters;
using AppRefiner.Stylers;

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
                    System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dllFile}. Error: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"Error discovering linter types in assembly {assembly.FullName}: {ex.Message}");
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
                    // Find all non-abstract types that inherit from BaseStyler
                    var types = assembly.GetTypes()
                        .Where(t => typeof(BaseStyler).IsAssignableFrom(t) && !t.IsAbstract);
                    
                    stylerTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    System.Diagnostics.Debug.WriteLine($"Error discovering styler types in assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return stylerTypes;
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

                    var plugin = new PluginMetadata
                    {
                        AssemblyName = assembly.GetName().Name ?? "Unknown",
                        Version = assembly.GetName().Version?.ToString() ?? "Unknown",
                        FilePath = assembly.Location,
                        LinterCount = linterCount,
                        StylerCount = stylerCount
                    };

                    metadata.Add(plugin);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with other assemblies
                    System.Diagnostics.Debug.WriteLine($"Error getting metadata for assembly {assembly.FullName}: {ex.Message}");
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

        public override string ToString()
        {
            return $"{AssemblyName} v{Version} ({LinterCount} linters, {StylerCount} stylers)";
        }
    }
}
