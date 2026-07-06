# Cross-Process Audit — Manual Test Script

**Companion to:** `2026-07-02-cross-process-audit-findings.md` (F1–F24)
**Purpose:** one batch verification session in an environment with App Designer, covering every fix from the audit. Scenarios are ordered so later ones build on earlier setup.

## Setup (required)

1. Build the full package: `.\build.ps1` (must include the rebuilt `AppRefinerHook.dll` and, if used, `scintilla_mods`).
2. **Close every running App Designer.** The new hook protocol (source-HWND packing, doc-modified notifications) requires the new DLL — an old in-process DLL sends zero HWNDs and its notifications are silently dropped. This is also the release-note text: *"Please restart any App Designers that were open prior to the update."*
3. Have the AppRefiner Debug Console open — several checks reference specific log lines.
4. Ideal environment: **two or three App Designer instances**, at least two against different databases, auto-connect ("Prompt for DB Connection on Attach") **enabled**.

---

## A. Startup auto-connect (F14)

With AppRefiner **not** running:
1. Open 3 App Designers. Start AppRefiner (AppRefiner has focus).
   - ☐ **No DB connect dialogs appear anywhere.** AppRefiner UI stays responsive (previously: invisible modal dialogs, AppRefiner appeared hung).
   - ☐ Debug log shows `MaybePromptForDbConnection: process <pid> is not foreground, deferring prompt` (up to once per discovered AD).
2. Focus App Designer #1 (click its window or an editor).
   - ☐ DB connect dialog appears, **visible**, centered on/owned by that App Designer, pre-filled with its DB name. Connect.
3. Focus App Designer #2.
   - ☐ Dialog appears for #2. **Cancel** it.
   - ☐ Refocus #2 / switch away and back — **no re-prompt** (Cancel set DoNotPromptForDB).
4. Focus App Designer #3 → dialog → Connect.
5. Close AppRefiner. With App Designer #1 still focused, start AppRefiner again.
   - ☐ Dialog for #1 appears on its own (~1 s after startup, from the DBName capture) since #1 is foreground. Others prompt on first focus, as above.

## B. Autocomplete acceptance (F3, F4)

In a PeopleCode editor with some local variables declared (e.g. `Local string &variable;`):
1. Type `&var`, wait for the list, accept the `&variable` entry (Tab or double-click).
   - ☐ Result is exactly `&variable` — no `&varvariable`, no `&var&variable`.
   - ☐ Debug log: `Autocomplete selected: ... lengthEntered: 4` then `Deleted 4 characters from position ...`.
2. **The smart-quote repro (F4):** put `’` (a smart quote / any non-ASCII char) in a comment *above* the cursor. Repeat step 1.
   - ☐ Still exactly `&variable` (previously this doubled deterministically).
3. Type `&va`, let the list open, type two more filtering characters, then accept.
   - ☐ The whole typed run is replaced correctly (covers characters typed while the list was open).
