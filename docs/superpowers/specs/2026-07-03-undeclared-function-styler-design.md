# Undeclared Function Styler + Quick Fixes — Design

Date: 2026-07-03
Status: Approved by Tim (design conversation, 2026-07-03)

## Overview

A new styler that red-squiggles calls to functions PeopleCode cannot resolve at
that point in the program, with quick fixes (Ctrl+.) that either insert the
missing `Declare Function` (from the local function cache or via the existing
Declare Function search dialog, pre-filled) or — for local functions defined
*below* their first use — move the implementation above the caller.

## Detection rules

New styler `AppRefiner/Stylers/UndeclaredFunctionStyler.cs` (`BaseStyler`,
auto-discovered, **enabled by default** — `Active = true` initializer, still
toggleable via the existing styler settings grid, `DatabaseRequirement =
Optional`). Detection uses no live database.

A `FunctionCallNode` is examined only when its callee is a bare
`IdentifierNode` (method calls, `create PKG:CLS()` / `create ClassName()`
object creations, and `%This.X()` never match by construction). All name
comparisons are case-insensitive.

For a call to name `X` at byte position `P`:

1. **Valid** if any `node.Functions` entry with `IsDeclaration` has name `X`.
   (PeopleCode requires declares above implementations and executable code, so
   existence implies visibility — no position check.)
2. **Valid** if any `node.Functions` entry with `IsImplementation` has name `X`
   and its `SourceSpan.Start` is **before** `P`. (PeopleCode is single-pass;
   forward references to later implementations are compile errors.)
3. **Valid** if `PeopleCodeTypeDatabase.GetFunction(X)` returns non-null
   (builtin function; bundled data, no DB needed).
4. Otherwise, if an `IsImplementation` entry named `X` exists but starts
   **after** `P`: flag as **defined-below** — squiggle over the identifier
   span, tooltip `Function 'X' is defined below its first use`.
5. Otherwise: flag as **unknown** — squiggle over the identifier span,
   tooltip `Function 'X' is not declared or defined`.

The known-names data is collected once per pass in `VisitProgram` from
`node.Functions` (names + declaration/implementation kind + start positions).
The squiggle appears regardless of DB connection state.

## Quick fixes

Both flavors use `AddIndicatorWithDeferredQuickFix` so options are computed at
Ctrl+. time and reflect the current cache/connection state.

### Unknown function (case 5)

The deferred resolver builds, in order:

1. **Import entries** — `functionCacheManager.SearchFunctionCache(process, X)`
   filtered to exact `FunctionName` matches (case-insensitive), capped at 10.
   One entry per match: description `Import 'X' from REC.FIELD (Event)`
   (fields from `FunctionPath`, format `REC:FIELD:EVENT`), refactor type
   `DeclareFunctionQuickFix`, context = the `FunctionSearchResult` itself.
   The cache is local SQLite keyed by `DBName` (known from the window title),
   so these entries work **without a live DB connection** — including the
   "connected last week, not today" case.
2. **Search entry** — `Search for function 'X'...`, offered **only when
   `editor.DataManager` is connected** (the dialog requires a connection).
   Selecting it opens the Declare Function dialog pre-filled with `X`.

If neither yields entries (disconnected and no cache hits), the resolver
returns an empty list and no quick-fix popup appears (existing behavior);
the squiggle still shows.

### Defined-below (case 4)

Single entry: `Move Function 'X' above 'Caller'` (Caller = name of the
function containing the call; if the call is in main/event code, wording
falls back to `Move Function 'X' above this statement`). Refactor type
`MoveFunctionAbove`, context = `X`. Import/search entries are **not** offered
— the function is known to be local.

## Quick-fix payload extension (no re-query on click)

`Indicator.QuickFixes` and the deferred-resolver return type change from
`List<(Type RefactorClass, string Description)>` to
`List<(Type RefactorClass, string Description, object? Context)>`.

`AutoCompleteService.HandleQuickFixSelection` sets
`editor.QuickFixContext = entry.Context ?? ExtractQuickFixContext(description)`
— existing stylers (`UnimportedClassStyler`,
`AmbiguousClassReferenceStyler`) pass `null` context and keep today's
description-string behavior; call sites are updated mechanically for the new
tuple shape.

This lets the resolver attach the full `FunctionSearchResult` to each import
entry, so selecting one performs **no second cache query and no description
parsing**.

## New/changed refactors

### `Refactors/QuickFixes/DeclareFunctionQuickFix.cs` (new, hidden)

Constructor reads `editor.QuickFixContext as FunctionSearchResult` (throws if
absent, mirroring `AddImportQuickFix`), then delegates to the existing
`DeclareFunction` refactor with example-call insertion suppressed, copying
its edits (the `AddImportQuickFix` wrapper pattern).

