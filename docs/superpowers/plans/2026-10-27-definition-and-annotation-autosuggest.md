# Definition-Name and Annotation-Type Autosuggest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two new autosuggest lists:
- **Definition names.** After a reference keyword plus one typed character (`HTML.Z`, `Field.E`, …), suggest definition names from the database.
- **Annotation types.** After `/* as ` in an annotation position, suggest the ApiObject subtypes, plus `CompIntfc.`, `Record.` and `Field.` where they fit.

**Architecture:** No new classes. Each piece is a method on an existing class:
- **Annotation detection and the annotation type list:** `TypeAnnotationParser` (parser project, unit-tested).
- **Definition-name detection:** `AutoCompleteService`.
- **Keyword → table map:** a static on `IDataManager`, used by a new `GetDefinitionNames` on each data manager.
- **Hook:** the C++ hook does a cheap text pre-check and posts one of two new messages.

**Tech Stack:** C# / .NET 8 (WinForms, xUnit), C++ hook DLL (AppRefinerHook), Oracle / SQL Server / SQL proxy (Meta-SQL).

**Spec:** `docs/superpowers/specs/2026-10-27-definition-and-annotation-autosuggest-design.md`

## Global Constraints

- **No new classes.** Add methods to the existing classes named per task. Don't add new enums or records where an existing type fits.
- **Trigger rule.** No popup at a bare `<keyword>.`. At least one identifier character must follow the dot, for every keyword.
- **Prefix queries.** Query by prefix only, and never cache full name lists. Fetch 501 rows (limit 500, plus one to detect truncation).
- **Case.** Definition names are always upper case. AppRefiner upper-cases the typed prefix before binding it. The SQL compares the bare key column (`KEY LIKE <bind> ESCAPE '\'`). Never `UPPER()` in these queries.
- **No settings.** Both suggestions are always on; there are no `autoSuggestSettings` flags for them.
- **Messages.**
  - `WM_AR_DEFINITION_SUGGEST` = 2525.
  - `WM_AR_ANNOTATION_SUGGEST` = 2526.
  - For both, `wParam` = position and `lParam` = `AR_PACK_HWND(hwnd, 0)`.
- **Database access.** Any database work declares `DataManagerRequirement` and checks `HasDatabaseConnection()`. With no connection, nothing is shown.
- **SQL dialects.**
  - SQL Server: positional `?` parameters only (ODBC).
  - SQL proxy: Meta-SQL with `:1`-style binds. Keep `MetaSQL/conversions/meta/` in sync.
