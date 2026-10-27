# Definition-Name and Annotation-Type Autosuggest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two new suggestions:
- After a reference keyword and one typed character (`HTML.Z`, `Field.E`, …), suggest definition names from the database.
- After `/* as ` in an annotation position, suggest the ApiObject subtypes, plus `CompIntfc.`, `Record.` and `Field.` where they fit.

**Architecture:**
- **Context detection.** Pure functions in `PeopleCodeParser.SelfHosted/Autosuggest/` decide from the text before the cursor whether either context applies, and what the annotation list holds. These are unit-tested.
- **Data access.** Each data manager gets a prefix-limited `GetDefinitionNames` query.
- **Wiring.** AppRefiner connects two new hook messages (2525, 2526) and Ctrl+Space to the detection and data layers. The C++ hook only does a cheap text pre-check, then posts.

**Tech Stack:** C# / .NET 8 (WinForms, xUnit), C++ hook DLL (AppRefinerHook), Oracle / SQL Server / SQL proxy (Meta-SQL).

**Spec:** `docs/superpowers/specs/2026-10-27-definition-and-annotation-autosuggest-design.md`

## Global Constraints

- **Trigger rule.** No popup at a bare `<keyword>.`. At least one identifier character must follow the dot, for every keyword.
- **Query shape.** Prefix query only; no caching of full name lists. Fetch limit + 1 rows, with limit = 500.
- **Settings.** None. Both suggestions are always on; no `autoSuggestSettings` flags.
- **Message IDs.**
  - `WM_AR_DEFINITION_SUGGEST` = 2525 and `WM_AR_ANNOTATION_SUGGEST` = 2526.
  - `wParam` = position, `lParam` = `AR_PACK_HWND(hwnd, 0)`.
