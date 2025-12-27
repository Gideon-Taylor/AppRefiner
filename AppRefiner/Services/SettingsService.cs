using AppRefiner.LanguageExtensions;
using AppRefiner.Linters;
using AppRefiner.Stylers;
using AppRefiner.TooltipProviders;
using System.Text.Json;

namespace AppRefiner
{
    public class GeneralSettingsData
    {
        public bool CodeFolding { get; set; }
        public bool InitCollapsed { get; set; }
        public bool OnlyPPC { get; set; }
        public bool BetterSQL { get; set; }
        public bool AutoDark { get; set; }
        public bool AutoPair { get; set; }
        public bool PromptForDB { get; set; }
        public string? LintReportPath { get; set; }
        public string? TNS_ADMIN { get; set; }
        public bool CheckEventMapping { get; set; }
        public bool CheckEventMapXrefs { get; set; }
        public bool ShowClassPath { get; set; }
        public bool ShowClassText { get; set; }

        public bool RememberFolds { get; set; }
        public bool OverrideFindReplace { get; set; }
        public bool OverrideOpen { get; set; }
        public bool AutoCenterDialogs { get; set; } = false;
        public bool MultiSelection { get; set; }
        public bool LineSelectionFix { get; set; }
        public string Theme { get; set; } = "Default";
        public bool ThemeFilled { get; set; } = false;
    }

    public class SettingsService
    {
        // Helper class for serializing/deserializing active states
        private class RuleState
        {
            public string TypeName { get; set; } = "";
            public bool Active { get; set; }
        }

        // --- General Settings --- 

        public GeneralSettingsData LoadGeneralSettings()
        {
            var settings = new GeneralSettingsData();
            try
            {
                settings.CodeFolding = Properties.Settings.Default.codeFolding;
                settings.InitCollapsed = Properties.Settings.Default.initCollapsed;
                settings.OnlyPPC = Properties.Settings.Default.onlyPPC;
                settings.BetterSQL = Properties.Settings.Default.betterSQL;
                settings.AutoDark = Properties.Settings.Default.autoDark;
                settings.AutoPair = Properties.Settings.Default.autoPair;
                settings.PromptForDB = Properties.Settings.Default.promptForDB;
                settings.LintReportPath = Properties.Settings.Default.LintReportPath;
                settings.CheckEventMapping = Properties.Settings.Default.checkEventMapping;
                settings.CheckEventMapXrefs = Properties.Settings.Default.checkEventMapXrefs;
                settings.ShowClassPath = Properties.Settings.Default.showClassPath;
                settings.ShowClassText = Properties.Settings.Default.showClassText;
                settings.TNS_ADMIN = Properties.Settings.Default.TNS_ADMIN;
                settings.RememberFolds = Properties.Settings.Default.rememberFolds;
                settings.OverrideFindReplace = Properties.Settings.Default.overrideFindReplace;
                settings.OverrideOpen = Properties.Settings.Default.overrideOpen;
                settings.AutoCenterDialogs = Properties.Settings.Default.AutoCenterDialogs;
                settings.MultiSelection = Properties.Settings.Default.multiSelection;
                settings.LineSelectionFix = Properties.Settings.Default.lineSelectionFix;
                settings.Theme = Properties.Settings.Default.theme;
                settings.ThemeFilled = Properties.Settings.Default.theme_filled;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading general settings");
                // Return default settings object on error or rethrow/handle as appropriate
                // For now, returning potentially partially filled or default object
                return new GeneralSettingsData(); // Or handle specific properties default
            }
            return settings;
        }

        public void SaveGeneralSettings(GeneralSettingsData settings)
        {
            Properties.Settings.Default.codeFolding = settings.CodeFolding;
            Properties.Settings.Default.initCollapsed = settings.InitCollapsed;
            Properties.Settings.Default.onlyPPC = settings.OnlyPPC;
            Properties.Settings.Default.betterSQL = settings.BetterSQL;
            Properties.Settings.Default.autoDark = settings.AutoDark;
            Properties.Settings.Default.autoPair = settings.AutoPair;
            Properties.Settings.Default.promptForDB = settings.PromptForDB;
            Properties.Settings.Default.LintReportPath = settings.LintReportPath;
            Properties.Settings.Default.TNS_ADMIN = settings.TNS_ADMIN;
            Properties.Settings.Default.checkEventMapping = settings.CheckEventMapping;
            Properties.Settings.Default.checkEventMapXrefs = settings.CheckEventMapXrefs;
            Properties.Settings.Default.showClassPath = settings.ShowClassPath;
            Properties.Settings.Default.showClassText = settings.ShowClassText;
            Properties.Settings.Default.rememberFolds = settings.RememberFolds;
            Properties.Settings.Default.overrideFindReplace = settings.OverrideFindReplace;
            Properties.Settings.Default.overrideOpen = settings.OverrideOpen;
            Properties.Settings.Default.AutoCenterDialogs = settings.AutoCenterDialogs;
            Properties.Settings.Default.multiSelection = settings.MultiSelection;
            Properties.Settings.Default.lineSelectionFix = settings.LineSelectionFix;
            Properties.Settings.Default.theme = settings.Theme;
            Properties.Settings.Default.theme_filled = settings.ThemeFilled;
        }

