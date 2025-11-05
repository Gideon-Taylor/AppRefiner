// =============================================================================
// CODE SNIPPETS TO ADD TO MainForm.cs FOR THEME INTEGRATION
// =============================================================================
// These snippets show what needs to be added to MainForm.cs after you've
// created cmbTheme and chkFilled controls in the Form Designer.
// =============================================================================

// -------------------------------------------------------------------------
// SNIPPET 1: Add after line 192 (after chkLineSelectionFix.Checked = ...)
// This populates the theme combo box and loads theme settings
// -------------------------------------------------------------------------
/*
            // Initialize theme combo box with all available themes
            cmbTheme.Items.Clear();
            foreach (var theme in Enum.GetNames(typeof(Theme)))
            {
                cmbTheme.Items.Add(theme);
            }

            // Load theme settings
            if (Enum.TryParse<Theme>(generalSettings.Theme, out var selectedTheme))
            {
                cmbTheme.SelectedItem = selectedTheme.ToString();
            }
            else
            {
                cmbTheme.SelectedItem = Theme.Default.ToString();
            }

            chkFilled.Checked = generalSettings.ThemeFilled;
*/

// -------------------------------------------------------------------------
// SNIPPET 2: Add after line 303 (after chkLineSelectionFix.CheckedChanged += ...)
// This wires up the event handlers for theme controls
// -------------------------------------------------------------------------
/*
            // Theme controls
            cmbTheme.SelectedIndexChanged += ThemeSetting_Changed;
            chkFilled.CheckedChanged += ThemeSetting_Changed;
*/

// -------------------------------------------------------------------------
// SNIPPET 3: Add to GetGeneralSettingsObject() method (after line 423)
// This includes theme settings when saving
// -------------------------------------------------------------------------
/*
                LineSelectionFix = chkLineSelectionFix.Checked,
                Theme = cmbTheme.SelectedItem?.ToString() ?? Theme.Default.ToString(),
                ThemeFilled = chkFilled.Checked
*/

// -------------------------------------------------------------------------
// SNIPPET 4: Add as a new method anywhere in the MainForm class
// This handles theme setting changes and applies to all processes
// -------------------------------------------------------------------------
/*
        /// <summary>
        /// Handles changes to theme-related settings (combo box or checkbox)
        /// </summary>
        private void ThemeSetting_Changed(object? sender, EventArgs e)
        {
            if (isLoadingSettings) return;
            if (cmbTheme.SelectedItem == null) return;

            // Parse the selected theme
            if (!Enum.TryParse<Theme>(cmbTheme.SelectedItem.ToString(), out var selectedTheme))
            {
                Debug.Log("Invalid theme selected");
                return;
            }

            // Determine the theme style based on checkbox
            var themeStyle = chkFilled.Checked ? ThemeStyle.Filled : ThemeStyle.Outline;

            Debug.Log($"Applying theme: {selectedTheme} with style: {themeStyle}");

            // Apply the theme to all known AppDesigner processes
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                try
                {
                    bool success = ThemeManager.ApplyTheme(appDesigner, selectedTheme, themeStyle);
                    if (success)
                    {
                        Debug.Log($"Successfully applied theme to process {appDesigner.ProcessId}");
                    }
                    else
                    {
                        Debug.Log($"Failed to fully apply theme to process {appDesigner.ProcessId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Error applying theme to process {appDesigner.ProcessId}");
                }
            }

            // Save the settings
            SaveSettings();
        }
*/

// -------------------------------------------------------------------------
// SNIPPET 5: Add as a new helper method in MainForm class
// This applies the current theme to a newly created AppDesigner process
// -------------------------------------------------------------------------
/*
        /// <summary>
        /// Applies the current theme settings to a newly created AppDesigner process
        /// </summary>
        /// <param name="process">The AppDesigner process to apply the theme to</param>
        private void ApplyCurrentThemeToProcess(AppDesignerProcess process)
        {
            if (process == null || cmbTheme.SelectedItem == null) return;

            if (Enum.TryParse<Theme>(cmbTheme.SelectedItem.ToString(), out var theme))
            {
                var style = chkFilled.Checked ? ThemeStyle.Filled : ThemeStyle.Outline;
                try
                {
                    ThemeManager.ApplyTheme(process, theme, style);
                    Debug.Log($"Applied theme {theme} ({style}) to new process {process.ProcessId}");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Failed to apply theme to process {process.ProcessId}");
                }
            }
        }
*/

// -------------------------------------------------------------------------
// SNIPPET 6: IMPORTANT - Call ApplyCurrentThemeToProcess after creating processes
// Add this line AFTER these locations:
// - Line 1870: After "var newProcess = new AppDesignerProcess(...)"
//   Add: ApplyCurrentThemeToProcess(newProcess);
//
// - Line 3696: After "var newProcess = new AppDesignerProcess(...)"
//   Add: ApplyCurrentThemeToProcess(newProcess);
// -------------------------------------------------------------------------

// =============================================================================
// FORM DESIGNER CONTROLS NEEDED:
// =============================================================================
// 1. ComboBox: cmbTheme
//    - DropDownStyle: DropDownList
//    - Location: Place near other settings controls
//    - Add a Label: "Theme:" next to it
//
// 2. CheckBox: chkFilled
//    - Text: "Filled Icons"
//    - Location: Place near cmbTheme
// =============================================================================
