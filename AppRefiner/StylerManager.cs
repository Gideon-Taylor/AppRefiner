using AppRefiner.Database;
using AppRefiner.Database.Models; // Assuming BaseStyler might need this
using AppRefiner.PeopleCode;
using AppRefiner.Plugins;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection; // Added for Assembly.GetExecutingAssembly
using System.Windows.Forms;
using System.Text.Json; // For settings serialization

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Manages the discovery, configuration, and execution of stylers.
    /// </summary>
    public class StylerManager
    {
        private readonly List<BaseStyler> stylers = new();
        private readonly DataGridView stylerGrid; // DataGridView for styler options
        private readonly MainForm mainForm; // Needed for Invoke potentially, though aiming to minimize direct use
        private readonly SettingsService settingsService; // Added SettingsService

        public StylerManager(MainForm form, DataGridView stylerOptionsGrid, SettingsService settings)
        {
            mainForm = form; // Store reference if needed for Invoke
            stylerGrid = stylerOptionsGrid;
            settingsService = settings; // Store SettingsService
        }

        public IEnumerable<BaseStyler> StylerRules => stylers;

        /// <summary>
        /// Discovers stylers, populates the grid, and loads saved states.
        /// </summary>
        public void InitializeStylerOptions()
        {
            stylers.Clear();
            stylerGrid.Rows.Clear();

            // Discover core stylers from the main assembly
            var executingAssembly = Assembly.GetExecutingAssembly();
            var coreStylerTypes = executingAssembly.GetTypes()
                .Where(p => typeof(BaseStyler).IsAssignableFrom(p) && !p.IsAbstract);

            // Discover stylers from plugins
            var pluginStylers = PluginManager.DiscoverStylerTypes();

            // Combine core and plugin stylers, ensuring uniqueness
            var allStylerTypes = coreStylerTypes.Concat(pluginStylers).Distinct();

            foreach (var type in allStylerTypes)
            {
                try
                {
                    BaseStyler? styler = (BaseStyler?)Activator.CreateInstance(type);
                    if (styler != null)
                    {
                        int rowIndex = stylerGrid.Rows.Add(styler.Active, styler.Description);
                        stylerGrid.Rows[rowIndex].Tag = styler;
                        stylers.Add(styler);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Failed to instantiate styler: {type.FullName}");
                }
            }

            // Load saved active states using SettingsService
            settingsService.LoadStylerStates(stylers, stylerGrid);
        }

        /// <summary>
        /// Processes active stylers for the given editor.
        /// </summary>
        /// <param name="editor">The Scintilla editor to process.</param>
        /// <param name="editorDataManager">The data manager associated with the editor.</param>
        public void ProcessStylersForEditor(ScintillaEditor? editor)
        {
            if (editor == null || !editor.IsValid() || editor.Type != EditorType.PeopleCode)
            {
                return; // Only process valid PeopleCode editors
            }

            var editorDataManager = editor?.DataManager;

            var (program, stream, comments) = editor.GetParsedProgram();

            // Get active stylers, filtering by database requirement
            var activeStylers = stylers.Where(a => a.Active && (a.DatabaseRequirement != DataManagerRequirement.Required || editorDataManager != null));

            MultiParseTreeWalker walker = new();
            List<Indicator> newIndicators = new();

            foreach (var styler in activeStylers)
            {
                styler.Reset(); // Reset internal state before processing
                styler.DataManager = editorDataManager;
                styler.Indicators = newIndicators; // Styler adds indicators to this list
                styler.Comments = comments;
                styler.Editor = editor;
                walker.AddListener(styler);
            }

            try
            {
                walker.Walk(program);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error during styler walk");
                // Potentially clear indicators or handle error state?
            }
            
            // Now apply the collected indicators, comparing against existing ones
            ApplyIndicators(editor, newIndicators);
        }

        /// <summary>
        /// Applies the new set of indicators to the editor, removing old ones and adding new ones efficiently.
        /// </summary>
        /// <param name="editor">The editor to apply indicators to.</param>
        /// <param name="newIndicators">The list of indicators generated by the stylers.</param>
        private void ApplyIndicators(ScintillaEditor editor, List<Indicator> newIndicators)
        {
            var currentIndicators = editor.ActiveIndicators; // Get existing indicators

            // Optimize comparison by using HashSet for quick lookups
            var newIndicatorSet = new HashSet<Indicator>(newIndicators);
            var currentIndicatorSet = new HashSet<Indicator>(currentIndicators);

            // Find indicators to remove (present in current, not in new)
            var indicatorsToRemove = currentIndicators.Where(ci => !newIndicatorSet.Contains(ci)).ToList();

            // Find indicators to add (present in new, not in current)
            var indicatorsToAdd = newIndicators.Where(ni => !currentIndicatorSet.Contains(ni)).ToList();

            // Use Invoke if required for UI thread safety, though ScintillaManager might handle this internally
            // For now, assume direct calls are safe or handled by ScintillaManager/caller.
            
            // Remove indicators that are no longer needed
            foreach (var indicator in indicatorsToRemove)
            {
                RemoveIndicator(editor, indicator);
            }

            // Add new indicators
            foreach (var indicator in indicatorsToAdd)
            {
                AddIndicator(editor, indicator);
            }

            /* Re-add any active Better Find markers */
            foreach (var indicator in editor.SearchIndicators)
            {
                AddIndicator(editor, indicator);
            }

            // Update the editor's active indicator list with the new set
            //editor.ActiveIndicators = newIndicators; // Replace the old list
        }

        public void RemoveIndicator(ScintillaEditor editor, Indicator indicator)
        {
            ScintillaManager.RemoveIndicator(editor, indicator);
        }

        public void AddIndicator(ScintillaEditor editor, Indicator indicator)
        {
            ScintillaManager.AddIndicator(editor, indicator);
        }


        // --- Grid Event Handlers (to be called from MainForm) ---

        /// <summary>
        /// Handles clicks within the styler options grid (e.g., checkbox changes).
        /// To be called by the MainForm's event handler.
        /// </summary>
        public void HandleStylerGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Commit any pending edits, especially for checkboxes
            stylerGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            // Future: If configuration buttons are added, handle them here.
            // if (e.ColumnIndex == CONFIG_BUTTON_COLUMN_INDEX && e.RowIndex >= 0)
            // {
            //     if (stylerGrid.Rows[e.RowIndex].Tag is BaseStyler styler)
            //     {
            //         // Show configuration dialog for the styler
            //     }
            // }
        }

        /// <summary>
        /// Handles changes to cell values in the styler options grid, specifically the 'Active' checkbox.
        /// To be called by the MainForm's event handler.
        /// </summary>
        public void HandleStylerGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0) // Assuming column 0 is the 'Active' checkbox
            {
                if (stylerGrid.Rows[e.RowIndex].Tag is BaseStyler styler)
                {
                    // Ensure the value is a boolean before casting
                    if (stylerGrid.Rows[e.RowIndex].Cells[0].Value is bool isActive)
                    {
                        styler.Active = isActive;
                        // Settings are now saved centrally on form close

                        // Optional: Trigger immediate re-styling of the active editor if needed
                        // This might require access to the active editor, passed in or retrieved.
                        // ProcessStylersForEditor(MainForm.ActiveEditor, ...);
                    }
                    else
                    {
                         Debug.Log($"Unexpected value type in StylerGrid Active column: {stylerGrid.Rows[e.RowIndex].Cells[0].Value?.GetType()}");
                    }
                }
            }
        }
    }
} 