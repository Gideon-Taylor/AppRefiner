using AppRefiner.Database;
using AppRefiner.Models;
using AppRefiner.Services.StackTrace;
using PeopleCodeParser.SelfHosted;
using SqlParser.Ast;

namespace AppRefiner.Dialogs
{
    public partial class StackTraceNavigatorDialog : Form
    {
        private readonly AppDesignerProcess appDesignerProcess;
        private readonly IntPtr owner;


        // UI Controls
        private Panel headerPanel;
        private Label titleLabel;
        private Button closeButton;
        private Panel contentPanel;
        private Label inputLabel;
        private TextBox stackTraceTextBox;
        private Label resultsLabel;
        private ListView resultsListView;
        private Label statusLabel;

        // Timer to ensure always-on-top behavior persists
        private System.Windows.Forms.Timer alwaysOnTopTimer;

        // Timer for debounced parsing
        private System.Windows.Forms.Timer parseTimer;

        // Variables for drag functionality
        private bool isDragging = false;
        private Point dragStartPoint;

        // Parsed entries
        private List<StackTraceEntry> currentEntries = new List<StackTraceEntry>();

        public StackTraceNavigatorDialog(AppDesignerProcess activeAppDesigner, IntPtr ownerHandle)
        {
            this.appDesignerProcess = activeAppDesigner;
            this.owner = ownerHandle;

            InitializeComponent();
            InitializeUI();
            SetupEventHandlers();

            // Apply AppRefiner dialog styling
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F);
            this.Size = new Size(600, 500);
            this.Load += StackTraceNavigatorDialog_Load;

            // Initialize timers
            InitializeAlwaysOnTopTimer();
            InitializeParseTimer();
        }

        private void StackTraceNavigatorDialog_Load(object? sender, EventArgs e)
        {
            // Position to right edge and center vertically on owner window
            if (owner != IntPtr.Zero)
            {
                PositionToRightOfOwner();
            }
            
            stackTraceTextBox.Focus();
        }

        /// <summary>
        /// Initializes the timer to maintain always-on-top behavior
        /// </summary>
        private void InitializeAlwaysOnTopTimer()
        {
            alwaysOnTopTimer = new System.Windows.Forms.Timer();
            alwaysOnTopTimer.Interval = 1000; // Check every second
            alwaysOnTopTimer.Tick += (s, e) => EnsureOnTop();
        }

        /// <summary>
        /// Initializes the timer for debounced parsing
        /// </summary>
        private void InitializeParseTimer()
        {
            parseTimer = new System.Windows.Forms.Timer();
            parseTimer.Interval = 300; // 300ms delay
            parseTimer.Tick += (s, e) =>
            {
                parseTimer.Stop();
                ParseStackTrace();
            };
        }

        /// <summary>
        /// Makes this window always stay on top using Windows API
        /// </summary>
        public void MakeAlwaysOnTop()
        {
            if (this.Handle != IntPtr.Zero)
            {
                WindowHelper.SetWindowPos(this.Handle, WindowHelper.HWND_TOPMOST, 0, 0, 0, 0, WindowHelper.SWP_NOMOVE | WindowHelper.SWP_NOSIZE | WindowHelper.SWP_SHOWWINDOW);
            }
        }

        /// <summary>
        /// Removes always-on-top behavior
        /// </summary>
        public void RemoveAlwaysOnTop()
        {
            if (this.Handle != IntPtr.Zero)
            {
                WindowHelper.SetWindowPos(this.Handle, WindowHelper.HWND_NOTOPMOST, 0, 0, 0, 0, WindowHelper.SWP_NOMOVE | WindowHelper.SWP_NOSIZE | WindowHelper.SWP_SHOWWINDOW);
            }
        }

