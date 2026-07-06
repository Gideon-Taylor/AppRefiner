# Snapshot History Dialog Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the click-and-inspect Snapshot History dialog with a split-view dialog: snapshot list with change stats on the left, live diff preview on the right, plus Copy Original / Copy Diff / Apply actions.

**Architecture:** A new static `UnifiedDiffBuilder` helper (over DiffPlex) produces line diffs, collapsed hunks, per-snapshot stats, and unified-diff text. `SnapshotHistoryDialog` is rewritten around a `SplitContainer` with a docked, edge-resizable borderless form. Two new persisted user settings hold the diff view mode and direction.

**Tech Stack:** .NET 8 WinForms, DiffPlex 1.9.0 (already referenced), `Properties.Settings` for persistence.

**Spec:** `docs/superpowers/specs/2026-07-03-snapshot-dialog-rework-design.md`

## Global Constraints

- **NO commits.** Tim has parallel uncommitted work on main; he will commit this feature himself later. Every "commit" step normally found in plans is intentionally absent.
- **Do NOT touch the "what's new" document** (`AppRefiner/Resources/whatsnew*` or similar) — it overlaps with Tim's other in-flight work.
- **Only touch these files:** `AppRefiner/Snapshots/UnifiedDiffBuilder.cs` (new), `AppRefiner/Dialogs/SnapshotHistoryDialog.cs`, `AppRefiner/Properties/Settings.settings`, `AppRefiner/Properties/Settings.Designer.cs`. Tim's parallel work is in stylers, refactors, and the declare-function dialog — do not modify anything else.
- **Builds:** verification is `dotnet build AppRefiner/AppRefiner.csproj` (~5 s). Tim normally runs builds himself — get his OK once at the start of the session before running builds, or stop at each build checkpoint and ask.
- **No automated tests:** AppRefiner has no test project (the xunit project only covers `PeopleCodeTypeInfo`). Verification is compile + the manual checklist in Task 4, per project norms.
- The custom `Debug.Log()` is AppRefiner's own class — do not add `using System.Diagnostics` debug calls.
- Never use `MessageBox.Show` — use the `MessageBoxDialog` pattern (see Task 3 code).

---

### Task 1: New persisted settings

**Files:**
- Modify: `AppRefiner/Properties/Settings.settings` (add two `<Setting>` elements before `</Settings>`)
- Modify: `AppRefiner/Properties/Settings.Designer.cs` (add two properties at the end of the `Settings` class, after `useEnhancedEditor`)

**Interfaces:**
- Produces: `Properties.Settings.Default.SnapshotDiffViewMode` (`int`: 0 = Changes only, 1 = Full file, 2 = Snapshot content; default 0) and `Properties.Settings.Default.SnapshotDiffDirectionCurrentFirst` (`bool`: `false` = snapshot → current; default `false`). Task 3 reads and writes both.

- [ ] **Step 1: Add settings to Settings.settings**

In `AppRefiner/Properties/Settings.settings`, add before the closing `</Settings>` tag (after the `useEnhancedEditor` setting):

```xml
    <Setting Name="SnapshotDiffViewMode" Type="System.Int32" Scope="User">
      <Value Profile="(Default)">0</Value>
    </Setting>
    <Setting Name="SnapshotDiffDirectionCurrentFirst" Type="System.Boolean" Scope="User">
      <Value Profile="(Default)">False</Value>
    </Setting>
```

- [ ] **Step 2: Add matching properties to Settings.Designer.cs**

In `AppRefiner/Properties/Settings.Designer.cs`, add inside the `Settings` class, after the last existing property (`useEnhancedEditor`), following the file's exact generated style:

