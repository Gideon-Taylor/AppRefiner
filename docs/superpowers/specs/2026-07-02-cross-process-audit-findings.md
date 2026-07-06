# Cross-Process Surface Audit — Findings Catalog

**Date:** 2026-07-02
**Scope:** All surfaces where AppRefiner reads/writes memory in remote App Designer (pside.exe) processes, marshals data via window messages, and tracks editor/process identity across multiple concurrent App Designer instances.
**Goal:** Address all findings before the next planned release.
**Status legend:** `[ ]` open · `[x]` fixed · `[~]` partially addressed / needs verification

Reported symptoms this audit explains:
- Autocomplete inserting the full item after a typed prefix (`&var` + accept → `&varvariable` or `&var&variable`).
- Intermittent "memory buffers go weird" — wrong values appearing in autocompletes and other features.
- Auto-connect DB dialogs appearing invisibly behind windows when multiple App Designers are open at AppRefiner startup (see F14).

---

## Critical — direct causes of observed corruption

### [~] F1. Shared `docText` buffer raced across threads and editors
**Fixed 2026-07-02 (needs manual verification):** `GetScintillaText`/`SetScintillaText` now use per-call temp buffers — the shared `docText` buffer no longer exists, which also covers same-thread reentrancy (a pumped WndProc handler re-entering mid-read) that a lock could not fix. All other shared named-buffer sequences (calltip, annotations, styled annotations, properties, userlist, autocomplete, fillups, search/replace/line buffers, pasteReplaceBuffer, openTarget, scintillaDLL, results list) now hold `MemoryManager.SyncRoot` for the full write → SendMessage → read sequence, and `MemoryManager`'s dictionary is internally locked. `FunctionParameterNames` buffers intentionally unlocked (single serialized styler consumer). Holding the lock across SendMessage is deadlock-safe: threads blocked in SendMessage service incoming sent messages.
**Where:** `ScintillaManager.GetScintillaText` (ScintillaManager.cs:319), `SetScintillaText` (:361), `MemoryManager.GetOrCreateBuffer` (MemoryManager.cs:31)
**Mechanism:** One process-wide remote buffer named `"docText"` serves every editor and every thread. `GetScintillaText` is a non-atomic two-step: `SCI_GETTEXT` writes into the remote buffer, then `ReadProcessMemory` reads it back. Concurrent callers interleave and read back *another call's text* (possibly a different editor's document).
**Concurrency is real, not theoretical:** `StylerManager.ProcessStylerWorkItem` runs on `Task.Run` threads (StylerManager.cs:147, :225 calls `GetScintillaText`), and autocomplete code uses `Task.Delay(...).ContinueWith(..., TaskScheduler.Default)` continuations (AutoCompleteService.cs:1140, MainForm.cs:2262/2337), all concurrent with UI-thread WndProc handlers.
**Fix direction:** Serialize the full write→SendMessage→read sequence per buffer (lock), or use per-call temp buffers for document reads. Make `MemoryManager._buffers` (plain `Dictionary`, no lock) thread-safe.

