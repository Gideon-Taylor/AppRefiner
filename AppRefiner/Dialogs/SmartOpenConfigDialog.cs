namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for configuring Smart Open settings including definition types and search options
    /// </summary>
    public partial class SmartOpenConfigDialog : Form
    {
        private SmartOpenConfig config;
        private Dictionary<string, CheckBox> definitionTypeCheckBoxes = new Dictionary<string, CheckBox>();

        /// <summary>
        /// Gets the configured Smart Open settings
        /// </summary>
        public SmartOpenConfig Configuration => config;

        /// <summary>
        /// Initializes a new instance of SmartOpenConfigDialog
        /// </summary>
        /// <param name="currentConfig">The current Smart Open configuration</param>
        public SmartOpenConfigDialog(SmartOpenConfig currentConfig)
        {
            InitializeComponent();
            config = currentConfig ?? SmartOpenConfig.GetDefault();
            InitializeDefinitionTypeCheckBoxes();
            LoadCurrentSettings();
        }

        /// <summary>
        /// Creates checkboxes for all definition types in a scrollable panel
        /// </summary>
        private void InitializeDefinitionTypeCheckBoxes()
        {
            definitionTypesPanel.SuspendLayout();
            definitionTypesPanel.Controls.Clear();
            definitionTypeCheckBoxes.Clear();

            var definitionTypes = SmartOpenConfig.GetAllDefinitionTypes();
            const int checkBoxHeight = 22;
            const int checkBoxSpacing = 25;
            const int leftMargin = 8;
            const int columnsCount = 2;
            const int columnWidth = 270;

            for (int i = 0; i < definitionTypes.Length; i++)
            {
                var definitionType = definitionTypes[i];
                var checkBox = new CheckBox
                {
                    Text = definitionType,
                    AutoSize = false,
                    Size = new Size(columnWidth - 20, checkBoxHeight),
                    Location = new Point(
                        leftMargin + (i % columnsCount) * columnWidth,
                        8 + (i / columnsCount) * checkBoxSpacing),
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
                    UseVisualStyleBackColor = true
                };

                definitionTypeCheckBoxes[definitionType] = checkBox;
                definitionTypesPanel.Controls.Add(checkBox);
            }

            // Set the panel's auto-scroll minimum size based on content
            int totalRows = (definitionTypes.Length + columnsCount - 1) / columnsCount;
            int contentHeight = totalRows * checkBoxSpacing + 16;
            definitionTypesPanel.AutoScrollMinSize = new Size(0, contentHeight);

            definitionTypesPanel.ResumeLayout();
        }

        /// <summary>
        /// Loads the current settings into the dialog controls
        /// </summary>
        private void LoadCurrentSettings()
        {
            // Load definition type settings
            foreach (var kvp in definitionTypeCheckBoxes)
            {
                if (config.EnabledTypes.TryGetValue(kvp.Key, out bool enabled))
                {
                    kvp.Value.Checked = enabled;
                }
                else
                {
                    // Default to enabled if not specified
                    kvp.Value.Checked = true;
                }
            }

            // Load other settings
            maxResultsNumericUpDown.Value = Math.Max(1, Math.Min(1000, config.MaxResultsPerType));
            sortByLastUpdateCheckBox.Checked = config.SortByLastUpdate;
        }

        /// <summary>
        /// Saves the current dialog settings to the configuration object
        /// </summary>
        private void SaveCurrentSettings()
        {
            // Save definition type settings
            config.EnabledTypes.Clear();
            foreach (var kvp in definitionTypeCheckBoxes)
            {
                config.EnabledTypes[kvp.Key] = kvp.Value.Checked;
            }

            // Save other settings
            config.MaxResultsPerType = (int)maxResultsNumericUpDown.Value;
            config.SortByLastUpdate = sortByLastUpdateCheckBox.Checked;
        }

        #region Event Handlers

        /// <summary>
        /// Handles the Select All button click
        /// </summary>
        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            foreach (var checkBox in definitionTypeCheckBoxes.Values)
            {
                checkBox.Checked = true;
            }
        }

        /// <summary>
        /// Handles the Select None button click
        /// </summary>
        private void SelectNoneButton_Click(object sender, EventArgs e)
        {
            foreach (var checkBox in definitionTypeCheckBoxes.Values)
            {
                checkBox.Checked = false;
            }
        }

        /// <summary>
        /// Handles the OK button click
        /// </summary>
        private void OkButton_Click(object sender, EventArgs e)
        {
            // Validate that at least one definition type is selected
            bool anySelected = definitionTypeCheckBoxes.Values.Any(cb => cb.Checked);
            if (!anySelected)
            {
                Task.Delay(100).ContinueWith(_ =>
                {
                    var mainHandle = this.Owner?.Handle ?? IntPtr.Zero;
                    if (mainHandle == IntPtr.Zero)
                    {
                        // Fallback if no owner
                        MessageBox.Show(this, "Please select at least one definition type to include in searches.", 
                            "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog("Please select at least one definition type to include in searches.", 
                            "Configuration Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    }
                });
                return;
            }

            // Save settings and close dialog
            SaveCurrentSettings();
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion

        #region WindowWrapper Helper Class

        /// <summary>
        /// Helper class to wrap a window handle for use as a dialog owner
        /// </summary>
        private class WindowWrapper : IWin32Window
        {
            public IntPtr Handle { get; }

            public WindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }
        }

        #endregion
    }
}