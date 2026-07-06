# Ctrl+Space Function-Name Autosuggest — Design

**Date:** 2026-07-03
**Status:** Draft — awaiting Tim's review. No code has been changed.

## Goal

Pressing Ctrl+Space with the caret on/after a plain identifier (no `&`, `%`, `.`, or `:` prefix) shows an autocomplete list of **function names callable at that position**:

- User-defined functions in the current program, honoring PeopleCode's declare-before-use rule (an implementation is only visible if it appears textually above the cursor; a `Declare Function` is visible program-wide).
- Built-in global functions (`Len`, `Find`, `MsgGet`, `CreateRecord`, `SQLExec`, …).

Example: with `Function DoAThing() ... End-Function;` above, typing `DoA` then Ctrl+Space offers `DoAThing`.

Prefixed identifiers are out of scope — `&`/`%`/`.`/`:` already have their own trigger paths, and Ctrl+Space already routes those contexts correctly.

## Key research findings (what makes this cheap)

1. **Ctrl+Space is already fully wired, end to end.** `InvokeAutocompleteCommand` (`AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`) owns the Ctrl+Space shortcut. The C++ `WH_KEYBOARD` hook (`AppRefinerHook/HookManager.cpp:1110-1137`) already intercepts Ctrl+Space in the App Designer process, suppresses the keystroke (`return 1`, so no space is inserted), and forwards `WM_AR_KEY_COMBINATION` to AppRefiner, which dispatches to the command. **No C++ changes are required.**

2. **The exact gap is one branch in the command.** `DetectAutocompleteContext` (`InvokeAutocompleteCommand.cs:102-126`) scans backward from the cursor through identifier chars looking for a trigger char (`%`, `&`, `.`, `:`, `(`, `,`). When it hits a non-trigger boundary — the plain-identifier case — it returns `null`, and `Execute` falls back to **variable suggestions** (line 60-64), which is useless there (variables all start with `&`). That fallback branch is where the new behavior goes.

3. **Built-in functions are enumerable with full signatures.** Global builtins are modeled as methods of the synthetic `"System"` object in the embedded `typeinfo.dat` catalog:
   `PeopleCodeTypeDatabase.GetObject("System").GetAllMethods()` → `IEnumerable<FunctionInfo>` with `.Name` populated from the embedded name table, plus parameters, overloads, and return types. This is the same API `ShowObjectMembers` already uses for builtin object members (`AutoCompleteService.cs:517`) and `ShowSystemVariables` uses for `%`-variables (`GetAllProperties()`, line 732) — so runtime enumeration is proven. There is no `GetAllFunctions()` facade method; the `"System"` object route is the enumeration path.

4. **The declare-before-use scope rule is already implemented.** `UndeclaredFunctionStyler` (`AppRefiner/Stylers/UndeclaredFunctionStyler.cs:33-99`) encodes exactly the visibility judgment we need:
   - `ProgramNode.Functions` holds both declares and implementations in textual order (`FunctionNode.IsDeclaration` / `.IsImplementation`, computed from `Body == null`).
   - Declares are visible everywhere; implementations only when `fn.SourceSpan.Start.ByteIndex < useSiteByteIndex`; first-implementation-wins dedup, case-insensitive.
   Substituting the cursor position for the call-site position gives "functions visible at cursor".

5. **A fresh AST is cheap at keystroke time.** `editor.GetParsedProgram()` (`ScintillaEditor.cs:362-422`) is hash-cached and invalidated by the hook's `WM_AR_DOC_MODIFIED`, so it re-parses only when content actually changed. No type inference is needed for this feature — just the parse.

## Design

### Approach chosen: extend the existing Ctrl+Space pipeline with a new autocomplete context

Pure C# change following the established "new trigger type" recipe. Alternatives considered:

- **B — auto-trigger while typing letters** (hook forwards alphabetic `SCN_CHARADDED`): rejected. Requires C++ changes, fires constantly (every word typed), needs debouncing and keyword suppression, and isn't what was asked for. Ctrl+Space-only keeps it deliberate.
- **C — separate command/shortcut**: rejected. Ctrl+Space is the natural key and is single-owner (`ApplicationKeyboardService` rejects duplicate registrations), and the existing command's `null`-context fallback is precisely the plain-identifier case. A second shortcut would fragment the UX.

### Components

**1. New context + message ID**
- `AutoCompleteContext.FunctionNames` added to the enum (`ScintillaEditor.cs:318`).
- `AR_FUNCTION_SUGGEST = 2524` in `MainForm.cs` (next free ID; 2500–2523 are taken) and mirrored in `InvokeAutocompleteCommand`. Add a comment line in `AppRefinerHook/Common.h` for catalog parity, but no C++ code — this message has no C++ producer; it flows only `InvokeAutocompleteCommand → mainForm.ProcessMessage`.

