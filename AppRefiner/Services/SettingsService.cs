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

        public void LoadStylerStates(IEnumerable<IStyler> stylers, DataGridView dataGridView)
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
                            .FirstOrDefault(r => r.Tag is IStyler s && s == styler);
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

        public void SaveStylerStates(IEnumerable<IStyler> stylers)
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

        public void LoadTooltipStates(IEnumerable<ITooltipProvider> tooltipProviders, DataGridView dataGridView)
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
                            .FirstOrDefault(r => r.Tag is ITooltipProvider p && p == provider);
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

        public void SaveTooltipStates(IEnumerable<ITooltipProvider> tooltipProviders)
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
    }
}