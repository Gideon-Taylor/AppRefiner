using System.Runtime.InteropServices;

namespace AppRefiner.Dialogs
{
    public partial class BetterFindDialog : Form
    {
        private readonly ScintillaEditor editor;
        //private DialogHelper.ModalDialogMouseHandler? mouseHandler;
        private readonly IntPtr owner;

        // Windows API constants and imports for always-on-top behavior
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // UI Controls
        private Panel headerPanel;
        private Label titleLabel;
        private Button closeButton;
        private Panel contentPanel;
        private ComboBox findComboBox;
        private ComboBox replaceComboBox;
        private CheckBox enableReplaceCheckBox;
        private GroupBox optionsGroupBox;
        private CheckBox matchCaseCheckBox;
        private CheckBox wholeWordCheckBox;
        private CheckBox wordStartCheckBox;
        private CheckBox useRegexCheckBox;
        private CheckBox wrapAroundCheckBox;
        private RadioButton selectionRadioButton;
        private RadioButton wholeDocumentRadioButton;
        private RadioButton methodRadioButton;
        private Button findNextButton;
        private Button findPreviousButton;
        private Button replaceButton;
        private Button replaceAllButton;
        private Button countButton;
        private Button markAllButton;
        private Button findAllButton;
        private CheckBox showResultsCheckBox;
        private Panel resultsPanel;
        private ListBox resultsListBox;
        private Splitter resultsSplitter;
        private Label statusLabel;

        // Replace mode state
        private bool replaceMode = false;

        /// <summary>
        /// Gets whether the dialog is currently in replace mode
        /// </summary>
        public bool IsReplaceMode => replaceMode;

        /// <summary>
        /// Enables replace mode for the dialog
        /// </summary>
        public void EnableReplaceMode()
        {
            if (!replaceMode)
            {
                enableReplaceCheckBox.Checked = true;
                UpdateReplaceVisibility();
            }
        }

        // Timer to ensure always-on-top behavior persists
        private System.Windows.Forms.Timer alwaysOnTopTimer;

        // Variables for drag functionality
        private bool isDragging = false;
        private Point dragStartPoint;

        public BetterFindDialog(ScintillaEditor editor, IntPtr ownerHandle, bool enableReplaceMode = false)
        {
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
            this.owner = ownerHandle;

            InitializeComponent();
            InitializeUI();
            LoadFromEditor();
            SetupEventHandlers();

            // Apply AppRefiner dialog styling and modal behavior
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Font = new Font("Segoe UI", 9F);
            this.Size = new Size(500, 300);
            this.Load += BetterFindDialog_Load;
            // Enable replace mode if requested
            if (enableReplaceMode)
            {
                enableReplaceCheckBox.Checked = true;
                UpdateReplaceVisibility();
            }

            // Create modal mouse handler
            //mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, ownerHandle);

            // Set Find Next as the default button for Enter key presses
            this.AcceptButton = findNextButton;

            // Center on owner window
            if (ownerHandle != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, ownerHandle);
            }

            // Initialize timer to maintain always-on-top behavior
            InitializeAlwaysOnTopTimer();
        }

