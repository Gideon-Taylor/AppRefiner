# Instances Tab + App Designer Lifecycle Tracking — Design

**Date:** 2026-06-11
**Status:** Implemented 2026-07-02 (needs manual verification)

## Implementation notes (2026-07-02)

- **§1–§2 lifecycle** landed earlier as audit findings F7/F8 (`WindowDestroyed` on `EVENT_OBJECT_DESTROY`, editor eviction, `Process.Exited` tracking). This pass added the spec's remaining §2 item — `DataManager.Disconnect()` + `Dispose()` on process exit — which F7/F8 had omitted, plus `RefreshInstancesGrid()` calls on every lifecycle event.
- **§3 metadata**: `AppDesignerProcess.ConnectionDescription` / `ToolsVersion`; `DBConnectDialog` exposes both (ToolsVersion captured from the single `GetToolsVersion()` call already made on connect). All three connect paths route through new `MainForm.ApplyDatabaseConnection(process, manager, desc, tools)`; disconnect through `DisconnectDatabase(process)`.
- **§4 tab**: `tabPageInstances` (first tab, but `SelectedIndex = 1` keeps Editor Tweaks as the startup tab), `dgvInstances` (7 columns, full-row single-select, read-only), Connect/Disconnect/Bring to Front buttons, empty-state label. `RefreshInstancesGrid` is event-driven, preserves selection by PID, and prunes dead-HWND editor entries as the self-healing backstop. Tools Ver shows "Needs DB" when not connected (this doc's earlier tweak).
- **§5 removal**: `btnConnectDB` (designer entries, handler, label toggle) deleted.
- The command palette `DatabaseConnectCommand` and the `ConnectToDB()` auto-prompt now stamp metadata + refresh via the shared helper.

## Original design follows.

---

## Problem

AppRefiner tracks Application Designer (pside.exe) processes in `MainForm.AppDesignerProcesses` and an implicit `activeAppDesigner`, but:

1. **Nothing is ever removed.** No code path removes entries from `AppDesignerProcesses`, removes editors from `AppDesignerProcess.Editors`, or resets `activeAppDesigner` to null. After closing App Designer, `activeAppDesigner` points at a dead process; every dereference (Connect DB button, event mapping, snapshots) operates on a corpse. Observed symptom: the Connect DB button's "no App Designer" message never fires because `activeAppDesigner` is stale-non-null, and the connect dialog silently dies trying to parent to a dead window handle.
2. **The Connect DB button on the Linter tab is ambiguous.** With multiple App Designers running there is no way to see or choose which instance the button connects. The unambiguous paths (auto-connect; command palette from inside a focused App Designer) make the button redundant and misleading. Its location on the Linter tab is also wrong — DB connectivity serves many features.

## Decisions

| Decision | Choice |
|---|---|
| Button fate | Remove `btnConnectDB` from the Linter tab entirely |
| Replacement UI | New "Instances" tab: live grid of all tracked App Designers with per-row actions |
| Lifecycle fix | Same effort — process-exit and editor-destroy tracking are prerequisites for live rows |
| Editor-close detection | Handle `EVENT_OBJECT_DESTROY` in the existing WinEvent hook (already within the hooked range 0x8000–0x8005; currently ignored) |
| Tab placement | First tab in the tab order; startup-selected tab unchanged |

## Components

### 1. WinEventService: WindowDestroyed event

- Add `EVENT_OBJECT_DESTROY = 0x8001` to `NativeMethods` constants.
- In `InternalWinEventProc`, add a branch for `EVENT_OBJECT_DESTROY` that raises a new `WindowDestroyed` event (same `syncContext.Post` marshaling pattern as the existing Focused/Created/Shown branches).
- Filter **at the callback** before posting: only `idObject == 0 (OBJID_WINDOW) && idChild == 0`. DESTROY fires for child objects far more often than the currently-handled events; filtering avoids flooding the UI thread. (Requires adding `OBJID_WINDOW = 0` constant; the existing branches are left as they are.)

### 2. MainForm: lifecycle handling

**Editor destroyed** (subscribe to `WindowDestroyed`):
- Cheap check: for the destroyed HWND, look it up in each tracked `AppDesignerProcess.Editors` (few processes; one dictionary lookup each). On hit: call the editor's existing `Cleanup()` (`ScintillaEditor.cs:817`), remove the entry; if `activeEditor` was that editor, set `activeEditor = null`.
- If the destroyed HWND equals a tracked process's `MainWindowHandle`, run the process-exit cleanup below (idempotent with the `Process.Exited` path).
- Refresh the Instances grid.

**Process exited:**
- When an `AppDesignerProcess` is registered, obtain its `Process`, set `EnableRaisingEvents = true`, and subscribe `Exited`. Handler marshals to the UI thread via `BeginInvoke`.
- Cleanup (idempotent — guarded by "still tracked?"):
  - `AppDesignerProcesses.Remove(pid)`
  - Call the instance's existing `Cleanup()`; `DataManager?.Disconnect()` + `Dispose()`
  - If `activeAppDesigner` is this instance → `activeAppDesigner = null`
  - If `activeEditor` belonged to this instance → `activeEditor = null`
  - Refresh the Instances grid
- Edge: `EnableRaisingEvents = true` on an already-exited process fires `Exited` immediately — the idempotent handler makes this safe.

### 3. AppDesignerProcess: connection description

- New properties: `public string? ConnectionDescription { get; set; }` — human-readable flavor, e.g. `Bootstrap as SYSADM` or `Read-only as PSRO` — and `public string? ToolsVersion { get; set; }` (stamped once at connect time from `DataManager.GetToolsVersion()`, which the connect flow already calls; the grid never re-queries the DB).
- `DBConnectDialog` exposes `public string? ConnectionDescription { get; private set; }`, set on successful connect from its own state (bootstrap vs read-only radio, username).
- All three dialog call sites (Instances tab, `ConnectToDB()` prompt flow, `DatabaseConnectCommand`) stamp it onto the instance after `DialogResult.OK`. Cleared on disconnect.

### 4. Instances tab (new TabPage, first in tab order)

A read-only, full-row-select, single-select `DataGridView` (`dgvInstances`), one row per tracked instance:

| Column | Source |
|---|---|
| Active | `●` when row instance == `activeAppDesigner` |
| PID | `ProcessId` |
| DB Name | `DBName` |
| DB Connection | `DataManager == null` → "Not connected"; else `ConnectionDescription ?? "Connected"` |
| Tools Ver | `ToolsVersion` (stamped at connect time; shows "Needs DB" when not connected — hints that connecting resolves it) |
| Editors | `Editors.Count` after pruning entries whose HWND fails `IsWindow()` (self-healing backstop) |
| Enhanced | `HasLexilla` (hook/enhanced Scintilla loaded) |

Buttons under the grid, enabled by selection + state:
- **Connect DB…** (enabled when selected row not connected): opens `DBConnectDialog(instance.MainWindowHandle, instance.DBName)`; on OK assigns the `DataManager` to the instance and all its editors (same propagation as the palette command), stamps `ConnectionDescription`, refreshes.
- **Disconnect** (enabled when connected): `DataManager.Disconnect()`, null out instance + editor DataManagers, clear `ConnectionDescription`, refresh.
- **Bring to Front**: focus the instance's main window (`SetForegroundWindow`, adding the P/Invoke if not already present).

Empty state: when no instances are tracked, the grid is hidden behind / replaced by a centered label: "No Application Designer sessions detected."

Refresh model — event-driven, no polling timer. `RefreshInstancesGrid()` is called on: process registered, process exited, editor added/removed, DB connect/disconnect (all paths, including palette and auto-prompt), active-instance change, and Instances tab selected. Refresh preserves the current row selection by PID where possible.

### 5. Removals

- `btnConnectDB` deleted: designer entries, `btnConnectDB_Click` handler, and the label-toggle updates (`MainForm.cs:1317` area and inside the old handler). The `MessageBoxDialog` "no App Designer" branch added previously is removed along with the handler.
- The command palette `DatabaseConnectCommand` and the `ConnectToDB()` auto-prompt flow are otherwise unchanged (they gain only the `ConnectionDescription` stamp and grid refresh).

## Error handling

| Failure | Behavior |
|---|---|
| `Process.GetProcessById` throws during registration (already exited) | Skip registration; Debug.Log |
| `Exited` fires for untracked/already-cleaned pid | Idempotent no-op |
| Destroyed HWND not tracked anywhere | No-op (fast path) |
| Connect dialog fails / cancelled | No state change; grid unchanged |
| `SetForegroundWindow` fails (focus stealing rules) | Best-effort; no error surfaced |

## Testing

No test project (standing project decision). Manual verification:
1. Launch two App Designers → both rows appear; Active dot follows focus between them.
2. Connect one via the Instances tab → row shows connection flavor + tools version; other row unaffected.
3. Connect via command palette from inside the other → its row updates too.
4. Close an App Designer → row disappears; if it was active, no stale dereferences (open palette, run linters on the remaining instance).
5. Open/close PeopleCode editors → Editors count tracks (event-driven; switching tabs also corrects it).
6. Close all App Designers → empty-state label; Connect buttons disabled; nothing silently breaks.
7. Bring to Front focuses the right window.
8. Linter tab no longer shows the Connect DB button; lint flows still work against a connected instance.

## Out of scope

- Marshaling `ApplicationKeyboardService` command execution to the UI thread (known separate issue)
- Auto-connect behavior changes
- Per-instance settings editing from the grid
- Editor-level detail (paths, dirty state) in the grid