```csharp
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public int SnapshotDiffViewMode {
            get {
                return ((int)(this["SnapshotDiffViewMode"]));
            }
            set {
                this["SnapshotDiffViewMode"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SnapshotDiffDirectionCurrentFirst {
            get {
                return ((bool)(this["SnapshotDiffDirectionCurrentFirst"]));
            }
            set {
                this["SnapshotDiffDirectionCurrentFirst"] = value;
            }
        }
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

---

### Task 2: UnifiedDiffBuilder helper

**Files:**
- Create: `AppRefiner/Snapshots/UnifiedDiffBuilder.cs`

**Interfaces:**
- Consumes: DiffPlex (`DiffPlex.Differ.CreateLineDiffs(string, string, bool)` → `DiffPlex.Model.DiffResult` with `DiffBlocks` / `PiecesForA` / `PiecesForB`).
- Produces (all in namespace `AppRefiner.Snapshots`, used by Task 3):
  - `enum DiffLineKind { Context, Added, Removed }`
  - `class DiffLine { DiffLineKind Kind; string Text; }`
  - `class DiffHunk { int OldStart, OldCount, NewStart, NewCount; List<DiffLine> Lines; string Header; }`
  - `class DiffStats { int Added; int Removed; }`
  - `static class UnifiedDiffBuilder`:
    - `DiffStats ComputeStats(string oldText, string newText)`
    - `List<DiffLine> BuildLines(string oldText, string newText)` — full-file interleaved line list
    - `List<DiffHunk> BuildHunks(string oldText, string newText, int context = 3)` — collapsed hunks, nearby hunks merged
    - `string FormatUnifiedDiff(string oldLabel, string newLabel, List<DiffHunk> hunks)` — `---`/`+++` headers + `@@` hunks

- [ ] **Step 1: Create the file with the complete implementation**

Create `AppRefiner/Snapshots/UnifiedDiffBuilder.cs`:

```csharp
using DiffPlex;
using DiffPlex.Model;
using System.Text;

namespace AppRefiner.Snapshots
{
    /// <summary>
    /// Kind of a line within a diff result
    /// </summary>
    public enum DiffLineKind
    {
        Context,
        Added,
        Removed
    }

    /// <summary>
    /// A single line in a diff result
    /// </summary>
    public class DiffLine
    {
        public DiffLineKind Kind { get; }
        public string Text { get; }

