using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AppRefiner.Commands;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for selecting a code definition to navigate to
    /// </summary>
    public class DefinitionSelectionDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TextBox searchBox;
        private readonly ListView definitionsListView;
        private readonly List<CodeDefinition> allDefinitions;
        private List<CodeDefinition> filteredDefinitions;
        private CodeDefinition? selectedDefinition;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        /// <summary>
        /// Gets the selected definition after dialog closes
        /// </summary>
        public CodeDefinition? SelectedDefinition => selectedDefinition;

        /// <summary>
        /// Initializes a new instance of DefinitionSelectionDialog with a list of code definitions
        /// </summary>
        /// <param name="definitions">The list of code definitions to display</param>
        /// <param name="owner">The owner window handle</param>
        public DefinitionSelectionDialog(List<CodeDefinition> definitions, IntPtr owner)
        {
            this.owner = owner;
            allDefinitions = definitions;
            filteredDefinitions = new List<CodeDefinition>(allDefinitions);
            
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.definitionsListView = new ListView();
            
            InitializeComponent();
            ConfigureForm();
            PopulateDefinitionsList();
        }

        private void InitializeComponent()
        {   
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "AppRefiner - Go To Definition";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // searchBox
            this.searchBox.BorderStyle = BorderStyle.FixedSingle;
            this.searchBox.Dock = DockStyle.Top;
            this.searchBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            this.searchBox.Location = new Point(0, 0);
            this.searchBox.Margin = new Padding(0);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new Size(550, 32);
            this.searchBox.TabIndex = 0;
            this.searchBox.TextChanged += new EventHandler(this.SearchBox_TextChanged);
            this.searchBox.KeyDown += new KeyEventHandler(this.SearchBox_KeyDown);

            // definitionsListView
            this.definitionsListView.BorderStyle = BorderStyle.None;
            this.definitionsListView.Dock = DockStyle.Fill;
            this.definitionsListView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.definitionsListView.FullRowSelect = true;
            this.definitionsListView.HeaderStyle = ColumnHeaderStyle.None;
            this.definitionsListView.HideSelection = false;
            this.definitionsListView.Location = new Point(0, 32);
            this.definitionsListView.Name = "definitionsListView";
            this.definitionsListView.Size = new Size(550, 318);
            this.definitionsListView.TabIndex = 1;
            this.definitionsListView.UseCompatibleStateImageBehavior = false;
            this.definitionsListView.View = View.Tile;
            this.definitionsListView.MouseDoubleClick += new MouseEventHandler(this.DefinitionsListView_MouseDoubleClick);
            this.definitionsListView.KeyDown += new KeyEventHandler(this.DefinitionsListView_KeyDown);

            // DefinitionSelectionDialog
            this.ClientSize = new Size(550, 380);
            this.Controls.Add(this.definitionsListView);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "Go To Definition";
            this.ShowInTaskbar = false;
            
            // Add background color to make dialog stand out
            this.BackColor = Color.FromArgb(240, 240, 245);
            
            // Add a 1-pixel border to make the dialog visually distinct
            this.Padding = new Padding(1);
            
            // Add resize event handler to update tile size when form is resized
            this.Resize += new EventHandler(this.DefinitionSelectionDialog_Resize);
            
            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Change the view to TileView to show both title and description
            definitionsListView.View = View.Tile;
            definitionsListView.TileSize = new Size(definitionsListView.Width - 25, 50);

            // Configure the list view for title and description
            definitionsListView.Columns.Add("Name", 250);
            definitionsListView.Columns.Add("Description", 250);

            // Show item tooltips
            definitionsListView.ShowItemToolTips = true;
        }

        private void PopulateDefinitionsList()
        {
            definitionsListView.Items.Clear();

            foreach (var definition in filteredDefinitions)
            {
                var item = new ListViewItem(definition.DisplayText);
                item.SubItems.Add(definition.Description);
                item.Tag = definition;

                // Add tooltip with description
                item.ToolTipText = definition.Description;

                definitionsListView.Items.Add(item);
            }

            if (definitionsListView.Items.Count > 0)
            {
                definitionsListView.Items[0].Selected = true;
            }
        }

        private void FilterDefinitions(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredDefinitions = new List<CodeDefinition>(allDefinitions);
            }
            else
            {
                filter = filter.ToLower();
                filteredDefinitions = allDefinitions
                    .Where(d => d.Name.ToLower().Contains(filter) ||
                                d.Type.ToLower().Contains(filter) ||
                                d.DefinitionType.ToString().ToLower().Contains(filter) ||
                                d.Scope.ToString().ToLower().Contains(filter))
                    .ToList();
            }

            PopulateDefinitionsList();

            if (definitionsListView.Items.Count > 0)
            {
                definitionsListView.Items[0].Selected = true;
                definitionsListView.EnsureVisible(0);
            }

            // Ensure focus stays in the search box
            searchBox.Focus();
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            FilterDefinitions(searchBox.Text);
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (definitionsListView.Items.Count > 0)
                {
                    int index = 0;
                    if (definitionsListView.SelectedIndices.Count > 0)
                    {
                        index = definitionsListView.SelectedIndices[0] + 1;
                        if (index >= definitionsListView.Items.Count)
                            index = 0;
                    }
                    definitionsListView.SelectedIndices.Clear();
                    definitionsListView.Items[index].Selected = true;
                    definitionsListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (definitionsListView.Items.Count > 0)
                {
                    int index = definitionsListView.Items.Count - 1;
                    if (definitionsListView.SelectedIndices.Count > 0)
                    {
                        index = definitionsListView.SelectedIndices[0] - 1;
                        if (index < 0)
                            index = definitionsListView.Items.Count - 1;
                    }
                    definitionsListView.SelectedIndices.Clear();
                    definitionsListView.Items[index].Selected = true;
                    definitionsListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                SelectDefinition();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
        }

        private void DefinitionsListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            SelectDefinition();
        }

        private void DefinitionsListView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SelectDefinition();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
            else if (e.KeyData == Keys.Tab || e.KeyData == (Keys.Tab | Keys.Shift))
            {
                // Send focus back to search box
                searchBox.Focus();
                e.Handled = true;
            }
        }

        private void SelectDefinition()
        {
            if (definitionsListView.SelectedItems.Count > 0)
            {
                selectedDefinition = (CodeDefinition?)definitionsListView.SelectedItems[0].Tag;
                
                if (selectedDefinition != null)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Hide();
                    this.Close();
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw a border around the form
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, 
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Left
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Top
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid,    // Right
                Color.FromArgb(100, 100, 120), // Border color
                1, ButtonBorderStyle.Solid);   // Bottom
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            searchBox.Focus();
            
            // Center on owner window
            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }
            
            // Create the mouse handler if this is a modal dialog
            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        private void DefinitionSelectionDialog_Resize(object? sender, EventArgs e)
        {
            // Update tile size when form is resized to prevent horizontal scrolling
            definitionsListView.TileSize = new Size(definitionsListView.Width - 25, 50);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
} 