# Unload AppRefinerHook.dll from App Designer on AppRefiner close

**Date:** 2026-07-08
**Status:** Design — pending review

## Problem

When AppRefiner exits, `AppRefinerHook.dll` remains loaded inside every attached
Application Designer process. This means the AppRefiner install (and the hook DLL
alongside it) cannot be deleted or replaced with a new version until every App
Designer instance is also closed.

`MainForm.OnFormClosing` already calls `EventHookInstaller.CleanupAllHooks()`
(MainForm.cs:1234), which calls `UnhookWindowsHookEx` for the `WH_GETMESSAGE` and
`WH_KEYBOARD` hooks. But that runs in the **AppRefiner** process and does nothing
about the DLL's presence in the remote App Designer process. Two things keep the
DLL pinned there, and neither is addressed:

1. **Self-reference pin.** In `DllMain` / `DLL_PROCESS_ATTACH`, the DLL calls
   `g_dllSelfReference = LoadLibraryA(<self>)` (dllmain.cpp:32-40) expressly to
   prevent unloading. `DLL_PROCESS_DETACH` deliberately does **not** free it, so
   the module reference count never reaches zero.
2. **Live subclasses.** Several window procedures point into the DLL's code:
   - Scintilla parent window (`SUBCLASS_ID`)
   - Scintilla editor (`SCINTILLA_SUBCLASS_ID`)
   - Main window (`MAIN_WINDOW_SUBCLASS_ID`)
   - Results list view (`RESULTS_LIST_SUBCLASS_ID`)
   - ComboBox button + its dialog (`ComboBoxButton`, `COMBO_DIALOG_SUBCLASS_ID`)
   - Minimap child windows (created by the DLL, painted by DLL code)

   Even if the self-reference were released, unmapping the DLL while any of these
   still point into it would crash App Designer on the next relevant message.

A vestigial `WM_REMOVE_HOOK` message (`WM_USER + 1004`) and `RemoveHook()` exist in
`EventHookInstaller.cs` but have **no handler** in the C++ hook — evidence that a
remote-teardown path was intended but never finished.

## Constraint

Releasing the self-reference and removing the subclasses is work that must execute
**inside the App Designer process**. It cannot be done from pure C#; it requires a
message handled by the hook DLL. Therefore this change spans **both** the C++ hook
(`AppRefinerHook` — requires an msbuild rebuild) and the C# `AppRefiner` project.

The `CreateRemoteThread` + `FreeLibrary` ejection technique is explicitly rejected:
it does not cleanly remove the subclass procedures and risks crashing App Designer.
The message-based teardown reuses the existing, proven cross-process infrastructure.

## Design

### New message: `WM_AR_DETACH`

Add a control message (next in the `WM_USER` control-message block, `WM_USER + 1012`)
in both `AppRefinerHook/Common.h` and `EventHookInstaller.cs`.

It is handled **synchronously** in `MainWindowSubclassProc` (the main window is
reliably subclassed whenever AppRefiner attaches, which is how the keyboard
shortcuts already work). Handling it via a window `SendMessage` (rather than a
`PostThreadMessage` thread message) gives us a synchronous completion signal: when
the send returns, teardown in that process is done. If the main window happens not
to be subclassed for a given instance, teardown is skipped for that instance and we
fall back to today's behavior (unhook only) — no regression.

