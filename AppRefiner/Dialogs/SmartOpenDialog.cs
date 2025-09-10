using AppRefiner.Database;
using AppRefiner.Database.Models;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static SqlParser.Ast.DataType;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for searching and opening PeopleSoft definitions using Smart Open functionality
    /// </summary>
    public class SmartOpenDialog : Form
    {
        #region Private Fields

        // Win32 API imports for keyboard events
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int VK_CONTROL = 0x11;
        private const int VK_O = 0x4F;
        
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TableLayoutPanel searchPanel;
        private readonly Label idSearchLabel;
        private readonly TextBox idSearchBox;
        private readonly Label descrSearchLabel;
        private readonly TextBox descrSearchBox;
        private readonly TreeView targetsTreeView;
        private readonly Func<OpenTargetSearchOptions, List<OpenTarget>> searchFunction;
        private readonly Action bypassAction;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        
        // Timer for implementing typing delay
        private readonly System.Windows.Forms.Timer searchTimer;
        
        private List<OpenTarget> allTargets = new List<OpenTarget>();
        private List<OpenTarget> filteredTargets = new List<OpenTarget>();
        private OpenTarget? selectedTarget;
        private SmartOpenConfig config;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the selected target after dialog closes
        /// </summary>
        public OpenTarget? SelectedTarget => selectedTarget;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of SmartOpenDialog
        /// </summary>
        /// <param name="searchFunction">Function to search for targets</param>
        /// <param name="owner">Owner window handle</param>
        /// <param name="bypassAction">Action to call for bypassing smart open</param>
        public SmartOpenDialog(
            Func<OpenTargetSearchOptions, List<OpenTarget>> searchFunction, 
            IntPtr owner,
            Action bypassAction)
        {
            this.searchFunction = searchFunction ?? throw new ArgumentNullException(nameof(searchFunction));
            this.bypassAction = bypassAction ?? throw new ArgumentNullException(nameof(bypassAction));
            this.owner = owner;

            // Load SmartOpen configuration
            var settingsService = new SettingsService();
            config = settingsService.LoadSmartOpenConfig();

            // Initialize UI components
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchPanel = new TableLayoutPanel();
            this.idSearchLabel = new Label();
            this.idSearchBox = new TextBox();
            this.descrSearchLabel = new Label();
            this.descrSearchBox = new TextBox();
            this.targetsTreeView = new TreeView();
            
            // Initialize search timer
            this.searchTimer = new System.Windows.Forms.Timer();
            this.searchTimer.Interval = 300; // 300ms delay
            this.searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();
            ConfigureForm();
            
            // Initialize with helpful placeholder message
            ShowPlaceholderMessage();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 35;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "AppRefiner - Smart Open";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // searchPanel
            this.searchPanel.ColumnCount = 4;
            this.searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this.searchPanel.Dock = DockStyle.Top;
            this.searchPanel.Location = new Point(0, 35);
            this.searchPanel.Name = "searchPanel";
            this.searchPanel.RowCount = 1;
            this.searchPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.searchPanel.Size = new Size(600, 35);
            this.searchPanel.TabIndex = 0;
            this.searchPanel.Padding = new Padding(8, 6, 8, 6);

            // idSearchLabel
            this.idSearchLabel.Anchor = AnchorStyles.Left;
            this.idSearchLabel.AutoSize = true;
            this.idSearchLabel.Location = new Point(8, 12);
            this.idSearchLabel.Margin = new Padding(0, 0, 8, 0);
            this.idSearchLabel.Name = "idSearchLabel";
            this.idSearchLabel.Size = new Size(21, 15);
            this.idSearchLabel.TabIndex = 0;
            this.idSearchLabel.Text = "ID:";

            // idSearchBox
            this.idSearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.idSearchBox.BorderStyle = BorderStyle.FixedSingle;
            this.idSearchBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.idSearchBox.Location = new Point(37, 8);
            this.idSearchBox.Name = "idSearchBox";
            this.idSearchBox.PlaceholderText = "Search ID...";
            this.idSearchBox.Size = new Size(260, 23);
            this.idSearchBox.TabIndex = 1;
            this.idSearchBox.TextChanged += SearchBox_TextChanged;
            this.idSearchBox.KeyDown += SearchBox_KeyDown;

            // descrSearchLabel
            this.descrSearchLabel.Anchor = AnchorStyles.Left;
            this.descrSearchLabel.AutoSize = true;
            this.descrSearchLabel.Location = new Point(305, 12);
            this.descrSearchLabel.Margin = new Padding(8, 0, 8, 0);
            this.descrSearchLabel.Name = "descrSearchLabel";
            this.descrSearchLabel.Size = new Size(70, 15);
            this.descrSearchLabel.TabIndex = 2;
            this.descrSearchLabel.Text = "Description:";

            // descrSearchBox
            this.descrSearchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.descrSearchBox.BorderStyle = BorderStyle.FixedSingle;
            this.descrSearchBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.descrSearchBox.Location = new Point(383, 8);
            this.descrSearchBox.Name = "descrSearchBox";
            this.descrSearchBox.PlaceholderText = "Search description...";
            this.descrSearchBox.Size = new Size(209, 23);
            this.descrSearchBox.TabIndex = 3;
            this.descrSearchBox.TextChanged += SearchBox_TextChanged;
            this.descrSearchBox.KeyDown += SearchBox_KeyDown;

            // Add controls to searchPanel
            this.searchPanel.Controls.Add(this.idSearchLabel, 0, 0);
            this.searchPanel.Controls.Add(this.idSearchBox, 1, 0);
            this.searchPanel.Controls.Add(this.descrSearchLabel, 2, 0);
            this.searchPanel.Controls.Add(this.descrSearchBox, 3, 0);

            // targetsTreeView
            this.targetsTreeView.BorderStyle = BorderStyle.None;
            this.targetsTreeView.Dock = DockStyle.Fill;
            this.targetsTreeView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.targetsTreeView.Location = new Point(0, 70);
            this.targetsTreeView.Name = "targetsTreeView";
            this.targetsTreeView.Size = new Size(600, 330);
            this.targetsTreeView.TabIndex = 4;
            this.targetsTreeView.ShowNodeToolTips = true;
            this.targetsTreeView.HideSelection = false;
            this.targetsTreeView.ItemHeight = 22;
            this.targetsTreeView.ShowLines = true;
            this.targetsTreeView.ShowPlusMinus = true;
            this.targetsTreeView.ShowRootLines = true;
            this.targetsTreeView.FullRowSelect = true;
            this.targetsTreeView.DoubleClick += TargetsTreeView_DoubleClick;
            this.targetsTreeView.KeyDown += TargetsTreeView_KeyDown;
            this.targetsTreeView.AfterSelect += TargetsTreeView_AfterSelect;

            // SmartOpenDialog
            this.ClientSize = new Size(600, 400);
            this.Controls.Add(this.targetsTreeView);
            this.Controls.Add(this.searchPanel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Text = "Smart Open";
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ConfigureForm()
        {
            // Add context menu for bypassing smart open
            var contextMenu = new ContextMenuStrip();
            var bypassItem = new ToolStripMenuItem("Use Application Designer Open Dialog...");
            bypassItem.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Hide();
                bypassAction?.Invoke();
                this.Close();
            };
            contextMenu.Items.Add(bypassItem);
            
            var separatorItem = new ToolStripSeparator();
            contextMenu.Items.Add(separatorItem);
            
            var configItem = new ToolStripMenuItem("Smart Open Settings...");
            configItem.Click += (s, e) =>
            {
                // TODO: Open SmartOpenConfigDialog
                // For now, just close this dialog
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            contextMenu.Items.Add(configItem);
            
            this.ContextMenuStrip = contextMenu;
            targetsTreeView.ContextMenuStrip = contextMenu;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the selected target from the dialog
        /// </summary>
        /// <returns>The selected OpenTarget, or null if none selected</returns>
        public OpenTarget? GetSelectedTarget()
        {
            return selectedTarget;
        }

        #endregion

        #region Search and Filtering

        private void PerformSearch()
        {
            try
            {
                // Convert SmartOpenConfig to OpenTargetSearchOptions with search terms
                var searchOptions = CreateSearchOptionsFromConfig();

                /* What's a good way to manage these */

                if (idSearchBox.Text.Contains(' '))
                {
                    var parts = idSearchBox.Text.Split(' ');
                    searchOptions.IDSearchTerm = parts[0];
                    searchOptions.DescriptionSearchTerm = parts[1] + "%" + descrSearchBox.Text;
                } else
                {
                    searchOptions.IDSearchTerm = idSearchBox.Text;
                    searchOptions.DescriptionSearchTerm = descrSearchBox.Text;
                }
                
                // Get results from the search function
                allTargets = searchFunction(searchOptions);
                
                // No need to filter anymore since the database query handles filtering
                filteredTargets = allTargets.ToList();
                
                // Populate the tree view
                PopulateTargetsTree();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error performing Smart Open search");
                
                // Clear the tree and show error
                targetsTreeView.Nodes.Clear();
                var errorNode = new TreeNode($"Error: {ex.Message}");
                errorNode.ForeColor = Color.Red;
                targetsTreeView.Nodes.Add(errorNode);
            }
        }

        private OpenTargetSearchOptions CreateSearchOptionsFromConfig()
        {
            // Convert enabled type strings to OpenTargetType enum values
            var enabledTypes = new HashSet<OpenTargetType>();
            
            foreach (var (typeName, isEnabled) in config.EnabledTypes)
            {
                if (isEnabled && IDataManager.TryMapStringToTargetType(typeName, out OpenTargetType targetType))
                {
                    enabledTypes.Add(targetType);
                }
            }
            
            return new OpenTargetSearchOptions(
                enabledTypes,
                config.MaxResultsPerType,
                config.SortByLastUpdate);
        }

        


        #endregion

        #region Tree View Population

        private void ShowPlaceholderMessage()
        {
            targetsTreeView.BeginUpdate();
            targetsTreeView.Nodes.Clear();
            
            var placeholderNode = new TreeNode("Start typing to search for definitions...");
            placeholderNode.ForeColor = Color.Gray;
            targetsTreeView.Nodes.Add(placeholderNode);
            
            targetsTreeView.EndUpdate();
        }

        private void PopulateTargetsTree()
        {
            targetsTreeView.BeginUpdate();
            targetsTreeView.Nodes.Clear();

            if (filteredTargets.Count == 0)
            {
                var noResultsNode = new TreeNode("No results found");
                noResultsNode.ForeColor = Color.Gray;
                targetsTreeView.Nodes.Add(noResultsNode);
                targetsTreeView.EndUpdate();
                return;
            }

            // Group targets by type
            var groupedTargets = filteredTargets
                .GroupBy(t => t.Type)
                .OrderBy(g => Regex.Replace(g.Key.ToString(), "([a-z])([A-Z])", "$1 $2"));

            foreach (var group in groupedTargets)
            {
                // Create group node
                var groupName = Regex.Replace(group.Key.ToString(), "([a-z])([A-Z])", "$1 $2");
                var groupNode = new TreeNode($"{groupName} ({group.Count()})");
                groupNode.Tag = null; // Group nodes have no target data
                groupNode.NodeFont = new Font(targetsTreeView.Font, FontStyle.Bold);

                // Add individual target nodes
                var sortedTargets = config.SortByLastUpdate 
                    ? group.OrderByDescending(t => GetTargetLastUpdate(t))
                    : group.OrderBy(t => t.Name);

                var limitedTargets = sortedTargets.Take(config.MaxResultsPerType);

                foreach (var target in limitedTargets)
                {
                    var targetNode = new TreeNode(target.Name);
                    targetNode.Tag = target;
                    targetNode.ToolTipText = !string.IsNullOrEmpty(target.Description) 
                        ? $"{target.Name}\n{target.Description}\nPath: {target.Path}"
                        : $"{target.Name}\nPath: {target.Path}";
                    
                    groupNode.Nodes.Add(targetNode);
                }

                targetsTreeView.Nodes.Add(groupNode);
            }

            // Expand all groups initially
            targetsTreeView.ExpandAll();

            // Select first target node if available
            SelectFirstTargetNode();

            targetsTreeView.EndUpdate();
        }

        private DateTime GetTargetLastUpdate(OpenTarget target)
        {
            // TODO: If we had last update information, we'd use it here
            // For now, return a default value
            return DateTime.MinValue;
        }

        private void SelectFirstTargetNode()
        {
            foreach (TreeNode groupNode in targetsTreeView.Nodes)
            {
                if (groupNode.Nodes.Count > 0)
                {
                    targetsTreeView.SelectedNode = groupNode.Nodes[0];
                    return;
                }
            }
        }

        #endregion

        #region Event Handlers

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            // Reset the timer - this implements the 300ms typing delay
            searchTimer.Stop();
            searchTimer.Start();
        }

        private void SearchTimer_Tick(object? sender, EventArgs e)
        {
            // Stop the timer and perform the search
            searchTimer.Stop();
            PerformSearch();
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down:
                    targetsTreeView.Focus();
                    e.Handled = true;
                    break;
                    
                case Keys.Enter:
                    // If there's text in either search box, perform immediate search, otherwise select target
                    if (!string.IsNullOrEmpty(idSearchBox.Text) || !string.IsNullOrEmpty(descrSearchBox.Text))
                    {
                        searchTimer.Stop();
                        PerformSearch();
                    }
                    else
                    {
                        SelectTarget();
                    }
                    e.Handled = true;
                    break;
                    
                case Keys.Escape:
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    e.Handled = true;
                    break;
            }
        }

        private void TargetsTreeView_DoubleClick(object? sender, EventArgs e)
        {
            SelectTarget();
        }

        private void TargetsTreeView_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    SelectTarget();
                    e.Handled = true;
                    break;
                    
                case Keys.Escape:
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    e.Handled = true;
                    break;
                    
                case Keys.Tab:
                case Keys.Tab | Keys.Shift:
                    idSearchBox.Focus();
                    e.Handled = true;
                    break;
                    
                case Keys.Left:
                    HandleLeftArrowNavigation();
                    e.Handled = true;
                    break;
            }
        }

        private void TargetsTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            // If we selected a group node, expand/collapse it
            if (e.Node?.Tag == null && e.Action == TreeViewAction.ByMouse && e.Node != null)
            {
                if (e.Node.IsExpanded)
                    e.Node.Collapse();
                else
                    e.Node.Expand();
            }
        }

        #endregion

        #region Navigation Methods

        private void HandleLeftArrowNavigation()
        {
            var selectedNode = targetsTreeView.SelectedNode;
            if (selectedNode == null) return;

            // If this is a target node (has Tag), collapse its parent group
            if (selectedNode.Tag is OpenTarget)
            {
                var parentNode = selectedNode.Parent;
                if (parentNode != null && parentNode.IsExpanded)
                {
                    parentNode.Collapse();
                    
                    // Move selection to next visible item
                    var nextVisibleNode = GetNextVisibleNode(parentNode);
                    if (nextVisibleNode != null)
                    {
                        targetsTreeView.SelectedNode = nextVisibleNode;
                    }
                }
            }
            // If this is a group node, just collapse it
            else if (selectedNode.IsExpanded)
            {
                selectedNode.Collapse();
            }
        }

        private TreeNode? GetNextVisibleNode(TreeNode currentNode)
        {
            // Find the next visible node after the current one
            var allNodes = GetAllVisibleNodes();
            var currentIndex = allNodes.IndexOf(currentNode);
            
            if (currentIndex >= 0 && currentIndex < allNodes.Count - 1)
            {
                return allNodes[currentIndex + 1];
            }
            
            return null;
        }

        private List<TreeNode> GetAllVisibleNodes()
        {
            var visibleNodes = new List<TreeNode>();
            
            foreach (TreeNode rootNode in targetsTreeView.Nodes)
            {
                visibleNodes.Add(rootNode);
                if (rootNode.IsExpanded)
                {
                    foreach (TreeNode childNode in rootNode.Nodes)
                    {
                        visibleNodes.Add(childNode);
                    }
                }
            }
            
            return visibleNodes;
        }

        #endregion

        #region Selection and Dialog Management

        private void SelectTarget()
        {
            var selectedNode = targetsTreeView.SelectedNode;
            if (selectedNode?.Tag is OpenTarget target)
            {
                selectedTarget = target;
                this.DialogResult = DialogResult.OK;
                this.Hide();
                this.Close();
            }
        }

        #endregion

        #region Bypass functionality

        private void BypassToOriginalOpenDialog()
        {
            try
            {
                // Close this dialog first
                this.DialogResult = DialogResult.Cancel;
                this.Hide();
                
                // Call the bypass action passed from MainForm
                bypassAction?.Invoke();
                
                // Close the dialog completely
                this.Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error during Smart Open bypass");
                
                // Ensure dialog is closed even if bypass fails
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        #endregion

        #region Form Events

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            
            // Handle Ctrl+O to bypass Smart Open and use the original App Designer Open dialog
            if (keyData == (Keys.Control | Keys.O))
            {
                BypassToOriginalOpenDialog();
                return true;
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw a border around the form
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(100, 100, 120), 1, ButtonBorderStyle.Solid);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            idSearchBox.Focus();

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

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Stop and dispose the search timer
                searchTimer?.Stop();
                searchTimer?.Dispose();
                
                // Dispose the mouse handler
                mouseHandler?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }
}