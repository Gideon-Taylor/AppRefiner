using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

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
        private readonly TreeView definitionsTreeView;
        private readonly List<GoToCodeDefinition> allDefinitions;
        private List<GoToCodeDefinition> filteredDefinitions;
        private GoToCodeDefinition? selectedDefinition;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private bool groupingEnabled = true;

        /// <summary>
        /// Gets the selected definition after dialog closes
        /// </summary>
        public GoToCodeDefinition? SelectedDefinition => selectedDefinition;

        /// <summary>
        /// Initializes a new instance of DefinitionSelectionDialog with a list of code definitions
        /// </summary>
        /// <param name="definitions">The list of code definitions to display</param>
        /// <param name="owner">The owner window handle</param>
        public DefinitionSelectionDialog(List<GoToCodeDefinition> definitions, IntPtr owner)
        {
            this.owner = owner;
            allDefinitions = definitions;
            filteredDefinitions = new List<GoToCodeDefinition>(allDefinitions);
            
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.definitionsTreeView = new TreeView();
            
            InitializeComponent();
            ConfigureForm();
            PopulateDefinitionsTree();
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

            // definitionsTreeView
            this.definitionsTreeView.BorderStyle = BorderStyle.None;
            this.definitionsTreeView.Dock = DockStyle.Fill;
            this.definitionsTreeView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.definitionsTreeView.Location = new Point(0, 32);
            this.definitionsTreeView.Name = "definitionsTreeView";
            this.definitionsTreeView.Size = new Size(550, 318);
            this.definitionsTreeView.TabIndex = 1;
            this.definitionsTreeView.ShowNodeToolTips = true;
            this.definitionsTreeView.HideSelection = false;
            this.definitionsTreeView.ItemHeight = 24;
            this.definitionsTreeView.DoubleClick += new EventHandler(this.DefinitionsTreeView_DoubleClick);
            this.definitionsTreeView.KeyDown += new KeyEventHandler(this.DefinitionsTreeView_KeyDown);
            this.definitionsTreeView.AfterSelect += new TreeViewEventHandler(this.DefinitionsTreeView_AfterSelect);

            // DefinitionSelectionDialog
            this.ClientSize = new Size(550, 380);
            this.Controls.Add(this.definitionsTreeView);
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
            
            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Configure the tree view for better visualization
            definitionsTreeView.ShowLines = true;
            definitionsTreeView.ShowPlusMinus = true;
            definitionsTreeView.ShowRootLines = true;
            definitionsTreeView.FullRowSelect = true;
            
            // Add context menu for toggling grouping
            var contextMenu = new ContextMenuStrip();
            var toggleGroupingItem = new ToolStripMenuItem("Toggle Grouping");
            toggleGroupingItem.Click += (s, e) => {
                groupingEnabled = !groupingEnabled;
                PopulateDefinitionsTree();
            };
            contextMenu.Items.Add(toggleGroupingItem);
            definitionsTreeView.ContextMenuStrip = contextMenu;
        }

        private void PopulateDefinitionsTree()
        {
            definitionsTreeView.BeginUpdate();
            definitionsTreeView.Nodes.Clear();

            if (groupingEnabled)
            {
                // Group by definition type
                var methodsNode = new TreeNode("Methods");
                var propertiesNode = new TreeNode("Properties");
                var functionsNode = new TreeNode("Functions");
                var instanceVariablesNode = new TreeNode("Instance Variables");

                // For methods, further group by scope
                var publicMethodsNode = new TreeNode("Public");
                var protectedMethodsNode = new TreeNode("Protected");
                var privateMethodsNode = new TreeNode("Private");
                methodsNode.Nodes.Add(publicMethodsNode);
                methodsNode.Nodes.Add(protectedMethodsNode);
                methodsNode.Nodes.Add(privateMethodsNode);

                foreach (var definition in filteredDefinitions)
                {
                    var node = new TreeNode(definition.DisplayText);
                    node.Tag = definition;
                    node.ToolTipText = definition.Description;

                    switch (definition.DefinitionType)
                    {
                        case GoToDefinitionType.Method:
                            switch (definition.Scope)
                            {
                                case GoToDefinitionScope.Public:
                                    publicMethodsNode.Nodes.Add(node);
                                    break;
                                case GoToDefinitionScope.Protected:
                                    protectedMethodsNode.Nodes.Add(node);
                                    break;
                                case GoToDefinitionScope.Private:
                                    privateMethodsNode.Nodes.Add(node);
                                    break;
                                default:
                                    methodsNode.Nodes.Add(node);
                                    break;
                            }
                            break;
                        case GoToDefinitionType.Property:
                            propertiesNode.Nodes.Add(node);
                            break;
                        case GoToDefinitionType.Function:
                            functionsNode.Nodes.Add(node);
                            break;
                        case GoToDefinitionType.Getter:
                        case GoToDefinitionType.Setter:
                        default:
                            instanceVariablesNode.Nodes.Add(node);
                            break;
                    }
                }

                // Only add nodes that have children
                if (publicMethodsNode.Nodes.Count > 0 || protectedMethodsNode.Nodes.Count > 0 || privateMethodsNode.Nodes.Count > 0)
                {
                    definitionsTreeView.Nodes.Add(methodsNode);
                }
                if (propertiesNode.Nodes.Count > 0) definitionsTreeView.Nodes.Add(propertiesNode);
                if (functionsNode.Nodes.Count > 0) definitionsTreeView.Nodes.Add(functionsNode);
                if (instanceVariablesNode.Nodes.Count > 0) definitionsTreeView.Nodes.Add(instanceVariablesNode);

                // Expand all nodes initially
                definitionsTreeView.ExpandAll();
            }
            else
            {
                // Flat list without grouping
                foreach (var definition in filteredDefinitions)
                {
                    var node = new TreeNode(definition.DisplayText);
                    node.Tag = definition;
                    node.ToolTipText = definition.Description;
                    definitionsTreeView.Nodes.Add(node);
                }
            }

            definitionsTreeView.EndUpdate();

            // Select first node if available
            if (definitionsTreeView.Nodes.Count > 0)
            {
                SelectFirstLeafNode(definitionsTreeView.Nodes[0]);
            }
        }

        private void SelectFirstLeafNode(TreeNode node)
        {
            if (node.Nodes.Count > 0)
            {
                SelectFirstLeafNode(node.Nodes[0]);
            }
            else
            {
                definitionsTreeView.SelectedNode = node;
            }
        }

        private void FilterDefinitions(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                filteredDefinitions = new List<GoToCodeDefinition>(allDefinitions);
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

            PopulateDefinitionsTree();

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
                definitionsTreeView.Focus();
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

        private void DefinitionsTreeView_DoubleClick(object? sender, EventArgs e)
        {
            SelectDefinition();
        }

        private void DefinitionsTreeView_KeyDown(object? sender, KeyEventArgs e)
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

        private void DefinitionsTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            // If we selected a category node, expand/collapse it
            if (e.Node.Tag == null && e.Action == TreeViewAction.ByMouse)
            {
                if (e.Node.IsExpanded)
                    e.Node.Collapse();
                else
                    e.Node.Expand();
            }
        }

        private void SelectDefinition()
        {
            if (definitionsTreeView.SelectedNode != null && definitionsTreeView.SelectedNode.Tag is GoToCodeDefinition definition)
            {
                selectedDefinition = definition;
                this.DialogResult = DialogResult.OK;
                this.Hide();
                this.Close();
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Dispose the mouse handler
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
} 