The handler runs on the remote UI thread (the window's owning thread), which is the
same thread that installed every subclass, so no locking is needed on the registry.

### Subclass registry (DLL-side, per process)

Add a small registry in the hook DLL that records every subclass the DLL installs:

```cpp
struct SubclassEntry { HWND hwnd; SUBCLASSPROC proc; UINT_PTR id; };
static std::vector<SubclassEntry> g_installedSubclasses;

void RegisterSubclass(HWND hwnd, SUBCLASSPROC proc, UINT_PTR id);   // after SetWindowSubclass success
void UnregisterSubclass(HWND hwnd, UINT_PTR id);                    // in each WM_NCDESTROY handler
void RemoveAllSubclasses();                                         // teardown: RemoveWindowSubclass for each, then clear
```

- Call `RegisterSubclass(...)` at each successful `SetWindowSubclass` site:
  parent, editor, main window, Results list (HookManager.cpp), and the ComboBox
  button + dialog (ComboBoxButton.cpp).
- Call `UnregisterSubclass(...)` in each existing `WM_NCDESTROY` self-removal path so
  the registry does not accumulate dead handles during normal use.
- Because hook globals are per-process, each App Designer process has its own
  registry and cleans up exactly its own subclasses.

Minimap windows are DLL-created child windows, not subclasses, so they are handled
separately: add `MinimapManager::DisableAll()` that iterates its tracked
scintilla→minimap map and calls the existing `DisableMinimap` for each (destroying
the minimap windows).

### Teardown order (inside the remote process, on `WM_AR_DETACH`)

1. `MinimapManager::DisableAll()` — destroy all minimap child windows.
2. `RemoveAllSubclasses()` — `RemoveWindowSubclass` for every registered subclass
   (including the main window subclass currently executing; removing a subclass from
   within its own proc is permitted and the send still returns cleanly).
3. `FreeLibrary(g_dllSelfReference); g_dllSelfReference = NULL;` — release the pin.

Throughout teardown the DLL remains mapped because the active `WH_GETMESSAGE` /
`WH_KEYBOARD` hooks still reference it, so no proc is unmapped mid-execution.

### AppRefiner side (C#)

In `MainForm.OnFormClosing`, **before** `CleanupAllHooks()`, iterate every attached
`AppDesignerProcess` and, for each with a valid `MainWindowHandle`, call a new
`EventHookInstaller.DetachFromProcess(mainWindowHandle)` that sends `WM_AR_DETACH`
via **`SendMessageTimeout`** with a short per-process timeout (e.g. 2000 ms) and
`SMTO_ABORTIFHUNG`. If a process does not respond, log it and continue — that
instance simply keeps the old DLL until it is closed. Then proceed with the existing
`CleanupAllHooks()` (`UnhookWindowsHookEx`), which drops the hook-injected
references; on the remote thread's next message pump Windows unmaps the DLL.

## Files touched

**C++ (`AppRefinerHook`, requires msbuild rebuild):**
- `Common.h` — define `WM_AR_DETACH`.
- `HookManager.cpp` — registry (`RegisterSubclass`/`UnregisterSubclass`/
  `RemoveAllSubclasses`); register at each `SetWindowSubclass`; unregister in each
  `WM_NCDESTROY`; handle `WM_AR_DETACH` in `MainWindowSubclassProc` with the teardown
  sequence; free `g_dllSelfReference`.
- `HookManager.h` — declarations for the registry helpers as needed.
- `ComboBoxButton.cpp` — register/unregister its button and dialog subclasses.
- `MinimapManager.cpp` / `.h` — add `DisableAll()`.

**C# (`AppRefiner`):**
- `Events/EventHookInstaller.cs` — `WM_AR_DETACH` constant; `SendMessageTimeout`
  P/Invoke; `DetachFromProcess(IntPtr mainWindowHandle)`. Optionally remove the dead
  `WM_REMOVE_HOOK` / `RemoveHook()` now that a real detach path exists.
- `MainForm.cs` — in `OnFormClosing`, loop attached `AppDesignerProcesses` calling
  `DetachFromProcess` before `CleanupAllHooks()`.

## Non-goals

- No change to hook installation or normal-operation behavior.
- No attempt to eject the DLL from the AppRefiner process itself (it unloads when
  AppRefiner exits).
- No detach on individual editor/document close — this is a shutdown-only teardown.

## Verification

Because the payoff is specifically "replace the files without closing App Designer,"
verify manually (no automated harness for cross-process injection):

1. Launch App Designer, attach AppRefiner, open a PeopleCode editor (exercises
   parent/editor/combobox/minimap subclasses), open the Find/Results paths.
2. Confirm `AppRefinerHook.dll` is loaded in the App Designer process
   (Process Explorer / `tasklist /m AppRefinerHook.dll`).
3. Close AppRefiner. Confirm the DLL is **no longer** listed in the still-running
   App Designer process, and that App Designer remains responsive (type in the
   editor, open dialogs).
4. Confirm the AppRefiner install directory (including the hook DLL) can now be
   deleted/replaced while App Designer stays open.
5. Repeat with two App Designer instances attached to confirm per-process teardown.