        public DiffLine(DiffLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    /// <summary>
    /// A contiguous group of changed lines with surrounding context lines
    /// </summary>
    public class DiffHunk
    {
        public int OldStart { get; set; }
        public int OldCount { get; set; }
        public int NewStart { get; set; }
        public int NewCount { get; set; }
        public List<DiffLine> Lines { get; } = new();

        /// <summary>
        /// Unified diff hunk header, e.g. "@@ -45,7 +45,8 @@". Follows the git
        /// convention of referencing the line before the hunk when one side
        /// contributes no lines.
        /// </summary>
        public string Header
        {
            get
            {
                int oldStart = OldCount == 0 ? OldStart - 1 : OldStart;
                int newStart = NewCount == 0 ? NewStart - 1 : NewStart;
                return $"@@ -{oldStart},{OldCount} +{newStart},{NewCount} @@";
            }
        }
    }

    /// <summary>
    /// Added/removed line counts between two versions of a text
    /// </summary>
    public class DiffStats
    {
        public int Added { get; set; }
        public int Removed { get; set; }
    }

    /// <summary>
    /// Builds line diffs, collapsed hunks and unified diff text from two
    /// versions of a text. Shared by the snapshot history dialog's preview
    /// pane, its per-snapshot change stats, and its Copy Diff action.
    /// </summary>
    public static class UnifiedDiffBuilder
    {
        private static readonly IDiffer differ = new Differ();

        /// <summary>
        /// Counts lines added and removed going from oldText to newText
        /// </summary>
        public static DiffStats ComputeStats(string oldText, string newText)
        {
            var result = differ.CreateLineDiffs(oldText ?? string.Empty, newText ?? string.Empty, false);
            var stats = new DiffStats();

            foreach (var block in result.DiffBlocks)
            {
                stats.Removed += block.DeleteCountA;
                stats.Added += block.InsertCountB;
            }

            return stats;
        }

        /// <summary>
        /// Produces the full interleaved line list: context lines from the old
        /// text with removed lines followed by their replacement added lines.
        /// </summary>
        public static List<DiffLine> BuildLines(string oldText, string newText)
        {
            var result = differ.CreateLineDiffs(oldText ?? string.Empty, newText ?? string.Empty, false);
            var lines = new List<DiffLine>();
            int aIndex = 0;

            foreach (var block in result.DiffBlocks)
            {
                while (aIndex < block.DeleteStartA)
                {
                    lines.Add(new DiffLine(DiffLineKind.Context, result.PiecesForA[aIndex]));
                    aIndex++;
                }

                for (int i = 0; i < block.DeleteCountA; i++)
                {
                    lines.Add(new DiffLine(DiffLineKind.Removed, result.PiecesForA[block.DeleteStartA + i]));
                }

                for (int i = 0; i < block.InsertCountB; i++)
                {
                    lines.Add(new DiffLine(DiffLineKind.Added, result.PiecesForB[block.InsertStartB + i]));
                }

                aIndex = block.DeleteStartA + block.DeleteCountA;
            }

            while (aIndex < result.PiecesForA.Length)
            {
                lines.Add(new DiffLine(DiffLineKind.Context, result.PiecesForA[aIndex]));
                aIndex++;
            }

            return lines;
        }

        /// <summary>
        /// Groups changes into hunks with the given number of context lines.
        /// Changes whose context regions touch or overlap share a hunk.
        /// </summary>
        public static List<DiffHunk> BuildHunks(string oldText, string newText, int context = 3)
        {
            var lines = BuildLines(oldText, newText);
            var hunks = new List<DiffHunk>();

            // 1-based old/new line numbers at each position in the line list
            var oldNums = new int[lines.Count];
            var newNums = new int[lines.Count];
            int oldNum = 1, newNum = 1;
            for (int i = 0; i < lines.Count; i++)
            {
                oldNums[i] = oldNum;
                newNums[i] = newNum;
                if (lines[i].Kind != DiffLineKind.Added) oldNum++;
                if (lines[i].Kind != DiffLineKind.Removed) newNum++;
            }

            int pos = 0;
            while (pos < lines.Count)
            {
                if (lines[pos].Kind == DiffLineKind.Context)
                {
                    pos++;
                    continue;
                }

                int start = Math.Max(pos - context, 0);
                int lastChange = pos;
                int scan = pos + 1;
                while (scan < lines.Count && scan - lastChange <= context * 2)
                {
                    if (lines[scan].Kind != DiffLineKind.Context)
                    {
                        lastChange = scan;
                    }
                    scan++;
                }
                int end = Math.Min(lastChange + context, lines.Count - 1);

                var hunk = new DiffHunk { OldStart = oldNums[start], NewStart = newNums[start] };
                for (int i = start; i <= end; i++)
                {
                    hunk.Lines.Add(lines[i]);
                    if (lines[i].Kind != DiffLineKind.Added) hunk.OldCount++;
                    if (lines[i].Kind != DiffLineKind.Removed) hunk.NewCount++;
                }
                hunks.Add(hunk);

                pos = end + 1;
            }

            return hunks;
        }

        /// <summary>
        /// Formats hunks as unified diff text with ---/+++ header lines.
        /// Always emits the header lines, even when there are no hunks.
        /// </summary>
        public static string FormatUnifiedDiff(string oldLabel, string newLabel, List<DiffHunk> hunks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {oldLabel}");
            sb.AppendLine($"+++ {newLabel}");

            foreach (var hunk in hunks)
            {
                sb.AppendLine(hunk.Header);
                foreach (var line in hunk.Lines)
                {
                    char prefix = line.Kind switch
                    {
                        DiffLineKind.Added => '+',
                        DiffLineKind.Removed => '-',
                        _ => ' '
                    };
                    sb.AppendLine($"{prefix}{line.Text}");
                }
            }

            return sb.ToString();
        }
    }
}
```

Note: if `DiffPlex.Model.DiffResult`'s piece collections are not named `PiecesForA`/`PiecesForB` in DiffPlex 1.9.0 the build will say so — check the actual property names with IntelliSense/metadata and adjust (they are the old-text and new-text line arrays; `DiffBlocks` entries carry `DeleteStartA`, `DeleteCountA`, `InsertStartB`, `InsertCountB`).

- [ ] **Step 2: Build to verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors.

---

### Task 3: Rewrite SnapshotHistoryDialog

**Files:**
- Modify: `AppRefiner/Dialogs/SnapshotHistoryDialog.cs` (full rewrite — replace the entire file)

**Interfaces:**
- Consumes:
  - Task 1 settings, Task 2 `UnifiedDiffBuilder` API.
  - `SnapshotManager.GetFileHistory(string relativePath, string? dbName)`, `.SaveEditorSnapshot(ScintillaEditor, string)`, `.ApplySnapshotToEditor(ScintillaEditor, int)`.
  - `ScintillaManager.GetScintillaText(editor)`, `ScintillaManager.IsEditorClean(editor)` (`ScintillaManager.cs:930`).
  - Existing dialog plumbing: `DialogHelper.ModalDialogMouseHandler`, `WindowHelper.CenterFormOnWindow`, `WindowWrapper`, `MessageBoxDialog`.
- Produces: same public surface as today — `SnapshotHistoryDialog(SnapshotManager, ScintillaEditor, IntPtr owner = default)` and `Snapshot? SelectedSnapshot` — so `SnapshotRevertCommand.cs:46` keeps working unmodified.

- [ ] **Step 1: Replace the file contents**

Replace `AppRefiner/Dialogs/SnapshotHistoryDialog.cs` entirely with:

```csharp
using AppRefiner.Database.Models;
using AppRefiner.Snapshots;

namespace AppRefiner.Dialogs
{
    /// <summary>
    /// Dialog for browsing snapshot history with a live diff preview and
    /// clipboard/apply actions
    /// </summary>
    public class SnapshotHistoryDialog : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int ResizeGripSize = 6;

