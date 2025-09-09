using AppRefiner.Database.Models;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for searching and navigating to function definitions from cache
    /// </summary>
    public class DeclareFunctionDialog : Form
    {
        #region Private Fields

        // Win32 API imports for positioning
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly TextBox searchBox;
        private readonly ListView resultsListView;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        
        private readonly string dbName;
        private readonly FunctionCacheManager functionCacheManager;
        private readonly AppDesignerProcess appDesignerProcess;
        private readonly IntPtr owner;
        
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private BackgroundWorker? backgroundWorker;
        private readonly System.Windows.Forms.Timer searchTimer;
        
        private bool isCacheLoaded = false;
        private FunctionSearchResult? selectedFunction;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the selected function after dialog closes
        /// </summary>
        public FunctionSearchResult? SelectedFunction => selectedFunction;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of DeclareFunctionDialog
        /// </summary>
        /// <param name="dbName">Database name for scoping searches</param>
        /// <param name="functionCacheManager">Function cache manager instance</param>
        /// <param name="appDesignerProcess">AppDesignerProcess for database context</param>
        /// <param name="owner">Owner window handle</param>
        public DeclareFunctionDialog(FunctionCacheManager functionCacheManager, 
            AppDesignerProcess appDesignerProcess, IntPtr owner)
        {
            this.functionCacheManager = functionCacheManager ?? throw new ArgumentNullException(nameof(functionCacheManager));
            this.appDesignerProcess = appDesignerProcess ?? throw new ArgumentNullException(nameof(appDesignerProcess));
            this.owner = owner;
            this.dbName = appDesignerProcess.DBName;
            // Initialize UI components
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.searchBox = new TextBox();
            this.resultsListView = new ListView();
            this.progressBar = new ProgressBar();
            this.statusLabel = new Label();

            // Initialize search timer
            this.searchTimer = new System.Windows.Forms.Timer();
            this.searchTimer.Interval = 300; // 300ms delay like SmartOpen
            this.searchTimer.Tick += SearchTimer_Tick;

            InitializeComponent();
            PositionInParent();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel - following LintProjectProgressDialog pattern
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 35;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "Declare Function";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // searchBox - following CommandPaletteDialog pattern
            this.searchBox.Dock = DockStyle.Top;
            this.searchBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.searchBox.Height = 25;
            this.searchBox.Location = new Point(0, 35);
            this.searchBox.Margin = new Padding(5);
            this.searchBox.PlaceholderText = "Type to search functions...";
            this.searchBox.TextChanged += SearchBox_TextChanged;
            this.searchBox.KeyDown += SearchBox_KeyDown;

            // progressBar - following LintProjectProgressDialog pattern
            this.progressBar.Dock = DockStyle.Top;
            this.progressBar.Style = ProgressBarStyle.Marquee;
            this.progressBar.MarqueeAnimationSpeed = 30;
            this.progressBar.Location = new Point(0, 60);
            this.progressBar.Height = 20;
            this.progressBar.Visible = false; // Hidden by default

            // statusLabel
            this.statusLabel.Text = "Ready";
            this.statusLabel.ForeColor = Color.DimGray;
            this.statusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            this.statusLabel.Dock = DockStyle.Top;
            this.statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.statusLabel.Height = 20;
            this.statusLabel.Padding = new Padding(5, 2, 5, 2);

            // resultsListView - following CommandPaletteDialog pattern exactly
            this.resultsListView.BorderStyle = BorderStyle.None;
            this.resultsListView.View = View.Tile;
            this.resultsListView.TileSize = new Size(520 - 25, 50); // Width minus scrollbar, height for stacked text
            this.resultsListView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.resultsListView.FullRowSelect = true;
            this.resultsListView.HeaderStyle = ColumnHeaderStyle.None;
            this.resultsListView.HideSelection = false;
            this.resultsListView.MultiSelect = false;
            this.resultsListView.Dock = DockStyle.Fill;
            this.resultsListView.ShowItemToolTips = true;
            this.resultsListView.DoubleClick += ResultsListView_DoubleClick;
            this.resultsListView.KeyDown += ResultsListView_KeyDown;
            
            // Configure columns for tile view (needed for SubItems display)
            this.resultsListView.Columns.Add("Function", 250);
            this.resultsListView.Columns.Add("Path", 250);

            // DeclareFunctionDialog - following CommandPaletteDialog styling
            this.ClientSize = new Size(520, 400);
            this.Controls.Add(this.resultsListView);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "DeclareFunctionDialog";
            this.Text = "Declare Function";
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.KeyDown += DeclareFunctionDialog_KeyDown;
            
            // Add background color and padding to match CommandPalette
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);
            
            // Add resize event handler to update tile size when form is resized
            this.Resize += DeclareFunctionDialog_Resize;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Add drop shadow effect to the form
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private void PositionInParent()
        {
            if (owner != IntPtr.Zero)
            {
                RECT parentRect;
                if (GetWindowRect(owner, out parentRect))
                {
                    int parentWidth = parentRect.Right - parentRect.Left;
                    int parentHeight = parentRect.Bottom - parentRect.Top;
                    int parentCenterX = parentRect.Left + (parentWidth / 2);
                    int parentCenterY = parentRect.Top + (parentHeight / 2);

                    // Position the dialog centered in the parent window
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(
                        parentCenterX - (this.Width / 2),
                        parentCenterY - (this.Height / 2)
                    );
                }
            }
        }

        #endregion

        #region Event Handlers

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

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

            // Check initial cache state
            CheckInitialCacheState();

            // Focus search box
            searchBox.Focus();
        }

        private void DeclareFunctionDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
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

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (resultsListView.Items.Count > 0)
                    {
                        if (resultsListView.SelectedItems.Count == 0)
                        {
                            resultsListView.Items[0].Selected = true;
                        }
                        else
                        {
                            int currentIndex = resultsListView.SelectedItems[0].Index;
                            if (currentIndex < resultsListView.Items.Count - 1)
                            {
                                resultsListView.Items[currentIndex + 1].Selected = true;
                            }
                        }
                        resultsListView.Focus();
                    }
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    if (resultsListView.SelectedItems.Count > 0)
                    {
                        SelectCurrentFunction();
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void ResultsListView_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    SelectCurrentFunction();
                    e.Handled = true;
                    break;

                case Keys.Up:
                    if (resultsListView.SelectedItems.Count > 0 && resultsListView.SelectedItems[0].Index == 0)
                    {
                        searchBox.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void ResultsListView_DoubleClick(object sender, EventArgs e)
        {
            SelectCurrentFunction();
        }

        private void DeclareFunctionDialog_Resize(object sender, EventArgs e)
        {
            // Update tile size when form is resized to prevent horizontal scrolling
            resultsListView.TileSize = new Size(resultsListView.Width - 25, 50);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw a border around the form to match CommandPaletteDialog
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

        #endregion

        #region Private Methods

        private void CheckInitialCacheState()
        {
            // TODO: Implement initial cache check
            // For now, assume cache needs to be loaded
            isCacheLoaded = false;
            var stats = functionCacheManager.GetCacheStatistics();
            isCacheLoaded = (stats.FunctionsByDatabase.ContainsKey(appDesignerProcess.DBName) && stats.FunctionsByDatabase[appDesignerProcess.DBName] > 0);

            if (!isCacheLoaded)
            {
                ShowProgress(true);
                UpdateStatus("Loading function cache...");
                StartCacheOperation(isInitialLoad: true);
            }
            else
            {
                UpdateStatus("Ready");
            }
        }

        private void PerformSearch()
        {
            if (!isCacheLoaded) return;

            string searchTerm = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                resultsListView.Items.Clear();
                UpdateStatus("Ready");
                return;
            }

            try
            {
                var results = functionCacheManager.SearchFunctionCache(appDesignerProcess, searchTerm);
                
                if (results.Count == 0)
                {
                    ShowRefreshCacheOption();
                }
                else
                {
                    DisplayResults(results);
                    UpdateStatus($"Found {results.Count} function(s)");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Search error: {ex.Message}");
                resultsListView.Items.Clear();
            }
        }

        private void ShowRefreshCacheOption()
        {
            resultsListView.Items.Clear();
            
            var refreshItem = new ListViewItem("Refresh Function Cache");
            refreshItem.SubItems.Add("Update cache with newly added functions from database");
            refreshItem.Tag = "REFRESH_CACHE";
            refreshItem.ForeColor = Color.DarkBlue;
            refreshItem.Font = new Font(refreshItem.Font, FontStyle.Italic);
            
            // Add tooltip with description like CommandPaletteDialog
            refreshItem.ToolTipText = "Update cache with newly added functions from database";
            
            resultsListView.Items.Add(refreshItem);
            refreshItem.Selected = true;
            
            UpdateStatus("No functions found - select refresh to update cache");
        }

        private void DisplayResults(List<FunctionSearchResult> results)
        {
            resultsListView.Items.Clear();
            
            foreach (var result in results)
            {
                string displayName = FormatFunctionDisplayName(result);
                var item = new ListViewItem(displayName);
                item.SubItems.Add(result.FunctionPath);
                item.Tag = result;
                
                // Add tooltip with function path like CommandPaletteDialog
                item.ToolTipText = result.FunctionPath;
                
                resultsListView.Items.Add(item);
            }

            // Auto-select first item
            if (resultsListView.Items.Count > 0)
            {
                resultsListView.Items[0].Selected = true;
            }
        }

        private string FormatFunctionDisplayName(FunctionSearchResult result)
        {
            // Build parameter list
            string parameterList = "";
            if (result.ParameterNames != null && result.ParameterNames.Count > 0)
            {
                parameterList = string.Join(", ", result.ParameterNames);
            }
            
            // Format as FunctionName(parameters) [returnType]
            string displayName = $"{result.FunctionName}({parameterList})";
            
            // Add return type if it exists and is not empty
            if (!string.IsNullOrEmpty(result.ReturnType))
            {
                displayName += $" Returns {result.ReturnType}";
            }
            
            return displayName;
        }

        private void SelectCurrentFunction()
        {
            if (resultsListView.SelectedItems.Count == 0) return;

            var selectedItem = resultsListView.SelectedItems[0];
            
            // Check if this is the refresh cache option
            if (selectedItem.Tag?.ToString() == "REFRESH_CACHE")
            {
                OnRefreshCacheSelected();
                return;
            }

            // Regular function selection
            if (selectedItem.Tag is FunctionSearchResult function)
            {
                selectedFunction = function;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void OnRefreshCacheSelected()
        {
            ShowProgress(true);
            UpdateStatus("Refreshing function cache...");
            StartCacheOperation(isInitialLoad: false);
        }

        private void StartCacheOperation(bool isInitialLoad)
        {
            functionCacheManager.OnCacheProgressUpdate += FunctionCacheManager_OnCacheProgressUpdate;
            progressBar.Style = ProgressBarStyle.Blocks;
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            backgroundWorker.RunWorkerAsync(isInitialLoad);
        }

        private int totalPrograms;
        private int processedPrograms;
        private void FunctionCacheManager_OnCacheProgressUpdate(int processed, int total)
        {
            this.Invoke(() =>
            {
                totalPrograms = total;
                processedPrograms = processed;
                progressBar.Maximum = total;
                progressBar.Value = processed;
                backgroundWorker?.ReportProgress(0, $"STATUS:Loading function cache...{(processedPrograms > 0 ? $"({processedPrograms}/{totalPrograms})" : $"")}");
            });
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            try
            {
                bool isInitialLoad = (bool)e.Argument!;
                backgroundWorker?.ReportProgress(0, $"STATUS:{(isInitialLoad ? "Loading" : "Refreshing")} function cache...{(processedPrograms > 0? $"({processedPrograms}/{totalPrograms}" : $"")}");
                
                // Call the function cache manager to update cache
                bool success = functionCacheManager.UpdateFunctionCache(appDesignerProcess);
                e.Result = success;
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void BackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is string message && message.StartsWith("STATUS:"))
            {
                UpdateStatus(message.Substring(7));
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            functionCacheManager.OnCacheProgressUpdate -= FunctionCacheManager_OnCacheProgressUpdate;

            ShowProgress(false);

            if (e.Result is Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
            else if (e.Result is bool success && success)
            {
                isCacheLoaded = true;
                UpdateStatus("Cache updated successfully");
                
                // Re-execute current search if there was one
                if (!string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    PerformSearch();
                }
                else
                {
                    UpdateStatus("Ready");
                }
            }
            else
            {
                UpdateStatus("Cache update failed");
            }
        }

        #endregion

        #region UI Update Methods

        private void UpdateStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => statusLabel.Text = text);
            }
            else
            {
                statusLabel.Text = text;
            }
        }

        private void ShowProgress(bool show)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => {
                    progressBar.Visible = show;
                    progressBar.Style = show ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
                });
            }
            else
            {
                progressBar.Visible = show;
                progressBar.Style = show ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            }
        }

        #endregion
    }
}