4. `%` system variables: type `%date` and accept `%DateTime`-style entry → ☐ single clean insertion.
5. `.` object members on a typed variable: accept a **method** → ☐ inserted, and ☐ the function call tip appears (~100 ms later). Accept a **property on your own class** from the `&` variable list (`(Property)` suffix) → ☐ inserts `%This.PropertyName`.
6. `:` app package drill-down: type a package root + `:` → accept a subpackage → list re-triggers; accept a class inside an `import` statement → ☐ `;` auto-appended; accept a class in a variable declaration → ☐ no `;`.
7. Ctrl+Space (manual invoke) in each of the above contexts → ☐ same behavior (exercises the C#-side packed-message path).

## C. Multi-instance routing (F5, F22)

With two App Designers, one editor open in each:
1. Alt+Tab from AD#1's editor to AD#2's editor and **immediately** (as fast as possible) type `&x`.
   - ☐ Suggestions appear in AD#2's editor; accepting inserts into AD#2's editor. Repeat several times both directions — text must never land in (or be computed from) the other editor.
2. Paste a SQL string (>3 chars) inside a string literal in AD#2 right after switching from AD#1.
   - ☐ SQL formatting applies to the pasted text in AD#2 (exercises the AR_INSERT_CHECK sender-process pointer fix — previously this read a pointer from the wrong process).
3. Hover (dwell) over an identifier in AD#1, then quickly switch and hover in AD#2 → ☐ tooltips always describe the editor you're hovering in.
4. Save (Ctrl+S) in AD#1's editor and immediately focus AD#2 → ☐ the snapshot recorded belongs to AD#1's program (savepoint routed to source editor). Check via snapshot history.
5. Watch the debug log during all of this: `ResolveEditorFromHwnd: ... untracked process` / `invalid source window` lines should only appear for windows that were genuinely closed.

## D. Shared buffers under load (F1, F2, F11)

1. Open a large PeopleCode program. Type in bursts (trigger stylers/typing-pause processing) while also invoking autocomplete triggers.
   - ☐ Suggestions always reflect the *current* document (no stale/foreign content).
   - ☐ Lint annotations, call tips, and inlay hints never show garbled/truncated text.
2. Run a full Lint (annotations) while typing in a second editor of the **same** App Designer → ☐ annotations attach to the right lines with the right text.

## E. Better Find (F23, F11)

1. Open Better Find (Ctrl+Alt+F). Replace All `foo` → `bar`. Then, without closing App Designer, Replace All `baz` → `qux`.
   - ☐ The **second** Replace All actually searches for `baz` (previously it silently re-searched for `foo`).
2. After a Replace All, save and reopen the program (or scroll through it).
   - ☐ No garbage/invisible characters at replacement sites (previously each replacement embedded a `\0` byte).
3. Count and Mark All still report/highlight correctly.

## F. Lifecycle (F7, F8)

1. Close an editor window inside App Designer, then open a different program that reuses the window slot.
   - ☐ Debug log: `Editor window 0x... destroyed — evicting tracked editor`.
   - ☐ The reopened editor gets correct caption/type/behavior (no stale state).
2. Exit one App Designer completely (with AppRefiner running).
   - ☐ Debug log: `App Designer process <pid> exited — cleaning up tracking state`.
   - ☐ The Instances view (if open) drops it.
3. Launch a brand-new App Designer.
   - ☐ It's adopted fully: "AppRefiner Connected!" in Results, editors get folding/styling, autocomplete works. (Previously a recycled PID could be treated as already-tracked → dead handle → everything silently inert.)

## G. Content dirty tracking (F24 Tier 2)

1. Type, pause (stylers run), then leave the editor idle and hover for tooltips a few times **without editing**.
   - ☐ Tooltips/features still work, and the debug log does **not** print `Self-hosted parser - New content hash:` for the idle accesses (fast path, no re-read).
2. Make an edit via **undo** (Ctrl+Z) and via **paste**, then hover/lint.
   - ☐ Results reflect the changed content (dirty flag fires for non-typing modifications too).
3. Trigger an AppRefiner-driven edit (e.g. type `&str |= "x";`-style concat shorthand, or run a refactor).
   - ☐ Subsequent tooltips/stylers see the post-edit content (our own remote edits mark dirty).

## G2. Stale indicator cleanup (F25)

1. In a PeopleCode editor, type until syntax/type errors are flagged mid-statement (squiggles appear), then finish the statement so the code is valid again and pause.
   - ☐ All error squiggles/highlights disappear on their own — no save or force-refresh needed.
2. Insert several lines *above* existing flagged errors, then fix the errors.
   - ☐ No leftover paint at the old (shifted) offsets.
3. (F26) On a line, write `&z = 3` so `&z` is flagged undefined. Move ABOVE it and add `Local integer &z;`, then pause.
   - ☐ The `&z` highlight clears on its own once declared — no save needed.

## H. Inlay hints (F19)

*(The copy-during-`SCI_SETINLAYINFO` assumption was verified from the scintilla_mods source on all three branches — hint text is copied into a `std::string`. This section is now a plain regression check.)*

1. Enable parameter names. Open two PeopleCode editors (A and B) with different function calls. Switch A↔B **10+ times**, letting hints render each time.
   - ☐ Hints always show the correct parameter names for the visible editor.
2. Long session with many switches → ☐ hints never permanently stop appearing (previously the shared buffer filled monotonically and hints died for the process lifetime).

## I. Old-Scintilla acceptance check (F12 — open verification item)

*(Verified from source for all scintilla_mods builds — cancel-during-notification is honored on 4.4.6/5.3.3/5.5.0. Only the **stock PSIDE Scintilla** case remains.)*

On the **oldest supported** App Designer (pre-8.61 / no Lexilla, without `scintilla_mods`):
1. Accept an autocomplete entry.
   - ☐ Single insertion. If the item text appears **twice**, the stock Scintilla there does not honor cancel-during-`SCN_AUTOCSELECTION` — F12 then needs hook-side suppression. Record the PeopleTools version either way and update F12 in the findings doc.

## J. Regression sweep (touched surfaces)

- ☐ Results-list "AppRefiner Connected!" message appears (results_text/item buffers under lock).
- ☐ Open-target navigation (double-click style open via stack trace navigator / go-to-definition) still opens the right definition (SetOpenTarget write-check).
- ☐ Theme icons render in autocomplete lists after switching themes.
- ☐ Minimap and Param Names context-menu toggles work (focus-scoped messages, intentionally unchanged).
- ☐ Better Find selection-scoped search works (GetSelectedText now range-reads).
- ☐ Command palette (Ctrl+Shift+P) and keyboard shortcuts work (AR_KEY_COMBINATION intentionally focus-scoped).
- ☐ Dark mode + annotations + folding init on a fresh editor.
- ☐ With `useEnhancedEditor`: enhanced Scintilla loads (`Callback: Scintilla.dll loaded successfully` in log).

## Sign-off

| Section | Findings covered | Pass |
|---|---|---|
| A | F14 | ☐ |
| B | F3, F4 | ☐ |
| C | F5, F22, (F16 indirectly) | ☐ |
| D | F1, F2, F11 | ☐ |
| E | F23, F11 | ☐ |
| F | F7, F8 | ☐ |
| G | F24 Tier 2 (+F18 indirectly) | ☐ |
| G2 | F25, F26 | ☐ |
| H | F19 | ☐ |
| I | F12 | ☐ |
| J | F6, F10, F13, F15, F20, F21, F24 Tier 1 | ☐ |

When all sections pass, flip the corresponding `[~]` entries to `[x]` in the findings catalog.