        // UI Controls
        private readonly Panel headerPanel;
        private readonly Label headerLabel;
        private readonly SplitContainer splitContainer;
        private readonly ListView historyListView;
        private readonly FlowLayoutPanel diffHeaderPanel;
        private readonly Label directionLabel;
        private readonly Button swapDirectionButton;
        private readonly RadioButton changesOnlyRadio;
        private readonly RadioButton fullFileRadio;
        private readonly RadioButton contentRadio;
        private readonly RichTextBox diffTextBox;
        private readonly Panel buttonPanel;
        private readonly Button copyOriginalButton;
        private readonly Button copyDiffButton;
        private readonly Button applyButton;
        private readonly Button closeButton;
        private readonly System.Windows.Forms.Timer copyFeedbackTimer;
        private readonly IntPtr owner;
        private DialogHelper.ModalDialogMouseHandler? mouseHandler;

        // Diff colors (same palette as DiffViewDialog)
        private static readonly Color AddedColor = Color.FromArgb(200, 255, 200);
        private static readonly Color RemovedColor = Color.FromArgb(255, 200, 200);
        private static readonly Color HunkHeaderColor = Color.FromArgb(240, 240, 240);
        private static readonly Color HunkHeaderTextColor = Color.FromArgb(70, 70, 100);

        // Data
        private readonly SnapshotManager snapshotManager;
        private readonly ScintillaEditor editor;
        private readonly List<Snapshot> snapshotHistory;
        private readonly List<DiffStats> snapshotStats = new();
        private readonly string currentContent;
        private Snapshot? selectedSnapshot;
        private bool directionCurrentFirst;
        private bool suppressViewModeEvents;

        public Snapshot? SelectedSnapshot => selectedSnapshot;

