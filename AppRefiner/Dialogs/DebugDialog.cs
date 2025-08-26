using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppRefiner.Dialogs
{
    public class DebugDialog : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly RichTextBox debugTextBox;
        private readonly Button clearButton;
        private readonly Button exportButton;
        private readonly IntPtr parentHandle;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        
        // Static list to store debug messages
        private static readonly List<DebugMessage> debugMessages = new List<DebugMessage>();
        private static readonly int MaxMessages = 1000; // Limit to prevent memory issues
        
        // Keep track of open debug dialog instances
        private static readonly List<WeakReference<DebugDialog>> openDialogs = new List<WeakReference<DebugDialog>>();
        
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

        /// <summary>
        /// Static method to add a debug message to the log
        /// </summary>
        /// <param name="message">The message text</param>
        /// <param name="type">The message type (Info, Warning, Error)</param>
        public static void Log(string message, DebugMessageType type = DebugMessageType.Info)
        {
            lock (debugMessages)
            {
                debugMessages.Add(new DebugMessage 
                { 
                    Message = message, 
                    Type = type, 
                    Timestamp = DateTime.Now 
                });
                
                // Remove oldest messages if we exceed the limit
                while (debugMessages.Count > MaxMessages)
                {
                    debugMessages.RemoveAt(0);
                }
            }

            // Update any open debug dialogs
            UpdateOpenDialogs();
        }
        
        // Update all open dialog instances
        private static void UpdateOpenDialogs()
        {
            lock (openDialogs)
            {
                // Clean up any closed dialogs
                for (int i = openDialogs.Count - 1; i >= 0; i--)
                {
                    if (!openDialogs[i].TryGetTarget(out var dialog) || dialog.IsDisposed)
                    {
                        openDialogs.RemoveAt(i);
                    }
                    else
                    {
                        // Update the dialog
                        dialog.BeginInvoke(new Action(() => {
                            dialog.AppendLatestMessages();
                        }));
                    }
                }
            }
        }
        
        /// <summary>
        /// Static method to open the debug dialog
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        /// <returns>The created debug dialog instance</returns>
        public static DebugDialog ShowDialog(IntPtr parentHwnd)
        {
            var dialog = new DebugDialog(parentHwnd);
            
            // Register this dialog in the open dialogs list
            lock (openDialogs)
            {
                openDialogs.Add(new WeakReference<DebugDialog>(dialog));
            }
            
            dialog.Show();
            return dialog;
        }

        /// <summary>
        /// Static method to open the indicator debug panel
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        /// <param name="mainForm">Reference to main form for accessing active editor</param>
        /// <returns>The created indicator debug panel instance</returns>
        public static IndicatorDebugPanel ShowIndicatorPanel(IntPtr parentHwnd, MainForm mainForm)
        {
            var panel = new IndicatorDebugPanel(parentHwnd, mainForm);
            panel.Show();
            return panel;
        }
        
        /// <summary>
        /// Constructor for the Debug Dialog
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        public DebugDialog(IntPtr parentHwnd)
        {
            this.parentHandle = parentHwnd;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.debugTextBox = new RichTextBox();
            this.clearButton = new Button();
            this.exportButton = new Button();
            
            InitializeComponent();
            PositionInParent();
            RefreshDebugMessages();
        }
        
        private void PositionInParent()
        {
            if (parentHandle != IntPtr.Zero)
            {
                RECT parentRect;
                if (GetWindowRect(parentHandle, out parentRect))
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
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            
            // Position on parent window
            if (parentHandle != IntPtr.Zero)
            {
                PositionInParent();
            }

            // Create the mouse handler if this is a modal dialog
            if (this.Modal && parentHandle != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, parentHandle);
            }
        }
        
        /// <summary>
        /// Refreshes all debug messages in the text box
        /// </summary>
        public void RefreshDebugMessages()
        {
            debugTextBox.Clear();
            lock (debugMessages)
            {
                foreach (var msg in debugMessages)
                {
                    AppendFormattedMessage(msg);
                }
            }
            
            // Scroll to the end
            ScrollToEnd();
        }
        
        /// <summary>
        /// Appends only the newest messages that aren't in the textbox yet
        /// </summary>
        public void AppendLatestMessages()
        {
            // Get the current count of messages in the textbox (approximated by line count)
            int currentLines = debugTextBox.Lines.Length;
            
            lock (debugMessages)
            {
                // If we have more messages than lines, append the new ones
                if (debugMessages.Count > currentLines)
                {
                    // Append only the new messages
                    for (int i = currentLines; i < debugMessages.Count; i++)
                    {
                        AppendFormattedMessage(debugMessages[i]);
                    }
                    
                    // Scroll to the end
                    ScrollToEnd();
                }
            }
        }
        
        private void ScrollToEnd()
        {
            debugTextBox.SelectionStart = debugTextBox.Text.Length;
            debugTextBox.ScrollToCaret();
        }
        
        private void AppendFormattedMessage(DebugMessage msg)
        {
            string timeStamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            // Store current selection state
            int currentSelection = debugTextBox.SelectionStart;
            
            // Format timestamp in gray
            debugTextBox.SelectionStart = debugTextBox.TextLength;
            debugTextBox.SelectionLength = 0;
            debugTextBox.SelectionColor = Color.Gray;
            debugTextBox.AppendText($"[{timeStamp}] ");
            
            // Format message type with color
            Color typeColor = Color.Black;
            string typePrefix = "";
            
            switch (msg.Type)
            {
                case DebugMessageType.Info:
                    typeColor = Color.DarkBlue;
                    typePrefix = "INFO";
                    break;
                case DebugMessageType.Warning:
                    typeColor = Color.Orange;
                    typePrefix = "WARN";
                    break;
                case DebugMessageType.Error:
                    typeColor = Color.Red;
                    typePrefix = "ERROR";
                    break;
            }
            
            debugTextBox.SelectionColor = typeColor;
            debugTextBox.AppendText($"[{typePrefix}] ");
            
            // Append the actual message in black
            debugTextBox.SelectionColor = Color.Black;
            debugTextBox.AppendText(msg.Message + Environment.NewLine);
            
            // Restore selection
            debugTextBox.SelectionStart = currentSelection;
            debugTextBox.SelectionLength = 0;
            debugTextBox.SelectionColor = Color.Black;
        }
        
        private void ClearMessages()
        {
            lock (debugMessages)
            {
                debugMessages.Clear();
            }
            RefreshDebugMessages();
        }
        
        private void ExportMessages()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.FileName = $"AppRefiner_Debug_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    StringBuilder sb = new StringBuilder();
                    lock (debugMessages)
                    {
                        foreach (var msg in debugMessages)
                        {
                            string timeStamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            string typeStr = msg.Type.ToString().ToUpper();
                            sb.AppendLine($"[{timeStamp}] [{typeStr}] {msg.Message}");
                        }
                    }
                    
                    try
                    {
                        File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                        MessageBox.Show("Debug log exported successfully.", "Export Complete", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export debug log: {ex.Message}", "Export Failed", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
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
            this.headerLabel.Text = "Debug Console";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            
            // Button panel for clear and export
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Bottom;
            buttonPanel.Height = 40;
            buttonPanel.Padding = new Padding(5);
            
            // Clear button
            this.clearButton.Text = "Clear";
            this.clearButton.Dock = DockStyle.Left;
            this.clearButton.Width = 100;
            this.clearButton.Click += (sender, e) => ClearMessages();
            buttonPanel.Controls.Add(this.clearButton);
            
            // Export button
            this.exportButton.Text = "Export Log";
            this.exportButton.Dock = DockStyle.Right;
            this.exportButton.Width = 100;
            this.exportButton.Click += (sender, e) => ExportMessages();
            buttonPanel.Controls.Add(this.exportButton);
            
            // Debug text box
            this.debugTextBox.Dock = DockStyle.Fill;
            this.debugTextBox.BackColor = Color.White;
            this.debugTextBox.ForeColor = Color.Black;
            this.debugTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.debugTextBox.ReadOnly = true;
            this.debugTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            // DebugDialog
            this.ClientSize = new Size(850, 400);
            this.Controls.Add(this.debugTextBox);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Name = "DebugDialog";
            this.Text = "Debug Console";
            this.ShowInTaskbar = false;
            
            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
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
    }
    
    /// <summary>
    /// Enum representing the type of debug message
    /// </summary>
    public enum DebugMessageType
    {
        Info,
        Warning,
        Error
    }
    
    /// <summary>
    /// Class representing a debug message
    /// </summary>
    public class DebugMessage
    {
        public string Message { get; set; } = "";
        public DebugMessageType Type { get; set; } = DebugMessageType.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Non-modal panel for debugging styler indicators
    /// </summary>
    public class IndicatorDebugPanel : Form
    {
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly DataGridView indicatorGrid;
        private readonly Button refreshButton;
        private readonly Button exportButton;
        private readonly TextBox filterTextBox;
        private readonly ComboBox stylerFilterCombo;
        private readonly ComboBox typeFilterCombo;
        private readonly IntPtr parentHandle;
        private readonly MainForm mainForm;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private System.Windows.Forms.Timer refreshTimer;

        /// <summary>
        /// Constructor for the Indicator Debug Panel
        /// </summary>
        /// <param name="parentHwnd">Handle to the parent window</param>
        /// <param name="mainForm">Reference to main form for accessing active editor</param>
        public IndicatorDebugPanel(IntPtr parentHwnd, MainForm mainForm)
        {
            this.parentHandle = parentHwnd;
            this.mainForm = mainForm;
            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.indicatorGrid = new DataGridView();
            this.refreshButton = new Button();
            this.exportButton = new Button();
            this.filterTextBox = new TextBox();
            this.stylerFilterCombo = new ComboBox();
            this.typeFilterCombo = new ComboBox();
            this.refreshTimer = new System.Windows.Forms.Timer();

            InitializeComponent();
            PositionInParent();
            SetupRefreshTimer();
            RefreshIndicatorData();
        }

        private void SetupRefreshTimer()
        {
            refreshTimer.Interval = 1000; // Refresh every second
            refreshTimer.Tick += (s, e) => RefreshIndicatorData();
            refreshTimer.Start();
        }

        private void PositionInParent()
        {
            if (parentHandle != IntPtr.Zero)
            {
                DebugDialog.RECT parentRect;
                if (GetWindowRect(parentHandle, out parentRect))
                {
                    int parentWidth = parentRect.Right - parentRect.Left;
                    int parentHeight = parentRect.Bottom - parentRect.Top;

                    // Position the dialog to the right side of the parent window
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(
                        parentRect.Right - 50, // Slightly overlap to show association
                        parentRect.Top + 50
                    );
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Position relative to parent window
            if (parentHandle != IntPtr.Zero)
            {
                PositionInParent();
            }
        }

        /// <summary>
        /// Refreshes the indicator data from the active editor
        /// </summary>
        public void RefreshIndicatorData()
        {
            try
            {
                // Ensure we're on the UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(RefreshIndicatorData));
                    return;
                }

                if (mainForm.ActiveEditor?.ActiveIndicators == null)
                {
                    indicatorGrid.Rows.Clear();
                    return;
                }

                var indicators = mainForm.ActiveEditor.ActiveIndicators;
                var currentRowCount = indicatorGrid.Rows.Count;

                // Only update if the data has changed
                if (currentRowCount != indicators.Count || HasIndicatorsChanged(indicators))
                {
                    indicatorGrid.Rows.Clear();

                    foreach (var indicator in indicators)
                    {
                        var endPosition = indicator.Start + indicator.Length - 1;
                        var colorHex = $"#{indicator.Color:X8}";
                        var quickFixCount = indicator.QuickFixes?.Count ?? 0;

                        var row = indicatorGrid.Rows.Add(
                            "Unknown", // Styler Name - we'll need to enhance this
                            indicator.Type.ToString(),
                            indicator.Start,
                            endPosition,
                            indicator.Length,
                            colorHex,
                            indicator.Tooltip ?? "",
                            quickFixCount
                        );

                        // Color the Color column cell
                        var color = Color.FromArgb((int)indicator.Color);
                        indicatorGrid.Rows[row].Cells["ColorColumn"].Style.BackColor = color;
                        indicatorGrid.Rows[row].Cells["ColorColumn"].Style.ForeColor = GetContrastColor(color);
                    }

                    // Apply current filters
                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                DebugDialog.Log($"Error refreshing indicator data: {ex.Message}", DebugMessageType.Error);
            }
        }

        private bool HasIndicatorsChanged(List<Stylers.Indicator> indicators)
        {
            // Simple comparison - in a production environment you might want a more sophisticated approach
            if (indicatorGrid.Rows.Count != indicators.Count)
                return true;

            // Check if any indicator properties have changed
            for (int i = 0; i < indicators.Count && i < indicatorGrid.Rows.Count; i++)
            {
                var indicator = indicators[i];
                var row = indicatorGrid.Rows[i];

                if (indicator.Start.ToString() != row.Cells["StartColumn"].Value?.ToString() ||
                    indicator.Type.ToString() != row.Cells["TypeColumn"].Value?.ToString())
                {
                    return true;
                }
            }

            return false;
        }

        private Color GetContrastColor(Color backgroundColor)
        {
            // Calculate luminance and return black or white for best contrast
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        private void ApplyFilters()
        {
            // Ensure we're on the UI thread
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyFilters));
                return;
            }

            string textFilter = filterTextBox.Text?.ToLower() ?? "";
            string stylerFilter = stylerFilterCombo.SelectedItem?.ToString() ?? "";
            string typeFilter = typeFilterCombo.SelectedItem?.ToString() ?? "";

            foreach (DataGridViewRow row in indicatorGrid.Rows)
            {
                if (row.IsNewRow) continue;

                bool visible = true;

                // Text filter (searches in tooltip)
                if (!string.IsNullOrEmpty(textFilter))
                {
                    var tooltip = row.Cells["TooltipColumn"].Value?.ToString()?.ToLower() ?? "";
                    if (!tooltip.Contains(textFilter))
                        visible = false;
                }

                // Styler filter
                if (!string.IsNullOrEmpty(stylerFilter) && stylerFilter != "All")
                {
                    var styler = row.Cells["StylerColumn"].Value?.ToString() ?? "";
                    if (styler != stylerFilter)
                        visible = false;
                }

                // Type filter  
                if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "All")
                {
                    var type = row.Cells["TypeColumn"].Value?.ToString() ?? "";
                    if (type != typeFilter)
                        visible = false;
                }

                row.Visible = visible;
            }
        }

        private void ExportIndicatorData()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "csv";
                saveFileDialog.FileName = $"AppRefiner_Indicators_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        
                        // Header
                        sb.AppendLine("Styler,Type,Start,End,Length,Color,Tooltip,QuickFixes");

                        // Data rows
                        foreach (DataGridViewRow row in indicatorGrid.Rows)
                        {
                            if (row.IsNewRow || !row.Visible) continue;

                            var values = new string[8];
                            for (int i = 0; i < 8; i++)
                            {
                                values[i] = row.Cells[i].Value?.ToString() ?? "";
                                // Escape commas and quotes for CSV
                                if (values[i].Contains(",") || values[i].Contains("\""))
                                {
                                    values[i] = "\"" + values[i].Replace("\"", "\"\"") + "\"";
                                }
                            }
                            sb.AppendLine(string.Join(",", values));
                        }

                        File.WriteAllText(saveFileDialog.FileName, sb.ToString());
                        
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var processId = mainForm.ActiveEditor?.ProcessId ?? 0;
                            var mainHandle = Process.GetProcessById((int)processId).MainWindowHandle;
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog("Indicator data exported successfully.", "Export Complete", 
                                MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                    catch (Exception ex)
                    {
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var processId = mainForm.ActiveEditor?.ProcessId ?? 0;
                            var mainHandle = Process.GetProcessById((int)processId).MainWindowHandle;
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog($"Failed to export indicator data: {ex.Message}", "Export Failed", 
                                MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                }
            }
        }

        private void OnIndicatorRowClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && !indicatorGrid.Rows[e.RowIndex].IsNewRow)
            {
                // Get the position from the clicked row
                var startCell = indicatorGrid.Rows[e.RowIndex].Cells["StartColumn"];
                var lengthCell = indicatorGrid.Rows[e.RowIndex].Cells["LengthColumn"];

                if (int.TryParse(startCell.Value?.ToString(), out int start) &&
                    int.TryParse(lengthCell.Value?.ToString(), out int length))
                {
                    // TODO: Highlight this position in the active editor
                    // This would require ScintillaManager methods to set selection/highlight
                    DebugDialog.Log($"Selected indicator at position {start}-{start + length}", DebugMessageType.Info);
                }
            }
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // Header panel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // Header label
            this.headerLabel.Text = "Indicator Debug Panel";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Filter panel
            Panel filterPanel = new Panel();
            filterPanel.Dock = DockStyle.Top;
            filterPanel.Height = 35;
            filterPanel.Padding = new Padding(5);

            Label filterLabel = new Label();
            filterLabel.Text = "Filter:";
            filterLabel.Dock = DockStyle.Left;
            filterLabel.Width = 40;
            filterLabel.TextAlign = ContentAlignment.MiddleLeft;
            filterPanel.Controls.Add(filterLabel);

            this.filterTextBox.Dock = DockStyle.Left;
            this.filterTextBox.Width = 150;
            this.filterTextBox.PlaceholderText = "Search tooltips...";
            this.filterTextBox.TextChanged += (s, e) => ApplyFilters();
            filterPanel.Controls.Add(this.filterTextBox);

            Label stylerLabel = new Label();
            stylerLabel.Text = " Styler:";
            stylerLabel.Dock = DockStyle.Left;
            stylerLabel.Width = 45;
            stylerLabel.TextAlign = ContentAlignment.MiddleLeft;
            filterPanel.Controls.Add(stylerLabel);

            this.stylerFilterCombo.Dock = DockStyle.Left;
            this.stylerFilterCombo.Width = 120;
            this.stylerFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this.stylerFilterCombo.Items.AddRange(new[] { "All" });
            this.stylerFilterCombo.SelectedIndex = 0;
            this.stylerFilterCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterPanel.Controls.Add(this.stylerFilterCombo);

            Label typeLabel = new Label();
            typeLabel.Text = " Type:";
            typeLabel.Dock = DockStyle.Left;
            typeLabel.Width = 35;
            typeLabel.TextAlign = ContentAlignment.MiddleLeft;
            filterPanel.Controls.Add(typeLabel);

            this.typeFilterCombo.Dock = DockStyle.Left;
            this.typeFilterCombo.Width = 100;
            this.typeFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this.typeFilterCombo.Items.AddRange(new[] { "All", "SQUIGGLE", "TEXTCOLOR", "BACKGROUND", "OUTLINE", "HIGHLIGHTER" });
            this.typeFilterCombo.SelectedIndex = 0;
            this.typeFilterCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterPanel.Controls.Add(this.typeFilterCombo);

            // Button panel
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Bottom;
            buttonPanel.Height = 40;
            buttonPanel.Padding = new Padding(5);

            this.refreshButton.Text = "Refresh";
            this.refreshButton.Dock = DockStyle.Left;
            this.refreshButton.Width = 100;
            this.refreshButton.Click += (sender, e) => RefreshIndicatorData();
            buttonPanel.Controls.Add(this.refreshButton);

            this.exportButton.Text = "Export";
            this.exportButton.Dock = DockStyle.Right;
            this.exportButton.Width = 100;
            this.exportButton.Click += (sender, e) => ExportIndicatorData();
            buttonPanel.Controls.Add(this.exportButton);

            // Data grid
            this.indicatorGrid.Dock = DockStyle.Fill;
            this.indicatorGrid.BackgroundColor = Color.White;
            this.indicatorGrid.BorderStyle = BorderStyle.None;
            this.indicatorGrid.AllowUserToAddRows = false;
            this.indicatorGrid.AllowUserToDeleteRows = false;
            this.indicatorGrid.ReadOnly = true;
            this.indicatorGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.indicatorGrid.MultiSelect = false;
            this.indicatorGrid.CellClick += OnIndicatorRowClick;

            // Add columns
            this.indicatorGrid.Columns.Add("StylerColumn", "Styler");
            this.indicatorGrid.Columns.Add("TypeColumn", "Type");
            this.indicatorGrid.Columns.Add("StartColumn", "Start");
            this.indicatorGrid.Columns.Add("EndColumn", "End");
            this.indicatorGrid.Columns.Add("LengthColumn", "Length");
            this.indicatorGrid.Columns.Add("ColorColumn", "Color");
            this.indicatorGrid.Columns.Add("TooltipColumn", "Tooltip");
            this.indicatorGrid.Columns.Add("QuickFixesColumn", "Quick Fixes");

            // Configure column widths
            this.indicatorGrid.Columns["StylerColumn"].Width = 120;
            this.indicatorGrid.Columns["TypeColumn"].Width = 80;
            this.indicatorGrid.Columns["StartColumn"].Width = 60;
            this.indicatorGrid.Columns["EndColumn"].Width = 60;
            this.indicatorGrid.Columns["LengthColumn"].Width = 60;
            this.indicatorGrid.Columns["ColorColumn"].Width = 80;
            this.indicatorGrid.Columns["TooltipColumn"].Width = 300;
            this.indicatorGrid.Columns["QuickFixesColumn"].Width = 80;

            // Main form
            this.ClientSize = new Size(900, 500);
            this.Controls.Add(this.indicatorGrid);
            this.Controls.Add(filterPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(this.headerPanel);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.Manual;
            this.Name = "IndicatorDebugPanel";
            this.Text = "Indicator Debug Panel";
            this.ShowInTaskbar = false;

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            mouseHandler?.Dispose();
            base.OnFormClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out DebugDialog.RECT lpRect);
    }
} 