### `Refactors/MoveFunctionAbove.cs` (new, hidden)

Constructor takes the function name (from `QuickFixContext`). On
`VisitProgram` (fresh AST — never reuses styler-time nodes):

- Locate the implementation `FunctionNode` named `X`.
- Locate the destination: the function containing `CurrentPosition`, else the
  first statement of main code containing it.
- Two edits: delete X's block — from the start of its leading comments to the
  end of `End-Function;` including the trailing line break — and insert that
  exact text immediately above the destination block's leading comments (or
  the block itself if none), followed by a blank line.
- If X or the destination cannot be found (code changed since the squiggle),
  produce no edits and report failure via the refactor's normal status
  mechanism.

### `Refactors/DeclareFunction.cs` (modified)

New optional constructor parameter `insertExampleCall` (default `true`). When
`false`, the refactor only inserts the `Declare Function …;` line and never
touches the cursor line. The command-palette flow keeps `true`; both
quick-fix paths pass `false`.

## Dialog and MainForm changes

- `DeclareFunctionDialog`: optional constructor parameter
  `string? initialSearchTerm`. When provided, seeds `searchBox.Text` so the
  existing debounce/search machinery runs the initial query.
- `MainForm.ShowDeclareFunctionDialog`: optional parameters
  `string? initialSearchTerm = null, bool insertExampleCall = true`, passed
  through to the dialog and to the `DeclareFunction` construction.
- `AutoCompleteService.HandleQuickFixSelection`: before the generic
  `Activator.CreateInstance` path, a special case for the search entry. The
  entry is registered with a sentinel refactor type (`typeof(MainForm)` is not
  used — a dedicated marker type `OpenDeclareFunctionDialogQuickFix` with no
  executable body keeps the tuple shape honest) and its context payload is the
  function name `X`. The handler recognizes the marker type, `BeginInvoke`s
  `mainForm.ShowDeclareFunctionDialog(initialSearchTerm: X,
  insertExampleCall: false)`, and returns `null` (no refactor to execute).

## Files touched

| File | Change |
|---|---|
| `Stylers/UndeclaredFunctionStyler.cs` | new |
| `Refactors/QuickFixes/DeclareFunctionQuickFix.cs` | new |
| `Refactors/MoveFunctionAbove.cs` | new |
| `Refactors/DeclareFunction.cs` | `insertExampleCall` option |
| `Stylers/BaseStyler.cs` | quick-fix tuple gains `object? Context` |
| `AutoCompleteService.cs` | context-aware selection + search-entry routing |
| `Dialogs/DeclareFunctionDialog.cs` | `initialSearchTerm` |
| `MainForm.cs` | `ShowDeclareFunctionDialog` optional params |
| `Stylers/UnimportedClassStyler.cs`, `Stylers/AmbiguousClassReferenceStyler.cs`, `TooltipProviders/ActiveIndicatorsTooltipProvider.cs` | mechanical tuple-shape updates |

## Edge cases

- Case-insensitivity everywhere (PeopleCode identifiers).
- Parse failure → stylers don't run (existing behavior).
- Same name declared *and* implemented locally → valid via rule 1.
- Call inside an app class program: bare-identifier calls still validated the
  same way (declares are legal in app class programs; bare method calls are
  not legal PeopleCode, so no false positives from methods).
- Multiple cache rows for the same name → one import entry each (≤10).
- Cache row selected but stale: `DeclareFunctionQuickFix` uses the captured
  `FunctionSearchResult` — no re-query, so staleness only matters if the
  declaration text itself is wrong, same risk as the dialog flow today.

## Manual test matrix

1. `&x = DoAThing()` with nothing declared, DB connected, cache populated →
   squiggle; Ctrl+. shows import entries + search entry; import inserts
   correct `Declare Function DoAThing PeopleCode REC.FIELD Event;` in the
   declare block, **no example call inserted**, call site untouched.
2. Same, DB disconnected but cache populated → import entries only.
3. Same, no cache data and disconnected → squiggle only, no popup.
4. Search entry → dialog opens pre-filled with the function name; picking a
   result declares it without inserting an example call.
5. Builtin (`CreateRecord(...)`) → no squiggle.
6. Already-declared function → no squiggle.
7. Locally defined function called *after* its implementation → no squiggle.
8. Forward reference (call in `Function A` to `Function B` defined below) →
   defined-below squiggle; Move quick fix relocates B (with its comments)
   above A; program compiles on save.
9. `create IS_CO_BASE:JSON:JsonObject()` and `create JsonObject()` → no
   squiggle from this styler.
10. Existing quick fixes (unimported class, ambiguous class) still work after
    the tuple change.