        /// <summary>
        /// Initializes a new instance of the SnapshotHistoryDialog class
        /// </summary>
        /// <param name="snapshotManager">The Snapshot manager</param>
        /// <param name="editor">The editor to show history for</param>
        /// <param name="owner">The owner window handle</param>
        public SnapshotHistoryDialog(SnapshotManager snapshotManager, ScintillaEditor editor, IntPtr owner = default)
        {
            this.snapshotManager = snapshotManager;
            this.editor = editor;
            this.owner = owner;

            this.headerPanel = new Panel();
            this.headerLabel = new Label();
            this.splitContainer = new SplitContainer();
            this.historyListView = new ListView();
            this.diffHeaderPanel = new FlowLayoutPanel();
            this.directionLabel = new Label();
            this.swapDirectionButton = new Button();
            this.changesOnlyRadio = new RadioButton();
            this.fullFileRadio = new RadioButton();
            this.contentRadio = new RadioButton();
            this.diffTextBox = new RichTextBox();
            this.buttonPanel = new Panel();
            this.copyOriginalButton = new Button();
            this.copyDiffButton = new Button();
            this.applyButton = new Button();
            this.closeButton = new Button();
            this.copyFeedbackTimer = new System.Windows.Forms.Timer();

            if (!string.IsNullOrEmpty(editor.RelativePath))
            {
                snapshotHistory = snapshotManager.GetFileHistory(editor.RelativePath, editor.AppDesignerProcess.DBName);
            }
            else
            {
                snapshotHistory = new List<Snapshot>();
            }

            currentContent = ScintillaManager.GetScintillaText(editor) ?? string.Empty;

            // Stats are stored snapshot -> current (Added = lines added since
            // the snapshot). Display swaps them when the direction is flipped.
            foreach (var snapshot in snapshotHistory)
            {
                snapshotStats.Add(UnifiedDiffBuilder.ComputeStats(snapshot.Content, currentContent));
            }

            directionCurrentFirst = Properties.Settings.Default.SnapshotDiffDirectionCurrentFirst;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();

            // SnapshotHistoryDialog
            this.Text = "Snapshot History";
            this.ClientSize = new Size(1000, 650);
            this.MinimumSize = new Size(800, 500);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(240, 240, 245);
            this.Padding = new Padding(1);

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(50, 50, 60);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 30;
            this.headerPanel.Controls.Add(this.headerLabel);

            // headerLabel
            this.headerLabel.Text = "Snapshot History";
            this.headerLabel.ForeColor = Color.White;
            this.headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.headerLabel.Dock = DockStyle.Fill;
            this.headerLabel.TextAlign = ContentAlignment.MiddleCenter;

            // buttonPanel
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.Height = 50;

            // copyOriginalButton
            this.copyOriginalButton.Text = "Copy Original";
            this.copyOriginalButton.Tag = this.copyOriginalButton.Text;
            this.copyOriginalButton.Size = new Size(120, 30);
            this.copyOriginalButton.Location = new Point(20, 10);
            this.copyOriginalButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            this.copyOriginalButton.Enabled = false;
            StyleAccentButton(this.copyOriginalButton);
            this.copyOriginalButton.Click += CopyOriginalButton_Click;

            // copyDiffButton
            this.copyDiffButton.Text = "Copy Diff";
            this.copyDiffButton.Tag = this.copyDiffButton.Text;
            this.copyDiffButton.Size = new Size(120, 30);
            this.copyDiffButton.Location = new Point(150, 10);
            this.copyDiffButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            this.copyDiffButton.Enabled = false;
            StyleAccentButton(this.copyDiffButton);
            this.copyDiffButton.Click += CopyDiffButton_Click;

            // applyButton
            this.applyButton.Text = "Apply";
            this.applyButton.Size = new Size(100, 30);
            this.applyButton.Location = new Point(770, 10);
            this.applyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.applyButton.Enabled = false;
            StyleAccentButton(this.applyButton);
            this.applyButton.Click += ApplyButton_Click;

            // closeButton
            this.closeButton.Text = "Close";
            this.closeButton.Size = new Size(90, 30);
            this.closeButton.Location = new Point(890, 10);
            this.closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.closeButton.BackColor = Color.FromArgb(100, 100, 100);
            this.closeButton.ForeColor = Color.White;
            this.closeButton.FlatStyle = FlatStyle.Flat;
            this.closeButton.FlatAppearance.BorderSize = 0;
            this.closeButton.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.buttonPanel.Controls.Add(this.copyOriginalButton);
            this.buttonPanel.Controls.Add(this.copyDiffButton);
            this.buttonPanel.Controls.Add(this.applyButton);
            this.buttonPanel.Controls.Add(this.closeButton);

            // copyFeedbackTimer
            this.copyFeedbackTimer.Interval = 1500;
            this.copyFeedbackTimer.Tick += (s, e) =>
            {
                this.copyFeedbackTimer.Stop();
                this.copyOriginalButton.Text = (string)this.copyOriginalButton.Tag!;
                this.copyDiffButton.Text = (string)this.copyDiffButton.Tag!;
            };

            // historyListView
            this.historyListView.Dock = DockStyle.Fill;
            this.historyListView.FullRowSelect = true;
            this.historyListView.HideSelection = false;
            this.historyListView.UseCompatibleStateImageBehavior = false;
            this.historyListView.View = View.Details;
            this.historyListView.MultiSelect = false;
            this.historyListView.Columns.Add("Date", 160);
            this.historyListView.Columns.Add("Changes", 80);
            this.historyListView.SelectedIndexChanged += HistoryListView_SelectedIndexChanged;

            // diffHeaderPanel
            this.diffHeaderPanel.Dock = DockStyle.Top;
            this.diffHeaderPanel.Height = 34;
            this.diffHeaderPanel.FlowDirection = FlowDirection.LeftToRight;
            this.diffHeaderPanel.WrapContents = false;
            this.diffHeaderPanel.Padding = new Padding(6, 4, 6, 0);

            // directionLabel
            this.directionLabel.AutoSize = true;
            this.directionLabel.Margin = new Padding(0, 8, 4, 0);
            this.directionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

            // swapDirectionButton
            this.swapDirectionButton.Text = "⇄";
            this.swapDirectionButton.Size = new Size(30, 24);
            this.swapDirectionButton.Margin = new Padding(0, 3, 20, 0);
            this.swapDirectionButton.FlatStyle = FlatStyle.Flat;
            this.swapDirectionButton.Click += SwapDirectionButton_Click;

            // view mode radios
            this.changesOnlyRadio.Text = "Changes only";
            this.fullFileRadio.Text = "Full file";
            this.contentRadio.Text = "Snapshot content";
            foreach (var radio in new[] { this.changesOnlyRadio, this.fullFileRadio, this.contentRadio })
            {
                radio.AutoSize = true;
                radio.Margin = new Padding(0, 7, 10, 0);
                radio.CheckedChanged += ViewModeRadio_CheckedChanged;
            }

            this.diffHeaderPanel.Controls.Add(this.directionLabel);
            this.diffHeaderPanel.Controls.Add(this.swapDirectionButton);
            this.diffHeaderPanel.Controls.Add(this.changesOnlyRadio);
            this.diffHeaderPanel.Controls.Add(this.fullFileRadio);
            this.diffHeaderPanel.Controls.Add(this.contentRadio);

            // diffTextBox
            this.diffTextBox.Dock = DockStyle.Fill;
            this.diffTextBox.BorderStyle = BorderStyle.FixedSingle;
            this.diffTextBox.ReadOnly = true;
            this.diffTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            this.diffTextBox.WordWrap = false;
            this.diffTextBox.ScrollBars = RichTextBoxScrollBars.Both;
            this.diffTextBox.BackColor = Color.White;

            // splitContainer
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Orientation = Orientation.Vertical;
            this.splitContainer.FixedPanel = FixedPanel.Panel1;
            this.splitContainer.Panel1MinSize = 200;
            this.splitContainer.Panel2MinSize = 400;
            this.splitContainer.Panel1.Controls.Add(this.historyListView);
            this.splitContainer.Panel2.Controls.Add(this.diffTextBox);
            this.splitContainer.Panel2.Controls.Add(this.diffHeaderPanel);

            // Fill control first so Top/Bottom-docked siblings take precedence
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.buttonPanel);
            this.Controls.Add(this.headerPanel);

