# Message Catalog Integration — Design

**Date:** 2026-07-03
**Status:** Draft — awaiting Tim's review. No code has been changed.

## Goal

Give developers visibility into the PeopleSoft Message Catalog without leaving Application Designer. Today there is none: you open a browser, navigate to the Message Catalog component, and page through messages one at a time — a workflow break every time you read or write a `MsgGet`-family call. Three pain points, three features:

1. **Reading code:** hover over a call like `MsgGet(20001, 13, "...")` and see the actual catalog message, severity, and explain text.
2. **Writing code:** a searchable Message Catalog browser dialog that inserts the right `set, num` (and default text) at the cursor — reachable cold from the command palette, or via Ctrl+Space from inside a call's argument list.
3. **Creating a new entry:** AppRefiner is strictly read-only against the database, so it cannot create catalog rows — but it can show the free number ranges in a set and validate a number the user picks, so the only work left in the browser UI is typing the text.

## The function surface

Exhaustive list of built-in functions that take `message_set` / `message_num` (per Tim), with 1-based argument positions:

| Function | set arg | num arg | default-txt arg |
|---|---|---|---|
| `MsgGet` | 1 | 2 | 3 |
| `MsgGetText` | 1 | 2 | 3 |
| `MsgGetExplainText` | 1 | 2 | 3 |
| `CreateException` | 1 | 2 | 3 |
| `MessageBox` | 3 | 4 | 5 |
| `MsgBoxButtonOverride` | 4 | 5 | 6 |

Because the positions vary per function, the design centers on a **single shared map** consumed by every feature (tooltips, Ctrl+Space detection, insert logic) so the knowledge lives in one place.

## Key research findings (what makes this cheap)

1. **`IDataManager` is built for exactly this kind of addition.** It already exposes ~20 read-only metadata queries (`GetRecordFields`, `GetOpenTargets`, `GetSqlDefinition`, …) implemented by both `OraclePeopleSoftDataManager` and `SqlServerPeopleSoftDataManager`. The catalog lives in two small, stable tables: `PSMSGSETDEFN` (set number, description) and `PSMSGCATDEFN` (set, number, severity, message text, explain text in `DESCRLONG`).
2. **Tooltip providers already do DB-backed hover.** `PeopleSoftObjectTooltipProvider` declares `DataManagerRequirement.Required` and queries record metadata during `VisitMemberAccess`. A message-catalog provider is the same shape over `VisitFunctionCall`.
3. **Dialog patterns are established.** `BetterFindDialog` / `SmartOpenDialog` give the styling (dark header, light content), modal ownership over the App Designer main window (`DialogHelper.ModalDialogMouseHandler`, `WindowWrapper`), and searchable list/grid UX to copy.
4. **Ctrl+Space is single-owner and extensible.** `InvokeAutocompleteCommand` owns the shortcut; the function-autosuggest design (2026-07-03) documents the recipe for adding a new context branch with no C++ changes. This feature adds one more branch — one that opens a dialog instead of a Scintilla popup.
5. **AST argument positions are directly available.** `FunctionCallNode` exposes the function identifier and argument expressions with `SourceSpan`s, so "is the cursor in the set/num argument of a mapped function" and "which args are already typed" are simple AST checks, not text scanning.

## Design decisions already made with Tim

- **Scope:** tooltip provider + browser dialog (with new-message panel) + Ctrl+Space-opens-dialog. Native Scintilla popup autosuggest for message numbers is **out** (prefix filtering by digits is useless when the number is the unknown); the dialog's real text search wins.
- **Insert is context-aware** (see §5): inside an existing call it fills only the missing args; invoked cold it inserts a full call snippet.
- **Free ranges are informational, not prescriptive.** Users often deliberately skip the start of a gap (e.g. leave 48–59 as buffer and take 60). The UI shows ranges, pre-fills a suggestion, but the user types the final number and gets live free/taken validation.

## Components

### 1. Shared function map — `MessageCatalogFunctions`

A small static class (suggested home: `AppRefiner/MessageCatalogFunctions.cs`) holding the table above:

```csharp
public static class MessageCatalogFunctions
{
    public record ArgPositions(int SetArg, int NumArg, int DefaultTxtArg); // 0-based indices
    // Case-insensitive dictionary: "MsgGet" -> (0, 1, 2), "MessageBox" -> (2, 3, 4), etc.
    public static bool TryGet(string functionName, out ArgPositions positions);
}
```