### [~] F2. `RemoteBuffer.Resize` frees remote memory that may be in use
**Fixed 2026-07-02 (needs manual verification):** Resize now only happens while `MemoryManager.SyncRoot` is held (internally by `GetOrCreateBuffer`, and call-site resizes are inside locked sequences), so no other thread can be mid-operation against the old address. Residual risk: long-lived cached remote addresses (F19's `paramNameAddresses`) would still dangle if their buffer were ever resized — tracked under F19.
**Where:** RemoteBuffer.cs:91–134, called from `MemoryManager.GetOrCreateBuffer` (MemoryManager.cs:44)
**Mechanism:** Resize allocates a new block and `VirtualFreeEx`'s the old one. Another thread mid-operation (e.g., between `SCI_GETTEXT` and `ReadProcessMemory`) still holds the old address. The freed address can be handed back by the next `VirtualAllocEx` for a *different* named buffer → cross-contamination between features ("buffers go weird").
**Fix direction:** Same lock as F1; additionally consider deferring the free (e.g., free-list drained only when no operation is in flight).

### [~] F3. Autocomplete acceptance re-derives typed prefix from fragile re-reads
**Fixed 2026-07-02 (needs manual verification in App Designer):** Hook now forwards `scn->position` in WPARAM (HookManager.cpp SCN_AUTOCSELECTION). MainForm deletes `[position, currentPos)` directly — no document re-read — and the Variable/SystemVariables/ObjectMembers handlers were simplified to insert-at-cursor only (their backward-scan code was deleted; the only remaining user-list flow is QuickFix, which never scanned). **No stale-DLL fallback by design** — AppRefiner ships as one package; **release notes must tell users to restart any App Designer that was open before the update** so the new hook DLL is loaded. `SCN_USERLISTSELECTION`/QuickFix path unchanged.
**Where:** `MainForm.WndProc` SCN_AUTOCSELECTION handler (MainForm.cs:2287–2368), `ScintillaManager.CalculateLengthEntered` (ScintillaManager.cs:1937), `AutoCompleteService.HandleVariableListSelection` (:1407) / `HandleSystemVariableListSelection` (:1229) / `HandleObjectMemberListSelection` (:1289)
**Mechanism:** On acceptance, the handler cancels Scintilla's insertion and re-derives `lengthEntered` by re-reading the entire document (through the racy F1 buffer) and scanning backward for the trigger char; the list-selection handler then re-reads the document *again* to find the trigger position. Any stale/wrong read → `lengthEntered = 0` → typed prefix not deleted → fallback inserts full item text → `&var&variable` / `&varvariable`.
**Fix direction:** Have the hook forward `scn->position` (SCN_AUTOCSELECTION's authoritative start-of-word; currently discarded — HookManager.cpp:467 sends `WPARAM 0`) and use `SCI_AUTOCPOSSTART`. Eliminate all backward text scanning in the acceptance path.

### [~] F4. Byte positions indexed into UTF-16 strings
**Fixed 2026-07-02 (needs manual verification):** `CalculateLengthEntered` (still used at autocomplete *show* time) now scans UTF-8 bytes; the char-indexed backward scans in the three list-selection handlers were deleted outright (see F3); `ShowAppPackageSuggestions` slices UTF-8 bytes for line content. Remaining byte/char mixing elsewhere (e.g., other `GetScintillaText` position consumers) should be audited separately when touched.
**Where:** `CalculateLengthEntered` (ScintillaManager.cs:1942, 1964–1989), `HandleVariableListSelection` (AutoCompleteService.cs:1430–1445), `HandleSystemVariableListSelection` (:1249–1264), `HandleObjectMemberListSelection` (:1359–1374), `ShowAppPackageSuggestions` (:284–289)
**Mechanism:** Scintilla positions are UTF-8 **byte** offsets; the code indexes C# UTF-16 strings with them (`content[i]`, `content.Substring(lineStartPos, ...)`, `position > content.Length`). One non-ASCII character above the cursor (smart quote in a comment) skews every downstream position → trigger char never found → same doubling symptom as F3, but deterministic per document.
**Repro hypothesis:** put `’` in a comment above the cursor, type `&var`, accept a completion → doubling should occur every time.
**Fix direction:** Convert between byte and char indices via UTF-8 encoding at the boundary, or operate on UTF-8 bytes directly. Audit every `SendMessage(SCI_GETCURRENTPOS/...)` consumer for unit consistency.

### [~] F5. All hook notifications routed to the global `activeEditor`
**Fixed 2026-07-02 (needs manual verification):** Every editor-scoped hook notification now carries the source Scintilla HWND in the high 32 bits of one message parameter (`AR_PACK_HWND` macro; per-message layout documented in AppRefinerHook/Common.h — relies on x64's 32-bit HWNDs and all payloads being 32-bit). `MainForm.WndProc` resolves `pid → AppDesignerProcesses[pid].GetOrInitEditor(hwnd)` per message (`ResolveEditorFromHwnd`) and drops notifications from unknown windows/processes instead of misapplying them. `AR_INSERT_CHECK` now reads its remote pointer with the *sender's* process handle. C#-side senders of hook-protocol messages (`InvokeAutocompleteCommand`, `CreateAutoComplete`, object-member call-tip retrigger) pack the editor HWND via `MainForm.PackHwnd`. Object-members handler also switched from `activeAppDesigner.TypeResolver` to the source editor's process resolver. Intentionally still focus-scoped: `AR_KEY_COMBINATION` and `AR_CONTEXT_MENU_OPTION` (user-focus semantics). **Ship note: hook DLL and AppRefiner must be updated together** — an old DLL sends zero HWNDs and its notifications are dropped (features silently inert) until the App Designer is restarted with the new DLL; reinforces the "restart open App Designers" release note from F3.
**Where:** `MainForm.WndProc` (MainForm.cs:2196 onward — every `WM_SCN_*` and `AR_*` case), hook side HookManager.cpp (no source HWND forwarded)
**Mechanism:** The hook never identifies which editor produced a notification; WndProc applies everything to `activeEditor`. `activeEditor` updates travel WinEvent → `syncContext.Post` (posted message), while hook notifications arrive via cross-thread `SendMessage` which is serviced ahead of posted messages. On focus switch + immediate typing, notifications from the new editor are processed against the previous editor — including across *different pside processes*.
**Worst case:** `AR_INSERT_CHECK` (MainForm.cs:2524–2603) treats `m.WParam` as a pointer, valid only in the **sender's** process, but dereferences it with `activeEditor.AppDesignerProcess.ProcessHandle` and then fires `SCI_CHANGEINSERTION` at `activeEditor` — wrong-process read + wrong-editor mutation.
**Also:** `WM_AR_TYPING_PAUSE` and `WM_AR_CURSOR_POSITION_CHANGED` are `PostMessage`'d from the hook (EditorManager.cpp:34, :160), so they are inherently stale on arrival.
**Fix direction:** Forward the source Scintilla HWND in every hook message (pack into WParam/LParam or a struct); resolve `pid → AppDesignerProcesses[pid].Editors[hwnd]` in WndProc. Never use `activeEditor` for notification handling.

### [~] F6. `RemoteBuffer` finalizer frees memory AppRefiner doesn't own
**Fixed 2026-07-02 (needs manual verification):** `~RemoteBuffer()` and `~MemoryManager()` removed — a GC-time `VirtualFreeEx` could run against a closed/recycled process handle or free a buffer mid-use; a missed explicit `Free()` is now a bounded leak instead of corruption. `FromRemoteAddress` removed entirely: its remaining users (`GetLineText`/`GetSelectedLines` via F24's `ReadDocumentRange`, and AR_INSERT_CHECK via new `AppDesignerProcess.ReadMemory`) now do plain reads with no ownership semantics, and the class documents why wrapping foreign pointers is forbidden. Named buffers are freed by `MemoryManager.Cleanup()`; temp buffers are freed explicitly by their creators.
**Where:** `~RemoteBuffer()` → `Free()` → `VirtualFreeEx` (RemoteBuffer.cs:407–429); `FromRemoteAddress` wrappers at ScintillaManager.cs:1278 (`SCI_GETRANGEPOINTER` — Scintilla's internal document memory), :1581 (same, immediately unreferenced → finalized), MainForm.cs:2530 (hook's stack-allocated 24-byte insert-check struct), :2547 (Scintilla's internal text pointer)
**Mechanism:** At arbitrary GC times, the finalizer thread calls `VirtualFreeEx(MEM_RELEASE)` on remote addresses obtained from Scintilla/the hook. It usually fails silently (address not a region base), but when the pointer happens to be a region base it *succeeds* — releasing live App Designer memory. Classic "works most of the time" corruption source.
**Related:** buffers hold raw copies of `ProcessHandle`; after `AppDesignerProcess.Cleanup()` closes the handle (AppDesignerProcess.cs:291), later finalizers use a closed, recyclable handle — could target an unrelated process.
**Fix direction:** Add `OwnsMemory` flag set false by `FromRemoteAddress`; remove or gate the finalizer; never `VirtualFreeEx` unowned addresses; tie buffer lifetime to `MemoryManager` (deterministic cleanup) instead of GC.

---

## High — multi-instance identity and lifecycle

### [~] F7. HWND reuse: `Editors` entries never removed
**Fixed 2026-07-02 (needs manual verification):** `WinEventService` now surfaces `EVENT_OBJECT_DESTROY` (already inside the hooked event range; filtered to `OBJID_WINDOW`) as a `WindowDestroyed` event. `MainForm.HandleWindowDestroyedEvent` evicts the tracked editor from its process's `Editors` map, clears `activeEditor`/`pendingSaveEditor`/styler-debounce references, and calls `editor.Cleanup()`.
**Where:** `AppDesignerProcess.Editors` (AppDesignerProcess.cs:52), `GetOrInitEditor` (:186); no `Editors.Remove` anywhere in the codebase
**Mechanism:** Windows recycles HWNDs. A new editor window landing on a recycled HWND resurrects the stale `ScintillaEditor` — old caption, type, autocomplete context, cached parse/content hash — producing wrong-values behavior that "fixes itself" on the next caption change.
**Fix direction:** Evict on `EVENT_OBJECT_DESTROY` (WinEventService already spans the range) or on `IsValid()` failure; validate cached editor identity (e.g., recheck grandparent caption) in `GetOrInitEditor`.

### [~] F8. PID reuse: `AppDesignerProcesses` / `trackedProcessIds` entries never removed
**Fixed 2026-07-02 (needs manual verification):** both registration sites call `WatchAppDesignerProcessExit(pid)` (`Process.Exited` with `EnableRaisingEvents`, marshalled via `BeginInvoke`, with a `HasExited` race guard). `HandleAppDesignerProcessExit` removes the dictionary/tracking entries, clears `activeAppDesigner`/`activeEditor`/`pendingSaveEditor` if they belonged to the dead process, prunes `lastKnownPositions` (`pid:caption` keys) and styler-debounce entries, and calls `process.Cleanup()` (buffer frees no-op against the dead process; the handle gets closed).
**Where:** MainForm.cs:79, :2060, :4611–4612; no removal path
**Mechanism:** After an App Designer exits, its entry (with closed/dead `ProcessHandle`) stays. A new pside.exe on a recycled PID is treated as already-tracked → hooks never installed, memory ops silently fail until AppRefiner restarts. `trackedProcessIds` also grows forever.
**Fix direction:** Watch process exit (e.g., `Process.Exited` or validate handle liveness on focus) and remove + `Cleanup()` the entry. The Instances-tab lifecycle-tracking spec (2026-06-11) is the natural home for this.

### [~] F9. `CaptionChanged` handler closes over the `activeEditor` field
**Fixed 2026-07-02:** the handler now casts the event `sender` and operates on the editor that raised the event.
**Where:** MainForm.cs:4286–4327
**Mechanism:** The handler is attached to a specific editor but its body operates on the *current* `activeEditor` field at fire time. If editor X's caption changes while Y is active, position restore / fold restore / content refresh hit Y.
**Fix direction:** Use the event `sender` (cast to `ScintillaEditor`) or capture a local.

### [~] F10. `MainThreadId` assumes `Threads[0]` is the UI thread
**Fixed 2026-07-02:** derived from `GetWindowThreadProcessId(MainWindowHandle)`; `Threads[0]` kept only as a logged fallback when no main window exists yet.
**Where:** AppDesignerProcess.cs:150
**Mechanism:** `Process.Threads` order is not guaranteed. Wrong thread → hook installed on a non-UI thread → notifications and thread-posted messages silently go nowhere.
**Fix direction:** Derive the thread from `GetWindowThreadProcessId(MainWindowHandle)`.

---

## Medium — protocol and state-consistency hazards

### [~] F11. Scintilla global autocomplete state mutated non-atomically
**Fixed 2026-07-02 (needs manual verification):** `ShowUserList`/`ShowAutoComplete` hold `MemoryManager.SyncRoot` across the entire separator/fillups/options/order → show → separator-reset sequence, so two threads can no longer interleave their setup. Not addressed: saved `autoCOptions`/`sortOrder` still not restored after the show, and `/` in suggestion text still breaks splitting (both cosmetic-risk leftovers).
**Where:** `ShowUserList` / `ShowAutoComplete` / `SetAutoCompleteContextCharacters` (ScintillaManager.cs:1783–2038)
**Mechanism:** Separator set to `/`, fillups/stops/order/ignore-case mutated, list shown, separator reset — with no synchronization. A second thread interleaving corrupts list parsing (one giant item / wrong splits). Saved `autoCOptions`/`sortOrder` are read but never restored. Suggestions containing `/` also break splitting.
**Fix direction:** Single lock around show sequences (can share the F1 lock); restore saved options; consider a separator unlikely to appear in suggestion text.

### [~] F12. Cancel-during-notification reliance for autocomplete insertion suppression
**Where:** MainForm.cs:2300 (`CancelUserList` inside SCN_AUTOCSELECTION), HookManager.cpp:458
**Mechanism:** The design assumes `SCI_AUTOCCANCEL` issued during SCN_AUTOCSELECTION prevents Scintilla's own insertion. That is version-dependent behavior; on the stock (old) PSIDE Scintilla vs `scintilla_mods`, if any version inserts anyway, both Scintilla and AppRefiner insert → doubling independent of F3/F4.
**VERIFIED from source for all scintilla_mods builds (2026-07-02):** 4-4-6-mods, 5-3-3-mods, and 5-5-0-mods all have `NotifyParent(scn); if (!ac.Active()) return;` in `AutoCompleteCompleted` — cancelling during the notification suppresses the insertion. The same source also confirms `scn.position = ac.posStart - ac.startLen`, the exact semantics the F3/F5 hook fix relies on. **Remaining open only for stock PSIDE Scintilla** (editors running without the enhanced editor DLL) — one manual acceptance test there closes this (test script section I).

### [~] F13. `LoadScintillaDll` sequential-write and result-handling bugs
**Where:** AppDesignerProcess.cs:400–428; same pattern check for `SetOpenTarget` (:299)
**Mechanism:** No `Reset()` before `WriteString` (sequential offset persists across calls) and the `WriteString` return value is ignored; the message always sends `remoteBuffer.Address` (offset 0). Second call ⇒ either silently fails to write (buffer exactly sized) or writes at a nonzero offset — the remote thread loads the *first* DLL path again. Also `PostThreadMessage` is async while the buffer is shared — the buffer must not be rewritten until consumed.
**Fixed 2026-07-02 (needs manual verification):** `Reset()` added, `WriteString` result checked, `PostThreadMessage` result returned, sequence locked. Async-consumption caveat documented in code (buffer is written once per process in practice).

### [~] F14. Auto-connect DB dialogs shown for background App Designers at startup
**Fixed 2026-07-02 (needs manual verification with 3 App Designers open before AppRefiner starts):** `ConnectToDB` now takes the target `AppDesignerProcess` as a parameter (was reading the global `activeAppDesigner`, which each startup discovery overwrote). The unconditional startup prompt was replaced by `MaybePromptForDbConnection(process)`, which self-guards (already connected / previously declined / DBName unknown) and **only prompts when that App Designer is the foreground application**, so the modal dialog owned by its main window is actually visible. It is invoked from: the delayed DBName capture (covers "AppRefiner started while an App Designer is focused"), both App Designer activation paths (editor focus via `SetActiveEditor`, window focus via the WinEvent handler — not gated on "changed" since startup enumeration sets `activeAppDesigner` without real focus), and the existing editor-init path. Cancel still sets `DoNotPromptForDB`, so each process prompts at most once.
**Where:** `ValidateAndCreateAppDesignerProcessInternal` (MainForm.cs:4625–4642) → `ConnectToDB` (:3876)
**Mechanism:** Each discovered process schedules `Task.Delay(1000) → Invoke(ConnectToDB)`. Two compounding problems:
1. `ConnectToDB()` reads the **global `activeAppDesigner`**, which each creation overwrote (:4613) — with 3 instances, up to 3 dialogs fire, potentially all targeting the *last* process, not the one each continuation was scheduled for.
2. The modal `DBConnectDialog` is owned by the App Designer main window, but at AppRefiner startup none of those windows is foreground (AppRefiner is). The dialogs open invisibly behind other windows; AppRefiner's UI thread is blocked in nested `ShowDialog` loops, so AppRefiner appears hung until the user hunts down each dialog.
**Agreed design:** Defer the auto-connect prompt to **first activation** of each App Designer as the active one — i.e., when it gains focus and AppRefiner sets it up as `activeAppDesigner` (hook install / focus path), if `PromptForDB && DataManager == null && !DoNotPromptForDB && !prompted-before`, show the dialog *then*. The owner window is foreground at that moment, so the dialog is visible and correctly parented. Add a per-process `HasPromptedForDB` (or reuse `DoNotPromptForDB` semantics) so it fires once per process. Remove the `Task.Delay(1000)` startup prompt entirely; keep the delayed DBName capture.
**Also fix:** `ConnectToDB` should take the target `AppDesignerProcess` as a parameter instead of reading the global.

### [~] F15. `GetScintillaText` reads past the NUL / stale tail bytes
**Fixed 2026-07-02 (with F1):** per-call temp buffers are zero-filled by VirtualAllocEx, and the read now truncates at the first NUL, so a shrunk document yields clean text.
**Where:** ScintillaManager.cs:323–336
**Mechanism:** `textLength` is captured via `SCI_GETLENGTH`, then `SCI_GETTEXT` fills the buffer, then `ReadString(textLength)` reads exactly that many bytes ignoring the NUL. If the document shrank between the two messages, the tail contains leftover bytes from a previous (possibly different-editor) read.
**Fix direction:** Truncate at the first NUL after read; ideally re-check length or use a single atomic read strategy.

### [~] F16. `ReadUtf8FromMemory` fails on page-boundary reads
**Fixed 2026-07-02:** on failure, retries reading only up to the next 4 KB page boundary before giving up.
**Where:** ScintillaManager.cs:2071–2111 (used for SCN_AUTOCSELECTION / SCN_USERLISTSELECTION text)
**Mechanism:** Always reads a full 256 bytes; if the source string sits near the end of a mapped region, `ReadProcessMemory` fails entirely → selection silently dropped.
**Fix direction:** On failure, retry with shrinking lengths (or read to page boundary).

---

## Low — correctness/robustness cleanups found during the audit

### [~] F17. `ScintillaEditor` constructor nulls `AppDesignerProcess`
**Fixed 2026-07-02:** the `AppDesignerProcess = null;` line is removed; a comment documents the assignment-before-Caption ordering requirement.
**Where:** ScintillaEditor.cs:493–501 (`AppDesignerProcess = null;` as last statement)
Works only because `InitEditor` reassigns immediately; any other construction path NREs. Remove the null assignment.

### [~] F18. `AppDesignerProcess.TypeResolver` getter recreates resolvers every access
**Fixed 2026-07-02:** the resolver is cached and only recreated when the `DataManager` instance actually changes (tracked by reference), preserving the resolver's metadata caches across accesses.
**Where:** AppDesignerProcess.cs:82–104
The inverted `else` branch replaces a live `DatabaseTypeMetadataResolver` with a `NullTypeMetadataResolver` and then re-creates a fresh database resolver on each get — discarding any metadata caching. Cache the instance; invalidate only when `DataManager` changes.

### [~] F19. `FunctionParameterNames` shared buffer never reset; stale-address hazard
**Fixed 2026-07-02 (needs manual verification):** the `parameterNames` buffer write offset is reset on editor change (the address cache is cleared at the same time, so nothing references the old strings). Single-run overflow still logs and drops hints.
**Copy assumption VERIFIED from source (2026-07-02):** in the Scintilla mods repo (`C:/Users/tslat/repos/GitHub/Scintilla`, branches 4-4-6-mods / 5-3-3-mods / 5-5-0-mods), `LineInlayHints::SetInlayInfo` constructs each `InlayHint` whose `text` member is a `std::string` — hint text is copied into Scintilla's own storage synchronously during `SCI_SETINLAYINFO`. No pointers into AppRefiner's buffer are retained, so the buffer reset is safe by design; the editor-switching test is now just a regression check.
**Where:** Stylers/FunctionParameterNames.cs:100, :197, :244
`parameterNames` buffer write offset grows monotonically across editor switches until full → hints silently stop for the process lifetime. `paramNameAddresses` cache is cleared on editor change but the addresses would dangle if the buffer were ever resized (Resize invalidates all addresses). Also verify the custom Scintilla copies inlay text during `SCI_SETINLAYINFO` rather than retaining pointers into `parameterNames` for paint-time reads — if it retains, any buffer churn garbles rendered hints.

### [~] F20. `SetScintillaText` size/encoding mismatch
**Where:** ScintillaManager.cs:365–370
Size computed with `Encoding.Default`, content written with `Encoding.UTF8`. Identical on .NET 8 (Default == UTF-8) but fragile; use UTF-8 for both.
**Fixed 2026-07-02 (with F1):** both use UTF-8; the search/replace functions' size computations were also switched from `Encoding.Default` to UTF-8.

### [~] F21. `SetOpenTarget` ignores `WriteString` result
**Where:** AppDesignerProcess.cs:340
If the write ever fails, the message is still sent with whatever the buffer previously contained → opens a stale target. Check the return value.
**Fixed 2026-07-02 (with F1's locking pass):** return value checked; sequence locked.

### [~] F23. `ReplaceAll` reused a stale search term and embedded NUL bytes (found during F1 pass)
**Where:** ScintillaManager.cs `ReplaceAll`
**Mechanism:** (a) the shared `searchBuffer` was written sequentially without `Reset()`, so the second Replace All in a session wrote at a nonzero offset while `SCI_SEARCHINTARGET` still pointed at offset 0 — searching for the *previous* term; (b) the replacement length passed to `SCI_REPLACETARGET` included the null terminator, embedding a `\0` character in the document on every replacement.
**Fixed 2026-07-02 (needs manual verification with Better Find's Replace All):** `Reset()` added; length is now the byte count without the terminator.

### [~] F24. Excessive full-document reads (GetScintillaText call reduction)
**Survey (2026-07-02):** 38 call sites; hot paths did multiple redundant full cross-process document copies per event — typing pause ≈3 reads (pre-read + GetParsedProgram hash check + styler staleness check), autocomplete trigger ≈2 reads + a fresh full parse/type-inference bypassing the parse cache, function call tips a full read+parse per `(`/`,` keystroke, and `GetSelectedText` copied the whole document to extract a selection.

**Tier 1 — mechanical (fixed 2026-07-02, needs manual verification):**
- New `ScintillaManager.ReadDocumentRange(editor, start, length)`: direct `SCI_GETRANGEPOINTER` + `ReadProcessMemory`, no remote allocation, no `RemoteBuffer` wrapper (also removed the F6 finalizer hazard from `GetLineText`/`GetSelectedLines`).
- `CalculateLengthEntered` reads only the current line up to the cursor (trigger + prefix cannot span lines).
- `GetSelectedText` reads only the selection range (also fixes its byte-vs-char `Substring` bug, F4 family).
- `ShowAppPackageSuggestions` reads only the current line.
- `AR_TYPING_PAUSE` pre-read removed (redundant with GetParsedProgram's own read).
- Dead `AutoCompleteLengthEntered` property removed (write-only since F3).

**Tier 2 — fixed 2026-07-02 (needs manual verification):** the hook now posts `WM_AR_DOC_MODIFIED` (packed source HWND) on every `SC_MOD_INSERTTEXT`/`SC_MOD_DELETETEXT` — including AppRefiner's own remote edits, since the subclassed parent sees all `WM_NOTIFY`. MainForm flips `ContentDirty` and bumps `ContentVersion` on the source editor (resolving WITHOUT initializing unknown editors). `GetParsedProgram`/`WithTokens` return the cached parse with **zero cross-process reads** when clean; StylerManager's post-styling staleness check compares `ContentVersion` instead of re-reading the full document. Old-hook-DLL caveat: no modified messages → caches never invalidate → stale everything; covered by the mandatory "restart open App Designers" release note (F3/F5). The AST-sharing policy question below still stands for deduplicating the suggest handlers' fresh parses.

**Original Tier 2 plan (for reference):** per-editor content dirty flag. Hook posts a cheap "document modified" ping per editor; `ContentString` becomes a trusted cache; `GetParsedProgram`'s hash check and StylerManager's staleness check become flag comparisons (zero-copy on unchanged documents). Requires F5's source-HWND routing to attribute modifications to the right editor. Part of this design: decide the AST-sharing policy — the suggest/calltip handlers currently do a fresh parse+type-inference per event *on purpose-adjacent grounds*: routing them through the cached `GetParsedProgram` today would share one `ProgramNode` between UI-thread handlers and the styler background thread, which mutates it (type-inference attributes) — a new race. Fix the sharing policy (lock, or immutable snapshots) before deduplicating the parses.

### [~] F25. Stale styler indicators linger after edits (positions drift)
**Reported during validation (2026-07-02):** parse/type-error indicators painted mid-typing were not cleaned up once the code became valid again; leftover ranges sat at wrong byte offsets until a save/force-refresh.
**Mechanism:** `StylerManager.ApplyIndicators` diffed old vs new indicator sets and removed stale ones via `SCI_INDICATORCLEARRANGE` at the *recorded* Start/Length — but Scintilla shifts painted indicator ranges as the document is edited while the recorded positions do not move, so the clear hit the wrong range and the drifted paint lingered.
**Fixed 2026-07-02 (needs manual verification):** each styler pass now clears the FULL document range for every indicator number involved (old set + new set + search/bookmark indicators — a few messages per distinct color) and repaints the fresh set, exactly what force-refresh did. New helper `ScintillaManager.ClearIndicatorNumbers`. Known residual (pre-existing): Better Find marks and bookmarks are re-painted at their recorded positions, which drift the same way.
**Follow-up 2026-07-02 (reported during validation):** the first fix only covered `ApplyIndicators`, but `CheckForContentChanges` calls `ResetStyles` → `ClearAllIndicators` *before* each styler pass, and that too cleared by recorded Start/Length — so a drifted squiggle (e.g. an "undefined variable" mark surviving after the variable is declared on a new line above) was cleared at the wrong range and lingered until save. `ClearAllIndicators` now routes through `ClearIndicatorNumbers` (full-range clear) as well. Possible secondary factor not changed: the 1000 ms styler-processing throttle in `CheckForContentChanges` can drop a trailing pass, though consecutive typing-pauses are usually >1 s apart; revisit only if stickiness persists after this fix.

### [~] F26. `ClearAllIndicators` also cleared by drifted recorded ranges (F25 sibling)
**Reported during validation (2026-07-02):** an "undefined variable" squiggle survived after the variable was declared on a new line above; it only cleared on save.
**Mechanism:** the F25 fix corrected `ApplyIndicators`, but the synchronous `ResetStyles` → `ClearAllIndicators` path (run in `CheckForContentChanges` *before* the async styler pass) still cleared each indicator at its recorded `Start`/`Length`. Text inserted above drifts the painted range while the recorded position does not move, so the clear missed the drifted paint — and then emptied `ActiveIndicators`, so the later `ApplyIndicators` had no record left to full-clear. The orphaned squiggle survived until a pass where offsets happened to match.
**Fixed 2026-07-02 (needs manual verification):** `ClearAllIndicators` now clears the full document range per distinct indicator number via `ClearIndicatorNumbers` (same primitive as F25). `RemoveIndicator` intentionally still clears by recorded range — it removes one specific indicator and is only called from the search/bookmark paths at fresh offsets.

### [~] F22. Startup ordering: sent messages vs posted focus updates (design note)
Sent hook messages are serviced ahead of the posted WinEvent-driven `activeEditor` updates (see F5). Any fix for F5 should also make focus updates synchronous with respect to notification handling, or make handlers resolve identity per-message so ordering no longer matters.
**Resolved 2026-07-02 by the F5 fix:** handlers resolve identity per-message, so sent-vs-posted ordering no longer affects correctness of notification routing. `activeEditor` remains focus-derived but is now only used for focus-semantic features (keyboard shortcuts, command palette, dialogs).

---

## Suggested fix order

1. **F3 + F4** — small, testable, directly fixes the `&varvariable` class of bugs.
2. **F1 + F2 + F11** — one synchronization design for `MemoryManager`/shared buffers covers all three.
3. **F5 (+F22)** — hook protocol change to carry source HWND; biggest structural change, fixes wrong-editor/wrong-process routing.
4. **F6** — finalizer/ownership fix; small and removes a random-corruption source.
5. **F14** — auto-connect on first activation (agreed design above).
6. **F7 + F8** — lifecycle eviction (pairs well with the Instances-tab work).
7. Remaining F9–F21 opportunistically as their files are touched.
