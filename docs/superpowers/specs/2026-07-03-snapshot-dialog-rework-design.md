# Snapshot History Dialog Rework — Design

**Date:** 2026-07-03
**Status:** Approved

## Problem

The current `SnapshotHistoryDialog` (600×350, fixed) lists snapshots with a Date column and a
Message column, where Message (`Snapshot.Description`) repeats the timestamp and shows the program
name — pure redundancy, since all listed snapshots belong to the current program. Inspecting a
snapshot requires selecting it, clicking "View Diff" (or "View Content"), reviewing a modal child
dialog, closing it, and repeating for each candidate. There is no way to copy a snapshot's content
or a diff to the clipboard.

## Goals

- Selecting a snapshot immediately shows its diff against the current editor content — no extra
  clicks, no child dialogs.
- Replace the redundant Message column with useful at-a-glance change stats.
- Add clipboard actions: Copy Original (snapshot text) and Copy Diff (unified diff).
- Keep Apply (today's "Revert to Selected") with a safety net instead of a confirmation prompt.

## Layout

One borderless dialog in the standard AppRefiner style (dark header strip `Color.FromArgb(50, 50, 60)`,
draggable via `DialogHelper.ModalDialogMouseHandler`, 1px border paint, Esc closes).

- Opens at ~1000×650, centered on the owning App Designer window via `WindowHelper.CenterFormOnWindow`.
- **User-resizable** despite `FormBorderStyle.None`: `WndProc` handles `WM_NCHITTEST` to return
  edge/corner hit codes (HTLEFT … HTBOTTOMRIGHT) within a ~6px grip band. Minimum size 800×500.
- A `SplitContainer` fills the body: Panel1 (left) is the snapshot list, ~260px, `FixedPanel.Panel1`
  so resizing grows the diff pane. Panel2 (right) is the diff pane.
- Bottom button row: `[Copy Original] [Copy Diff]` left-aligned; `[Apply] [Close]` right-aligned.
  Buttons use the existing dialog button styling (flat, accent blue for primary actions, gray for
  Close).

## Snapshot list (left pane)

`ListView` in Details mode, two columns, newest first, first row auto-selected on open:

| Column | Content |
|---|---|
| Date | `12:50:57 (2 min ago)` — time plus relative age; prefix the `yyyy-MM-dd` date only when the snapshot is not from today |
| Changes | `+12 −3` line stats vs the current editor content; `±0` when identical |

- Stats are computed once when the dialog opens: one DiffPlex line diff per snapshot against the
  current editor text (`ScintillaManager.GetScintillaText`, fetched once). Snapshot content is
  already in memory (`GetFileHistory` loads it), so this is pure CPU work.
- The +/− orientation follows the direction setting (see below). Flipping direction swaps the two
  numbers in place — no recomputation.
- Relative-age strings: seconds/minutes/hours/days granularity ("just now", "N min ago",
  "N hr ago", "N days ago").

## Diff pane (right pane)

### Header strip

- **Direction control**: a label plus ⇄ button reading `Snapshot (12:43:28) → Current` or
  `Current → Snapshot (12:43:28)`. Clicking ⇄ flips the direction, re-renders the pane, swaps the
  list stats, and persists the setting. The explicit label removes any ambiguity about what
  green/red mean.
- **View mode radios**: `(●) Changes only  ( ) Full file  ( ) Snapshot content`. Changing mode
  re-renders and persists.

### View modes

1. **Changes only** (first-run default) — git-style collapsed hunks: only changed regions with
   3 context lines, separated by `@@ -a,b +c,d @@` hunk headers. Hunks whose context regions touch
   or overlap are merged.
2. **Full file** — every line of the comparison rendered; added/removed lines colored (the current
   `DiffViewDialog` presentation).
3. **Snapshot content** — the snapshot's raw text with no diff coloring (absorbs the old
   "View Content" button).

### Rendering

Read-only `RichTextBox`, Consolas 10pt, no word wrap, both scrollbars. Colors reuse the
`DiffViewDialog` palette: added `(200, 255, 200)`, removed `(255, 200, 200)`, hunk header
`(240, 240, 240)` with dark-blue text, plain white for context. Selecting a different list row
re-renders immediately.

### Direction semantics

