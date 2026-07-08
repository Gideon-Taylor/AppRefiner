# Unload AppRefinerHook.dll on AppRefiner Close — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When AppRefiner exits, cleanly unload `AppRefinerHook.dll` from every attached Application Designer process so the AppRefiner install (and the hook DLL) can be replaced without closing App Designer.

**Architecture:** A new synchronous `WM_AR_DETACH` window message, sent to each App Designer main window on AppRefiner close, triggers in-remote-process teardown: destroy DLL-created child windows (minimap, combo button), remove every subclass the DLL installed, and release the DLL's self-reference (`g_dllSelfReference`). AppRefiner then unhooks (existing `CleanupAllHooks`), and Windows unmaps the now-unreferenced DLL on the remote thread's next message pump. A per-process subclass registry inside the DLL provides the list of subclasses/editors to tear down.

**Tech Stack:** C++ Win32 (AppRefinerHook DLL, built with msbuild/Visual Studio 2022, x64), C# .NET 8 WinForms (AppRefiner).

## Global Constraints

- Platform: Windows x64 only. HWNDs are treated as 32-bit-significant in the existing message protocol; this change passes HWNDs only as full `IntPtr`/`HWND` (no packing needed).
- The C++ hook DLL requires a Visual Studio / msbuild build; it cannot be built in WSL. Command: `msbuild AppRefinerHook/AppRefinerHook.vcxproj /p:Configuration=Release /p:Platform=x64`.
- The C# project builds with `dotnet build AppRefiner/AppRefiner.csproj` (~5s on this machine).
- Use the project's `Debug.Log(...)` for logging in C#, not `System.Diagnostics.Debug`. Use `OutputDebugStringA(...)` in C++ (matches existing hook code).
- New control message value must not collide: the last used is `WM_AR_SET_PARAM_NAMES = WM_USER + 1011`. Use `WM_USER + 1012` for `WM_AR_DETACH`.
- All teardown code runs on the remote App Designer UI thread (the window's owning thread), which is the same thread that installed every subclass — so the registry needs **no locking**.

## Testing note (read before starting)

This feature is entirely cross-process Win32 side effects (hook injection, subclassing, module load/unload in another process). There is **no unit-testable surface** and the project's established practice is manual testing against Application Designer (see CLAUDE.md "Testing"). Therefore each task's verification is a **build** plus, for the final task, a **manual end-to-end check with Process Explorer / `tasklist`**. Do not fabricate unit tests for cross-process behavior; follow the manual verification steps exactly.

Per project convention, the developer (Tim) runs builds himself. When executing a task, make the edits, then **name the build command and stop for the developer to run it** rather than invoking msbuild/dotnet automatically.

## File Structure

- `AppRefinerHook/Common.h` — add `WM_AR_DETACH` define. (owns: cross-process message IDs)
- `AppRefinerHook/HookManager.h` — declare registry helpers + `TearDownAndUnload()`. (owns: hook/subclass proc declarations)
- `AppRefinerHook/HookManager.cpp` — registry implementation; register/unregister at HookManager subclass sites; `WM_AR_DETACH` handler; `TearDownAndUnload()`. (owns: hooks, core subclasses, teardown orchestration)
- `AppRefinerHook/ComboBoxButton.cpp` — register/unregister the dialog subclass in the shared registry. (owns: combo button/dialog lifecycle)
- `AppRefinerHook/MinimapManager.h` / `.cpp` — add `UnregisterWindowClass()`. (owns: minimap window class + windows)
- `AppRefiner/Events/EventHookInstaller.cs` — `WM_AR_DETACH` constant, `SendMessageTimeout` P/Invoke, `DetachFromProcess(...)`; delete dead `WM_REMOVE_HOOK`/`RemoveHook()`. (owns: managed side of the hook protocol)
- `AppRefiner/MainForm.cs` — call `DetachFromProcess` for each attached process in `OnFormClosing` before `CleanupAllHooks()`. (owns: shutdown sequencing)

---

## Task 1: Subclass registry in the hook DLL

Adds a per-process registry of every subclass the DLL installs, wired into all install/remove sites. No teardown yet — this task must leave normal behavior unchanged and is independently reviewable (registry stays correct across editor open/close).

**Files:**
- Modify: `AppRefinerHook/HookManager.h` (add declarations after line 27, before the `extern "C"` block)
- Modify: `AppRefinerHook/HookManager.cpp` (add registry near the globals at top; register at lines ~1197, ~1234, ~1269, ~1288; unregister in `WM_NCDESTROY` at ~618, ~673, ~777, ~963)
- Modify: `AppRefinerHook/ComboBoxButton.cpp` (register at ~569; unregister at ~293 and ~639)

**Interfaces:**
- Produces:
  - `void RegisterSubclass(HWND hwnd, SUBCLASSPROC proc, UINT_PTR id);` — records a subclass; ignores duplicates (same hwnd+id).
  - `void UnregisterSubclass(HWND hwnd, UINT_PTR id);` — removes a matching record if present.
  - `void RemoveAllSubclasses();` — calls `RemoveWindowSubclass` for every recorded entry, then clears the registry.
  - `std::vector<HWND> GetRegisteredScintillaEditors();` — returns the HWNDs recorded with `id == SCINTILLA_SUBCLASS_ID` (used by Task 2 teardown).
  - `SCINTILLA_SUBCLASS_ID`, `SUBCLASS_ID`, `MAIN_WINDOW_SUBCLASS_ID`, `RESULTS_LIST_SUBCLASS_ID` are existing constants defined in `Common.h`.

- [ ] **Step 1: Declare registry helpers in `HookManager.h`**

Insert after line 27 (after the `KeyboardHook` declaration), before the `// Export functions` block:

```cpp
// --- Subclass registry -------------------------------------------------------
// Records every window this DLL subclasses so teardown can remove them all before
// the DLL is unloaded from the App Designer process. Single-threaded: only touched
// on the remote UI thread that installs/removes subclasses.
#include <vector>

struct SubclassEntry {
    HWND hwnd;
    SUBCLASSPROC proc;
    UINT_PTR id;
};

void RegisterSubclass(HWND hwnd, SUBCLASSPROC proc, UINT_PTR id);
void UnregisterSubclass(HWND hwnd, UINT_PTR id);
void RemoveAllSubclasses();
std::vector<HWND> GetRegisteredScintillaEditors();
```

- [ ] **Step 2: Implement the registry in `HookManager.cpp`**

Add near the top of the file, after the existing global declarations (after line 26, the `g_openTargetBuffer` block):

```cpp
// Registry of subclasses installed by this DLL (see HookManager.h). Per-process,
// single-threaded (remote UI thread), so no locking is required.
static std::vector<SubclassEntry> g_installedSubclasses;

void RegisterSubclass(HWND hwnd, SUBCLASSPROC proc, UINT_PTR id) {
    for (const auto& e : g_installedSubclasses) {
        if (e.hwnd == hwnd && e.id == id) {
            return; // already recorded
        }
    }
    g_installedSubclasses.push_back({ hwnd, proc, id });
}

void UnregisterSubclass(HWND hwnd, UINT_PTR id) {
    for (auto it = g_installedSubclasses.begin(); it != g_installedSubclasses.end(); ++it) {
        if (it->hwnd == hwnd && it->id == id) {
            g_installedSubclasses.erase(it);
            return;
        }
    }
}

void RemoveAllSubclasses() {
    // Copy first: RemoveWindowSubclass triggers no reentrancy here, but a subclass
    // proc's WM_NCDESTROY path could call UnregisterSubclass; iterate a snapshot.
    std::vector<SubclassEntry> snapshot = g_installedSubclasses;
    for (const auto& e : snapshot) {
        if (e.hwnd && IsWindow(e.hwnd)) {
            RemoveWindowSubclass(e.hwnd, e.proc, e.id);
        }
    }
    g_installedSubclasses.clear();
}

std::vector<HWND> GetRegisteredScintillaEditors() {
    std::vector<HWND> editors;
    for (const auto& e : g_installedSubclasses) {
        if (e.id == SCINTILLA_SUBCLASS_ID) {
            editors.push_back(e.hwnd);
        }
    }
    return editors;
}
```

- [ ] **Step 3: Register at the four HookManager subclass sites**

In `GetMsgHook`, add a `RegisterSubclass` call immediately after each successful `SetWindowSubclass`:

At ~line 1197 (parent window), the existing code is:
```cpp
                if (SetWindowSubclass(hWndToSubclass, SubclassProc, SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                    ackFlags |= AR_SUB_ACK_PARENT_SUBCLASSED;
                }
```
Change to:
```cpp
                if (SetWindowSubclass(hWndToSubclass, SubclassProc, SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                    ackFlags |= AR_SUB_ACK_PARENT_SUBCLASSED;
                    RegisterSubclass(hWndToSubclass, SubclassProc, SUBCLASS_ID);
                }
```

At ~line 1234 (Scintilla editor):
```cpp
                    if (SetWindowSubclass(scintillaChild, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                        ackFlags |= AR_SUB_ACK_SCI_SUBCLASSED;
                        RegisterSubclass(scintillaChild, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID);
                    }
```

At ~line 1269 (main window) — the existing call is not wrapped in an `if`; wrap it:
```cpp
                if (SetWindowSubclass(hMainWindow, MainWindowSubclassProc, MAIN_WINDOW_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                    RegisterSubclass(hMainWindow, MainWindowSubclassProc, MAIN_WINDOW_SUBCLASS_ID);
                }
```

At ~line 1288 (Results list) — likewise wrap:
```cpp
                if (SetWindowSubclass(hResultsListView, ResultsListSubclassProc, RESULTS_LIST_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
                    RegisterSubclass(hResultsListView, ResultsListSubclassProc, RESULTS_LIST_SUBCLASS_ID);
                }
```

- [ ] **Step 4: Unregister in the four HookManager `WM_NCDESTROY` handlers**

Add an `UnregisterSubclass` call right before each existing `RemoveWindowSubclass` in the `WM_NCDESTROY` branches:

At ~line 618 (`SubclassProc`):
```cpp
            UnregisterSubclass(hWnd, SUBCLASS_ID);
            RemoveWindowSubclass(hWnd, SubclassProc, SUBCLASS_ID);
```
At ~line 673 (`ScintillaSubclassProc`):
```cpp
            UnregisterSubclass(hWnd, SCINTILLA_SUBCLASS_ID);
            RemoveWindowSubclass(hWnd, ScintillaSubclassProc, SCINTILLA_SUBCLASS_ID);
```
At ~line 777 (`MainWindowSubclassProc`):
```cpp
            UnregisterSubclass(hWnd, MAIN_WINDOW_SUBCLASS_ID);
            RemoveWindowSubclass(hWnd, MainWindowSubclassProc, MAIN_WINDOW_SUBCLASS_ID);
```
At ~line 963 (`ResultsListSubclassProc`):
```cpp
            UnregisterSubclass(hWnd, RESULTS_LIST_SUBCLASS_ID);
            RemoveWindowSubclass(hWnd, ResultsListSubclassProc, RESULTS_LIST_SUBCLASS_ID);
```

- [ ] **Step 5: Register/unregister the ComboBox dialog subclass**

`ComboBoxButton.cpp` uses functions declared in `HookManager.h`; ensure `#include "HookManager.h"` is present at the top (add it if missing).

At ~line 569 (`Setup`), the existing block:
```cpp
    if (SetWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
        OutputDebugStringA("ComboBoxButton::Setup - Subclassed dialog window");
        flags |= AR_SUB_ACK_DIALOG_SUBCLASSED;
```
Add the register call inside the success branch:
```cpp
    if (SetWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID, (DWORD_PTR)callbackWindow)) {
        OutputDebugStringA("ComboBoxButton::Setup - Subclassed dialog window");
        flags |= AR_SUB_ACK_DIALOG_SUBCLASSED;
        RegisterSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID);
```

At ~line 293 (`DialogSubclassProc` `WM_NCDESTROY`):
```cpp
        RemovePropW(hWnd, COMBO_BUTTON_PROP);
        UnregisterSubclass(hWnd, COMBO_DIALOG_SUBCLASS_ID);
        RemoveWindowSubclass(hWnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID);
```

At ~line 639 (`Cleanup`):
```cpp
    // Remove subclass (this will trigger cleanup in WM_NCDESTROY equivalent)
    UnregisterSubclass(dialogHwnd, COMBO_DIALOG_SUBCLASS_ID);
    RemoveWindowSubclass(dialogHwnd, DialogSubclassProc, COMBO_DIALOG_SUBCLASS_ID);
```

Note: `SUBCLASSPROC` is a member function pointer for `DialogSubclassProc`? No — `DialogSubclassProc` is a `static` class method with the `SUBCLASSPROC` signature, so `DialogSubclassProc` decays to a plain function pointer and is a valid `SUBCLASSPROC`, exactly as it is already passed to `SetWindowSubclass`. No cast needed beyond what the existing call uses.

- [ ] **Step 6: Build the hook DLL (developer runs)**

Command (developer runs on Windows with VS 2022):
```
msbuild AppRefinerHook/AppRefinerHook.vcxproj /p:Configuration=Release /p:Platform=x64
```
Expected: build succeeds with no errors. This task changes only bookkeeping, so runtime behavior is unchanged.

- [ ] **Step 7: Commit**

```bash
git add AppRefinerHook/HookManager.h AppRefinerHook/HookManager.cpp AppRefinerHook/ComboBoxButton.cpp
git commit -m "feat(hook): track installed subclasses in a per-process registry"
```

---

## Task 2: `WM_AR_DETACH` teardown handler in the hook DLL

Adds the message and the in-remote-process teardown that destroys child windows, removes all subclasses, and releases the self-reference. Depends on Task 1's registry.

**Files:**
- Modify: `AppRefinerHook/Common.h` (add `WM_AR_DETACH` after line 34)
- Modify: `AppRefinerHook/MinimapManager.h` (declare `UnregisterWindowClass`)
- Modify: `AppRefinerHook/MinimapManager.cpp` (implement it; expose the class name)
- Modify: `AppRefinerHook/HookManager.h` (declare `TearDownAndUnload`)
- Modify: `AppRefinerHook/HookManager.cpp` (implement `TearDownAndUnload`; handle `WM_AR_DETACH` in `MainWindowSubclassProc`)

**Interfaces:**
- Consumes: `RemoveAllSubclasses()`, `GetRegisteredScintillaEditors()` (Task 1); `MinimapManager::DisableMinimap(HWND)`, `ComboBoxButton::Cleanup(HWND)` (existing); `g_dllSelfReference` (existing global in HookManager.cpp).
- Produces:
  - `#define WM_AR_DETACH (WM_USER + 1012)` (Common.h) — consumed by Task 3.
  - `void TearDownAndUnload();` (HookManager.h)
  - `static void MinimapManager::UnregisterWindowClass();` (MinimapManager.h)

- [ ] **Step 1: Define `WM_AR_DETACH` in `Common.h`**

After line 34 (`#define WM_AR_SET_PARAM_NAMES (WM_USER + 1011)`), add:
```cpp
// Message to tear down all subclasses/child windows and release the DLL self-reference
// so the hook DLL can be unloaded from this App Designer process (sent on AppRefiner close).
#define WM_AR_DETACH (WM_USER + 1012)
```

- [ ] **Step 2: Add `UnregisterWindowClass` to MinimapManager**

In `MinimapManager.h`, add to the `public:` section (after `GetMinimapWindow`, line 26):
```cpp
    // Unregister the minimap window class (call during DLL teardown, after all
    // minimap windows have been destroyed).
    static void UnregisterWindowClass();
```

In `MinimapManager.cpp`, add the implementation (the class name `MINIMAP_WINDOW_CLASS` and `g_hModule` are already in scope at file top):
```cpp
void MinimapManager::UnregisterWindowClass()
{
    // No-op if never registered or already gone; UnregisterClassW fails harmlessly then.
    UnregisterClassW(MINIMAP_WINDOW_CLASS, g_hModule);
}
```

- [ ] **Step 3: Declare `TearDownAndUnload` in `HookManager.h`**

Add below the registry declarations from Task 1 (Step 1):
```cpp
// Full teardown for the current process: destroys DLL-created child windows,
// removes every subclass, and releases the DLL self-reference so the hook DLL can
// be unloaded once the hooks are removed. Runs on the remote UI thread.
void TearDownAndUnload();
```

- [ ] **Step 4: Implement `TearDownAndUnload` in `HookManager.cpp`**

Add after the registry functions from Task 1. It needs `MinimapManager.h` and `ComboBoxButton.h`, which are already included at the top of HookManager.cpp (lines 2-3):
```cpp
void TearDownAndUnload() {
    OutputDebugStringA("TearDownAndUnload: beginning hook DLL teardown\n");

    // 1. Destroy DLL-created child windows keyed per Scintilla editor.
    std::vector<HWND> editors = GetRegisteredScintillaEditors();
    for (HWND sci : editors) {
        if (sci && IsWindow(sci)) {
            MinimapManager::DisableMinimap(sci);   // no-op if not enabled
            ComboBoxButton::Cleanup(sci);          // removes dialog subclass + destroys button
        }
    }

    // 2. Unregister the minimap window class now that no minimap windows remain.
    MinimapManager::UnregisterWindowClass();

    // 3. Remove every subclass this DLL installed (includes the main window subclass
    //    currently executing; removing a subclass from within its own proc is allowed).
    RemoveAllSubclasses();

    // 4. Release the self-reference pin taken in DllMain so the module refcount can
    //    reach zero once the WH_GETMESSAGE/WH_KEYBOARD hooks are removed by AppRefiner.
    if (g_dllSelfReference != NULL) {
        FreeLibrary(g_dllSelfReference);
        g_dllSelfReference = NULL;
        OutputDebugStringA("TearDownAndUnload: released DLL self-reference\n");
    }

    OutputDebugStringA("TearDownAndUnload: complete\n");
}
```

Note: `ComboBoxButton::Cleanup` also calls `UnregisterSubclass` for the dialog (Task 1, Step 5), so its dialog entry is gone before `RemoveAllSubclasses`; the snapshot copy in `RemoveAllSubclasses` makes that ordering safe.

- [ ] **Step 5: Handle `WM_AR_DETACH` in `MainWindowSubclassProc`**

In `MainWindowSubclassProc` (HookManager.cpp), add a handler right after the `WM_NCDESTROY` block (after ~line 779) and before `HWND callbackWindow = (HWND)dwRefData;`:
```cpp
        // Handle detach/unload request from AppRefiner (sent on AppRefiner close)
        if (uMsg == WM_AR_DETACH) {
            OutputDebugStringA("MainWindowSubclassProc: WM_AR_DETACH received\n");
            TearDownAndUnload();
            return 1; // handled; do not call DefSubclassProc (this proc was just removed)
        }
```

- [ ] **Step 6: Build the hook DLL (developer runs)**

```
msbuild AppRefinerHook/AppRefinerHook.vcxproj /p:Configuration=Release /p:Platform=x64
```
Expected: build succeeds. No behavior change yet at runtime because nothing sends `WM_AR_DETACH` until Task 4.

- [ ] **Step 7: Commit**

```bash
git add AppRefinerHook/Common.h AppRefinerHook/HookManager.h AppRefinerHook/HookManager.cpp AppRefinerHook/MinimapManager.h AppRefinerHook/MinimapManager.cpp
git commit -m "feat(hook): add WM_AR_DETACH teardown that unloads the DLL"
```

---

## Task 3: Managed detach API in `EventHookInstaller`

Adds the C# side: the message constant, a `SendMessageTimeout` import, and `DetachFromProcess`. Also removes the dead `WM_REMOVE_HOOK` / `RemoveHook()` now that a real detach path exists.

**Files:**
- Modify: `AppRefiner/Events/EventHookInstaller.cs`

**Interfaces:**
- Consumes: `WM_AR_DETACH` semantics = `WM_USER + 1012` (matches Common.h from Task 2).
- Produces: `public static void DetachFromProcess(IntPtr mainWindowHandle)` — consumed by Task 4.

- [ ] **Step 1: Add the `WM_AR_DETACH` constant**

In `EventHookInstaller.cs`, alongside the other `WM_*` constants (after line 18, `WM_AR_SET_PARAM_NAMES`):
```csharp
        private const uint WM_AR_DETACH = WM_USER + 1012;
```

- [ ] **Step 2: Add the `SendMessageTimeout` P/Invoke and flags**

Add near the other Win32 imports (after the `PostThreadMessage` import, line 56):
```csharp
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
            IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
```

- [ ] **Step 3: Implement `DetachFromProcess`**

Add as a public method (e.g. after `CleanupAllHooks`, around line 198):
```csharp
        /// <summary>
        /// Asks the hook DLL inside the given App Designer process to tear down all
        /// subclasses/child windows and release its self-reference, so the DLL can be
        /// unloaded once the hooks are removed. Sent to the main window synchronously
        /// with a timeout so an unresponsive App Designer cannot block AppRefiner's close.
        /// Best-effort: on timeout/no-response the DLL simply stays loaded in that instance.
        /// </summary>
        public static void DetachFromProcess(IntPtr mainWindowHandle)
        {
            if (mainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            IntPtr ret = SendMessageTimeout(mainWindowHandle, WM_AR_DETACH, IntPtr.Zero,
                IntPtr.Zero, SMTO_ABORTIFHUNG, 2000, out _);

            if (ret == IntPtr.Zero)
            {
                Debug.Log($"DetachFromProcess: no response from main window {mainWindowHandle} " +
                          "(timeout or hung); continuing shutdown");
            }
            else
            {
                Debug.Log($"DetachFromProcess: teardown acknowledged by main window {mainWindowHandle}");
            }
        }
```

- [ ] **Step 4: Remove the dead `WM_REMOVE_HOOK` / `RemoveHook()`**

Delete the now-obsolete definitions:
- The constant at line 11: `private const uint WM_REMOVE_HOOK = WM_USER + 1004;`
- The method `RemoveHook` (lines ~177-181):
```csharp
        // Method to remove the hook
        public static bool RemoveHook(uint threadId)
        {
            return PostThreadMessage(threadId, WM_REMOVE_HOOK, IntPtr.Zero, IntPtr.Zero);
        }
```

Before deleting, confirm there are no remaining references (there should be none — the hook never handled it):
```
grep -rn "RemoveHook\|WM_REMOVE_HOOK" AppRefiner --include=*.cs
```
Expected after edit: no matches. If any *other* references exist, stop and reassess rather than deleting.

- [ ] **Step 5: Build AppRefiner (developer runs)**

```
dotnet build AppRefiner/AppRefiner.csproj
```
Expected: build succeeds, no warnings about the removed symbols.

- [ ] **Step 6: Commit**

```bash
git add AppRefiner/Events/EventHookInstaller.cs
git commit -m "feat: add EventHookInstaller.DetachFromProcess; drop dead RemoveHook"
```

---

## Task 4: Wire detach into AppRefiner shutdown

Calls `DetachFromProcess` for each attached App Designer process during `OnFormClosing`, before `CleanupAllHooks()`. This is the task that makes the whole feature take effect, so it ends with the full manual end-to-end verification.

**Files:**
- Modify: `AppRefiner/MainForm.cs` (`OnFormClosing`, starting at line 1231; `AppDesignerProcesses` field is at line 85)

**Interfaces:**
- Consumes: `EventHookInstaller.DetachFromProcess(IntPtr)` (Task 3); `AppDesignerProcesses` (`Dictionary<uint, AppDesignerProcess>`, line 85); `AppDesignerProcess.MainWindowHandle` (`IntPtr`).

- [ ] **Step 1: Send detach to every attached process before unhooking**

In `OnFormClosing`, replace the opening of the method (lines 1231-1234):
```csharp
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up all hooks to ensure they're properly removed
            AppRefiner.Events.EventHookInstaller.CleanupAllHooks();
```
with:
```csharp
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ask the hook DLL in each attached App Designer process to tear itself down
            // (remove subclasses/child windows, release its self-reference) BEFORE we
            // unhook. This lets the DLL be unloaded from those processes so AppRefiner
            // (and the hook DLL) can be replaced without closing App Designer.
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                AppRefiner.Events.EventHookInstaller.DetachFromProcess(appDesigner.MainWindowHandle);
            }

            // Clean up all hooks to ensure they're properly removed
            AppRefiner.Events.EventHookInstaller.CleanupAllHooks();
```

- [ ] **Step 2: Build AppRefiner (developer runs)**

```
dotnet build AppRefiner/AppRefiner.csproj
```
Expected: build succeeds.

- [ ] **Step 3: Manual end-to-end verification (developer runs)**

Ensure the freshly built `AppRefinerHook.dll` (from Task 2) and AppRefiner (Task 4) are deployed together, then:

1. Launch Application Designer; attach AppRefiner; open a PeopleCode editor. Interact enough to exercise every subclass: type in the editor (parent + editor subclasses), enable the minimap via the combo button (minimap + combo button/dialog), open the Find dialog / a Results list (main window + results list subclasses).
2. In Process Explorer (or `tasklist /m AppRefinerHook.dll`), confirm `AppRefinerHook.dll` **is** loaded in the App Designer process.
3. Close AppRefiner (normal exit).
4. Confirm via Process Explorer / `tasklist /m AppRefinerHook.dll` that the DLL is **no longer** loaded in the still-running App Designer process. (It unmaps on the remote thread's next message pump — clicking around App Designer forces this.)
5. Confirm App Designer remains fully responsive: type in the editor, open/close dialogs, open another object — no crash.
6. Confirm `AppRefiner.exe` and `AppRefinerHook.dll` can now be deleted/replaced while App Designer stays open. **Caveat:** if the enhanced editor was used this session, the swapped Scintilla DLL under `scintilla_mods\` (`AppDesignerProcess.ScintillaModsDirectory`, which lives inside the install) stays mapped in that App Designer instance while editors are open and remains locked until the instance closes — this teardown only unloads `AppRefinerHook.dll`. Run this check once with the enhanced editor off (whole install replaceable) and once with it on (all but the `scintilla_mods` DLL replaceable) so the limitation is observed, not discovered as a failure.
7. Repeat steps 1-6 with **two** App Designer instances attached to confirm per-process teardown (both should release `AppRefinerHook.dll`).
8. Reattach test (the feature's real payoff): after closing AppRefiner and replacing the install, launch the **new** AppRefiner against the **same still-running** App Designer, open a combo dialog and toggle the minimap. A stale combo-button/minimap window class left behind by the previous teardown would crash here; a clean run confirms the class-unregister teardown worked.

Expected: `AppRefinerHook.dll` is gone from every App Designer process after AppRefiner closes; App Designer stays responsive; AppRefiner + its hook DLL are replaceable (with the `scintilla_mods` caveat above); reattach works without a crash.

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/MainForm.cs
git commit -m "feat: detach hook DLL from App Designer processes on AppRefiner close"
```

---

## Self-Review

**Spec coverage:**
- Self-reference pin release → Task 2, Step 4 (`FreeLibrary(g_dllSelfReference)`).
- Remove all subclasses (parent, editor, main, results, combo dialog) → Task 1 (registry + all sites) + Task 2 `RemoveAllSubclasses`.
- Destroy combo button + minimap child windows → Task 2, Step 4 (`ComboBoxButton::Cleanup`, `MinimapManager::DisableMinimap`).
- Unregister minimap window class → Task 2, Steps 2 & 4.
- `WM_AR_DETACH` handled synchronously in `MainWindowSubclassProc` → Task 2, Step 5.
- `SendMessageTimeout` with 2000ms `SMTO_ABORTIFHUNG`, best-effort → Task 3, Steps 2-3.
- Iterate attached processes before `CleanupAllHooks` → Task 4, Step 1.
- Remove dead `WM_REMOVE_HOOK`/`RemoveHook` → Task 3, Step 4.
- Registry is DLL-side, per-process, unlocked → Task 1 (matches the two decisions confirmed during brainstorming).

**Placeholder scan:** No TBD/TODO/"handle edge cases"; every code step shows complete code.

**Type consistency:** `RegisterSubclass`/`UnregisterSubclass`/`RemoveAllSubclasses`/`GetRegisteredScintillaEditors` used identically in Tasks 1 & 2. `SubclassEntry` fields (`hwnd`, `proc`, `id`) consistent. `DetachFromProcess(IntPtr)` defined in Task 3, called with `appDesigner.MainWindowHandle` (IntPtr) in Task 4. `WM_AR_DETACH` = `WM_USER + 1012` in both Common.h (Task 2) and EventHookInstaller.cs (Task 3).
