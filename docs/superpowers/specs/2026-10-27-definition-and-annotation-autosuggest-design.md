# Definition-name and annotation-type autosuggest

## Why

Two kinds of name are hard to remember and currently get no autosuggest:

1. **Definition names after a reference keyword**, such as `HTML.`, `SQL.`, `Page.`, `CompIntfc.`, `Record.` and `Field.`. The names live in the database, and on large databases there are very many of them. Campus has about 110k fields, 60k records and 22k pages.
2. **ApiObject subtypes in `/* as T */` annotations.** Code only ever declares `ApiObject`, so users don't know the ~94 subtype names (`Folder`, `FolderCollection`, …) that the annotation mechanism accepts.

## Goals

- Typing a reference keyword, a dot and one character pops up matching definition names. This works the same way for every supported keyword.
- Typing `/* as ` in a position where an annotation is meaningful pops up the annotation types valid there.
- The two features chain: choosing `CompIntfc.`, `Record.` or `Field.` in the annotation list leads into the definition-name list.
- Ctrl+Space re-opens either list in the same contexts.

## Non-goals and anti-goals

- **No popup at the bare dot.** `Field.` shows nothing, and neither does `SQL.`. Showing a list for some keywords at the dot but not others would read as a bug. The rule is the same for every keyword: **at least one character after the dot**.
- No caching of full name lists per kind. The kinds are too large.
- No suggestions after `CompIntfc.X.`, the collection paths. Not in v1.
- Keywords with no single obvious table stay silent: `BarName`, `ItemName`, `Scroll`, `Operation`, `Package`, `Portal`, `URL`, `BusEvent`, `BusActivity`, `BusProcess`, `AnalyticModel`, `Interlink`.

## Feature 1: definition names

### Trigger (C++ hook, `HookManager.cpp` SCN_CHARADDED)

- **When.** An identifier character (`[A-Za-z0-9_]`) is typed and the text before the cursor on the current line matches `<keyword>.<ident>`. In that pattern:
  - `<keyword>` is one of the supported keywords below, matched case-insensitively.
  - `<ident>` is one or more identifier characters, ending at the cursor.
- **Message.** The hook posts the new `WM_AR_DEFINITION_SUGGEST` (2525), with `wParam` = position and `lParam` = `AR_PACK_HWND(hwnd, 0)`. Document the message in the `Common.h` message table and mirror it in `MainForm.cs`.
- **Word boundary.** The keyword must not follow `.`, `&`, `%`, `:` or an identifier character. This keeps `&x.Page.A` and `%This.Record.A` out.
- **Where the check runs.** The hook does only this cheap text check. C# makes every other decision.

### Handler (C#)

New `AutoCompleteService.ShowDefinitionNameSuggestions(editor, position)`:

- **Database.** Declares `DataManagerRequirement`. Without a database connection it returns without showing anything.
- **Comments and strings.** The cursor must not be inside a string or comment. The one exception is an unfinished `/* as …` annotation comment, which Feature 2 needs for chaining.
- **Query.**
  - Calls a new data-manager method, `GetDefinitionNames(DefinitionKind kind, string prefix, int limit)`, implemented in both the Oracle and SQL Server managers.
  - Each query is a prefix `LIKE` on the key column, ordered by name, and returns `limit + 1` rows so the handler can tell when the result was truncated.
  - `limit` = 500.
- **Popup.**
  - Shown with a new `AutoCompleteContext.DefinitionName`, following the 7-touch-point recipe.
  - Selecting a name replaces the typed prefix with the name only.
  - Lists are plain names. No descriptions are needed in v1.
- **Re-querying.** The handler remembers the last query (editor, kind, prefix, whether it was truncated).
  - When the next character extends that prefix and the last result was **not** truncated, the handler does nothing, because Scintilla's own filtering is already correct.
  - When the last result **was** truncated, the handler re-queries with the longer prefix and shows the new list.

### Keyword → table