        public void SaveChanges()
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving settings");
                // Inform the user?
            }
        }

        // --- Linter States --- 

        public void LoadLinterStates(IEnumerable<BaseLintRule> linterRules, DataGridView dataGridView)
        {
            try
            {
                var states = JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.LinterStates);

                if (states == null) return;

                var ruleMap = linterRules.ToDictionary(l => l.GetType().FullName ?? "");

                foreach (var state in states)
                {
                    if (ruleMap.TryGetValue(state.TypeName, out var linter))
                    {
                        linter.Active = state.Active;
                        // Update corresponding grid row (requires DataGridView access)
                        var row = dataGridView.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseLintRule l && l == linter);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing LinterStates - using defaults.");
                // Use defaults if settings are corrupt
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading linter states");
            }
        }

        public void SaveLinterStates(IEnumerable<BaseLintRule> linterRules)
        {
            try
            {
                var states = linterRules.Select(l => new RuleState
                {
                    TypeName = l.GetType().FullName ?? "",
                    Active = l.Active
                }).ToList();

                Properties.Settings.Default.LinterStates =
                    JsonSerializer.Serialize(states);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving linter states");
            }
        }

        // --- Styler States --- 

        public void LoadStylerStates(IEnumerable<BaseStyler> stylers, DataGridView dataGridView)
        {
            try
            {
                var states = JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.StylerStates);

                if (states == null) return;

                var stylerMap = stylers.ToDictionary(s => s.GetType().FullName ?? "");

                foreach (var state in states)
                {
                    if (stylerMap.TryGetValue(state.TypeName, out var styler))
                    {
                        styler.Active = state.Active;
                        // Update corresponding grid row
                        var row = dataGridView.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseStyler s && s == styler);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing StylerStates - using defaults.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading styler states");
            }
        }

        public void SaveStylerStates(IEnumerable<BaseStyler> stylers)
        {
            try
            {
                var states = stylers.Select(s => new RuleState
                {
                    TypeName = s.GetType().FullName ?? "",
                    Active = s.Active
                }).ToList();

                Properties.Settings.Default.StylerStates =
                    JsonSerializer.Serialize(states);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving styler states");
            }
        }

        // --- Tooltip Provider States --- 

        public void LoadTooltipStates(IEnumerable<BaseTooltipProvider> tooltipProviders, DataGridView dataGridView)
        {
            try
            {
                var states = JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.TooltipStates);

                if (states == null) return;

                var providerMap = tooltipProviders.ToDictionary(p => p.GetType().FullName ?? "");

                foreach (var state in states)
                {
                    if (providerMap.TryGetValue(state.TypeName, out var provider))
                    {
                        provider.Active = state.Active;
                        // Update corresponding grid row
                        var row = dataGridView.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => r.Tag is BaseTooltipProvider p && p == provider);
                        if (row != null)
                        {
                            row.Cells[0].Value = state.Active;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing TooltipStates - using defaults.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading tooltip states");
            }
        }

        public void SaveTooltipStates(IEnumerable<BaseTooltipProvider> tooltipProviders)
        {
            try
            {
                var states = tooltipProviders.Select(p => new RuleState
                {
                    TypeName = p.GetType().FullName ?? "",
                    Active = p.Active
                }).ToList();

                Properties.Settings.Default.TooltipStates =
                    JsonSerializer.Serialize(states);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving tooltip states");
            }
        }

        // --- Language Extension States ---

        public void LoadLanguageExtensionStates(IEnumerable<BaseTypeExtension> extensions, DataGridView? dataGridView)
        {
            try
            {
                var states = JsonSerializer.Deserialize<List<RuleState>>(
                    Properties.Settings.Default.LanguageExtensionStates);

                if (states == null) return;

                var extensionMap = extensions.ToDictionary(e => e.GetType().FullName ?? "");

                foreach (var state in states)
                {
                    if (extensionMap.TryGetValue(state.TypeName, out var extension))
                    {
                        extension.Active = state.Active;
                        // Update corresponding grid row if provided
                        if (dataGridView != null)
                        {
                            var row = dataGridView.Rows.Cast<DataGridViewRow>()
                                .FirstOrDefault(r => r.Tag is BaseTypeExtension e && e == extension);
                            if (row != null)
                            {
                                row.Cells[0].Value = state.Active;
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing LanguageExtensionStates - using defaults.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading language extension states");
            }
        }

        public void SaveLanguageExtensionStates(IEnumerable<BaseTypeExtension> extensions)
        {
            try
            {
                var states = extensions.Select(e => new RuleState
                {
                    TypeName = e.GetType().FullName ?? "",
                    Active = e.Active
                }).ToList();

                Properties.Settings.Default.LanguageExtensionStates =
                    JsonSerializer.Serialize(states);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving language extension states");
            }
        }

        // --- Language Extension Configurations ---

        /// <summary>
        /// Loads language extension configurations from settings
        /// </summary>
        /// <param name="extensions">The extensions to load configurations for</param>
        public void LoadLanguageExtensionConfigs(IEnumerable<BaseTypeExtension> extensions)
        {
            try
            {
                var configJson = Properties.Settings.Default.LanguageExtensionConfigs;

                if (string.IsNullOrEmpty(configJson))
                {
                    return;
                }

                var configs = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);
                if (configs == null) return;

                var extensionMap = extensions.ToDictionary(e => e.GetType().FullName ?? "");

                foreach (var kvp in configs)
                {
                    if (extensionMap.TryGetValue(kvp.Key, out var extension))
                    {
                        extension.SetExtensionConfig(kvp.Value);
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing LanguageExtensionConfigs - using defaults.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading language extension configurations");
            }
        }

        /// <summary>
        /// Saves language extension configurations to settings
        /// </summary>
        /// <param name="extensions">The extensions to save configurations for</param>
        public void SaveLanguageExtensionConfigs(IEnumerable<BaseTypeExtension> extensions)
        {
            try
            {
                var configs = new Dictionary<string, string>();

                foreach (var extension in extensions)
                {
                    string typeName = extension.GetType().FullName ?? string.Empty;
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        configs[typeName] = extension.GetExtensionConfig();
                    }
                }

                Properties.Settings.Default.LanguageExtensionConfigs =
                    JsonSerializer.Serialize(configs);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving language extension configurations");
            }
        }

        /// <summary>
        /// Updates the configuration for a specific language extension
        /// </summary>
        /// <param name="extension">The extension to update</param>
        /// <param name="extensions">All extensions (to save complete config)</param>
        public void UpdateLanguageExtensionConfig(BaseTypeExtension extension, IEnumerable<BaseTypeExtension> extensions)
        {
            SaveLanguageExtensionConfigs(extensions);
        }

        // --- Smart Open Configuration --- 

        /// <summary>
        /// Loads the Smart Open configuration from settings
        /// </summary>
        /// <returns>SmartOpenConfig object with current settings or default if not found</returns>
        public SmartOpenConfig LoadSmartOpenConfig()
        {
            try
            {
                var configJson = Properties.Settings.Default.smartOpenConfig;
                
                if (string.IsNullOrEmpty(configJson))
                {
                    return SmartOpenConfig.GetDefault();
                }

                var config = JsonSerializer.Deserialize<SmartOpenConfig>(configJson);
                return config ?? SmartOpenConfig.GetDefault();
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing SmartOpenConfig - using defaults.");
                return SmartOpenConfig.GetDefault();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading Smart Open configuration");
                return SmartOpenConfig.GetDefault();
            }
        }

        /// <summary>
        /// Saves the Smart Open configuration to settings
        /// </summary>
        /// <param name="config">The SmartOpenConfig to save</param>
        public void SaveSmartOpenConfig(SmartOpenConfig config)
        {
            try
            {
                Properties.Settings.Default.smartOpenConfig = JsonSerializer.Serialize(config);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving Smart Open configuration");
            }
        }

        // --- Auto Suggest Configuration ---

        /// <summary>
        /// Loads the Auto Suggest configuration from settings
        /// </summary>
        /// <returns>AutoSuggestSettings object with current settings or default if not found</returns>
        public AutoSuggestSettings LoadAutoSuggestSettings()
        {
            try
            {
                var configJson = Properties.Settings.Default.autoSuggestSettings;

                if (string.IsNullOrEmpty(configJson))
                {
                    return AutoSuggestSettings.GetDefault();
                }

                var config = JsonSerializer.Deserialize<AutoSuggestSettings>(configJson);
                return config ?? AutoSuggestSettings.GetDefault();
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex, "Error deserializing AutoSuggestSettings - using defaults.");
                return AutoSuggestSettings.GetDefault();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error loading Auto Suggest configuration");
                return AutoSuggestSettings.GetDefault();
            }
        }

        /// <summary>
        /// Saves the Auto Suggest configuration to settings
        /// </summary>
        /// <param name="config">The AutoSuggestSettings to save</param>
        public void SaveAutoSuggestSettings(AutoSuggestSettings config)
        {
            try
            {
                Properties.Settings.Default.autoSuggestSettings = JsonSerializer.Serialize(config);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error saving Auto Suggest configuration");
            }
        }
    }
}