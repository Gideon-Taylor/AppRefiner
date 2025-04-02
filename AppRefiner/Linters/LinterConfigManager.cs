using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Manages loading and saving linter configurations
    /// </summary>
    public static class LinterConfigManager
    {
        private const string CONFIG_FILENAME = "LinterConfig.json";
        
        /// <summary>
        /// Gets the full path to the linter configuration file
        /// </summary>
        private static string ConfigFilePath => Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
            CONFIG_FILENAME);

        /// <summary>
        /// Dictionary to store linter configurations by type name
        /// </summary>
        private static Dictionary<string, string> LinterConfigs { get; set; } = new();

        /// <summary>
        /// Loads all linter configurations from the config file
        /// </summary>
        public static void LoadLinterConfigs()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var configs = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (configs != null)
                    {
                        LinterConfigs = configs;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.Log($"Error loading linter configs: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves all linter configurations to the config file
        /// </summary>
        public static void SaveLinterConfigs()
        {
            try
            {
                string json = JsonSerializer.Serialize(LinterConfigs, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.Log($"Error saving linter configs: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies configurations to a list of linters
        /// </summary>
        /// <param name="linters">The list of linters to configure</param>
        public static void ApplyConfigurations(IEnumerable<BaseLintRule> linters)
        {
            foreach (var linter in linters)
            {
                string typeName = linter.GetType().FullName ?? string.Empty;
                if (!string.IsNullOrEmpty(typeName) && LinterConfigs.TryGetValue(typeName, out string? config))
                {
                    linter.SetLinterConfig(config);
                }
            }
        }

        /// <summary>
        /// Updates the configuration for a specific linter
        /// </summary>
        /// <param name="linter">The linter to update</param>
        public static void UpdateLinterConfig(BaseLintRule linter)
        {
            string typeName = linter.GetType().FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(typeName))
            {
                LinterConfigs[typeName] = linter.GetLinterConfig();
                SaveLinterConfigs();
            }
        }

        /// <summary>
        /// Gets the configuration for a specific linter type
        /// </summary>
        /// <param name="typeName">The full name of the linter type</param>
        /// <returns>The configuration JSON string or null if not found</returns>
        public static string? GetLinterConfig(string typeName)
        {
            if (LinterConfigs.TryGetValue(typeName, out string? config))
            {
                return config;
            }
            return null;
        }
    }
}