            this.splitContainer.SplitterDistance = 260;

            // Restore persisted view mode without triggering renders
            this.suppressViewModeEvents = true;
            switch (Properties.Settings.Default.SnapshotDiffViewMode)
            {
                case 1:
                    this.fullFileRadio.Checked = true;
                    break;
                case 2:
                    this.contentRadio.Checked = true;
                    break;
                default:
                    this.changesOnlyRadio.Checked = true;
                    break;
            }
            this.suppressViewModeEvents = false;

            PopulateHistoryList();

            this.headerPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private static void StyleAccentButton(Button button)
        {
            button.BackColor = Color.FromArgb(0, 122, 204);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }

        private void PopulateHistoryList()
        {
            historyListView.BeginUpdate();
            historyListView.Items.Clear();

            for (int i = 0; i < snapshotHistory.Count; i++)
            {
                var item = new ListViewItem(FormatSnapshotDate(snapshotHistory[i].CreatedAt));
                item.SubItems.Add(FormatStats(snapshotStats[i]));
                item.Tag = snapshotHistory[i];
                historyListView.Items.Add(item);
            }

            historyListView.EndUpdate();

            if (historyListView.Items.Count > 0)
            {
                historyListView.Items[0].Selected = true;
            }
            else
            {
                RenderPreview();
            }
        }

        private static string FormatSnapshotDate(DateTime createdAt)
        {
            string time = createdAt.Date == DateTime.Today
                ? createdAt.ToString("HH:mm:ss")
                : createdAt.ToString("yyyy-MM-dd HH:mm:ss");
            return $"{time} ({FormatRelativeAge(createdAt)})";
        }

        private static string FormatRelativeAge(DateTime createdAt)
        {
            var age = DateTime.Now - createdAt;
            if (age.TotalSeconds < 60)
            {
                return "just now";
            }
            if (age.TotalMinutes < 60)
            {
                return $"{(int)age.TotalMinutes} min ago";
            }
            if (age.TotalHours < 24)
            {
                return $"{(int)age.TotalHours} hr ago";
            }
            return $"{(int)age.TotalDays} days ago";
        }

        private string FormatStats(DiffStats stats)
        {
            int added = directionCurrentFirst ? stats.Removed : stats.Added;
            int removed = directionCurrentFirst ? stats.Added : stats.Removed;
            return added == 0 && removed == 0 ? "±0" : $"+{added} −{removed}";
        }

        private void HistoryListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            selectedSnapshot = historyListView.SelectedItems.Count > 0
                ? (Snapshot)historyListView.SelectedItems[0].Tag!
                : null;

            bool hasSelection = selectedSnapshot != null;
            copyOriginalButton.Enabled = hasSelection;
            copyDiffButton.Enabled = hasSelection;
            applyButton.Enabled = hasSelection;

            RenderPreview();
        }

        private void SwapDirectionButton_Click(object? sender, EventArgs e)
        {
            directionCurrentFirst = !directionCurrentFirst;
            Properties.Settings.Default.SnapshotDiffDirectionCurrentFirst = directionCurrentFirst;
            Properties.Settings.Default.Save();

            for (int i = 0; i < historyListView.Items.Count; i++)
            {
                historyListView.Items[i].SubItems[1].Text = FormatStats(snapshotStats[i]);
            }

            RenderPreview();
        }