- **Database gating.** Anything that touches the database declares `DataManagerRequirement` and checks `HasDatabaseConnection()`. With no connection, it shows nothing.
- **SQL Server.** Positional `?` parameters only (ODBC).
- **SQL proxy.** Meta-SQL with `:1`-style binds. Keep `MetaSQL/conversions/meta/` in sync.
- **Logging and dialogs.** Use `Debug.Log` (AppRefiner's own `Debug`), never `Debug.WriteLine`. Never use `MessageBox.Show`.
- **Git.** Commit directly to main and don't push. Tim builds the hook DLL and AppRefiner himself; building AppRefiner to a temp output folder to check compilation is fine.

## Review Focus

1. **A `/* as ` typed above an existing block comment.** The lexer can run the new comment on to a later `*/`. Detection must look only at the text before the cursor, so this still works. Pinned by Task 2's `Annotation_detected_before_a_later_comment`.
2. **Names with `_`, which is a LIKE wildcard** (e.g. `HTML.ZZ_A`). Without escaping, the query returns non-matching rows. Escape `_` and `%` with `\` and use `ESCAPE '\'` (Task 3).
3. **Lowercase typing** (`html.zz`). Names are stored in uppercase, so upper-case the prefix in C# before binding, and keep the key column bare (no `UPPER()`) so its index is used (Task 3).
4. **Reference keywords inside strings and ordinary comments** (`"Page.A"`, `/* Page.A`, `REM Page.A;`). No popup. Pinned by Task 2 tests.
5. **A member chain that ends in a keyword name** (`&rs.Record.A`, `%This.Page.A`). No popup. Pinned by Task 2 tests, and by the hook's boundary check in Task 5.

---

### Task 1: ApiObject subtype enumeration

**Files:**
- Modify: `PeopleCodeTypeInfo/Database/PeopleCodeTypeDatabase.cs`, next to `IsApiObjectSubtype` (~line 162)
- Test: `PeopleCodeTypeInfo.Tests/ApiObjectSubtypeNamesTests.cs` (new)

**Interfaces:**
- Produces: `public static IReadOnlyList<string> PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()`. It returns the names the annotation parser accepts as single-part targets, sorted case-insensitively.

`HashTableReader` can't enumerate objects. Build the list instead by iterating the `PeopleCodeType` enum values: take each value's `GetTypeName()`, keep it when `IsApiObjectSubtype(name)` is true and `BuiltinTypeExtensions.FromString(name) != PeopleCodeType.Unknown`, and cache the result in a `Lazy<>`. This is exactly the acceptance rule in `TypeAnnotationParser.ParseTarget`'s single-part branch.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Includes_known_subtypes()
{
    var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
    Assert.Contains("Session", names, StringComparer.OrdinalIgnoreCase);
    Assert.Contains("ComponentInterface", names, StringComparer.OrdinalIgnoreCase);
}

[Fact] public void Excludes_non_api_types()
{
    var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
    Assert.DoesNotContain("Rowset", names, StringComparer.OrdinalIgnoreCase);
    Assert.DoesNotContain("ApiObject", names, StringComparer.OrdinalIgnoreCase);
}

[Fact] public void Every_name_is_a_subtype_with_a_known_enum_value()
{
    foreach (var name in PeopleCodeTypeDatabase.GetApiObjectSubtypeNames())
    {
        Assert.True(PeopleCodeTypeDatabase.IsApiObjectSubtype(name), name);
        Assert.NotEqual(PeopleCodeType.Unknown, BuiltinTypeExtensions.FromString(name));
    }
}

[Fact] public void Is_sorted_and_distinct()
{
    var names = PeopleCodeTypeDatabase.GetApiObjectSubtypeNames();
    Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
}
```

If `Session` or `ComponentInterface` turns out not to be a subtype in typeinfo.dat, swap in two names that are, and note the swap in the report.

- [ ] **Step 2: Run the tests and confirm they fail.** `dotnet test PeopleCodeTypeInfo.Tests --filter ApiObjectSubtypeNamesTests` should fail to compile with "GetApiObjectSubtypeNames not defined".
- [ ] **Step 3: Implement** as described above.
- [ ] **Step 4: Run the tests and confirm they pass.** Then run the full `dotnet test PeopleCodeTypeInfo.Tests` and confirm there are no regressions (227 or more passing).
- [ ] **Step 5: Commit.** Message: `Enumerate the ApiObject subtypes annotations accept`.

---

### Task 2: Autosuggest context detection

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Autosuggest/AutosuggestContext.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Autosuggest/AutosuggestContextTests.cs`

**Interfaces:**
- Consumes: `PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()` (Task 1).
- Produces, in namespace `PeopleCodeParser.SelfHosted.Autosuggest`:

```csharp
public enum DefinitionKind { Record, Field, Page, Component, CompIntfc, HTML, Image, StyleSheet, SQL, MenuName, Message, FileLayout, Node }
public enum AnnotationSlot { ApiObject, Record, Field, Any, Value }
public sealed record DefinitionNameRequest(DefinitionKind Kind, string Prefix);
public sealed record AnnotationTypeRequest(AnnotationSlot Slot, string Partial);

public static class AutosuggestContext
{
    // Case-insensitive; Panel→Page, PanelGroup→Component. Unsupported keywords return false.
    public static bool TryGetDefinitionKind(string keyword, out DefinitionKind kind);
    public static DefinitionNameRequest? DetectDefinitionName(string textBeforeCursor);
    public static AnnotationTypeRequest? DetectAnnotationType(string textBeforeCursor);
    public static IReadOnlyList<string> AnnotationTypes(AnnotationSlot slot);
}
```

**Rules:**
- **Input.** Both detectors take only the document text before the cursor. Lex it with `PeopleCodeLexer`, including trivia, to find out whether the cursor is inside a string or a comment. An unterminated block comment at the end means the cursor is inside it.
- **`DetectDefinitionName`.**
  - The text must end in `<keyword>.<ident>`, where `<ident>` is one or more `[A-Za-z0-9_]` characters and `<keyword>` maps through `TryGetDefinitionKind`.
  - The character before `<keyword>` must not be an identifier character, `.`, `&`, `%` or `:`.
  - The cursor must not be inside a string or comment. The one exception is an unterminated block comment whose body, trimmed at the start, begins with `as` plus whitespace; this is how the annotation list chains into definition names.
- **`DetectAnnotationType`.**
  - The cursor must be inside an unterminated block comment on the current line whose body so far matches `^\s*as\s+([A-Za-z0-9_]*)$`, ignoring case. The captured group is `Partial`.
  - Then look at the last significant token before the `/*`:
    - If it is `ApiObject`, `Record`, `Field` or `any`, and the token before that is `Local`, `Global`, `Component`, `Instance`, `Property`, `As` or `Returns`, the slot is that type.
    - Otherwise, if that token ends an expression (identifier, user variable, `)`, `]`, literal) or is `;`, the slot is `Value`.
    - Otherwise there is no slot (return null).
- **`AnnotationTypes`.**

| Slot | List |
|---|---|
| ApiObject | the subtypes, then `CompIntfc.` |
| Record | `Record.` |
| Field | `Field.`, `Record.` |
| Any, Value | the subtypes, then `CompIntfc.`, `Field.`, `Record.` |

- [ ] **Step 1: Write the failing tests.** Use `[Theory]` rows. Each row gives the text before the cursor and the expected result.

Definition names:

| Text before cursor | Expected |
|---|---|
| `Local string &s = HTML.Z` | (HTML, "Z") |
| `SQLExec(SQL.MY_S` | (SQL, "MY_S") |
| `x = panel.a` | (Page, "a") |
| `x = PanelGroup.X` | (Component, "X") |
| `x = CompIntfc.CI_` | (CompIntfc, "CI_") |
| `x = Node.P` | (Node, "P") |
| `x = HTML.` | null |
| `x = Foo.A` | null |
| `x = BarName.A` | null |
| `x = &rs.Record.A` | null |
| `x = %This.Page.A` | null |
| `x = "HTML.A` | null |
| `/* HTML.A` | null |
| `REM HTML.A` | null |
| `Local ApiObject /* as CompIntfc.U` | (CompIntfc, "U") |
| `/* note */ x = Record.A` | (Record, "A") |

Annotation types:

| Text before cursor | Expected |
|---|---|
| `Local ApiObject /* as ` | (ApiObject, "") |
| `instance any /*as Fo` | (Any, "Fo") |
| `Local Record /* as ` | (Record, "") |
| `Global Field /* AS ` | (Field, "") |
| `Function F(&a As ApiObject /* as ` | (ApiObject, "") |
| `property ApiObject /* as ` | (ApiObject, "") |
| `method M() Returns ApiObject /* as ` | (ApiObject, "") |
| `&f = &s.GetFolder() /* as ` | (Value, "") |
| `&f = &s.GetFolder(); /* as ` | (Value, "") |
| `Local string /* as ` | null |
| `If &x Then /* as ` | null |
| `/* as ` | null |
| `Local ApiObject /* note as ` | null |
| `Local ApiObject /* as CompIntfc.` | null |
| `x = "/* as ` | null |

Named facts:
- `Annotation_detected_before_a_later_comment`: the input is `"Local ApiObject /* as "`, which is the text before the cursor when a `/* old */` comment follows later in the document. Expect (ApiObject, ""). This pins the before-cursor-only rule.
- `AnnotationTypes(Record)` equals `["Record."]`.
- `AnnotationTypes(Field)` equals `["Field.", "Record."]`.
- `AnnotationTypes(ApiObject)` ends with `"CompIntfc."`, contains `"Session"` and does not contain `"Record."`.
- `AnnotationTypes(Value)` equals `AnnotationTypes(Any)`, and its last three entries are `"CompIntfc.", "Field.", "Record."`.
- `Every_subtype_entry_parses`: for every entry of `AnnotationTypes(Any)` that does not end in `.`, `TypeAnnotationParser.TryParse` on a lexed `/* as <entry> */` returns a non-null `Target`.

- [ ] **Step 2: Run the tests and confirm they fail.** `dotnet test PeopleCodeParser.SelfHosted.Tests --filter AutosuggestContextTests` should fail to compile.
- [ ] **Step 3: Implement `AutosuggestContext`** following the rules above.
- [ ] **Step 4: Run the tests and confirm they pass.** Then run the full `dotnet test PeopleCodeParser.SelfHosted.Tests` and confirm there are no regressions (784 or more passing).
- [ ] **Step 5: Commit.** Message: `Detect definition-name and annotation-type suggest contexts`.

---

### Task 3: `GetDefinitionNames` in every data manager

**Files:**
- Modify: `AppRefiner/Database/IDataManager.cs`
- Modify: `AppRefiner/Database/OraclePeopleSoftDataManager.cs`
- Modify: `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs`
- Modify: `AppRefiner/Database/SqlProxyDataManager.cs`
- Modify: `AppRefiner/Database/SqlProxyQueries/SqlProxyQueryCatalog.cs`
- Create: `AppRefiner/Database/DefinitionNameTables.cs`
- Create: `MetaSQL/conversions/meta/GetDefinitionNames.sql.md`. Follow the format of the existing `GetOpenTargets__*.sql.md` files and add it to `MetaSQL/conversions/INDEX.md`.

**Interfaces:**
- Consumes: `DefinitionKind` (Task 2).
- Produces: `List<string> IDataManager.GetDefinitionNames(DefinitionKind kind, string prefix, int limit)`.
  - Returns at most `limit` names, ordered by name.
  - Callers pass `limit + 1` to detect truncation; this method just honours `limit`.
  - Query failures throw, the same as the other methods.

**Details:**
- `DefinitionNameTables.For(DefinitionKind)` returns `(string Table, string Key, string? Filter)`, using the spec's table:

| Kind | Table | Key | Filter |
|---|---|---|---|
| Record | PSRECDEFN | RECNAME | |
| Field | PSDBFIELD | FIELDNAME | |
| Page | PSPNLDEFN | PNLNAME | |
| Component | PSPNLGRPDEFN | PNLGRPNAME | |
| CompIntfc | PSBCDEFN | BCNAME | |
| HTML | PSCONTDEFN | CONTNAME | CONTTYPE = 4 |
| Image | PSCONTDEFN | CONTNAME | CONTTYPE = 1 |
| StyleSheet | PSCONTDEFN | CONTNAME | CONTTYPE = 9 |
| SQL | PSSQLDEFN | SQLID | SQLTYPE = '0' |
| MenuName | PSMENUDEFN | MENUNAME | |
| Message | PSMSGDEFN | MSGNAME | |
| FileLayout | PSFLDDEFN | FLDDEFNNAME | |
| Node | PSMSGNODEDEFN | MSGNODENAME | |

- **Query shape:** `SELECT {Key} FROM {Table} WHERE {Key} LIKE <bind> ESCAPE '\' [AND {Filter}] ORDER BY {Key}`.
  - Build the bind value from `prefix.ToUpperInvariant()`, with `\`, `%` and `_` escaped by `\`, plus a trailing `%`.
  - Don't wrap `{Key}` in `UPPER()`.
- **Row limit:**
  - Oracle: use the same mechanism `GetOpenTargets` uses for `max_rows_per_type`.
  - SQL Server: `SELECT TOP ({limit})`, the same as the message catalog query (~line 1031).
  - Proxy: `Query(sql, binds, maxRows: limit)`. The proxy uses one Meta-SQL constant per kind, or one template with `%Table(...)`, whichever fits `SqlProxyQueryCatalog`'s constant-string style. Tables and filters are fixed values from `DefinitionNameTables`, never user input.
- **Testing.** This task has no unit tests, because there is no DB test harness. Verify it by building AppRefiner to a temp output folder: `dotnet build AppRefiner/AppRefiner.csproj -o <scratch>/build` must succeed. Tim covers runtime in the final manual test.

- [ ] **Step 1: Add `DefinitionNameTables` and the `IDataManager` member.**
- [ ] **Step 2: Implement the method** in Oracle, then SQL Server, then the SQL proxy, and add the MetaSQL doc.
- [ ] **Step 3: Build to the temp folder.** Expect 0 errors.
- [ ] **Step 4: Commit.** Message: `Query definition names by prefix`.

---

### Task 4: AppRefiner wiring (popups, selection, Ctrl+Space)

**Files:**
- Modify: `AppRefiner/ScintillaEditor.cs`, enum `AutoCompleteContext` (~line 342)
- Modify: `AppRefiner/AutoCompleteService.cs` (`UserListType` ~line 170, new Show methods, selection handling)
- Modify: `AppRefiner/MainForm.cs`: the AR constants (~line 144), the WndProc handlers next to `AR_OBJECT_MEMBERS` (~line 2955), and the SCN_AUTOCSELECTION map (~line 2456)
- Modify: `AppRefiner/ScintillaManager.cs`: `CalculateLengthEntered` (~line 2218) and `SetAutoCompleteContextCharacters` (~line 2353)
- Modify: `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`

**Interfaces:**
- Consumes: `AutosuggestContext`, its records and `DefinitionKind` (Task 2); `IDataManager.GetDefinitionNames` (Task 3).
- Produces:
  - `AutoCompleteContext.DefinitionName = 7` and `AutoCompleteContext.AnnotationType = 8`.
  - `UserListType.DefinitionName = 7` and `UserListType.AnnotationType = 8`.
  - `MainForm` constants `AR_DEFINITION_SUGGEST = 2525` and `AR_ANNOTATION_SUGGEST = 2526`.

**Behavior:**
- **Text before the cursor.** Both handlers read the document text up to `position`. Use the existing `ScintillaManager` text read; the whole prefix is fine.
- **`ShowDefinitionNameSuggestions(editor, position)`:**
  - Declares `DataManagerRequirement`, with a `HasDatabaseConnection()` check.
  - Runs `DetectDefinitionName`, then queries `limit = 500` by calling `GetDefinitionNames(kind, prefix, 501)`.
  - `truncated = count > 500`; show the first 500.
  - Keep a per-editor `(DefinitionKind Kind, string Prefix, bool Truncated)` from the last show. When the new request has the same kind, the new prefix starts with the stored prefix, the stored result was not truncated, and the popup is still active (`SCI_AUTOCACTIVE`), return without querying.
  - Run the query off the UI thread, following the pattern the existing DB-backed suggestions use.
- **`ShowAnnotationTypeSuggestions(editor, position)`:** run `DetectAnnotationType`, then show `AnnotationTypes(slot)` with `customOrder: true`. Don't gate on the database.
- **Selection.** Insert the chosen text in place of the typed prefix.
  - `CalculateLengthEntered`: for `DefinitionName`, count back to the `.`; for `AnnotationType`, count back to the whitespace after `as`.
  - The annotation context's fill-up characters must not include `.`, so entries like `CompIntfc.` insert whole.
- **Ctrl+Space (`DetectAutocompleteContext`).** Before the existing backward scan, check `DetectAnnotationType` and then `DetectDefinitionName` on `text[..position]`, and route to 2526 or 2525. Everything else is unchanged, so a bare `Field.` still goes to object members and shows nothing.
- **Object members.** The `AR_OBJECT_MEMBERS` handler is unchanged.

- [ ] **Step 1: Add the enum values, constants and WndProc handlers.**
- [ ] **Step 2: Add the Show methods, selection handling and Scintilla context cases.**
- [ ] **Step 3: Add Ctrl+Space routing.**
- [ ] **Step 4: Build to the temp folder.** Expect 0 errors.
- [ ] **Step 5: Commit.** Message: `Show definition-name and annotation-type suggestions`.

---

### Task 5: Hook triggers

**Files:**
- Modify: `AppRefinerHook/Common.h`: the message table comment block and two `#define`s after 2523/2524.
- Modify: `AppRefinerHook/HookManager.cpp`: the SCN_CHARADDED handling, next to the `.` trigger (~line 412).

**Interfaces:** posts `WM_AR_DEFINITION_SUGGEST` (2525) and `WM_AR_ANNOTATION_SUGGEST` (2526), each with `wParam` = current position and `lParam` = `AR_PACK_HWND(hwnd, 0)`.

**Behavior:**
- **Text check.** Read at most 64 characters before the cursor, on the current line only, with `SCI_GETTEXTRANGE` or the equivalent.
- **Identifier character typed** (`[A-Za-z0-9_]`). Post 2525 when the text before the cursor ends in `<keyword>.<ident>`:
  - `<keyword>` is one of `Record Field Page Panel Component PanelGroup CompIntfc HTML Image StyleSheet SQL MenuName Message FileLayout Node`, matched case-insensitively.
  - The character before `<keyword>` must not be an identifier character, `.`, `&`, `%` or `:`.
  - Add a comment that the list must match `AutosuggestContext.TryGetDefinitionKind`.
- **Space typed.** Post 2526 when the line text before the cursor matches `/\*\s*as ` at its end, ignoring case.
- **Use PostMessage, not SendMessage,** because the handlers may hit the DB or SQL proxy.
- **Verification.** Tim builds the hook. The implementer checks the code against the surrounding trigger code and does not run MSBuild on the C++ project.

- [ ] **Step 1: Add the defines and the table entries.**
- [ ] **Step 2: Add both triggers.**
- [ ] **Step 3: Commit.** Message: `Trigger definition-name and annotation-type suggestions from the hook`.

---

### Manual test checklist (Tim, after rebuilding the hook and AppRefiner)

- **Definition names.**
  - `Field.E` pops up promptly on Campus.
  - `Field.EMPL…` re-queries past the first 500.
  - `SQL.` with no letter typed shows nothing.
  - `HTML.zz_` matches `ZZ_` names only.
- **Annotations.**
  - `Local ApiObject /* as ` lists the subtypes.
  - Choosing `CompIntfc.` and typing a letter lists CIs.
  - `Local string /* as ` shows nothing.
- **No DB connection:** no definition popups, and annotation popups still work.