- **Logging and dialogs.** Use `Debug.Log` (AppRefiner's own `Debug`), never `Debug.WriteLine`. Never `MessageBox.Show`.
- **Git and builds.**
  - Commit directly to main. Don't push.
  - Tim builds the hook DLL and AppRefiner himself. Building AppRefiner to a temp output folder to check that it compiles is fine.

## Review Focus

1. **A `/* as ` typed above an existing block comment.** The lexer runs the new comment on to that later `*/`. Detection must look only at the text before the cursor. Pinned by `Annotation_detected_before_a_later_comment` in Task 2.
2. **Names containing `_`, which is a LIKE wildcard** (for example `HTML.ZZ_A`). The prefix must escape `\`, `%` and `_` with `\` (Task 3).
3. **Lowercase typing** (for example `html.zz`). Upper-case the prefix in C# (Task 4). Never add `UPPER()` in the SQL (Task 3).
4. **Reference keywords inside strings and ordinary comments** (`"Page.A"`, `/* Page.A`, `REM Page.A;`). No popup. AppRefiner has no test project, so this goes on the manual checklist. Task 4's reviewer should check the code path explicitly.
5. **A member chain that ends in a keyword name** (`&rs.Record.A`, `%This.Page.A`). No popup. This is guarded by the hook's boundary check (Task 5) and by the C# check (Task 4), and is on the manual checklist.

---

### Task 1: ApiObject subtype names

**Files:**
- Modify: `PeopleCodeTypeInfo/Database/PeopleCodeTypeDatabase.cs`, next to `IsApiObjectSubtype` (~line 162)
- Test: `PeopleCodeTypeInfo.Tests/ApiObjectSubtypeNamesTests.cs` (new)

**Interfaces:**
- Produces: `public static IReadOnlyList<string> PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()`.
  - Returns the names the annotation parser accepts as single-part targets.
  - Sorted case-insensitively.

`HashTableReader` can't enumerate the objects it holds, so build the list another way:
- Iterate over the `PeopleCodeType` enum values and take each one's `GetTypeName()`.
- Keep a name when `IsApiObjectSubtype(name)` is true and `BuiltinTypeExtensions.FromString(name) != PeopleCodeType.Unknown`. This is exactly the single-part rule in `TypeAnnotationParser.ParseTarget`.
- Cache the result in a `Lazy<>`.

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

If `Session` or `ComponentInterface` is not a subtype in typeinfo.dat, swap in two names that are, and note the swap in the report.

- [ ] **Step 2: Run the tests and confirm they fail.** `dotnet test PeopleCodeTypeInfo.Tests --filter ApiObjectSubtypeNamesTests` should fail to compile.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests and confirm they pass.** Then run the full `dotnet test PeopleCodeTypeInfo.Tests` and confirm there are no regressions (at least 227 passing).
- [ ] **Step 5: Commit.** Message: `Enumerate the ApiObject subtypes annotations accept`.

---

### Task 2: Annotation suggest context on `TypeAnnotationParser`

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Annotations/TypeAnnotation.cs` (`TypeAnnotationParser`)
- Test: `PeopleCodeParser.SelfHosted.Tests/Annotations/AnnotationSuggestTests.cs` (new; put it next to the existing annotation tests if they live elsewhere)

**Interfaces:**
- Consumes: `PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()` (Task 1).
- Produces, on `TypeAnnotationParser`:

```csharp
// The unfinished annotation the cursor is in, or null. Category null = `any` or a value position (every type fits).
public static bool TryGetPartialAnnotation(string textBeforeCursor, out AnnotationCategory? category, out string partial);
// Suggestions for a category, in display order.
public static IReadOnlyList<string> SuggestedTypes(AnnotationCategory? category);
```

**Rules for `TryGetPartialAnnotation`:**
- **Lex the text before the cursor.** Use `PeopleCodeLexer` with trivia. The cursor must be inside an unterminated block comment.
- **Match the comment body.** The comment starts on the current line, and its body so far matches `^\s*as\s+([A-Za-z0-9_]*)$`, ignoring case. The captured group is `partial`.
- **Look at the last significant token before the `/*`.**
  - It is `ApiObject`, `Record`, `Field` or `any`, and the token before it is `Local`, `Global`, `Component`, `Instance`, `Property`, `As` or `Returns`. Then the category is ApiObject, Record or Field respectively, or `null` for `any`.
  - Otherwise, it ends an expression (identifier, user variable, `)`, `]`, literal) or is `;`. Then the category is `null` (a value position).
  - Otherwise, return false.

**`SuggestedTypes`:**

| Category | List |
|---|---|
| ApiObject | the subtypes, then `CompIntfc.` |
| Record | `Record.` |
| Field | `Field.`, `Record.` |
| null | the subtypes, then `CompIntfc.`, `Field.`, `Record.` |

- [ ] **Step 1: Write the failing tests.** Use `[Theory]` rows of (text before cursor → expected result).

Rows that should match (expected category, expected partial):

| Text before cursor | Category | Partial |
|---|---|---|
| `Local ApiObject /* as ` | ApiObject | `""` |
| `instance any /*as Fo` | null | `"Fo"` |
| `Local Record /* as ` | Record | `""` |
| `Global Field /* AS ` | Field | `""` |
| `Function F(&a As ApiObject /* as ` | ApiObject | `""` |
| `property ApiObject /* as ` | ApiObject | `""` |
| `method M() Returns ApiObject /* as ` | ApiObject | `""` |
| `&f = &s.GetFolder() /* as ` | null | `""` |
| `&f = &s.GetFolder(); /* as ` | null | `""` |

Rows that should return false:
- `Local string /* as `
- `If &x Then /* as `
- `/* as `
- `Local ApiObject /* note as `
- `Local ApiObject /* as CompIntfc.`
- `x = "/* as `

Named facts:
- **`Annotation_detected_before_a_later_comment`.** Input `"Local ApiObject /* as "` gives (ApiObject, `""`). This is the text before the cursor when a `/* old */` follows later in the document.
- **Record list.** `SuggestedTypes(Record)` equals `["Record."]`.
- **Field list.** `SuggestedTypes(Field)` equals `["Field.", "Record."]`.
- **ApiObject list.** `SuggestedTypes(ApiObject)` ends with `"CompIntfc."`, contains `"Session"`, and does not contain `"Record."`.
- **Null list.** The last three entries of `SuggestedTypes(null)` are `"CompIntfc.", "Field.", "Record."`.
- **`Every_subtype_entry_parses`.** For every entry of `SuggestedTypes(null)` that does not end in `.`, `TryParse` on a lexed `/* as <entry> */` has a non-null `Target`.

- [ ] **Step 2: Run the tests and confirm they fail.** `dotnet test PeopleCodeParser.SelfHosted.Tests --filter AnnotationSuggestTests` should fail to compile.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests and confirm they pass.** Then run the full `dotnet test PeopleCodeParser.SelfHosted.Tests` and confirm there are no regressions (at least 784 passing).
- [ ] **Step 5: Commit.** Message: `Detect unfinished type annotations and list the types that fit`.

---

### Task 3: `GetDefinitionNames` in every data manager

**Files:**
- Modify: `AppRefiner/Database/IDataManager.cs`. Add the member and a static map next to the existing statics (`TryMapStringToTargetType`, `CreateObjectPairs`).
- Modify: `AppRefiner/Database/OraclePeopleSoftDataManager.cs`
- Modify: `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs`
- Modify: `AppRefiner/Database/SqlProxyDataManager.cs`
- Modify: `AppRefiner/Database/SqlProxyQueries/SqlProxyQueryCatalog.cs`
- Create: `MetaSQL/conversions/meta/GetDefinitionNames.sql.md`. Follow the format of the existing `GetOpenTargets__*.sql.md` files, and add it to `MetaSQL/conversions/INDEX.md`.

**Interfaces:**
- **Map.** `static (string Table, string Key, string? Filter)? IDataManager.DefinitionNameSource(string keyword)`.
  - Case-insensitive.
  - Returns null for unsupported keywords.
  - This is also the "is this keyword supported" check Task 4 uses.
- **Query method.** `List<string> GetDefinitionNames(string keyword, string prefix, int limit)` on `IDataManager`.
  - `prefix` is already upper-cased and LIKE-escaped by the caller (Task 4).
  - Returns at most `limit` names ordered by key.
  - Returns an empty list for unsupported keywords.
  - Query failures throw.

**Map contents:**

| Keyword(s) | Table | Key | Filter |
|---|---|---|---|
| Record | PSRECDEFN | RECNAME | |
| Field | PSDBFIELD | FIELDNAME | |
| Page, Panel | PSPNLDEFN | PNLNAME | |
| Component, PanelGroup | PSPNLGRPDEFN | PNLGRPNAME | |
| CompIntfc | PSBCDEFN | BCNAME | |
| HTML | PSCONTDEFN | CONTNAME | CONTTYPE = 4 |
| Image | PSCONTDEFN | CONTNAME | CONTTYPE = 1 |
| StyleSheet | PSCONTDEFN | CONTNAME | CONTTYPE = 9 |
| SQL | PSSQLDEFN | SQLID | SQLTYPE = '0' |
| MenuName | PSMENUDEFN | MENUNAME | |
| Message | PSMSGDEFN | MSGNAME | |
| FileLayout | PSFLDDEFN | FLDDEFNNAME | |
| Node | PSMSGNODEDEFN | MSGNODENAME | |
| Operation | PSOPERATION | IB_OPERATIONNAME | |

**Query and row limit:**
- **Query.** `SELECT {Key} FROM {Table} WHERE {Key} LIKE <bind> ESCAPE '\' [AND {Filter}] ORDER BY {Key}`.
  - The bind value is `prefix + "%"`.
  - No `UPPER()`.
  - Table, key and filter come only from the fixed map, never from user input.
- **Row limit.**
  - Oracle: `FETCH FIRST {limit} ROWS ONLY`, or a `ROWNUM` wrapper if any query in the manager shows the target version needs it.
  - SQL Server: `SELECT TOP ({limit})`, like the message catalog query (~line 1031).
  - Proxy: `Query(sql, binds, maxRows: limit)`. Build the Meta-SQL with `%Table({Table})` from the map in one catalog template.
- **Verification.** There's no DB test harness. Building AppRefiner to a temp folder (`dotnet build AppRefiner/AppRefiner.csproj -o <scratch>/build`) must succeed. Runtime behavior is covered by Tim's manual test.

- [ ] **Step 1: Add the map and the interface member.**
- [ ] **Step 2: Implement it in Oracle, SQL Server and the proxy, and add the MetaSQL doc.**
- [ ] **Step 3: Build to the temp folder.** Expect 0 errors.
- [ ] **Step 4: Commit.** Message: `Query definition names by prefix`.

---

### Task 4: AppRefiner wiring (detection, popups, selection, Ctrl+Space)

**Files:**
- Modify: `AppRefiner/ScintillaEditor.cs`, enum `AutoCompleteContext` (~line 342)
- Modify: `AppRefiner/AutoCompleteService.cs`: `UserListType` (~line 170), the new methods, and selection handling
- Modify: `AppRefiner/MainForm.cs`: the AR constants (~line 144), WndProc handlers next to `AR_OBJECT_MEMBERS` (~line 2955), and the SCN_AUTOCSELECTION map (~line 2456)
- Modify: `AppRefiner/ScintillaManager.cs`: `CalculateLengthEntered` (~line 2218) and `SetAutoCompleteContextCharacters` (~line 2353)
- Modify: `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`

**Interfaces:**
- **Consumes.**
  - `TypeAnnotationParser.TryGetPartialAnnotation` and `SuggestedTypes` (Task 2).
  - `IDataManager.DefinitionNameSource` and `GetDefinitionNames` (Task 3).
- **Produces.**
  - `AutoCompleteContext.DefinitionName = 7` and `AutoCompleteContext.AnnotationType = 8`.
  - `UserListType.DefinitionName = 7` and `UserListType.AnnotationType = 8`.
  - The constants `AR_DEFINITION_SUGGEST = 2525` and `AR_ANNOTATION_SUGGEST = 2526`.
  - `public static bool AutoCompleteService.TryGetDefinitionReference(string textBeforeCursor, out string keyword, out string prefix)`.

**Behavior:**
- **`TryGetDefinitionReference`.**
  - The text must end in `<keyword>.<ident>`, where `<ident>` is one or more `[A-Za-z0-9_]` characters and `DefinitionNameSource(keyword)` is non-null.
  - The character before `<keyword>` must not be an identifier character, `.`, `&`, `%` or `:`.
  - Lex the text with `PeopleCodeLexer`. The cursor must not be inside a string or comment, with one exception: an unterminated block comment whose body so far starts with `as` plus whitespace, ignoring case. That exception is the annotation → definition chaining.
- **`ShowDefinitionNameSuggestions(editor, position)`.**
  - Declare `DataManagerRequirement` and check `HasDatabaseConnection()`.
  - Read the text before the cursor, then call `TryGetDefinitionReference`.
  - Upper-case the prefix and escape `\`, `%` and `_` with `\`.
  - Call `GetDefinitionNames(keyword, escaped, 501)`. `truncated = count > 500`. Show the first 500.
  - Keep a per-editor `(keyword, prefix, truncated)` from the last show. Skip querying when all of these hold: same keyword, the new prefix starts with the stored one, the stored result was not truncated, and `SCI_AUTOCACTIVE` is set.
  - Run the query off the UI thread, following the pattern of the existing database-backed suggestions.
- **`ShowAnnotationTypeSuggestions(editor, position)`.**
  - Call `TryGetPartialAnnotation`.
  - Show `SuggestedTypes(category)` with `customOrder: true`.
  - No database gate.
- **Selection.** Insert the chosen text in place of the typed prefix.
  - `CalculateLengthEntered` counts back to the `.` for `DefinitionName`, and back to the whitespace after `as` for `AnnotationType`.
  - The annotation context's fill-up characters exclude `.`, so `CompIntfc.` inserts whole.
- **Ctrl+Space.** In `DetectAutocompleteContext`, before the backward scan, check `TryGetPartialAnnotation` and then `TryGetDefinitionReference` on `text[..position]`. Route to 2526 or 2525 respectively. Everything else is unchanged.

- [ ] **Step 1: Add the enum values, constants and WndProc handlers.**
- [ ] **Step 2: Add `TryGetDefinitionReference`, the two Show methods, selection handling and the Scintilla context cases.**
- [ ] **Step 3: Add the Ctrl+Space routing.**
- [ ] **Step 4: Build to the temp folder.** Expect 0 errors.
- [ ] **Step 5: Commit.** Message: `Show definition-name and annotation-type suggestions`.

---

### Task 5: Hook triggers

**Files:**
- Modify: `AppRefinerHook/Common.h`: the message table comment block, plus two `#define`s after 2523.
- Modify: `AppRefinerHook/HookManager.cpp`: SCN_CHARADDED, next to the `.` trigger (~line 412).

**Interfaces:**
- Posts `WM_AR_DEFINITION_SUGGEST` (2525) and `WM_AR_ANNOTATION_SUGGEST` (2526).
- For both, `wParam` = current position and `lParam` = `AR_PACK_HWND(hwnd, 0)`.

**Behavior:**
- **Text read.** Read at most 64 characters before the cursor, on the current line.
- **Identifier character typed (`[A-Za-z0-9_]`).** Post 2525 when the text before the cursor ends in `<keyword>.<ident>`.
  - `<keyword>` is one of: `Record Field Page Panel Component PanelGroup CompIntfc HTML Image StyleSheet SQL MenuName Message FileLayout Node Operation`, compared case-insensitively.
  - The character before the keyword must not be an identifier character, `.`, `&`, `%` or `:`.
  - Add a comment next to the keyword list saying it must match `IDataManager.DefinitionNameSource`.
- **Space typed.** Post 2526 when the line text before the cursor ends in `/\*\s*as `, ignoring case.
- **Message calls.** Use PostMessage, not SendMessage.
- **Verification.** Tim builds the hook. Check the new code against the surrounding trigger code only; don't run MSBuild on the C++ project.

- [ ] **Step 1: Add the defines and the table entries.**
- [ ] **Step 2: Add both triggers.**
- [ ] **Step 3: Commit.** Message: `Trigger definition-name and annotation-type suggestions from the hook`.

---

### Manual test checklist (Tim, after rebuilding the hook and AppRefiner)

- **Definition names.**
  - `Field.E` pops up promptly on Campus.
  - Typing on to `Field.EMPL…` re-queries past the first 500.
  - `SQL.` with no letter typed shows nothing.
  - `SQL.X` lists only standalone SQL.
  - `html.zz_` lists only `ZZ_` names.
  - `Node.P` and `Operation.P` list names.
- **No false triggers.** No popup for `"Page.A"`, `/* Page.A`, `REM Page.A;`, `&rs.Record.A` or `%This.Page.A`.
- **Annotations.**
  - `Local ApiObject /* as ` lists the subtypes.
  - Choosing `CompIntfc.` and then typing a letter lists CIs.
  - `Local string /* as ` shows nothing.
- **No DB connection.** No definition popups. Annotation popups still work.