        /// <summary>
        /// Positions the dialog to the right edge of the owner window, centered vertically
        /// This keeps the dialog accessible while avoiding covering the code being examined
        /// </summary>
        private void PositionToRightOfOwner()
        {
            try
            {
                // Get owner window bounds
                if (!WindowHelper.GetWindowRect(owner, out WindowHelper.RECT ownerRect))
                    return;

                // Get screen bounds to ensure dialog stays on screen
                Rectangle screenBounds = Screen.FromHandle(owner).WorkingArea;

                // Calculate position: right edge of owner, vertically centered
                int dialogX = ownerRect.Right - this.Width - 20; // 20px padding from right edge
                int dialogY = ownerRect.Top + (ownerRect.Height - this.Height) / 2;

                // Ensure dialog stays within screen bounds
                dialogX = Math.Max(screenBounds.Left, Math.Min(dialogX, screenBounds.Right - this.Width));
                dialogY = Math.Max(screenBounds.Top, Math.Min(dialogY, screenBounds.Bottom - this.Height));

                // If there's not enough room to the right, try positioning to the left
                if (dialogX < ownerRect.Left)
                {
                    dialogX = ownerRect.Left + 20; // 20px padding from left edge
                    dialogX = Math.Max(screenBounds.Left, Math.Min(dialogX, screenBounds.Right - this.Width));
                }

                this.Location = new Point(dialogX, dialogY);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error positioning dialog to right of owner: {ex.Message}");
                // Fallback to center if positioning fails
                if (owner != IntPtr.Zero)
                {
                    WindowHelper.CenterFormOnWindow(this, owner);
                }
            }
        }

        /// <summary>
        /// Ensures the dialog stays on top when focus changes
        /// Closes dialog if foreground window doesn't belong to AppRefiner or pside processes
        /// </summary>
        public void EnsureOnTop()
        {
            if (this.Handle != IntPtr.Zero && this.Visible)
            {
                // Simple always-on-top without process checking to avoid deadlocks
                try
                {
                    WindowHelper.BringWindowToTop(this.Handle);
                    MakeAlwaysOnTop();
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error in EnsureOnTop: {ex.Message}");
                }
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                // Defer always-on-top behavior until after dialog is fully loaded
                Task.Delay(100).ContinueWith(_ =>
                {
                    this.BeginInvoke(new System.Action(() =>
                    {
                        // Make always on top when the dialog becomes visible
                        MakeAlwaysOnTop();
                        // Start the timer to maintain always-on-top behavior
                        alwaysOnTopTimer?.Start();
                        
                        this.Activate();
                        stackTraceTextBox.Focus();
                    }));
                });
            }
            else
            {
                // Stop the timer when dialog is hidden
                alwaysOnTopTimer?.Stop();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // This method is required for the designer but we'll build UI manually
            this.ResumeLayout(false);
        }

        private void InitializeUI()
        {
            var padding = 1;

            // Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(50, 50, 60),
                Padding = new Padding(padding)
            };

            titleLabel = new Label
            {
                Text = "Stack Trace Navigator",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            closeButton = new Button
            {
                Text = "×",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(25, 25),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 80);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(closeButton);

            // Content Panel
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(240, 240, 245)
            };

            CreateInputControls();
            CreateResultsControls();
            CreateStatusControls();

            // Add panels to form
            this.Controls.Add(contentPanel);
            this.Controls.Add(headerPanel);
        }

        private void CreateInputControls()
        {
            inputLabel = new Label
            {
                Text = "Paste stack trace:",
                Location = new Point(10, 8),
                Size = new Size(120, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            stackTraceTextBox = new TextBox
            {
                Location = new Point(10, 35),
                Size = new Size(560, 145),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                PlaceholderText = "Paste your PeopleCode stack trace here..."
            };

            contentPanel.Controls.Add(inputLabel);
            contentPanel.Controls.Add(stackTraceTextBox);
        }

        private void CreateResultsControls()
        {
            resultsLabel = new Label
            {
                Text = "Stack trace entries:",
                Location = new Point(10, 190),
                Size = new Size(150, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            resultsListView = new ListView
            {
                Location = new Point(10, 215),
                Size = new Size(560, 200),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                MultiSelect = false
            };

            // Add columns
            resultsListView.Columns.Add("Entry", 500);
            resultsListView.Columns.Add("Status", 55);

            contentPanel.Controls.Add(resultsLabel);
            contentPanel.Controls.Add(resultsListView);
        }

        private void CreateStatusControls()
        {
            statusLabel = new Label
            {
                Text = "",
                Location = new Point(10, 425),
                Size = new Size(560, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(100, 100, 120)
            };

            contentPanel.Controls.Add(statusLabel);
        }

        private void SetupEventHandlers()
        {
            // Dialog events
            closeButton.Click += (s, e) => Close();
            this.KeyDown += StackTraceNavigatorDialog_KeyDown;

            // Header drag functionality
            headerPanel.MouseDown += HeaderPanel_MouseDown;
            headerPanel.MouseMove += HeaderPanel_MouseMove;
            headerPanel.MouseUp += HeaderPanel_MouseUp;
            titleLabel.MouseDown += HeaderPanel_MouseDown;
            titleLabel.MouseMove += HeaderPanel_MouseMove;
            titleLabel.MouseUp += HeaderPanel_MouseUp;

            // Text input events
            stackTraceTextBox.TextChanged += StackTraceTextBox_TextChanged;

            // Results list events - navigate on single click (selection change)
            resultsListView.SelectedIndexChanged += ResultsListView_SelectedIndexChanged;

            // Form events
            this.FormClosed += StackTraceNavigatorDialog_FormClosed;
        }

        #region Event Handlers

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = e.Location;
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void HeaderPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging && e.Button == MouseButtons.Left)
            {
                Point newLocation = new(
                    this.Location.X + (e.X - dragStartPoint.X),
                    this.Location.Y + (e.Y - dragStartPoint.Y)
                );
                this.Location = newLocation;
            }
        }

        private void HeaderPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                this.Cursor = Cursors.Default;
            }
        }

        private void StackTraceTextBox_TextChanged(object? sender, EventArgs e)
        {
            // Restart the debounce timer
            parseTimer.Stop();
            parseTimer.Start();
        }

        private void ResultsListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Navigate to selected entry on single click
            if (resultsListView.SelectedItems.Count > 0)
            {
                var selectedItem = resultsListView.SelectedItems[0];
                if (selectedItem.Tag is StackTraceEntry entry)
                {
                    Task.Delay(100).ContinueWith(_ => { NavigateToSelectedEntry(entry); });
                }
            }
        }

