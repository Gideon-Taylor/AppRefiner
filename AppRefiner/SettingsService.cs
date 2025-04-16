using AppRefiner.Linters;
using AppRefiner.Stylers;
using AppRefiner.TooltipProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms; // Required for CheckBox references if loading/saving general settings here

namespace AppRefiner
{
    public class SettingsService
    {
        // Helper class for serializing/deserializing active states
        private class RuleState
        {
            public string TypeName { get; set; } = "";
            public bool Active { get; set; }
        }

        // --- General Settings --- 

        public void LoadGeneralSettings(CheckBox chkInitCollapsed, CheckBox chkOnlyPPC, CheckBox chkBetterSQL, 
                                      CheckBox chkAutoDark, CheckBox chkLintAnnotate, CheckBox chkAutoPair, 
                                      CheckBox chkPromptForDB, out string? lintReportPath)
        {
            // Wrap in try-finally if isLoadingSettings logic is needed externally
            try
            {
                chkInitCollapsed.Checked = Properties.Settings.Default.initCollapsed;
                chkOnlyPPC.Checked = Properties.Settings.Default.onlyPPC;
                chkBetterSQL.Checked = Properties.Settings.Default.betterSQL;
                chkAutoDark.Checked = Properties.Settings.Default.autoDark;
                chkLintAnnotate.Checked = Properties.Settings.Default.lintAnnotate;
                chkAutoPair.Checked = Properties.Settings.Default.autoPair;
                chkPromptForDB.Checked = Properties.Settings.Default.promptForDB;
                lintReportPath = Properties.Settings.Default.LintReportPath;
            }
            catch (Exception ex)
            {
                 Debug.LogException(ex, "Error loading general settings");
                 lintReportPath = null; // Default on error
                 // Optionally apply default values to checkboxes here
            }
        }

        public void SaveGeneralSettings(bool initCollapsed, bool onlyPPC, bool betterSQL, 
                                      bool autoDark, bool lintAnnotate, bool autoPair, 
                                      bool promptForDB, string? lintReportPath)
        {
            Properties.Settings.Default.initCollapsed = initCollapsed;
            Properties.Settings.Default.onlyPPC = onlyPPC;
            Properties.Settings.Default.betterSQL = betterSQL;
            Properties.Settings.Default.autoDark = autoDark;
            Properties.Settings.Default.lintAnnotate = lintAnnotate;
            Properties.Settings.Default.autoPair = autoPair;
            Properties.Settings.Default.promptForDB = promptForDB;
            Properties.Settings.Default.LintReportPath = lintReportPath;
            // Note: Saving Linter/Styler/Tooltip states is handled separately
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
                    if(providerMap.TryGetValue(state.TypeName, out var provider))
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