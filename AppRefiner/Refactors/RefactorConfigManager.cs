using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Manages loading and saving refactor configurations
    /// </summary>
    public static class RefactorConfigManager
    {
        private const string CONFIG_FILENAME = "RefactorConfig.json";
        private const string APP_DATA_FOLDER_NAME = "AppRefiner";

        /// <summary>
        /// Gets the full path to the refactor configuration file in the user's AppData folder
        /// </summary>
        private static string ConfigFilePath
        {
            get
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolderPath = Path.Combine(appDataPath, APP_DATA_FOLDER_NAME);
                return Path.Combine(appFolderPath, CONFIG_FILENAME);
            }
        }

        /// <summary>
        /// Dictionary to store refactor configurations by type name
        /// </summary>
        private static Dictionary<string, string> RefactorConfigs { get; set; } = new();

        /// <summary>
        /// Loads all refactor configurations from the config file
        /// </summary>
        public static void LoadRefactorConfigs()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var configs = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (configs != null)
                    {
                        RefactorConfigs = configs;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.Log($"Error loading refactor configs: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves all refactor configurations to the config file
        /// </summary>
        public static void SaveRefactorConfigs()
        {
            try
            {
                // Ensure the directory exists before saving
                string? directoryPath = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string json = JsonSerializer.Serialize(RefactorConfigs, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.Log($"Error saving refactor configs: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies stored configuration to a refactor instance
        /// </summary>
        /// <param name="instance">The refactor instance to configure</param>
        public static void ApplyConfigurationToInstance(BaseRefactor instance)
        {
            if (instance == null) return;

            string typeName = instance.GetType().FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(typeName) && RefactorConfigs.TryGetValue(typeName, out string? config))
            {
                instance.ApplyRefactorConfig(config);
            }
        }

        /// <summary>
        /// Updates the configuration for a specific refactor type
        /// </summary>
        /// <param name="refactorType">The refactor type to update</param>
        /// <param name="jsonConfig">The configuration JSON string</param>
        public static void UpdateRefactorConfig(Type refactorType, string jsonConfig)
        {
            string typeName = refactorType.FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(typeName))
            {
                RefactorConfigs[typeName] = jsonConfig;
                SaveRefactorConfigs();
            }
        }

        /// <summary>
        /// Gets the configuration for a specific refactor type
        /// </summary>
        /// <param name="refactorType">The refactor type</param>
        /// <returns>The configuration JSON string or null if not found</returns>
        public static string? GetRefactorConfig(Type refactorType)
        {
            string typeName = refactorType.FullName ?? string.Empty;
            if (RefactorConfigs.TryGetValue(typeName, out string? config))
            {
                return config;
            }
            return null;
        }

        /// <summary>
        /// Gets the configuration for a specific refactor type by name
        /// </summary>
        /// <param name="typeName">The full name of the refactor type</param>
        /// <returns>The configuration JSON string or null if not found</returns>
        public static string? GetRefactorConfig(string typeName)
        {
            if (RefactorConfigs.TryGetValue(typeName, out string? config))
            {
                return config;
            }
            return null;
        }

        /// <summary>
        /// Checks if a refactor type has configurable properties
        /// </summary>
        /// <param name="refactorType">The refactor type to check</param>
        /// <returns>True if the refactor has configurable properties</returns>
        public static bool HasConfigurableProperties(Type refactorType)
        {
            return BaseRefactor.GetConfigurableProperties(refactorType).Count > 0;
        }

        /// <summary>
        /// Gets the configuration for a refactor type, creating default if none exists
        /// </summary>
        /// <param name="refactorType">The refactor type</param>
        /// <returns>The configuration JSON string</returns>
        public static string GetOrCreateRefactorConfig(Type refactorType)
        {
            var existingConfig = GetRefactorConfig(refactorType);
            if (existingConfig != null)
            {
                return existingConfig;
            }

            // Create default configuration
            var defaultConfig = BaseRefactor.GetDefaultRefactorConfig(refactorType);
            UpdateRefactorConfig(refactorType, defaultConfig);
            return defaultConfig;
        }
    }
}