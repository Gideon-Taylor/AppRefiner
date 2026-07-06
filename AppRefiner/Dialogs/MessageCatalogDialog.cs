using AppRefiner.Database;
using AppRefiner.Database.Models;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Message Catalog browser: browse sets, search messages, preview explain text,
    /// copy or insert message references. Read-only — creating catalog rows still
    /// happens in the PIA; this dialog removes every other reason to leave App Designer.
    /// </summary>
    public class MessageCatalogDialog : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int ResizeGripSize = 6;
        private const int SearchResultCap = 200;

        private readonly IDataManager dataManager;
        private readonly IntPtr owner;
        private readonly MessageCatalogCallContext? callContext;

        private readonly Panel headerPanel = new();
        private readonly Label headerLabel = new();
        private readonly SplitContainer splitContainer = new();
        private readonly TextBox setsFilterTextBox = new();
        private readonly LinkLabel unlockSetLink = new();
        private readonly ListView setsListView = new();
        private readonly FlowLayoutPanel searchPanel = new();
        private readonly TextBox searchTextBox = new();
        private readonly Label resultCapLabel = new();
        private readonly ListView messagesListView = new();
        private readonly RichTextBox previewTextBox = new();
        private readonly Panel buttonPanel = new();
        private readonly Button refreshButton = new();
        private readonly Button copyButton = new();
        private readonly ComboBox functionCombo = new();
        private readonly Button insertButton = new();
        private readonly Button closeButton = new();

        private readonly Button newMessageToggle = new();
        private readonly Panel newMessagePanel = new();
        private readonly Label rangesHeaderLabel = new();
        private readonly FlowLayoutPanel rangesFlow = new();
        private readonly Panel numberRowPanel = new();
        private readonly Panel buttonRowPanel = new();
        private readonly Label insertNoteLabel = new();
        private readonly TextBox numberTextBox = new();
        private readonly Label validationLabel = new();
        private readonly TextBox intendedTextBox = new();
        private readonly Button insertNewButton = new();

        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        private List<MessageSetInfo> allSets = new();
        private List<MessageCatalogEntry> currentSetMessages = new();

        public string? TextToInsert { get; private set; }

        private MessageCatalogEntry? SelectedEntry =>
            messagesListView.SelectedItems.Count > 0
                ? (MessageCatalogEntry)messagesListView.SelectedItems[0].Tag!
                : null;

        private int? SelectedSetNumber =>
            setsListView.SelectedItems.Count > 0
                ? ((MessageSetInfo)setsListView.SelectedItems[0].Tag!).SetNumber
                : null;

        public MessageCatalogDialog(IDataManager dataManager, IntPtr owner = default)
            : this(dataManager, null, owner)
        {
        }

        public MessageCatalogDialog(IDataManager dataManager, MessageCatalogCallContext? callContext,
            IntPtr owner = default)
        {
            this.dataManager = dataManager;
            this.callContext = callContext;
            this.owner = owner;
            InitializeComponent();
            LoadSets();
            ApplyCallContext();
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            this.Text = "Message Catalog";
            this.ClientSize = new Size(950, 620);
            this.MinimumSize = new Size(760, 480);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(ResizeGripSize);

            // header
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerLabel.Text = "Message Catalog";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.headerPanel.Controls.Add(this.headerLabel);

            // left pane: set filter + sets list
            // Tab order: the filter box is the only tab stop in the left pane (plus the
            // new-message fields when that panel is open), so Tab lands on message search
            // next — the flow is type-the-set, Tab, type-the-search.
            this.setsFilterTextBox.Dock = DockStyle.Top;
            this.setsFilterTextBox.TabIndex = 0;
            this.setsFilterTextBox.PlaceholderText = "Filter sets (number or description)";
            this.setsFilterTextBox.TextChanged += (s, e) => PopulateSetsList();

            this.unlockSetLink.Text = "Unlock set";
            this.unlockSetLink.AutoSize = true;
            this.unlockSetLink.Dock = DockStyle.Top;
            this.unlockSetLink.TabStop = false;
            this.unlockSetLink.Visible = false;
            this.unlockSetLink.LinkClicked += (s, e) =>
            {
                setsListView.Enabled = true;
                setsFilterTextBox.Enabled = true;
                unlockSetLink.Visible = false;
            };

            this.setsListView.Dock = DockStyle.Fill;
            this.setsListView.TabStop = false;
            this.setsListView.View = View.Details;
            this.setsListView.FullRowSelect = true;
            this.setsListView.HideSelection = false;
            this.setsListView.MultiSelect = false;
            this.setsListView.Columns.Add("Set", 70);
            this.setsListView.Columns.Add("Description", 180);
            this.setsListView.SelectedIndexChanged += (s, e) => OnSetSelected();

            // right pane: search row + message grid + preview
            this.searchPanel.Dock = DockStyle.Top;
            this.searchPanel.TabIndex = 0;
            this.searchPanel.Height = 34;
            this.searchPanel.FlowDirection = FlowDirection.LeftToRight;
            this.searchPanel.WrapContents = false;
            this.searchPanel.Padding = new Padding(6, 4, 6, 0);

            this.searchTextBox.Width = 320;
            this.searchTextBox.PlaceholderText = "Search messages (Enter searches listed sets)";
            this.searchTextBox.TextChanged += (s, e) => ApplyClientFilter();
            this.searchTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    RunServerSearch();
                }
            };

            this.resultCapLabel.AutoSize = true;
            this.resultCapLabel.Margin = new Padding(10, 9, 0, 0);
            this.resultCapLabel.ForeColor = Color.FromArgb(120, 120, 130);

            this.searchPanel.Controls.Add(this.searchTextBox);
            this.searchPanel.Controls.Add(this.resultCapLabel);

            this.messagesListView.Dock = DockStyle.Fill;
            this.messagesListView.TabIndex = 1;
            this.messagesListView.View = View.Details;
            this.messagesListView.FullRowSelect = true;
            this.messagesListView.HideSelection = false;
            this.messagesListView.MultiSelect = false;
            this.messagesListView.Columns.Add("Set", 60);
            this.messagesListView.Columns.Add("Num", 60);
            this.messagesListView.Columns.Add("Severity", 70);
            this.messagesListView.Columns.Add("Text", 380);
            this.messagesListView.SelectedIndexChanged += (s, e) => RenderPreview();
            this.messagesListView.DoubleClick += (s, e) => AcceptSelection();
            this.messagesListView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AcceptSelection(); }
            };

            this.previewTextBox.Dock = DockStyle.Bottom;
            this.previewTextBox.TabStop = false;
            this.previewTextBox.Height = 140;
            this.previewTextBox.ReadOnly = true;
            this.previewTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.previewTextBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.previewTextBox.BackColor = Color.White;

            // bottom buttons
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.TabIndex = 1;
            this.buttonPanel.Height = 50;

            this.refreshButton.Text = "Refresh";
            this.refreshButton.TabIndex = 0;
            this.refreshButton.Size = new Size(90, 30);
            this.refreshButton.Location = new Point(20, 10);
            this.refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            StyleAccentButton(this.refreshButton);
            this.refreshButton.Click += (s, e) => RefreshFromDatabase();

            this.copyButton.Text = "Copy set, num";
            this.copyButton.TabIndex = 1;
            this.copyButton.Size = new Size(120, 30);
            this.copyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.copyButton.Enabled = false;
            StyleAccentButton(this.copyButton);
            this.copyButton.Click += (s, e) =>
            {
                var entry = SelectedEntry;
                if (entry != null) Clipboard.SetText($"{entry.SetNumber}, {entry.MessageNumber}");
            };

            // function picker + insert
            this.functionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this.functionCombo.TabIndex = 2;
            this.functionCombo.Size = new Size(160, 30);
            this.functionCombo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.functionCombo.Items.AddRange(MessageCatalogFunctions.FunctionNames.Cast<object>().ToArray());
            this.functionCombo.SelectedItem =
                MessageCatalogFunctions.FunctionNames.Contains(Properties.Settings.Default.MessageCatalogInsertFunction)
                    ? Properties.Settings.Default.MessageCatalogInsertFunction
                    : "MsgGetText";

            this.insertButton.Text = "Insert";
            this.insertButton.TabIndex = 3;
            this.insertButton.Size = new Size(90, 30);
            this.insertButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.insertButton.Enabled = false;
            StyleAccentButton(this.insertButton);
            this.insertButton.Click += (s, e) => AcceptSelection();

            this.closeButton.Text = "Close";
            this.closeButton.TabIndex = 4;
            this.closeButton.Size = new Size(90, 30);
            this.closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.closeButton.BackColor = Color.FromArgb(100, 100, 100);
            this.closeButton.ForeColor = Color.White;
            this.closeButton.FlatStyle = FlatStyle.Flat;
            this.closeButton.FlatAppearance.BorderSize = 0;
            this.closeButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.buttonPanel.Controls.Add(this.refreshButton);
            this.buttonPanel.Controls.Add(this.copyButton);
            this.buttonPanel.Controls.Add(this.functionCombo);
            this.buttonPanel.Controls.Add(this.insertButton);
            this.buttonPanel.Controls.Add(this.closeButton);

            // new-message panel (collapsed by default)
            this.newMessageToggle.Text = "▸ New message…";
            this.newMessageToggle.Dock = DockStyle.Bottom;
            this.newMessageToggle.TabStop = false;
            this.newMessageToggle.Height = 28;
            this.newMessageToggle.FlatStyle = FlatStyle.Flat;
            this.newMessageToggle.FlatAppearance.BorderSize = 0;
            this.newMessageToggle.TextAlign = ContentAlignment.MiddleLeft;
            this.newMessageToggle.Click += (s, e) =>
            {
                newMessagePanel.Visible = !newMessagePanel.Visible;
                newMessageToggle.Text = newMessagePanel.Visible ? "▾ New message" : "▸ New message…";
                if (newMessagePanel.Visible) UpdateNewMessagePanel();
            };

            this.newMessagePanel.Dock = DockStyle.Bottom;
            this.newMessagePanel.Height = 250;
            this.newMessagePanel.Visible = false;
            this.newMessagePanel.Padding = new Padding(6);

            this.rangesHeaderLabel.Text = "Free number ranges";
            this.rangesHeaderLabel.Dock = DockStyle.Top;
            this.rangesHeaderLabel.Height = 18;
            this.rangesHeaderLabel.ForeColor = Color.FromArgb(120, 120, 130);

            // Fill-docked so the range chips get all the vertical space the
            // fixed rows below don't need — gappy sets can have dozens of ranges
            this.rangesFlow.Dock = DockStyle.Fill;
            this.rangesFlow.FlowDirection = FlowDirection.LeftToRight;
            this.rangesFlow.WrapContents = true;
            this.rangesFlow.AutoScroll = true;

            this.numberRowPanel.Dock = DockStyle.Bottom;
            this.numberRowPanel.TabIndex = 0;
            this.numberRowPanel.Height = 30;

            this.numberTextBox.Location = new Point(0, 3);
            this.numberTextBox.Width = 80;
            this.numberTextBox.TextChanged += (s, e) => ValidateChosenNumber();

            this.validationLabel.Location = new Point(88, 6);
            this.validationLabel.AutoSize = true;

            this.numberRowPanel.Controls.Add(this.numberTextBox);
            this.numberRowPanel.Controls.Add(this.validationLabel);

            this.intendedTextBox.Dock = DockStyle.Bottom;
            this.intendedTextBox.TabIndex = 1;
            this.intendedTextBox.PlaceholderText = "Intended message text (optional)";

            this.buttonRowPanel.Dock = DockStyle.Bottom;
            this.buttonRowPanel.TabIndex = 2;
            this.buttonRowPanel.Height = 36;

            this.insertNewButton.Text = "Insert code";
            this.insertNewButton.Size = new Size(100, 28);
            this.insertNewButton.Location = new Point(0, 5);
            this.insertNewButton.Enabled = false;
            StyleAccentButton(this.insertNewButton);
            this.insertNewButton.Click += (s, e) => AcceptNewMessage();

            this.buttonRowPanel.Controls.Add(this.insertNewButton);

            this.insertNoteLabel.Text = "Inserts code at the cursor only — the catalog entry itself must still be created online.";
            this.insertNoteLabel.Dock = DockStyle.Bottom;
            this.insertNoteLabel.Height = 30;
            this.insertNoteLabel.ForeColor = Color.FromArgb(120, 120, 130);

            // Fill first, then Top, then the Bottom rows: later-added Bottom-docked
            // controls land closer to the bottom edge (reverse z-order layout)
            this.newMessagePanel.Controls.Add(this.rangesFlow);
            this.newMessagePanel.Controls.Add(this.rangesHeaderLabel);
            this.newMessagePanel.Controls.Add(this.numberRowPanel);
            this.newMessagePanel.Controls.Add(this.intendedTextBox);
            this.newMessagePanel.Controls.Add(this.buttonRowPanel);
            this.newMessagePanel.Controls.Add(this.insertNoteLabel);

            // split container
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.TabIndex = 0;
            // The SplitContainer itself is focusable by default (keyboard splitter
            // movement) and steals a tab stop between the filter and search boxes
            this.splitContainer.TabStop = false;
            this.splitContainer.Orientation = Orientation.Vertical;
            this.splitContainer.FixedPanel = FixedPanel.Panel1;
            // Dock=Top controls dock outermost-last in the Controls.Add sequence,
            // so the link (added before the filter box) lands BELOW the filter box.
            this.splitContainer.Panel1.Controls.Add(this.setsListView);
            this.splitContainer.Panel1.Controls.Add(this.unlockSetLink);
            this.splitContainer.Panel1.Controls.Add(this.setsFilterTextBox);
            this.splitContainer.Panel1.Controls.Add(this.newMessageToggle);
            this.splitContainer.Panel1.Controls.Add(this.newMessagePanel);
            this.splitContainer.Panel2.Controls.Add(this.messagesListView);
            this.splitContainer.Panel2.Controls.Add(this.previewTextBox);
            this.splitContainer.Panel2.Controls.Add(this.searchPanel);

            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.buttonPanel);
            this.Controls.Add(this.headerPanel);

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            // Min sizes / splitter distance only after layout has resumed —
            // while suspended the SplitContainer is its default 150px and the
            // internal clamp throws (see SnapshotHistoryDialog).
            this.splitContainer.Panel1MinSize = 200;
            this.splitContainer.Panel2MinSize = 400;
            this.splitContainer.SplitterDistance = 270;

            // Right-anchored buttons captured offsets at default panel width;
            // re-position against the real width so anchors recapture.
            this.copyButton.Location = new Point(this.buttonPanel.ClientSize.Width - 500, 10);
            this.functionCombo.Location = new Point(this.buttonPanel.ClientSize.Width - 370, 12);
            this.insertButton.Location = new Point(this.buttonPanel.ClientSize.Width - 205, 10);
            this.closeButton.Location = new Point(this.buttonPanel.ClientSize.Width - 105, 10);

            // Developers reach for the set number first — start typing immediately.
            // (ApplyCallContext moves focus to message search when the set is already typed.)
            this.ActiveControl = this.setsFilterTextBox;
        }

        private static void StyleAccentButton(Button button)
        {
            button.BackColor = Color.FromArgb(0, 122, 204);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }

        private void LoadSets()
        {
            allSets = MessageCatalogCache.GetMessageSets(dataManager);
            PopulateSetsList();
        }

        /// <summary>
        /// Insert mode: the cursor sits inside a MsgGet-family call, so the function
        /// picker is hidden (the function is already typed at the call site) and, when
        /// the set number is already a typed literal, the set list locks to it.
        /// </summary>
        private void ApplyCallContext()
        {
            if (callContext == null) return;

            // The function is already typed at the call site — no picker
            functionCombo.Visible = false;
            headerLabel.Text = $"Message Catalog — inserting into {callContext.FunctionName}(…)";

            if (callContext.TypedSetNumber != null)
            {
                foreach (ListViewItem item in setsListView.Items)
                {
                    if (((MessageSetInfo)item.Tag!).SetNumber == callContext.TypedSetNumber.Value)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
                // Lock to the typed set, with an escape hatch in case the typed set was the mistake
                setsListView.Enabled = false;
                setsFilterTextBox.Enabled = false;
                unlockSetLink.Visible = true;

                // Set already known — searching for the message is the next step.
                // (ActiveControl works pre-show; Focus() would no-op before ShowDialog.)
                this.ActiveControl = this.searchTextBox;
            }
        }

        private void PopulateSetsList()
        {
            string filter = setsFilterTextBox.Text.Trim();
            setsListView.BeginUpdate();
            setsListView.Items.Clear();
            foreach (var set in allSets)
            {
                if (filter.Length > 0
                    && !set.SetNumber.ToString().Contains(filter)
                    && !set.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var item = new ListViewItem(set.SetNumber.ToString());
                item.SubItems.Add(set.Description);
                item.Tag = set;
                setsListView.Items.Add(item);
            }
            setsListView.EndUpdate();

            // Filter narrowed to exactly one set: select it so the grid loads and
            // the search box live-filters it immediately (type set → Tab → search)
            if (setsListView.Items.Count == 1 && !setsListView.Items[0].Selected)
            {
                setsListView.Items[0].Selected = true;
            }
        }

        private void OnSetSelected()
        {
            var setNumber = SelectedSetNumber;
            if (setNumber == null) return;

            currentSetMessages = MessageCatalogCache.GetMessagesForSet(dataManager, setNumber.Value);
            resultCapLabel.Text = "";
            ApplyClientFilter();
            if (newMessagePanel.Visible) UpdateNewMessagePanel();
        }

        /// <summary>Filter the loaded set client-side: matches message text OR explain text.</summary>
        private void ApplyClientFilter()
        {
            string term = searchTextBox.Text.Trim();
            var visible = term.Length == 0
                ? currentSetMessages
                : currentSetMessages.Where(m =>
                        m.MessageText.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || m.ExplainText.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            PopulateMessagesList(visible);
        }

        /// <summary>Server search scoped to the sets the filter box has narrowed the
        /// list to (empty filter = all sets). Matches message text plus the first
        /// 4000 characters of explain text (CLOB substring bound).</summary>
        private void RunServerSearch()
        {
            string term = searchTextBox.Text.Trim();
            if (term.Length == 0) return;

            resultCapLabel.ForeColor = Color.FromArgb(120, 120, 130);

            var scopeSets = setsListView.Items.Cast<ListViewItem>()
                .Select(item => ((MessageSetInfo)item.Tag!).SetNumber)
                .ToList();
            if (scopeSets.Count == 0)
            {
                resultCapLabel.Text = "no sets match the filter";
                PopulateMessagesList(new List<MessageCatalogEntry>());
                return;
            }

            // Full scope needs no predicate
            IReadOnlyCollection<int>? scope = scopeSets.Count == allSets.Count ? null : scopeSets;

            var results = dataManager.SearchMessageCatalog(term, scope, SearchResultCap);
            resultCapLabel.Text = results.Count >= SearchResultCap
                ? $"showing first {SearchResultCap}"
                : $"{results.Count} match{(results.Count == 1 ? "" : "es")}";
            PopulateMessagesList(results);
        }

        private void PopulateMessagesList(List<MessageCatalogEntry> messages)
        {
            messagesListView.BeginUpdate();
            messagesListView.Items.Clear();
            foreach (var message in messages)
            {
                var item = new ListViewItem(message.SetNumber.ToString());
                item.SubItems.Add(message.MessageNumber.ToString());
                item.SubItems.Add(message.Severity);
                item.SubItems.Add(message.MessageText);
                item.Tag = message;
                messagesListView.Items.Add(item);
            }
            messagesListView.EndUpdate();
            RenderPreview();
        }

        private void AcceptSelection()
        {
            var entry = SelectedEntry;
            if (entry == null) return;

            // At the num-arg position with a typed set literal, the set is fixed by the call
            // site — a message picked from a different set (via Unlock or a search scoped
            // beyond that set) would silently insert a number that's wrong for that set.
            if (callContext != null
                && callContext.CursorArgIndex == callContext.ArgInfo.NumArg
                && callContext.TypedSetNumber != null
                && entry.SetNumber != callContext.TypedSetNumber.Value)
            {
                resultCapLabel.ForeColor = Color.FromArgb(150, 60, 60);
                resultCapLabel.Text = $"message is in set {entry.SetNumber} — the call has set {callContext.TypedSetNumber} typed";
                return;
            }

            TextToInsert = BuildInsertText(entry.SetNumber, entry.MessageNumber, entry.MessageText);
            if (callContext == null)
            {
                Properties.Settings.Default.MessageCatalogInsertFunction = (string)functionCombo.SelectedItem!;
                Properties.Settings.Default.Save();
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Cold insert: a complete call using the picked function, with the catalog
        /// text as the default-message string. PeopleCode escapes quotes by doubling.
        /// In-call insert: only the tail the call still needs from the cursor's position.
        /// </summary>
        private string BuildInsertText(int setNumber, int messageNumber, string defaultText)
        {
            string escaped = defaultText.Replace("\"", "\"\"");

            if (callContext == null)
            {
                string functionName = (string)functionCombo.SelectedItem!;
                MessageCatalogFunctions.TryGetArgPositions(functionName, out var argInfo);
                return $"{functionName}({argInfo.ColdLeadingArgs}{setNumber}, {messageNumber}, \"{escaped}\")";
            }

            // In-call: emit only what the call still needs from the cursor's position.
            // If a default-text arg already exists, don't add another.
            string tail = callContext.HasDefaultTextArg ? "" : $", \"{escaped}\"";
            return callContext.CursorArgIndex == callContext.ArgInfo.SetArg
                ? $"{setNumber}, {messageNumber}{tail}"
                : $"{messageNumber}{tail}";
        }

        private void UpdateNewMessagePanel()
        {
            rangesFlow.Controls.Clear();
            if (SelectedSetNumber == null)
            {
                validationLabel.Text = "Select a message set first";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                return;
            }

            var usedNumbers = currentSetMessages.Select(m => m.MessageNumber).ToList();
            var ranges = MessageCatalogFreeRanges.Compute(usedNumbers);

            foreach (var range in ranges)
            {
                var link = new LinkLabel
                {
                    Text = range.Label,
                    AutoSize = true,
                    TabStop = false,
                    Margin = new Padding(0, 3, 16, 6),
                    Tag = range
                };
                link.LinkClicked += (s, e) =>
                    numberTextBox.Text = ((MessageNumberRange)((LinkLabel)s!).Tag!).Start.ToString();
                rangesFlow.Controls.Add(link);
            }

            // Pre-fill with next free after the highest used number (the last range is the open tail)
            numberTextBox.Text = ranges[^1].Start.ToString();
            ValidateChosenNumber();
        }

        private void ValidateChosenNumber()
        {
            if (SelectedSetNumber == null)
            {
                validationLabel.Text = "Select a message set first";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                insertNewButton.Enabled = false;
                return;
            }

            if (!int.TryParse(numberTextBox.Text.Trim(), out int number) || number < 1)
            {
                validationLabel.Text = "Enter a positive number";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                insertNewButton.Enabled = false;
                return;
            }

            var colliding = currentSetMessages.FirstOrDefault(m => m.MessageNumber == number);
            if (colliding != null)
            {
                string text = colliding.MessageText.Length > 60
                    ? colliding.MessageText.Substring(0, 60) + "…"
                    : colliding.MessageText;
                validationLabel.Text = $"✗ {number} exists: \"{text}\"";
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                insertNewButton.Enabled = false;
            }
            else
            {
                validationLabel.Text = $"✓ {number} is free";
                validationLabel.ForeColor = Color.FromArgb(40, 130, 60);
                insertNewButton.Enabled = true;
            }
        }

        private void AcceptNewMessage()
        {
            if (SelectedSetNumber == null) return;

            // Same cross-set hole as AcceptSelection: at the num-arg position with a typed
            // set literal, a number chosen against a different (unlocked) set would be wrong.
            if (callContext != null
                && callContext.CursorArgIndex == callContext.ArgInfo.NumArg
                && callContext.TypedSetNumber != null
                && SelectedSetNumber.Value != callContext.TypedSetNumber.Value)
            {
                validationLabel.ForeColor = Color.FromArgb(150, 60, 60);
                validationLabel.Text = $"number is for set {SelectedSetNumber} — the call has set {callContext.TypedSetNumber} typed";
                return;
            }

            if (!int.TryParse(numberTextBox.Text.Trim(), out int number)) return;

            TextToInsert = BuildInsertText(SelectedSetNumber.Value, number, intendedTextBox.Text);
            if (callContext == null)
            {
                Properties.Settings.Default.MessageCatalogInsertFunction = (string)functionCombo.SelectedItem!;
                Properties.Settings.Default.Save();
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void RenderPreview()
        {
            var entry = SelectedEntry;
            copyButton.Enabled = entry != null;
            insertButton.Enabled = entry != null;
            if (entry == null)
            {
                previewTextBox.Text = "";
                return;
            }
            previewTextBox.Text = string.IsNullOrWhiteSpace(entry.ExplainText)
                ? entry.MessageText
                : $"{entry.MessageText}\n\nExplain:\n{entry.ExplainText.Trim()}";
        }

        private void RefreshFromDatabase()
        {
            var keepSet = SelectedSetNumber;
            MessageCatalogCache.Clear(dataManager);
            LoadSets();
            if (keepSet != null)
            {
                foreach (ListViewItem item in setsListView.Items)
                {
                    if (((MessageSetInfo)item.Tag!).SetNumber == keepSet.Value)
                    {
                        item.Selected = true;
                        break;
                    }
                }
            }
        }

        // ---- borderless-form boilerplate: identical in structure to SnapshotHistoryDialog ----

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Edge resizing for the borderless form
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                // Signed extraction handles negative coords on multi-monitor setups
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));
                var pos = PointToClient(new Point(x, y));

                bool left = pos.X < ResizeGripSize;
                bool right = pos.X >= ClientSize.Width - ResizeGripSize;
                bool top = pos.Y < ResizeGripSize;
                bool bottom = pos.Y >= ClientSize.Height - ResizeGripSize;

                if (top && left) m.Result = (IntPtr)13;          // HTTOPLEFT
                else if (top && right) m.Result = (IntPtr)14;    // HTTOPRIGHT
                else if (bottom && left) m.Result = (IntPtr)16;  // HTBOTTOMLEFT
                else if (bottom && right) m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                else if (left) m.Result = (IntPtr)10;            // HTLEFT
                else if (right) m.Result = (IntPtr)11;           // HTRIGHT
                else if (top) m.Result = (IntPtr)12;             // HTTOP
                else if (bottom) m.Result = (IntPtr)15;          // HTBOTTOM
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (owner != IntPtr.Zero)
            {
                WindowHelper.CenterFormOnWindow(this, owner);
            }

            if (this.Modal && owner != IntPtr.Zero)
            {
                mouseHandler = new DialogHelper.ModalDialogMouseHandler(this, headerPanel, owner);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using var pen = new Pen(Color.FromArgb(100, 100, 120));
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}
