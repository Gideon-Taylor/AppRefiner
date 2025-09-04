using AppRefiner.Database.Models;
using System.Runtime.InteropServices;

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
        private readonly TextBox searchBox;
        private readonly TreeView targetsTreeView;
        private readonly Func<string, OpenTargetSearchOptions, List<OpenTarget>> searchFunction;
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
            Func<string, OpenTargetSearchOptions, List<OpenTarget>> searchFunction, 
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
            this.searchBox = new TextBox();
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

            // searchBox
            this.searchBox.BorderStyle = BorderStyle.FixedSingle;
            this.searchBox.Dock = DockStyle.Top;
            this.searchBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            this.searchBox.Location = new Point(0, 35);
            this.searchBox.Margin = new Padding(0);
            this.searchBox.Name = "searchBox";
            this.searchBox.Size = new Size(600, 27);
            this.searchBox.TabIndex = 0;
            this.searchBox.PlaceholderText = "Search for projects, pages, and other definitions...";
            this.searchBox.TextChanged += SearchBox_TextChanged;
            this.searchBox.KeyDown += SearchBox_KeyDown;

            // targetsTreeView
            this.targetsTreeView.BorderStyle = BorderStyle.None;
            this.targetsTreeView.Dock = DockStyle.Fill;
            this.targetsTreeView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.targetsTreeView.Location = new Point(0, 62);
            this.targetsTreeView.Name = "targetsTreeView";
            this.targetsTreeView.Size = new Size(600, 338);
            this.targetsTreeView.TabIndex = 1;
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
            this.Controls.Add(this.searchBox);
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

        private void PerformSearch(string searchTerm)
        {
            try
            {
                // Convert SmartOpenConfig to OpenTargetSearchOptions
                var searchOptions = CreateSearchOptionsFromConfig();
                
                // Get results from the search function
                allTargets = searchFunction(searchTerm, searchOptions);
                
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
                if (isEnabled && TryMapStringToTargetType(typeName, out OpenTargetType targetType))
                {
                    enabledTypes.Add(targetType);
                }
            }
            
            return new OpenTargetSearchOptions(
                enabledTypes,
                config.MaxResultsPerType,
                config.SortByLastUpdate);
        }

        private bool TryMapStringToTargetType(string typeName, out OpenTargetType targetType)
        {
            targetType = typeName switch
            {
                "Activity" => OpenTargetType.Activity,
                "Analytic Model" => OpenTargetType.AnalyticModel,
                "Analytic Type" => OpenTargetType.AnalyticType,
                "App Engine Program" => OpenTargetType.AppEngineProgram,
                "Application Package" => OpenTargetType.ApplicationPackage,
                "Application Class" => OpenTargetType.ApplicationClass,
                "Approval Rule Set" => OpenTargetType.ApprovalRuleSet,
                "Business Interlink" => OpenTargetType.BusinessInterlink,
                "Business Process" => OpenTargetType.BusinessProcess,
                "Component" => OpenTargetType.Component,
                "Component Interface" => OpenTargetType.ComponentInterface,
                "Field" => OpenTargetType.Field,
                "File Layout" => OpenTargetType.FileLayout,
                "File Reference" => OpenTargetType.FileReference,
                "HTML" => OpenTargetType.HTML,
                "Image" => OpenTargetType.Image,
                "Menu" => OpenTargetType.Menu,
                "Message" => OpenTargetType.Message,
                "Optimization Model" => OpenTargetType.OptimizationModel,
                "Page" => OpenTargetType.Page,
                "Page (Fluid)" => OpenTargetType.PageFluid,
                "Project" => OpenTargetType.Project,
                "Record" => OpenTargetType.Record,
                "SQL" => OpenTargetType.SQL,
                "Style Sheet" => OpenTargetType.StyleSheet,
                _ => OpenTargetType.Project
            };

            return !typeName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
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
                .OrderBy(g => g.Key.ToString());

            foreach (var group in groupedTargets)
            {
                // Create group node
                var groupName = group.Key.ToString();
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
            PerformSearch(searchBox.Text);
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
                    SelectTarget();
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
                    searchBox.Focus();
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