**2. Context detection — `InvokeAutocompleteCommand.DetectAutocompleteContext`**
When the backward scan terminates at a non-trigger boundary or document start, count the identifier chars traversed. If ≥ 1 and the first is a letter (PeopleCode identifiers can't start with a digit), return `(AutoCompleteContext.FunctionNames, identifierStart, '\0')`; otherwise return `null` as today. `Execute` maps `FunctionNames → AR_FUNCTION_SUGGEST`. The existing `null → AR_VARIABLE_SUGGEST` default is preserved for the no-prefix case (see Open Questions).

**3. MainForm handler**
New `else if (m.Msg == AR_FUNCTION_SUGGEST)` branch in `WndProc` (alongside 2441–3013): resolve editor via `ResolveEditorFromHwnd(UnpackHwnd(...))`, then `autoCompleteService.ShowFunctionSuggestions(editor, position)`. No `AutoSuggestSettings` gate — this fires only on an explicit user action, unlike the char-typed triggers (see Open Questions).

**4. Shared visibility helper (refactor)**
Extract the visibility rule from `UndeclaredFunctionStyler` into a reusable helper, e.g. `FunctionVisibility.GetVisibleFunctions(ProgramNode program, int byteIndex)`:
- all `IsDeclaration` nodes, plus `IsImplementation` nodes with `SourceSpan.Start.ByteIndex < byteIndex`;
- dedup by name (`OrdinalIgnoreCase`), declaration/first-implementation wins.
The styler is updated to consume the helper so the rule lives in one place.

**5. `AutoCompleteService.ShowFunctionSuggestions(editor, position)`**
- `var program = editor.GetParsedProgram();` — no type inference needed.
- User functions: `FunctionVisibility.GetVisibleFunctions(program, position)` (position from Scintilla is already a UTF-8 byte index, matching `SourceSpan.Start.ByteIndex`).
- Builtins: `PeopleCodeTypeDatabase.GetObject("System").GetAllMethods()` filtered to non-empty `.Name`, sorted, **cached in a static** after first build (the catalog is immutable).
- Merge with user functions shadowing same-named builtins (matches `TypeInferenceVisitor.ResolveFunctionInfo`, which checks user functions before the builtin fallback).
- Entries formatted `"Name?<icon>"`: user functions → `AutoCompleteIcons.ExternalFunction`, builtins → `AutoCompleteIcons.ClassMethod` (both already registered; no new art). Ordering per Open Question #2.
- `ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.FunctionNames, position, list, ...)`.

**6. `ScintillaManager` plumbing (two switch cases)**
- `CalculateLengthEntered` (line 2034): the existing logic requires a trigger char and returns 0 otherwise. Add a `FunctionNames` branch that scans the line bytes backward counting identifier bytes (`[A-Za-z0-9_]`) until the first non-identifier byte — that count is `lengthEntered`, so Scintilla pre-filters to the typed prefix (`DoA` → selects `DoAThing`) and the selection handler knows how much prefix to delete.
- `SetAutoCompleteContextCharacters` (line 2116): `FunctionNames => "(\t;"` — same as ObjectMembers, so typing `(` accepts the entry and the existing `SCN_CHARADDED` `'('` path fires `AR_FUNCTION_CALL_TIP`, chaining straight into the signature call tip (which already resolves both user and builtin functions).

**7. Selection handling**
- New `UserListType.FunctionNames` (`AutoCompleteService.cs:168`).
- Map `AutoCompleteContext.FunctionNames → UserListType.FunctionNames` in the `SCN_AUTOCSELECTION` handler switch (`MainForm.cs:2383`).
- `HandleFunctionNamesSelection`: insert the bare function name (the prefix has already been deleted by the shared `SCN_AUTOCSELECTION` code at `MainForm.cs:2377`). No parentheses inserted — consistent with `HandleObjectMemberListSelection`, which strips `()`; the user types `(` and gets the call tip. (See Open Question #3.)

### Data flow

```
Ctrl+Space in App Designer editor
  → WH_KEYBOARD hook intercepts + suppresses (existing)
  → WM_AR_KEY_COMBINATION → ApplicationKeyboardService (existing)
  → InvokeAutocompleteCommand.Execute
      → DetectAutocompleteContext ⇒ FunctionNames   [NEW branch]
      → mainForm.ProcessMessage(AR_FUNCTION_SUGGEST, pos, PACK(hwnd,0))
  → MainForm.WndProc → ShowFunctionSuggestions      [NEW]
      → GetParsedProgram + FunctionVisibility + System builtins
      → ScintillaManager.ShowAutoComplete(FunctionNames, …)  → popup in App Designer
  → user picks → SCN_AUTOCSELECTION → prefix deleted → name inserted  [NEW list type]
  → user types '(' → existing call-tip path shows the signature
```

### Error handling

- No active editor / position ≤ 0 / empty text: existing guards in `Execute` already bail.
- Parse failures: `GetParsedProgram` returns the best-effort AST (error-recovering parser); if `null`, show builtins only, or nothing if that also fails — never throw across the message pump.
- Empty result list: don't call `ShowAutoComplete` (Scintilla shows an empty box otherwise).
- Same-name declare + implementation, forward references, name collisions with builtins: covered by the helper's dedup rules above.

### Known pre-existing caveat (not made worse, worth noting)

`DetectAutocompleteContext` indexes the C# `string` from `GetScintillaText` using a Scintilla **byte** position. For documents containing non-ASCII characters above the cursor, the scan can misalign. This is pre-existing behavior for all Ctrl+Space contexts; the new branch inherits it. A cleanup (scan bytes like `CalculateLengthEntered` does, or convert indexes) could ride along or be a separate fix — flagging for Tim's call.

### Out of scope / future ideas

- **Cross-program functions with auto-Declare (phase 2):** `FunctionCacheManager.SearchFunctionCache` already finds function implementations across the whole PeopleSoft DB, and the `DeclareFunction` refactor already inserts a correctly-placed `Declare Function` line (this pairing powers today's undeclared-function quick fix). A later enhancement could append cache hits to the Ctrl+Space list (distinct icon) and, on selection, insert both the name and the declare. Deliberately excluded from v1 to keep the list fast and local.
- Auto-trigger on typing letters (no Ctrl+Space) — rejected above.
- Method-name suggestions inside app classes (`%This.` members) — already covered by the ObjectMembers path after typing `%This.`.
- Keyword completion (`If`, `Local`, …) — different feature.

## Open questions for Tim (recommendations inline)

1. **Empty prefix:** Ctrl+Space after whitespace (no identifier typed) currently shows variable suggestions. Keep that default (recommended — preserves existing behavior; functions appear only when you've started typing a name), or show functions/a merged list instead?
2. **Ordering:** user functions floated above builtins via custom order (recommended — most relevant first; precedent: type-matched variables are floated in `ShowVariableSuggestions`), or a single alphabetical merge relying on icons to distinguish?
3. **Insertion:** bare name (recommended — consistent with ObjectMembers; `(` fillup chains to the call tip), or auto-append `()` for zero-arg functions since we know the signatures?
4. **Settings flag:** add a `FunctionSuggestions` toggle to `AutoSuggestSettings`? Recommended: no — the trigger is always an explicit keypress, unlike the char-typed suggestions the existing flags gate.

## Touch points summary (all C#, no C++ code changes)

| File | Change |
|---|---|
| `AppRefiner/ScintillaEditor.cs` | `AutoCompleteContext.FunctionNames` enum value |
| `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs` | boundary case in `DetectAutocompleteContext`; message-ID map; `AR_FUNCTION_SUGGEST` const |
| `AppRefiner/MainForm.cs` | `AR_FUNCTION_SUGGEST` const + `WndProc` handler + `SCN_AUTOCSELECTION` context→listType mapping |
| `AppRefiner/AutoCompleteService.cs` | `ShowFunctionSuggestions`, `UserListType.FunctionNames`, `HandleFunctionNamesSelection`, static builtin-list cache |
| `AppRefiner/ScintillaManager.cs` | `CalculateLengthEntered` + `SetAutoCompleteContextCharacters` cases |
| New helper (e.g. `AppRefiner/FunctionVisibility.cs`) | extracted visible-functions rule |
| `AppRefiner/Stylers/UndeclaredFunctionStyler.cs` | consume the shared helper |
| `AppRefinerHook/Common.h` | comment-only catalog entry for 2524 |

## Manual test plan

1. `Function DoAThing() ... End-Function;` above cursor, type `DoA` + Ctrl+Space → list shows `DoAThing` (user-function icon), pre-filtered/selected; Enter inserts `DoAThing` (typed prefix not duplicated).
2. Function implementation **below** the cursor → not offered; add a `Declare Function` for it at top → offered.
3. `Le` + Ctrl+Space → builtin `Len` (and other `Le…` builtins) offered; select, type `(` → call tip with `Len` signature appears.
4. User function named same as a builtin → appears once, user-function icon.
5. Ctrl+Space after `&`, `%`, `.`, `:` contexts → existing behaviors unchanged; Ctrl+Space on whitespace → existing variable-suggestion default unchanged.
6. Accept an entry by typing `(` (fillup) → name inserted + call tip fires.
7. Escape closes the popup; no stray space character ever appears in the document.
8. Program with a syntax error below the cursor → list still shows (error-recovered AST).