        private void StackTraceNavigatorDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && resultsListView.SelectedItems.Count > 0)
            {
                var selectedItem = resultsListView.SelectedItems[0];
                if (selectedItem.Tag is StackTraceEntry entry)
                {
                    Task.Delay(100).ContinueWith(_ => { NavigateToSelectedEntry(entry); });
                }
                e.Handled = true;
            }
        }

        private void StackTraceNavigatorDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Stop and dispose timers
            alwaysOnTopTimer?.Stop();
            alwaysOnTopTimer?.Dispose();
            alwaysOnTopTimer = null;

            parseTimer?.Stop();
            parseTimer?.Dispose();
            parseTimer = null;

            // Remove always-on-top behavior when closing
            RemoveAlwaysOnTop();
        }

        #endregion

        private void ParseStackTrace()
        {
            try
            {
                string stackTraceText = stackTraceTextBox.Text;
                
                if (string.IsNullOrWhiteSpace(stackTraceText))
                {
                    currentEntries.Clear();
                    UpdateResultsList();
                    UpdateStatus("");
                    return;
                }

                UpdateStatus("Processing stack trace...");
                
                // Use the new comprehensive parsing method that front-loads all processing
                Task.Run(async () =>
                {
                    try
                    {
                        var processedEntries = await StackTraceParser.ParseAndProcessStackTraceAsync(stackTraceText, appDesignerProcess.DataManager);
                        
                        // Update UI on main thread
                        this.BeginInvoke(new System.Action(() =>
                        {
                            currentEntries = processedEntries;
                            UpdateResultsList();
                            UpdateValidationStatus();
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new System.Action(() =>
                        {
                            UpdateStatusError($"Error processing stack trace: {ex.Message}");
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                UpdateStatusError($"Error parsing stack trace: {ex.Message}");
            }
        }

        private void UpdateResultsList()
        {
            resultsListView.Items.Clear();
            
            foreach (var entry in currentEntries)
            {
                var item = new ListViewItem(entry.DisplayName);
                
                // Show navigation status
                string status = entry.IsValid ? "Valid" : "Invalid";
                
                item.SubItems.Add(status);
                item.Tag = entry;
                
                // Color entries based on status
                if (!entry.IsValid)
                {
                    item.BackColor = Color.FromArgb(255, 230, 230); // Light red for invalid
                    item.ToolTipText = entry.ErrorMessage ?? "Entry could not be validated";
                }
                else
                {
                    item.BackColor = Color.FromArgb(230, 255, 230); // Light green for ready
                    item.ToolTipText = "Ready for navigation";
                }
                
                resultsListView.Items.Add(item);
            }
        }

        private void UpdateValidationStatus()
        {
            int totalCount = currentEntries.Count;
            int validCount = currentEntries.Count(e => e.IsValid);
            
            if (totalCount > 0)
            {
                UpdateStatus($"{totalCount} entries, {validCount} ready");
            }
        }

        private void NavigateToSelectedEntry(StackTraceEntry entry)
        {
            try
            {
                if (!entry.IsValid || string.IsNullOrEmpty(entry.OpenTargetString))
                {
                    UpdateStatusError($"Cannot navigate: {entry.ErrorMessage ?? "Invalid entry"}");
                    return;
                }

                // Always set PendingSelection for consistent navigation behavior
                if (entry.SelectionSpan != null)
                {
                    appDesignerProcess.PendingSelection = entry.SelectionSpan;
                }
                else if (entry.ByteOffset.HasValue)
                {
                    // Fallback: create a minimal selection at the byte offset
                    appDesignerProcess.PendingSelection = new SourceSpan(
                        new SourcePosition(entry.ByteOffset.Value),
                        new SourcePosition(entry.ByteOffset.Value)
                    );
                }

                // Navigate using pre-calculated OpenTarget string
                appDesignerProcess.SetOpenTarget(entry.OpenTargetString);

                UpdateStatus($"Navigated to: {entry.DisplayName}");
            }
            catch (Exception ex)
            {
                UpdateStatusError($"Error navigating to entry: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = Color.FromArgb(100, 100, 120);
        }

        private void UpdateStatusError(string message)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = Color.Red;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw border
            using var pen = new Pen(Color.FromArgb(100, 100, 120));
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        }

        /// <summary>
        /// Ensures the dialog doesn't cover selected text by repositioning if necessary
        /// </summary>
        /// <param name="editor">The editor containing the selection</param>
        /// <param name="selection">The SourceSpan of the selected text</param>
        public void AvoidSelectionOverlap(ScintillaEditor editor, SourceSpan selection)
        {
            try
            {
                // Get screen rectangle of selected text
                Rectangle textRect = ScintillaManager.GetTextScreenRect(editor, selection.Start.ByteIndex, selection.End.ByteIndex);
                if (textRect.IsEmpty)
                    return;

                // Get current dialog screen position
                Rectangle dialogRect = new(this.Location, this.Size);

                // Check if dialog overlaps with selected text
                if (!dialogRect.IntersectsWith(textRect))
                    return; // No overlap, no need to move

                // Get screen bounds to ensure dialog stays on screen
                Rectangle screenBounds = Screen.FromHandle(this.Handle).WorkingArea;

                // Calculate new position - try different strategies in order of preference
                Point newLocation = Point.Empty;

                // Strategy 1: Try moving dialog above the text
                newLocation = new Point(dialogRect.X, textRect.Top - dialogRect.Height - 10);
                if (newLocation.Y >= screenBounds.Top)
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }

                // Strategy 2: Try moving dialog below the text
                newLocation = new Point(dialogRect.X, textRect.Bottom + 10);
                if (newLocation.Y + dialogRect.Height <= screenBounds.Bottom)
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }

                // Strategy 3: Try moving dialog to the left of text
                newLocation = new Point(textRect.Left - dialogRect.Width - 10, dialogRect.Y);
                if (newLocation.X >= screenBounds.Left)
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }

                // Strategy 4: Try moving dialog to the right of text
                newLocation = new Point(textRect.Right + 10, dialogRect.Y);
                if (newLocation.X + dialogRect.Width <= screenBounds.Right)
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }

                // Strategy 5: If all else fails, try to find any non-overlapping position
                // Check top-left quadrant
                newLocation = new Point(screenBounds.Left + 10, screenBounds.Top + 10);
                Rectangle testRect = new(newLocation, this.Size);
                if (!testRect.IntersectsWith(textRect))
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }

                // Check top-right quadrant
                newLocation = new Point(screenBounds.Right - dialogRect.Width - 10, screenBounds.Top + 10);
                testRect = new Rectangle(newLocation, this.Size);
                if (!testRect.IntersectsWith(textRect))
                {
                    SetNewLocationIfValid(newLocation, screenBounds);
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log error but don't let it break the navigation functionality
                Debug.Log($"Error avoiding selection overlap: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a new location for the dialog if it's valid (within screen bounds)
        /// </summary>
        /// <param name="newLocation">Proposed new location</param>
        /// <param name="screenBounds">Screen working area bounds</param>
        private void SetNewLocationIfValid(Point newLocation, Rectangle screenBounds)
        {
            // Ensure the dialog stays within screen bounds
            newLocation.X = Math.Max(screenBounds.Left, Math.Min(newLocation.X, screenBounds.Right - this.Width));
            newLocation.Y = Math.Max(screenBounds.Top, Math.Min(newLocation.Y, screenBounds.Bottom - this.Height));

            // Set the new location
            this.Location = newLocation;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}