- **Snapshot → current** (first-run default): −red = lines as they were in the snapshot,
  +green = what has changed since. Reads chronologically, matching git habits.
- **Current → snapshot**: +green = lines Apply will insert, −red = lines Apply will remove.
  Directly previews the Apply action.

Both the pane and Copy Diff always use the same, currently selected direction.

## Actions

- **Copy Original** — puts the selected snapshot's `Content` on the clipboard.
- **Copy Diff** — puts a proper unified diff on the clipboard: `---` / `+++` header lines carrying
  the `FilePath` plus a timestamp label (snapshot time / "current editor"), then `@@` hunks with
  3 context lines. Always hunked format, regardless of the pane's view mode, honoring the current
  direction.
- Both copy buttons flash their text to "Copied!" for ~1.5 s (WinForms timer), then restore.
- **Apply** — no confirmation prompt (the visible diff is the confirmation):
  1. If the editor is dirty (`!ScintillaManager.IsEditorClean(editor)`), call
     `SnapshotManager.SaveEditorSnapshot` with the current content first, so unsaved work is
     recoverable. If the editor is clean, skip — the latest savepoint snapshot already captured
     this state.
  2. `ScintillaManager.SetScintillaText(editor, snapshot.Content)` via
     `SnapshotManager.ApplySnapshotToEditor`.
  3. Close with `DialogResult.OK`.
  - On failure, report via the project's `MessageBoxDialog` pattern (`Task.Delay(100).ContinueWith`,
    `WindowWrapper` over the App Designer main window). The old raw `MessageBox.Show` calls are
    removed.
- **Close** / Esc — `DialogResult.Cancel`, as today.

## Settings persistence

Two new entries in `Properties.Settings` (same mechanism as `MaxFileSnapshots` /
`SnapshotDatabasePath`), saved immediately on change and restored on next open:

- `SnapshotDiffViewMode` (int: 0 = Changes only, 1 = Full file, 2 = Snapshot content) —
  first-run default 0.
- `SnapshotDiffDirectionCurrentFirst` (bool) — `false` (snapshot → current) on first run.

## Implementation shape

- **`AppRefiner/Dialogs/SnapshotHistoryDialog.cs`** — rewritten around the split layout. Public
  surface (`SelectedSnapshot`, constructor signature) unchanged so `SnapshotRevertCommand` and
  `MainForm` callers are untouched.
- **New `AppRefiner/Snapshots/UnifiedDiffBuilder.cs`** — static helper over DiffPlex that produces:
  - `DiffStats ComputeStats(string oldText, string newText)` → added/removed line counts;
  - a hunked diff model (list of hunks, each with header numbers and typed lines) consumed by both
    the pane renderer and Copy Diff's text formatter;
  - the unified-diff string formatter used by Copy Diff.
  Keeping this out of the dialog makes the hunk-numbering logic testable and reusable.
- **`DiffViewDialog` / `TextViewDialog`** — remain in the codebase untouched (public types,
  potentially referenced by plugins) but are no longer used by the snapshot flow. The dialog's
  private `GenerateUnifiedDiff` is deleted.

## Edge cases and error handling

- **No snapshots**: list empty, diff pane shows "No snapshots recorded for this program";
  Copy Original, Copy Diff, and Apply disabled (Close remains enabled).
- **Identical snapshot (±0)**: "Changes only" mode shows "No differences from current editor
  content." Apply stays enabled (harmless no-op).
- **Current editor text unavailable** (`GetScintillaText` returns null): treat as empty string for
  diffing; Apply still works.
- **Snapshot save failure during Apply's safety step**: abort the apply and report via
  `MessageBoxDialog` — never overwrite dirty content whose backup failed.

## Testing

Manual verification against PeopleSoft Application Designer (project norm):

- Open dialog with 0, 1, and many snapshots; verify stats, relative dates, auto-selection.
- Switch rows and confirm instant re-render; toggle all three view modes; flip direction and
  confirm pane, stats, and Copy Diff all agree.
- Copy Original / Copy Diff → paste into an external editor and verify format.
- Apply with a clean editor (no extra snapshot created) and a dirty editor (safety snapshot
  appears in history on reopen).
- Resize the dialog from all edges/corners; verify min size and splitter behavior.
- Restart AppRefiner and confirm view mode and direction persist.