        private void BetterFindDialog_Load(object? sender, EventArgs e)
        {
            findComboBox.Focus();
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
        /// Makes this window always stay on top using Windows API
        /// </summary>
        public void MakeAlwaysOnTop()
        {
            if (this.Handle != IntPtr.Zero)
            {
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        /// <summary>
        /// Removes always-on-top behavior
        /// </summary>
        public void RemoveAlwaysOnTop()
        {
            if (this.Handle != IntPtr.Zero)
            {
                SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
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
                // Check if current foreground window belongs to AppRefiner.exe or pside.exe
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(foregroundWindow, out uint processId);
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById((int)processId);
                        string processName = process.ProcessName.ToLowerInvariant();

                        // Close dialog if foreground window doesn't belong to our target processes
                        if (processName != "apprefiner" && processName != "pside")
                        {
                            this.Close();
                            return;
                        }
                    }
                    catch
                    {
                        // If we can't get process info, assume it's not our process and close
                        this.Close();
                        return;
                    }
                }

                BringWindowToTop(this.Handle);
                MakeAlwaysOnTop();
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                // Make always on top when the dialog becomes visible
                MakeAlwaysOnTop();
                // Start the timer to maintain always-on-top behavior
                alwaysOnTopTimer?.Start();

                // Ensure focus goes to the find combo box
                this.BeginInvoke(new Action(() =>
                {
                    this.Activate();
                    findComboBox.Focus();
                    findComboBox.SelectAll();
                }));
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
            // following AppRefiner patterns

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
                Text = "Better Find",
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

            CreateFindControls();
            CreateReplaceControls();
            CreateOptionsControls();
            CreateButtonControls();
            CreateResultsControls();
            CreateStatusControls();

            // Add panels to form
            this.Controls.Add(contentPanel);
            this.Controls.Add(headerPanel);

            // Set initial replace mode visibility
            UpdateReplaceVisibility();
        }

        private void CreateFindControls()
        {
            var findLabel = new Label
            {
                Text = "Find:",
                Location = new Point(10, 8),
                Size = new Size(40, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            findComboBox = new ComboBox
            {
                Location = new Point(55, 5),
                Size = new Size(320, 23),
                DropDownStyle = ComboBoxStyle.DropDown
            };


            contentPanel.Controls.Add(findLabel);
            contentPanel.Controls.Add(findComboBox);
        }

        private void CreateReplaceControls()
        {
            enableReplaceCheckBox = new CheckBox
            {
                Text = "Replace",
                Location = new Point(385, 8),
                Size = new Size(80, 20),
                FlatStyle = FlatStyle.Flat
            };

            var replaceLabel = new Label
            {
                Text = "Replace:",
                Location = new Point(10, 38),
                Size = new Size(55, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            replaceComboBox = new ComboBox
            {
                Location = new Point(70, 35),
                Size = new Size(305, 23),
                DropDownStyle = ComboBoxStyle.DropDown
            };


            contentPanel.Controls.Add(enableReplaceCheckBox);
            contentPanel.Controls.Add(replaceLabel);
            contentPanel.Controls.Add(replaceComboBox);
        }

        private void CreateOptionsControls()
        {
            optionsGroupBox = new GroupBox
            {
                Text = "Options",
                Location = new Point(10, 40),
                Size = new Size(480, 130),
                FlatStyle = FlatStyle.Flat
            };

            // Search options column 1
            matchCaseCheckBox = new CheckBox
            {
                Text = "Match case",
                Location = new Point(10, 25),
                Size = new Size(100, 20),
                FlatStyle = FlatStyle.Flat
            };

            wholeWordCheckBox = new CheckBox
            {
                Text = "Whole word",
                Location = new Point(10, 50),
                Size = new Size(100, 20),
                FlatStyle = FlatStyle.Flat
            };

            wordStartCheckBox = new CheckBox
            {
                Text = "Word start",
                Location = new Point(10, 75),
                Size = new Size(100, 20),
                FlatStyle = FlatStyle.Flat
            };

            // Regex options column 2
            useRegexCheckBox = new CheckBox
            {
                Text = "Regular expression",
                Location = new Point(130, 25),
                Size = new Size(130, 20),
                FlatStyle = FlatStyle.Flat
            };

            wrapAroundCheckBox = new CheckBox
            {
                Text = "Wrap around",
                Location = new Point(130, 45),
                Size = new Size(100, 20),
                FlatStyle = FlatStyle.Flat
            };


            // Scope options column 3
            var scopeGroupBox = new GroupBox
            {
                Text = "Scope",
                Location = new Point(370, 15),
                Size = new Size(100, 105),
                FlatStyle = FlatStyle.Flat
            };

            wholeDocumentRadioButton = new RadioButton
            {
                Text = "Document",
                Location = new Point(10, 20),
                Size = new Size(85, 20),
                Checked = true,
                FlatStyle = FlatStyle.Flat
            };

            selectionRadioButton = new RadioButton
            {
                Text = "Selection",
                Location = new Point(10, 45),
                Size = new Size(85, 20),
                FlatStyle = FlatStyle.Flat,
                Enabled = false // Will be enabled if there's a selection
            };

            methodRadioButton = new RadioButton
            {
                Text = "Method",
                Location = new Point(10, 70),
                Size = new Size(85, 20),
                FlatStyle = FlatStyle.Flat
            };

            scopeGroupBox.Controls.Add(wholeDocumentRadioButton);
            scopeGroupBox.Controls.Add(selectionRadioButton);
            scopeGroupBox.Controls.Add(methodRadioButton);

            optionsGroupBox.Controls.Add(matchCaseCheckBox);
            optionsGroupBox.Controls.Add(wholeWordCheckBox);
            optionsGroupBox.Controls.Add(wordStartCheckBox);
            optionsGroupBox.Controls.Add(useRegexCheckBox);
            optionsGroupBox.Controls.Add(wrapAroundCheckBox);
            optionsGroupBox.Controls.Add(scopeGroupBox);

            contentPanel.Controls.Add(optionsGroupBox);
        }

        private void CreateButtonControls()
        {
            var buttonY = 180;
            var buttonWidth = 100;
            var buttonHeight = 28;
            var buttonSpacing = 110;

            // Row 1: Find Next, Find Previous, Count, Mark All
            findNextButton = new Button
            {
                Text = "Find Next",
                Location = new Point(10, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            findPreviousButton = new Button
            {
                Text = "Find Previous",
                Location = new Point(10 + buttonSpacing, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            countButton = new Button
            {
                Text = "Count",
                Location = new Point(10 + buttonSpacing * 2, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            markAllButton = new Button
            {
                Text = "Mark All",
                Location = new Point(10 + buttonSpacing * 3, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            // Row 2: Replace, Replace All, Find All, Show Results checkbox
            var buttonY2 = buttonY + 35;

            replaceButton = new Button
            {
                Text = "Replace",
                Location = new Point(10, buttonY2),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            replaceAllButton = new Button
            {
                Text = "Replace All",
                Location = new Point(10 + buttonSpacing, buttonY2),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            findAllButton = new Button
            {
                Text = "Find All",
                Location = new Point(10 + buttonSpacing * 2, buttonY2),
                Size = new Size(buttonWidth, buttonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 225),
                UseVisualStyleBackColor = false
            };

            showResultsCheckBox = new CheckBox
            {
                Text = "Show Results",
                Location = new Point(10 + buttonSpacing * 3, buttonY2 + 5),
                Size = new Size(100, 20),
                FlatStyle = FlatStyle.Flat
            };

            contentPanel.Controls.Add(findNextButton);
            contentPanel.Controls.Add(findPreviousButton);
            contentPanel.Controls.Add(countButton);
            contentPanel.Controls.Add(markAllButton);
            contentPanel.Controls.Add(replaceButton);
            contentPanel.Controls.Add(replaceAllButton);
            contentPanel.Controls.Add(findAllButton);
            contentPanel.Controls.Add(showResultsCheckBox);
        }

        private void CreateResultsControls()
        {
            // Create splitter for resizing
            resultsSplitter = new Splitter
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                BackColor = Color.FromArgb(200, 200, 210),
                Visible = false
            };

            // Create results panel
            resultsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 150,
                BackColor = Color.FromArgb(250, 250, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Padding = new Padding(5)
            };

            // Create results list box
            resultsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.25F),
                IntegralHeight = false
            };

            resultsPanel.Controls.Add(resultsListBox);
            contentPanel.Controls.Add(resultsSplitter);
            contentPanel.Controls.Add(resultsPanel);
        }

        private void CreateStatusControls()
        {
            statusLabel = new Label
            {
                Text = "",
                Location = new Point(10, 240),
                Size = new Size(460, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(100, 100, 120)
            };

            contentPanel.Controls.Add(statusLabel);
        }

        private void SetupEventHandlers()
        {
            // Dialog events
            closeButton.Click += (s, e) => Close();
            this.KeyDown += BetterFindDialog_KeyDown;

            // Header drag functionality
            headerPanel.MouseDown += HeaderPanel_MouseDown;
            headerPanel.MouseMove += HeaderPanel_MouseMove;
            headerPanel.MouseUp += HeaderPanel_MouseUp;
            titleLabel.MouseDown += HeaderPanel_MouseDown;
            titleLabel.MouseMove += HeaderPanel_MouseMove;
            titleLabel.MouseUp += HeaderPanel_MouseUp;

            // Replace mode toggle
            enableReplaceCheckBox.CheckedChanged += EnableReplaceCheckBox_CheckedChanged;

            // Search option events
            useRegexCheckBox.CheckedChanged += UseRegexCheckBox_CheckedChanged;
            findComboBox.TextChanged += FindComboBox_TextChanged;
            findComboBox.DropDown += FindComboBox_DropDown;
            findComboBox.DropDownClosed += FindComboBox_DropDownClosed;
            // Button events
            findNextButton.Click += FindNextButton_Click;
            findPreviousButton.Click += FindPreviousButton_Click;
            replaceButton.Click += ReplaceButton_Click;
            replaceAllButton.Click += ReplaceAllButton_Click;
            countButton.Click += CountButton_Click;
            markAllButton.Click += MarkAllButton_Click;
            findAllButton.Click += FindAllButton_Click;
            showResultsCheckBox.CheckedChanged += ShowResultsCheckBox_CheckedChanged;
            resultsListBox.SelectedIndexChanged += ResultsListBox_SelectedIndexChanged;

            // Form events
            this.FormClosed += BetterFindDialog_FormClosed;
        }

        private void FindComboBox_DropDownClosed(object? sender, EventArgs e)
        {
            alwaysOnTopTimer.Enabled = true; // Disable timer while dropdown is open
        }

        private void FindComboBox_DropDown(object? sender, EventArgs e)
        {
            alwaysOnTopTimer.Enabled = false; // Disable timer while dropdown is open
        }

        private void LoadFromEditor()
        {
            var searchState = editor.SearchState;

            // Load search term and history
            findComboBox.Text = searchState.LastSearchTerm;
            findComboBox.Items.Clear();
            foreach (var term in searchState.SearchHistory)
            {
                findComboBox.Items.Add(term);
            }

            // Load replace text and history
            replaceComboBox.Text = searchState.LastReplaceText;
            replaceComboBox.Items.Clear();
            foreach (var term in searchState.ReplaceHistory)
            {
                replaceComboBox.Items.Add(term);
            }

            // Load options
            matchCaseCheckBox.Checked = searchState.MatchCase;
            wholeWordCheckBox.Checked = searchState.WholeWord;
            wordStartCheckBox.Checked = searchState.WordStart;
            useRegexCheckBox.Checked = searchState.UseRegex;
            wrapAroundCheckBox.Checked = searchState.WrapSearch;
            // Always use POSIX regex when regex is enabled

            // Check if there's a selection and store it in SearchState
            var (selectedText, selStart, selEnd) = ScintillaManager.GetSelectedText(editor);
            if (!string.IsNullOrEmpty(selectedText))
            {
                selectionRadioButton.Enabled = true;
                // Store the initial selection range for search scope
                searchState.SetSelectionRange(selStart, selEnd);

                // Determine if selection spans multiple lines using smart logic
                int startLine = ScintillaManager.GetLineFromPosition(editor, selStart);
                int endLine = ScintillaManager.GetLineFromPosition(editor, selEnd);

                if (startLine == endLine)
                {
                    // Same line: Use selected text as search term, keep Document scope
                    if (string.IsNullOrEmpty(findComboBox.Text))
                    {
                        findComboBox.Text = selectedText;
                    }
                    wholeDocumentRadioButton.Checked = true;
                    selectionRadioButton.Checked = false;
                    methodRadioButton.Checked = false;
                    searchState.SearchInSelection = false;
                    searchState.SearchInMethod = false;
                }
                else
                {
                    // Multi-line: Clear search term, use Selection scope
                    findComboBox.Text = "";
                    wholeDocumentRadioButton.Checked = false;
                    selectionRadioButton.Checked = true;
                    methodRadioButton.Checked = false;
                    searchState.SearchInSelection = true;
                    searchState.SearchInMethod = false;
                }
            }
            else
            {
                // No selection, clear stored range and use previous scope preference
                searchState.ClearSelectionRange();
                // Load scope preference
                wholeDocumentRadioButton.Checked = !searchState.SearchInSelection && !searchState.SearchInMethod;
                selectionRadioButton.Checked = searchState.SearchInSelection;
                methodRadioButton.Checked = searchState.SearchInMethod;
            }

        }

        private void SaveToEditor()
        {
            var searchState = editor.SearchState;

            // Save current search and replace terms
            searchState.LastSearchTerm = findComboBox.Text;
            searchState.LastReplaceText = replaceComboBox.Text;

            // Update histories
            if (!string.IsNullOrWhiteSpace(findComboBox.Text))
            {
                searchState.UpdateSearchHistory(findComboBox.Text);
            }
            if (!string.IsNullOrWhiteSpace(replaceComboBox.Text))
            {
                searchState.UpdateReplaceHistory(replaceComboBox.Text);
            }

            // Save options
            searchState.MatchCase = matchCaseCheckBox.Checked;
            searchState.WholeWord = wholeWordCheckBox.Checked;
            searchState.WordStart = wordStartCheckBox.Checked;
            searchState.UseRegex = useRegexCheckBox.Checked;
            searchState.WrapSearch = wrapAroundCheckBox.Checked;
            // Always use POSIX regex when regex is enabled
            searchState.UsePosixRegex = useRegexCheckBox.Checked;
            searchState.UseCxx11Regex = false;
            searchState.SearchInSelection = selectionRadioButton.Checked;
            searchState.SearchInMethod = methodRadioButton.Checked;
        }

        private void UpdateReplaceVisibility()
        {
            var needsHeightAdjust = (replaceMode != enableReplaceCheckBox.Checked);

            replaceMode = enableReplaceCheckBox.Checked;

            replaceComboBox.Visible = replaceMode;
            replaceButton.Visible = replaceMode;
            replaceAllButton.Visible = replaceMode;

            // Find the replace label and hide/show it
            foreach (Control control in contentPanel.Controls)
            {
                if (control is Label label && label.Text == "Replace:")
                {
                    label.Visible = replaceMode;
                    break;
                }
            }

            if (needsHeightAdjust)
            {
                var deltaY = replaceMode ? 30 : -30;
                this.Height += deltaY;

                optionsGroupBox.Location = new Point(optionsGroupBox.Location.X, optionsGroupBox.Location.Y + deltaY);
                findNextButton.Location = new Point(findNextButton.Location.X, findNextButton.Location.Y + deltaY);
                findPreviousButton.Location = new Point(findPreviousButton.Location.X, findPreviousButton.Location.Y + deltaY);
                countButton.Location = new Point(countButton.Location.X, countButton.Location.Y + deltaY);
                markAllButton.Location = new Point(markAllButton.Location.X, markAllButton.Location.Y + deltaY);
                replaceButton.Location = new Point(replaceButton.Location.X, replaceButton.Location.Y + deltaY);
                replaceAllButton.Location = new Point(replaceAllButton.Location.X, replaceAllButton.Location.Y + deltaY);
                findAllButton.Location = new Point(findAllButton.Location.X, findAllButton.Location.Y + deltaY);
                showResultsCheckBox.Location = new Point(showResultsCheckBox.Location.X, showResultsCheckBox.Location.Y + deltaY);
                statusLabel.Location = new Point(statusLabel.Location.X, statusLabel.Location.Y + deltaY);
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

        private void UpdateResultsVisibility()
        {
            bool showResults = showResultsCheckBox.Checked;
            resultsPanel.Visible = showResults;
            resultsSplitter.Visible = showResults;

            // Adjust dialog height when results are shown/hidden
            if (showResults)
            {
                this.Height = 320 + 150; // Base height + results height
            }
            else
            {
                this.Height = 320; // Base height
            }
        }

        private void PopulateResultsList(List<SearchMatch> matches)
        {
            resultsListBox.Items.Clear();
            foreach (var match in matches)
            {
                resultsListBox.Items.Add(match);
            }

            // Select first item if available
            if (resultsListBox.Items.Count > 0)
            {
                resultsListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Ensures the dialog doesn't cover found text by repositioning if necessary
        /// </summary>
        /// <param name="textStartPos">Start position of found text</param>
        /// <param name="textEndPos">End position of found text</param>
        private void AvoidTextOverlap(int textStartPos, int textEndPos)
        {
            try
            {
                // Get screen rectangle of found text
                Rectangle textRect = ScintillaManager.GetTextScreenRect(editor, textStartPos, textEndPos);
                if (textRect.IsEmpty)
                    return;

                // Get current dialog screen position
                Rectangle dialogRect = new(this.Location, this.Size);

                // Check if dialog overlaps with found text
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
                // Log error but don't let it break the search functionality
                Debug.LogError($"Error avoiding text overlap: {ex.Message}");
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

        #region Event Handlers

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = e.Location;
                // Change cursor to indicate dragging
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void HeaderPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isDragging && e.Button == MouseButtons.Left)
            {
                // Calculate the new position
                Point newLocation = new(
                    this.Location.X + (e.X - dragStartPoint.X),
                    this.Location.Y + (e.Y - dragStartPoint.Y)
                );

                // Move the form
                this.Location = newLocation;
            }
        }

        private void HeaderPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                // Reset cursor
                this.Cursor = Cursors.Default;
            }
        }

        private void EnableReplaceCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateReplaceVisibility();
        }

        private void UseRegexCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            // Validate regex when enabling regex mode
            if (useRegexCheckBox.Checked)
            {
                ValidateRegex();
            }
            else
            {
                UpdateStatus("");
            }
        }

        private void FindComboBox_TextChanged(object? sender, EventArgs e)
        {
            SaveToEditor();
            UpdateStatus("");

            // Validate regex if regex mode is enabled
            if (useRegexCheckBox.Checked)
            {
                ValidateRegex();
            }
        }

        private void ValidateRegex()
        {
            try
            {
                if (string.IsNullOrEmpty(findComboBox.Text))
                {
                    UpdateStatus("");
                    return;
                }

                // Test compile the regex to check for syntax errors (always POSIX)
                var regexOptions = System.Text.RegularExpressions.RegexOptions.None;
                if (!matchCaseCheckBox.Checked)
                    regexOptions |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;

                var regex = new System.Text.RegularExpressions.Regex(findComboBox.Text, regexOptions);
                UpdateStatus($"Regex is valid");
            }
            catch (ArgumentException ex)
            {
                UpdateStatusError($"Invalid regex: {ex.Message}");
            }
        }

        private void FindNextButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) { return; }
            if (ScintillaManager.FindNext(editor))
            {
                UpdateStatus("Text found");

                // Avoid covering the found text with the dialog
                var (selectedText, selStart, selEnd) = ScintillaManager.GetSelectedText(editor);
                if (!string.IsNullOrEmpty(selectedText))
                {
                    AvoidTextOverlap(selStart, selEnd);
                }
            }
            else
            {
                UpdateStatusError("Text not found");
            }
        }

        private void FindPreviousButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) { return; }
            if (ScintillaManager.FindPrevious(editor))
            {
                UpdateStatus("Text found");

                // Avoid covering the found text with the dialog
                var (selectedText, selStart, selEnd) = ScintillaManager.GetSelectedText(editor);
                if (!string.IsNullOrEmpty(selectedText))
                {
                    AvoidTextOverlap(selStart, selEnd);
                }
            }
            else
            {
                UpdateStatusError("Text not found");
            }
        }

        private void ReplaceButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) { return; }
            if (ScintillaManager.ReplaceSelection(editor, replaceComboBox.Text))
            {
                UpdateStatus("Text replaced");
                // Find next occurrence
                FindNextButton_Click(sender, e);
            }
            else
            {
                UpdateStatusError("No text selected for replacement");
            }

        }

        private void ReplaceAllButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) { return; }
            int count = ScintillaManager.ReplaceAll(editor, findComboBox.Text, replaceComboBox.Text);
            if (count > 0)
            {
                UpdateStatus($"Replaced {count} occurrence(s)");
            }
            else
            {
                UpdateStatusError("No occurrences found to replace");
            }
        }

        private void BetterFindDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                if (e.Shift)
                {
                    FindPreviousButton_Click(sender, e);
                }
                else
                {
                    FindNextButton_Click(sender, e);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (e.Control)
                {
                    // Ctrl+Enter: Replace/Replace All functionality
                    if (replaceMode)
                    {
                        if (e.Shift)
                        {
                            ReplaceAllButton_Click(sender, e);
                        }
                        else
                        {
                            ReplaceButton_Click(sender, e);
                        }
                    }
                    else
                    {
                        FindNextButton_Click(sender, e);
                    }
                    e.Handled = true;
                }
                else
                {
                    // Plain Enter: Find Next (default button behavior)
                    FindNextButton_Click(sender, e);
                    e.Handled = true;
                }
            }
        }

        private void BetterFindDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveToEditor();

            // Clear search highlights and indicators when dialog closes
            ScintillaManager.ClearSearchHighlights(editor);
            ScintillaManager.ClearSearchIndicators(editor);

            // Stop and dispose timer
            alwaysOnTopTimer?.Stop();
            alwaysOnTopTimer?.Dispose();
            alwaysOnTopTimer = null;

            // Remove always-on-top behavior when closing
            RemoveAlwaysOnTop();

            //mouseHandler?.Dispose();
        }

        private void CountButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) return;

            int count = ScintillaManager.CountMatches(editor, findComboBox.Text);
            if (count > 0)
                UpdateStatus($"Found {count} occurrence(s)");
            else
                UpdateStatusError("No occurrences found");
        }

        private void MarkAllButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) return;

            // Clear existing search indicators first
            ScintillaManager.ClearSearchIndicators(editor);

            int count = ScintillaManager.MarkAllMatches(editor, findComboBox.Text);
            if (count > 0)
                UpdateStatus($"Marked {count} occurrence(s)");
            else
                UpdateStatusError("No occurrences found to mark");
        }

        private void FindAllButton_Click(object sender, EventArgs e)
        {
            SaveToEditor();
            if (!editor.SearchState.HasValidSearch) return;

            var matches = ScintillaManager.FindAllMatches(editor, findComboBox.Text);
            PopulateResultsList(matches);

            if (matches.Count > 0)
            {
                showResultsCheckBox.Checked = true; // This will trigger UpdateResultsVisibility
                UpdateStatus($"Found {matches.Count} occurrence(s) in document");
            }
            else
            {
                UpdateStatusError("No occurrences found");
            }
        }

        private void ShowResultsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateResultsVisibility();
        }

        private void ResultsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (resultsListBox.SelectedItem is SearchMatch match)
            {
                // Navigate to match position
                ScintillaManager.SetSelection(editor, match.Position, match.Position + match.Length);

                // Move dialog away from selection
                AvoidTextOverlap(match.Position, match.Position + findComboBox.Text.Length);

                UpdateStatus($"Navigated to line {match.LineNumber + 1}, Position {match.Position}");
            }
        }

        #endregion

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw border
            using var pen = new Pen(Color.FromArgb(100, 100, 120));
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
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