        private void ViewModeRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (suppressViewModeEvents || sender is not RadioButton radio || !radio.Checked)
            {
                return;
            }

            Properties.Settings.Default.SnapshotDiffViewMode = GetSelectedViewMode();
            Properties.Settings.Default.Save();
            RenderPreview();
        }

        private int GetSelectedViewMode()
        {
            if (fullFileRadio.Checked)
            {
                return 1;
            }
            return contentRadio.Checked ? 2 : 0;
        }

        private void UpdateDirectionLabel()
        {
            string timestamp = selectedSnapshot?.CreatedAt.ToString("HH:mm:ss") ?? "…";
            directionLabel.Text = directionCurrentFirst
                ? $"Current → Snapshot ({timestamp})"
                : $"Snapshot ({timestamp}) → Current";
        }

        private void RenderPreview()
        {
            UpdateDirectionLabel();
            diffTextBox.Clear();

            if (selectedSnapshot == null)
            {
                diffTextBox.Text = snapshotHistory.Count == 0
                    ? "No snapshots recorded for this program."
                    : string.Empty;
                return;
            }

            int viewMode = GetSelectedViewMode();

            if (viewMode == 2)
            {
                diffTextBox.Text = selectedSnapshot.Content;
                return;
            }

            string oldText = directionCurrentFirst ? currentContent : selectedSnapshot.Content;
            string newText = directionCurrentFirst ? selectedSnapshot.Content : currentContent;

            diffTextBox.SuspendLayout();

            if (viewMode == 0)
            {
                var hunks = UnifiedDiffBuilder.BuildHunks(oldText, newText);
                if (hunks.Count == 0)
                {
                    diffTextBox.Text = "No differences from current editor content.";
                }
                else
                {
                    foreach (var hunk in hunks)
                    {
                        AppendFormattedText(hunk.Header, HunkHeaderTextColor, HunkHeaderColor);
                        foreach (var line in hunk.Lines)
                        {
                            AppendDiffLine(line);
                        }
                    }
                }
            }
            else
            {
                foreach (var line in UnifiedDiffBuilder.BuildLines(oldText, newText))
                {
                    AppendDiffLine(line);
                }
            }

            diffTextBox.SelectionStart = 0;
            diffTextBox.SelectionLength = 0;
            diffTextBox.ResumeLayout();
        }

        private void AppendDiffLine(DiffLine line)
        {
            switch (line.Kind)
            {
                case DiffLineKind.Added:
                    AppendFormattedText($"+{line.Text}", Color.Black, AddedColor);
                    break;
                case DiffLineKind.Removed:
                    AppendFormattedText($"-{line.Text}", Color.Black, RemovedColor);
                    break;
                default:
                    AppendFormattedText($" {line.Text}", Color.Black, Color.White);
                    break;
            }
        }

        private void AppendFormattedText(string text, Color textColor, Color backgroundColor)
        {
            int start = diffTextBox.TextLength;
            diffTextBox.AppendText(text + Environment.NewLine);
            int end = diffTextBox.TextLength;

            diffTextBox.Select(start, end - start);
            diffTextBox.SelectionColor = textColor;
            diffTextBox.SelectionBackColor = backgroundColor;
            diffTextBox.SelectionLength = 0;
        }

        private void CopyOriginalButton_Click(object? sender, EventArgs e)
        {
            if (selectedSnapshot == null)
            {
                return;
            }

            Clipboard.SetText(selectedSnapshot.Content);
            ShowCopyFeedback(copyOriginalButton);
        }

        private void CopyDiffButton_Click(object? sender, EventArgs e)
        {
            if (selectedSnapshot == null)
            {
                return;
            }

            string snapshotLabel = $"{selectedSnapshot.FilePath} @ {selectedSnapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}";
            string currentLabel = $"{selectedSnapshot.FilePath} (current editor)";

            string oldText = directionCurrentFirst ? currentContent : selectedSnapshot.Content;
            string newText = directionCurrentFirst ? selectedSnapshot.Content : currentContent;
            string oldLabel = directionCurrentFirst ? currentLabel : snapshotLabel;
            string newLabel = directionCurrentFirst ? snapshotLabel : currentLabel;

            var hunks = UnifiedDiffBuilder.BuildHunks(oldText, newText);
            Clipboard.SetText(UnifiedDiffBuilder.FormatUnifiedDiff(oldLabel, newLabel, hunks));
            ShowCopyFeedback(copyDiffButton);
        }

