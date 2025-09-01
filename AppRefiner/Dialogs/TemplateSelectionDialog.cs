using AppRefiner.Templates;

namespace AppRefiner.Dialogs
{
    public class TemplateSelectionDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TextBox searchBox;
        private readonly ListView templateListView;
        private readonly List<Template> allTemplates;
        private List<Template> filteredTemplates;
        private Template? selectedTemplate;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        public Template? SelectedTemplate => selectedTemplate;

        public TemplateSelectionDialog(List<Template> templates, IntPtr owner)
        {
            this.owner = owner;
            allTemplates = templates;
            filteredTemplates = new List<Template>(allTemplates);
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.templateListView = new ListView();
            InitializeComponent();
            ConfigureForm();
            PopulateTemplateList();
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
            this.headerLabel.Text = "AppRefiner - Select Template";
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

            // templateListView
            this.templateListView.BorderStyle = BorderStyle.None;
            this.templateListView.Dock = DockStyle.Fill;
            this.templateListView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.templateListView.FullRowSelect = true;
            this.templateListView.HeaderStyle = ColumnHeaderStyle.None;
            this.templateListView.HideSelection = false;
            this.templateListView.Location = new Point(0, 32);
            this.templateListView.Name = "templateListView";
            this.templateListView.Size = new Size(550, 318);
            this.templateListView.TabIndex = 1;
            this.templateListView.UseCompatibleStateImageBehavior = false;
            this.templateListView.View = View.Tile;
            this.templateListView.MouseDoubleClick += new MouseEventHandler(this.TemplateListView_MouseDoubleClick);
            this.templateListView.KeyDown += new KeyEventHandler(this.TemplateListView_KeyDown);

            // TemplateSelectionDialog
            this.ClientSize = new Size(550, 380);
            this.Controls.Add(this.templateListView);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "Select Template";
            this.ShowInTaskbar = false;

            // Add background color to make dialog stand out
            this.BackColor = Color.FromArgb(240, 240, 245);

            // Add a 1-pixel border to make the dialog visually distinct
            this.Padding = new Padding(1);

            // Add resize event handler to update tile size when form is resized
            this.Resize += new EventHandler(this.TemplateSelectionDialog_Resize);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Change the view to TileView to show both title and description
            templateListView.View = View.Tile;
            templateListView.TileSize = new Size(templateListView.Width - 25, 50);

            // Configure the list view for title and description
            templateListView.Columns.Add("Name", 250);
            templateListView.Columns.Add("Description", 250);

            // Show item tooltips
            templateListView.ShowItemToolTips = true;
        }

        private void PopulateTemplateList()
        {
            templateListView.Items.Clear();

            foreach (var template in filteredTemplates)
            {
                var item = new ListViewItem(template.TemplateName);
                item.SubItems.Add(template.Description);
                item.Tag = template;

                // Add tooltip with description
                item.ToolTipText = template.Description;

                templateListView.Items.Add(item);
            }

            if (templateListView.Items.Count > 0)
            {
                templateListView.Items[0].Selected = true;
            }
        }

        private void FilterTemplates(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredTemplates = new List<Template>(allTemplates);
            }
            else
            {
                filter = filter.ToLower();
                filteredTemplates = allTemplates
                    .Where(t => t.TemplateName.ToLower().Contains(filter) ||
                                t.Description.ToLower().Contains(filter))
                    .ToList();
            }

            PopulateTemplateList();

            if (templateListView.Items.Count > 0)
            {
                templateListView.Items[0].Selected = true;
                templateListView.EnsureVisible(0);
            }

            // Ensure focus stays in the search box
            searchBox.Focus();
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            FilterTemplates(searchBox.Text);
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (templateListView.Items.Count > 0)
                {
                    int index = 0;
                    if (templateListView.SelectedIndices.Count > 0)
                    {
                        index = templateListView.SelectedIndices[0] + 1;
                        if (index >= templateListView.Items.Count)
                            index = 0;
                    }
                    templateListView.SelectedIndices.Clear();
                    templateListView.Items[index].Selected = true;
                    templateListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (templateListView.Items.Count > 0)
                {
                    int index = templateListView.Items.Count - 1;
                    if (templateListView.SelectedIndices.Count > 0)
                    {
                        index = templateListView.SelectedIndices[0] - 1;
                        if (index < 0)
                            index = templateListView.Items.Count - 1;
                    }
                    templateListView.SelectedIndices.Clear();
                    templateListView.Items[index].Selected = true;
                    templateListView.EnsureVisible(index);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                SelectTemplate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
        }

        private void TemplateListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            SelectTemplate();
        }

        private void TemplateListView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SelectTemplate();
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

        private void SelectTemplate()
        {
            if (templateListView.SelectedItems.Count > 0)
            {
                selectedTemplate = (Template?)templateListView.SelectedItems[0].Tag;

                if (selectedTemplate != null)
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

        private void TemplateSelectionDialog_Resize(object? sender, EventArgs e)
        {
            // Update tile size when form is resized to prevent horizontal scrolling
            templateListView.TileSize = new Size(templateListView.Width - 25, 50);
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