| Keyword(s) | Table | Key | Filter |
|---|---|---|---|
| `Record` | PSRECDEFN | RECNAME | |
| `Field` | PSDBFIELD | FIELDNAME | |
| `Page`, `Panel` | PSPNLDEFN | PNLNAME | (all page types) |
| `Component`, `PanelGroup` | PSPNLGRPDEFN | PNLGRPNAME | |
| `CompIntfc` | PSBCDEFN | BCNAME | |
| `HTML` | PSCONTDEFN | CONTNAME | CONTTYPE = 4 |
| `Image` | PSCONTDEFN | CONTNAME | CONTTYPE = 1 |
| `StyleSheet` | PSCONTDEFN | CONTNAME | CONTTYPE = 9 |
| `SQL` | PSSQLDEFN | SQLID | SQLTYPE = '0' (SQL objects only; not App Engine/view SQL) |
| `MenuName` | PSMENUDEFN | MENUNAME | |
| `Message` | PSMSGDEFN | MSGNAME | |
| `FileLayout` | PSFLDDEFN | FLDDEFNNAME | |
| `Node` | PSMSGNODEDEFN | MSGNODENAME | |

The Open Definition queries in `OraclePeopleSoftDataManager.GetOpenTargets` hold most of this mapping. Reuse their table and filter knowledge, but write separate queries: these return names only, filter by prefix only, and SQL needs the SQLTYPE filter.

**SQL Server:** use positional `?` parameters only (ODBC).

## Feature 2: annotation types

### Trigger (C++ hook)

- **When.** A space is typed and the text before the cursor on the current line matches `/\*\s*as ` (case-insensitive), with nothing after `as` except the space just typed.
- **Message.** The hook posts the new `WM_AR_ANNOTATION_SUGGEST` (2526). Same `wParam`/`lParam` layout as above.

### Handler (C#)

New `AutoCompleteService.ShowAnnotationTypeSuggestions(editor, position)`. It looks at the token immediately before the `/*` on the same line. These are the two anchors `AnnotationIndex` recognizes.

- **A type** (the `ForType` position), e.g. `Local ApiObject /* as `, `instance any /* as ` or a parameter or property type. Which list is shown depends on that type:

| Declared type | List |
|---|---|
| `ApiObject` | the ApiObject subtypes, plus `CompIntfc.` |
| `Record` | `Record.` |
| `Field` | `Field.`, `Record.` |
| `any` | all of the above |
| anything else | no popup |

- **A value** (the `ForValue` position): the comment comes directly after an expression, or directly after the `;` that ends one. Show the full list, the same as for `any`.
- **Anything else** (a free-standing comment): no popup.

### Subtype list

- **Which subtypes.** Every object in `PeopleCodeTypeDatabase` with `IsApiObjectType`, minus any whose name `BuiltinTypeExtensions.FromString` maps to `PeopleCodeType.Unknown`. `TypeAnnotationParser` rejects those, so the list must never offer a name the parser would flag.
- **Shared rule.** Expose the filter as one shared enumeration in PeopleCodeTypeInfo so the parser's rule and the list can't drift apart.
- **Ordering.** Sort alphabetically. The prefix entries (`CompIntfc.`, `Record.`, `Field.`) go at the end.

### Chaining

- Selecting a prefix entry inserts it verbatim, e.g. `CompIntfc.`.
- Typing the first name character then triggers Feature 1, which is allowed inside an unfinished `/* as` comment.
- Selecting a subtype inserts just the name. Closing the comment with ` */` is left to the user.

## Ctrl+Space

Extend `InvokeAutocompleteCommand.DetectAutocompleteContext`:

- **Definition names.** The cursor is in `<keyword>.<ident>` with at least one identifier character. Route to the definition-name handler. A bare `<keyword>.` keeps today's object-members route, which shows nothing; this keeps the at-least-one-character rule.
- **Annotation types.** The cursor is inside an unfinished `/* as <partial>`. Route to the annotation handler, which filters by the typed partial.

## Settings

Add two toggles next to the existing `autoSuggestSettings` flags, both on by default (my call): "Definition names" and "Annotation types". Each handler checks its flag first, the same way `AR_OBJECT_MEMBERS` checks `ObjectMembers`.

## Testing

- **Unit tests (no DB):**
  - Context detection: the keyword/word-boundary rules, inside-comment exemptions, and the annotation anchor → list mapping.
  - The subtype enumeration matches `TypeAnnotationParser` acceptance: every listed name parses, and the list contains every subtype the parser accepts.
- **Manual testing, done by Tim with the rebuilt hook DLL:**
  - Large kinds (Field, Record) respond quickly.
  - Re-querying after truncation brings in names past the first 500.
  - Annotation popups appear in each anchor position, and the chaining works.

## Left to the builder

- How the hook reads the line text. `SCI_GETLINE` up to the cursor is fine.
- Where the context detection lives. Prefer one C# helper used by both the message handlers and Ctrl+Space.
- Exact names for `DefinitionKind` and the settings properties.
