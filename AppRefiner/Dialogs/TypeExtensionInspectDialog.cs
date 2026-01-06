namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for inspecting the transforms (methods and properties) provided by a type extension
    /// </summary>
    public partial class TypeExtensionInspectDialog : Form
    {
        private readonly LanguageExtensions.BaseTypeExtension extension;

        /// <summary>
        /// Initializes a new instance of TypeExtensionInspectDialog
        /// </summary>
        /// <param name="typeExtension">The type extension to inspect</param>
        public TypeExtensionInspectDialog(LanguageExtensions.BaseTypeExtension typeExtension)
        {
            InitializeComponent();
            extension = typeExtension ?? throw new ArgumentNullException(nameof(typeExtension));

            // Set dialog title with target type name
            this.Text = $"Inspect Extension: {extension.TargetType.Name}";

            InitializeDataGridViews();
            LoadTransforms();
        }

        /// <summary>
        /// Configures the DataGridView controls for display
        /// </summary>
        private void InitializeDataGridViews()
        {
            // Configure Methods grid
            ConfigureDataGridView(methodsDataGridView, "Methods");

            // Configure Properties grid
            ConfigureDataGridView(propertiesDataGridView, "Properties");
        }

        /// <summary>
        /// Applies common configuration to a DataGridView
        /// </summary>
        private void ConfigureDataGridView(DataGridView grid, string gridName)
        {
            grid.AutoGenerateColumns = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = SystemColors.Window;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Add columns
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Signature",
                HeaderText = "Signature",
                DataPropertyName = "Signature",
                Width = 400,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                DataPropertyName = "Description",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
        }

        /// <summary>
        /// Loads transforms from the extension and populates the grids
        /// </summary>
        private void LoadTransforms()
        {
            var methods = new List<TransformDisplayItem>();
            var properties = new List<TransformDisplayItem>();

            // Separate transforms by type
            foreach (var transform in extension.Transforms)
            {
                var item = new TransformDisplayItem
                {
                    Signature = transform.Signature,
                    Description = transform.Description
                };

                if (transform.ExtensionType == LanguageExtensions.LanguageExtensionType.Method)
                {
                    methods.Add(item);
                }
                else // Property
                {
                    properties.Add(item);
                }
            }

            // Bind to grids
            methodsDataGridView.DataSource = methods;
            propertiesDataGridView.DataSource = properties;

            // Update group box labels with counts
            methodsGroupBox.Text = $"Methods ({methods.Count})";
            propertiesGroupBox.Text = $"Properties ({properties.Count})";
        }

        /// <summary>
        /// Helper class for binding transform data to DataGridView
        /// </summary>
        private class TransformDisplayItem
        {
            public string Signature { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        #region Event Handlers

        /// <summary>
        /// Handles the Close button click
        /// </summary>
        private void CloseButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        #endregion
    }
}