Consumers: the tooltip provider, the Ctrl+Space context detection, and the dialog's insert logic. Adding a future function is a one-line change.

### 2. Data layer — `IDataManager` additions

New model in `AppRefiner/Database/Models/MessageCatalog.cs`:

```csharp
public class MessageSetInfo   { int SetNumber; string Description; }
public class MessageCatalogEntry
{
    int SetNumber; int MessageNumber;
    string Severity;      // decoded from MSG_SEVERITY: M=Message, W=Warning, E=Error, C=Cancel
    string MessageText;   // MESSAGE_TEXT
    string ExplainText;   // DESCRLONG
}
```

New `IDataManager` methods, implemented in both Oracle and SqlServer managers:

- `List<MessageSetInfo> GetMessageSets()` — `PSMSGSETDEFN`, ordered by set number.
- `List<MessageCatalogEntry> GetMessagesForSet(int setNumber)` — `PSMSGCATDEFN` rows for one set, ordered by message number. Loads text + explain in one query (per-set row counts are small enough that lazy explain loading isn't worth the complexity).
- `MessageCatalogEntry? GetMessage(int setNumber, int messageNumber)` — single-row lookup for tooltips.
- `List<MessageCatalogEntry> SearchMessages(string searchTerm, int? setNumber, int limit)` — case-insensitive `LIKE` against `MESSAGE_TEXT` and `DESCRLONG`, optional set filter, server-side row cap (suggest 200). Bind variables throughout, matching existing manager query style.

**Caching:** a `MessageCatalogCache` (keyed by the data manager instance, same lifetime pattern as `RecordFieldCache`) memoizes `GetMessageSets` and `GetMessagesForSet` results and single-message lookups (including negative lookups, so hovering a typo doesn't re-query on every mouse move). The dialog's Refresh button clears it. `SearchMessages` is not cached (always live).

### 3. Tooltip provider — `MessageCatalogTooltipProvider`

`AppRefiner/TooltipProviders/MessageCatalogTooltipProvider.cs`, extending `BaseTooltipProvider`:

- `DatabaseRequirement = DataManagerRequirement.Required`; inert without a connection.
- `VisitFunctionCall(FunctionCallNode node)`: if the call's function is an `IdentifierNode` found in `MessageCatalogFunctions` **and** both the set and num arguments are numeric literals, register a tooltip over the whole call span (hovering anywhere on the call works — friendlier than requiring the hover to hit the number itself). Variable or expression arguments → no tooltip.
- Tooltip content:

```
Message Catalog 20001/13 — MYAPP Messages
Severity: Error
"Vendor %1 not found in vendor master"
Explain: The vendor ID entered on the transaction does not exist… (truncated at ~500 chars)
```

- Entry not found → `No catalog entry for 20001/13` — doubling as typo detection.
- Lookups go through `MessageCatalogCache`, so repeated hovers are free.

### 4. Browser dialog — `MessageCatalogDialog`

`AppRefiner/Dialogs/MessageCatalogDialog.cs`, following the BetterFind styling and modal-ownership pattern.

**Layout:**

```
┌─ Message Catalog ────────────────────────────────────────────┐
│ [set filter……]        │ [search messages……]   [□ all sets]   │
│ ┌──────────────────┐  │ ┌──────────────────────────────────┐ │
│ │ 20001 MYAPP Msgs │  │ │ Num  Sev    Text                 │ │
│ │ 20002 MYAPP Batch│  │ │ 12   Error  Invalid date range   │ │
│ │ …                │  │ │ 13   Error  Vendor %1 not found  │ │
│ └──────────────────┘  │ └──────────────────────────────────┘ │
│                       │ ┌─ Preview ────────────────────────┐ │
│ [▸ New message…]      │ │ full text + explain text         │ │
│                       │ └──────────────────────────────────┘ │
│              [Refresh]      [Copy set, num]  [Insert]        │
└──────────────────────────────────────────────────────────────┘
```

- **Left pane:** message sets (number + description), filter box matches either. Selecting a set loads its messages (cached).
- **Right pane:** message grid (number, severity, text) for the selected set. The search box filters the loaded set client-side; ticking **all sets** switches to server-side `SearchMessages` across the catalog (results gain a Set column; row cap surfaced as "showing first 200").
- **Preview pane:** full message text and explain text for the selected row.
- **Keyboard:** Enter = Insert, Esc = close, arrows navigate the grid — same conventions as SmartOpen.

**Invocation modes:**
- **Cold** (command palette): full browse; Insert produces a complete call snippet (§5).
- **Insert mode** (Ctrl+Space inside a call): dialog receives the call-site context — function name, which mapped args are already present, and their values. If the set arg is already a typed literal, that set is pre-selected and the set list locked to it (with an explicit "unlock" affordance in case the typed set was the mistake).

### 5. Insert behavior — context-aware

Driven by the call-site context and the shared map:

- **Inside an existing call:** fill only the args the call still needs, using the map's positions. Examples:
  - `MsgGet(|)` → `MsgGet(20001, 13, "Vendor %1 not found")`
  - `MsgGet(20001, |)` → set locked; inserts `13, "Vendor %1 not found"`
  - `MessageBox(0, "", |)` → inserts `20001, 13, "Vendor %1 not found"` at args 3–5
  - The default-txt string is the catalog `MESSAGE_TEXT` with embedded `"` doubled per PeopleCode string quoting.
- **Cold:** inserts a full call at the cursor, e.g. `MsgGetText(20001, 13, "Vendor %1 not found")`. A function picker (dropdown near the Insert button) offers all six mapped functions; last choice remembered in settings. For `MessageBox`/`MsgBoxButtonOverride` the leading non-catalog args are emitted as placeholder defaults (`0, ""`) for the user to adjust.
- **Copy `set, num`** button for when insertion isn't wanted (always available).
- Text insertion/replacement goes through the existing `ScintillaManager` text APIs; argument spans come from the `FunctionCallNode` so replacement ranges are exact byte ranges, not regex guesses.

### 6. New-message panel

Collapsible section under the set list, for the selected set:

```
▾ New message in set 20001
  Free ranges:   48–99 (52 free) · 104–199 (96 free) · 205+ (open)
  Number: [ 60 ]   ✓ 60 is free
  Intended text (optional): [ Vendor %1 has been inactivated ]
                                            [Insert as new]
```

- **Free ranges** are computed client-side from the set's loaded message numbers (gaps between consecutive numbers, plus the open-ended tail after the max). Purely informational.
- **Number box** pre-fills with the next free number after the highest used; clicking a range chip pre-fills that range's start. The user can type anything.
- **Live validation:** free → green check; taken → shows the colliding entry's text inline ("✗ 60 exists: 'Vendor %1 on hold'"). Validation is against the loaded (cached) set — the Refresh button is the answer to "someone just took my number" staleness, which read-only tooling can't fully solve anyway.
- **Intended text** (optional): carried into the inserted snippet as the default-msg string, so the code is immediately meaningful and the text is ready to paste into the browser UI when actually creating the catalog row.
- **Insert as new** uses the same context-aware insert logic (§5) with the chosen number and intended text.

### 7. Command + Ctrl+Space integration

**Command:** `BrowseMessageCatalogCommand` in `Commands/BuiltIn/` — "Browse Message Catalog". `RequiresActiveEditor = true` (insertion targets the editor); `DynamicEnabledCheck` requires a live DB connection. Shortcut: try `Ctrl+Alt+M` with fallbacks per the standard registration pattern (final combination decided at implementation against the live shortcut table).

**Ctrl+Space branch:** in `InvokeAutocompleteCommand`, after the existing trigger-char scan fails to match a prefix context, add an AST check: `editor.GetParsedProgram()`, find the innermost `FunctionCallNode` whose span contains the cursor, and if its function name is in `MessageCatalogFunctions` **and** the cursor's argument index (derived from argument spans / commas) equals the set or num position, open `MessageCatalogDialog` in insert mode instead of showing a popup. Otherwise fall through to the existing behavior (including the function-name autosuggest branch if that design lands first — the two branches are orderable: this one requires being *inside* a mapped call's parens, so they don't conflict).

No C++ changes — same as the function-autosuggest design, the Ctrl+Space keystroke already reaches `InvokeAutocompleteCommand`.

## Data flow

```
Hover over MsgGet(20001, 13, …)
  → TooltipManager → MessageCatalogTooltipProvider.VisitFunctionCall
  → MessageCatalogFunctions map → literal args 20001/13
  → MessageCatalogCache → (miss) IDataManager.GetMessage → tooltip text

Ctrl+Space inside MessageBox(0, "", |
  → InvokeAutocompleteCommand → AST: cursor in arg 3 of mapped function
  → MessageCatalogDialog(insert mode, call-site context)
  → user searches/picks → context-aware insert via ScintillaManager

Command palette "Browse Message Catalog"
  → MessageCatalogDialog(cold) → GetMessageSets / GetMessagesForSet (cached)
  → Insert full snippet or Copy set, num
```

## Error handling

- **No DB connection:** command disabled via `DynamicEnabledCheck`; tooltip provider skipped by the existing `DataManagerRequirement` gating; Ctrl+Space branch falls through to existing behavior.
- **Query failures:** log via AppRefiner's `Debug.Log`, return empty results; the dialog shows an empty grid rather than throwing across the message pump.
- **Parse failures at Ctrl+Space:** `GetParsedProgram` is error-recovering; if no containing mapped call is found, fall through to existing Ctrl+Space behavior.
- **Non-literal set/num args:** tooltips and insert-mode set locking simply don't engage; the dialog still opens unlocked from Ctrl+Space if the cursor is positionally in a mapped arg.
- **Stale cache vs. concurrent catalog edits:** acknowledged limitation of read-only tooling; Refresh button is the escape hatch.

## Out of scope / future ideas

- Writing catalog rows (AppRefiner stays strictly read-only).
- Native Scintilla popup autosuggest for set/num (rejected: prefix-filter-by-digit doesn't help when the number is the unknown).
- A linter flagging hardcoded user-facing strings in `MessageBox`/`Error`/`Warning` that should be catalog entries.
- A styler squiggling references to nonexistent set/num pairs (the tooltip's "no catalog entry" covers discovery for now; a styler adds always-on visibility later using the same cache).
- Language/`LANGUAGE_CD` awareness — v1 reads the base-language tables.

## Touch points summary (all C#, no C++ changes)

| File | Change |
|---|---|
| `AppRefiner/MessageCatalogFunctions.cs` | **new** — shared function→arg-positions map |
| `AppRefiner/Database/Models/MessageCatalog.cs` | **new** — `MessageSetInfo`, `MessageCatalogEntry` |
| `AppRefiner/Database/IDataManager.cs` | 4 new method signatures |
| `AppRefiner/Database/OraclePeopleSoftDataManager.cs` | implementations |
| `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs` | implementations |
| `AppRefiner/Database/MessageCatalogCache.cs` | **new** — memoization + refresh |
| `AppRefiner/TooltipProviders/MessageCatalogTooltipProvider.cs` | **new** — hover lookups |
| `AppRefiner/Dialogs/MessageCatalogDialog.cs` | **new** — browser + new-message panel + insert |
| `AppRefiner/Commands/BuiltIn/BrowseMessageCatalogCommand.cs` | **new** — palette command + shortcut |
| `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs` | Ctrl+Space in-mapped-call branch |

## Manual test plan

1. Hover each of the six functions with literal set/num against a live DB → tooltip shows set name, severity, text, explain. `MessageBox`/`MsgBoxButtonOverride` positions resolve correctly despite leading args.
2. Hover a call with a nonexistent set/num → "No catalog entry" tooltip. Hover with variable args → no tooltip.
3. Palette → Browse Message Catalog: filter sets, search within a set, all-sets text search (cap message visible), preview shows explain text.
4. Cold insert of each function via the picker → well-formed call with escaped quotes in default text; picker choice persists across dialog opens.
5. Ctrl+Space at `MsgGet(|`, `MsgGet(20001, |`, `MessageBox(0, "", |`, `MsgBoxButtonOverride(0, "", &btns, |` → dialog opens in insert mode, set locked when arg 1 typed; insert fills exactly the missing args.
6. Ctrl+Space outside any mapped call → existing behavior unchanged.
7. New-message panel: ranges match the set's real gaps; typing a taken number shows the colliding message; typing a free mid-gap number (e.g. 60 in 48–99) validates green; Insert-as-new emits the chosen number + intended text.
8. Disconnect DB → command disabled, tooltips inert, Ctrl+Space falls through.
9. Refresh after adding a catalog row in the browser → new row visible, validation updated.