        private void ShowCopyFeedback(Button button)
        {
            copyFeedbackTimer.Stop();
            copyOriginalButton.Text = (string)copyOriginalButton.Tag!;
            copyDiffButton.Text = (string)copyDiffButton.Tag!;
            button.Text = "Copied!";
            copyFeedbackTimer.Start();
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            if (selectedSnapshot == null)
            {
                return;
            }

            // Safety net: back up unsaved work before overwriting it. A clean
            // editor already has its content in the latest savepoint snapshot.
            if (!ScintillaManager.IsEditorClean(editor))
            {
                var latestContent = ScintillaManager.GetScintillaText(editor) ?? currentContent;
                if (!snapshotManager.SaveEditorSnapshot(editor, latestContent))
                {
                    ShowError("Failed to back up the current editor content, so the revert was cancelled. See debug log for details.");
                    return;
                }
            }

            if (snapshotManager.ApplySnapshotToEditor(editor, selectedSnapshot.Id))
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                ShowError("Failed to apply the selected snapshot. See debug log for details.");
            }
        }

        private void ShowError(string message)
        {
            var mainHandle = owner;
            Task.Delay(100).ContinueWith(_ =>
            {
                var handleWrapper = new WindowWrapper(mainHandle);
                new MessageBoxDialog(message, "Snapshot History", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
            });
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Edge resizing for the borderless form
            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
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

            copyFeedbackTimer.Dispose();
            mouseHandler?.Dispose();
            mouseHandler = null;
        }
    }
}
```

Implementation notes for the engineer:

- `MessageBoxDialog` lives in `AppRefiner.Dialogs`; check its constructor signature in `Dialogs/MessageBoxDialog.cs` if the call doesn't compile — the pattern above comes from CLAUDE.md's "MessageBox Dialog Pattern" section.
- `MinimumSize` on a `FormBorderStyle.None` form is respected by the interactive resize the `WM_NCHITTEST` codes enable — no extra code needed.
- The old file's `GenerateUnifiedDiff` method and its `using AppRefiner.Database.Models;`-only imports are gone; `DiffViewDialog`/`TextViewDialog` are intentionally no longer referenced (leave those files untouched).
- `SplitterDistance` is set after the `SplitContainer` is added to the form and `ClientSize` is set, so the 260px distance is valid against the real width.

- [ ] **Step 2: Build to verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded, 0 errors. If `MessageBoxDialog`'s constructor differs, read `AppRefiner/Dialogs/MessageBoxDialog.cs` and match its real signature.

- [ ] **Step 3: Confirm no other files reference removed members**

Run: `grep -rn "GenerateUnifiedDiff" AppRefiner/` (or the Grep tool)
Expected: no matches outside the rewritten file (the method was private; this is a sanity check).

---

### Task 4: Manual verification (with Tim)

**Files:** none — verification only. Requires PeopleSoft Application Designer.

- [ ] **Step 1: Basic flow**

Open a PeopleCode program that has several snapshots, run the "Snapshot: Revert to Previous Version" command (command palette). Verify:
- Dialog opens ~1000×650 centered on App Designer; header drags the window; Esc closes.
- List shows Date (`HH:mm:ss (N min ago)` for today; date-prefixed otherwise) and Changes (`+a −r`, `±0` for identical).
- First row auto-selected with diff rendered immediately; switching rows re-renders with no clicks.

- [ ] **Step 2: View modes and direction**

- Toggle Changes only / Full file / Snapshot content — pane re-renders correctly in each; hunk headers only in Changes only mode.
- Click ⇄ — direction label flips, +/− colors invert, list stats swap.
- Close and reopen the dialog — last-used view mode and direction are restored. Restart AppRefiner — still restored.

- [ ] **Step 3: Clipboard actions**

- Copy Original → paste in Notepad → exact snapshot text.
- Copy Diff → paste → `---`/`+++` labels with file path + timestamp, `@@` hunk headers with plausible line numbers, 3 context lines. Button flashes "Copied!" and restores after ~1.5 s.
- Copy Diff on a `±0` snapshot → just the two header lines, no hunks.

- [ ] **Step 4: Apply paths**

- Apply with a **clean** editor (just saved): content replaced, dialog closes, reopening shows **no** new snapshot beyond what existed.
- Make an edit **without saving**, Apply: content replaced, and reopening the dialog shows a new snapshot at the top containing the unsaved pre-revert text.

- [ ] **Step 5: Edge cases**

- Open the dialog on a program with no snapshots: "No snapshots recorded for this program." shown, Copy/Apply disabled, Close works.
- Resize from all four edges and corners; verify 800×500 minimum and splitter behavior (left panel fixed on resize).
- Empty-ish program (few lines) and a large program (500+ lines) render acceptably fast when switching rows.
