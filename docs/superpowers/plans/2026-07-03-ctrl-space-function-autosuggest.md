# Ctrl+Space Function-Name Autosuggest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ctrl+Space on a plain identifier (no `&`/`%`/`.`/`:` prefix) shows an autocomplete list of functions callable at the cursor — user functions honoring PeopleCode's declare-before-use rule, plus builtin global functions.

**Architecture:** Extends the existing Ctrl+Space pipeline (`InvokeAutocompleteCommand` → `MainForm.WndProc` → `AutoCompleteService` → cross-process `SCI_AUTOCSHOW`) with a new `FunctionNames` autocomplete context. The declare-before-use visibility rule is extracted from `UndeclaredFunctionStyler` into a shared `FunctionVisibilityIndex` in the parser project. Builtins come from `PeopleCodeTypeDatabase.GetObject("System").GetAllMethods()`, cached statically.

**Tech Stack:** C# / .NET 8, Win32 message plumbing (existing). No C++ changes, no unit tests (per Tim — compile verification + manual testing in Application Designer).

**Spec:** `docs/superpowers/specs/2026-07-03-ctrl-space-function-autosuggest-design.md` (approved; all recommended options accepted: keep variables default for empty prefix, user functions floated above builtins via custom order, bare-name insertion, no new settings flag).

## Global Constraints

- Message ID for the new suggest message is **2524** — IDs 2500–2523 are taken (see `AppRefinerHook/Common.h:73-96` and `MainForm.cs:119-139`).
- No C++ code changes. The only `AppRefinerHook` edit is a catalog comment in `Common.h`.
- No new NuGet dependencies.
- Build verification: `dotnet build AppRefiner/AppRefiner.csproj` (~5 s). **Never** launch AppRefiner.exe, never build the C++ `AppRefinerHook` project (unneeded for this work).
- **No unit tests** (per Tim): do not create or run test projects. Verification is compile-only (`dotnet build`); runtime behavior is verified manually by Tim inside Application Designer at the end (Task 6 checklist).
- AppRefiner uses its own `Debug.Log()` / `Debug.LogException()` (namespace `AppRefiner`), not `System.Diagnostics.Debug`.

---

### Task 1: `FunctionVisibilityIndex` shared helper

**Files:**
- Create: `PeopleCodeParser.SelfHosted/FunctionVisibilityIndex.cs`

**Interfaces:**
- Consumes: `ProgramNode.Functions` (`List<FunctionNode>`), `FunctionNode.IsDeclaration` / `.IsImplementation` / `.Name` / `.SourceSpan.Start.ByteIndex` (all existing, `PeopleCodeParser.SelfHosted/Nodes/`).
- Produces: `FunctionVisibilityIndex.Build(ProgramNode)` (static factory), `.Declarations` / `.Implementations` (`IReadOnlyDictionary<string, FunctionNode>`, OrdinalIgnoreCase, first-implementation-wins), `.GetVisibleAt(int byteIndex)` (`List<FunctionNode>`). Namespace `PeopleCodeParser.SelfHosted`. Tasks 2 and 5 rely on these exact names.

- [ ] **Step 1: Write the implementation**

Create `PeopleCodeParser.SelfHosted/FunctionVisibilityIndex.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Indexes a program's top-level functions and answers PeopleCode's single-pass
/// visibility rule: a Declare Function is visible everywhere in the program, while a
/// function implementation is only visible at positions textually below its start.
/// Shared by UndeclaredFunctionStyler (per-call-site checks) and function-name
/// autocomplete (enumeration at the cursor).
/// </summary>
public sealed class FunctionVisibilityIndex
{
    private readonly Dictionary<string, FunctionNode> _declarations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FunctionNode> _implementations = new(StringComparer.OrdinalIgnoreCase);

    private FunctionVisibilityIndex() { }

    /// <summary>Declare Function nodes by name. Visible program-wide.</summary>
    public IReadOnlyDictionary<string, FunctionNode> Declarations => _declarations;

    /// <summary>
    /// First implementation per name — visibility is judged against the earliest
    /// definition of the name.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionNode> Implementations => _implementations;

    public static FunctionVisibilityIndex Build(ProgramNode program)
    {
        var index = new FunctionVisibilityIndex();
        foreach (var fn in program.Functions)
        {
            if (fn.IsDeclaration)
            {
                index._declarations[fn.Name] = fn;
            }
            else if (fn.IsImplementation)
            {
                index._implementations.TryAdd(fn.Name, fn);
            }
        }
        return index;
    }

    /// <summary>
    /// All functions callable at the given UTF-8 byte position: every declare, plus
    /// implementations that start above the position. Deduped case-insensitively;
    /// a declare shadows a same-named implementation.
    /// </summary>
    public List<FunctionNode> GetVisibleAt(int byteIndex)
    {
        var result = new List<FunctionNode>(_declarations.Count + _implementations.Count);
        result.AddRange(_declarations.Values);
        foreach (var impl in _implementations.Values)
        {
            if (impl.SourceSpan.Start.ByteIndex < byteIndex && !_declarations.ContainsKey(impl.Name))
            {
                result.Add(impl);
            }
        }
        return result;
    }
}
```

- [ ] **Step 2: Compile-verify**

Run: `dotnet build PeopleCodeParser.SelfHosted/PeopleCodeParser.SelfHosted.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```powershell
git add PeopleCodeParser.SelfHosted/FunctionVisibilityIndex.cs
git commit -m "feat: add FunctionVisibilityIndex for declare-before-use function visibility"
```

---

### Task 2: `UndeclaredFunctionStyler` consumes the shared index

**Files:**
- Modify: `AppRefiner/Stylers/UndeclaredFunctionStyler.cs:25-53, 69-74`

**Interfaces:**
- Consumes: `FunctionVisibilityIndex.Build` / `.Declarations` / `.Implementations` from Task 1.
- Produces: no new surface — behavior must be identical to before (the styler's own dictionaries are replaced by the index's).

- [ ] **Step 1: Replace the private dictionaries with the index**

In `AppRefiner/Stylers/UndeclaredFunctionStyler.cs`, add `using PeopleCodeParser.SelfHosted;` to the usings, then replace the two dictionary fields (lines 25-26) and `VisitProgram` (lines 33-53):

```csharp
private FunctionVisibilityIndex? _functionIndex;
```

```csharp
public override void VisitProgram(ProgramNode node)
{
    _functionIndex = FunctionVisibilityIndex.Build(node);
    base.VisitProgram(node);
}
```

In `VisitFunctionCall`, replace the two lookups (lines 69 and 72):

```csharp
// Declares must precede implementations and executable code, so existence
// alone makes the name visible everywhere
if (_functionIndex == null || _functionIndex.Declarations.ContainsKey(name))
    return;

if (_functionIndex.Implementations.TryGetValue(name, out var impl))
```

(The rest of `VisitFunctionCall` — the forward-reference indicator, builtin check, deferred quick fix — is unchanged.)

- [ ] **Step 2: Compile-verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```powershell
git add AppRefiner/Stylers/UndeclaredFunctionStyler.cs
git commit -m "refactor: UndeclaredFunctionStyler uses shared FunctionVisibilityIndex"
```

---

### Task 3: New context + message ID + Ctrl+Space detection

**Files:**
- Modify: `AppRefiner/ScintillaEditor.cs:318-326` (enum)
- Modify: `AppRefiner/MainForm.cs:139` area (constant)
- Modify: `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs` (constant, mapping, detection)
- Modify: `AppRefinerHook/Common.h:96` area (comment only)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `AutoCompleteContext.FunctionNames = 6` (enum member, `ScintillaEditor.cs`); `AR_FUNCTION_SUGGEST = 2524` constants in `MainForm` (`internal const`) and `InvokeAutocompleteCommand` (`private const`). Tasks 4 and 5 rely on `AutoCompleteContext.FunctionNames`; Task 5 relies on `AR_FUNCTION_SUGGEST`.

- [ ] **Step 1: Add the enum value**

In `AppRefiner/ScintillaEditor.cs`, extend the `AutoCompleteContext` enum (line 318):

```csharp
public enum AutoCompleteContext
{
    None = 0,
    AppPackage = 1,
    Variable = 2,
    ObjectMembers = 3,
    SystemVariables = 4,
    FunctionCallTip = 5,
    FunctionNames = 6
}
```

- [ ] **Step 2: Add the MainForm constant**

In `AppRefiner/MainForm.cs`, after the `AR_CONTEXT_MENU_OPTION` constant (line 139):

```csharp
private const int AR_FUNCTION_SUGGEST = 2524; // Function name suggestions (Ctrl+Space on a plain identifier; C#-only, no hook producer)
```

- [ ] **Step 3: Add the Common.h catalog comment (no code)**

In `AppRefinerHook/Common.h`, after the `WM_AR_EDITOR_DESTROYED 2523` line (line 96):

```cpp
// 2524 is AR_FUNCTION_SUGGEST (C#-side only: raised by InvokeAutocompleteCommand for
// Ctrl+Space on a plain identifier; the hook never produces it — reserved here so the
// next hook message doesn't collide)
```

- [ ] **Step 4: Extend the Ctrl+Space context detection**

In `AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs`:

Add the constant next to the others (line 15):

```csharp
private const int AR_FUNCTION_SUGGEST = 2524;
```

Add the mapping case to the `messageId` switch in `Execute` (line 70):

```csharp
messageId = contextType switch
{
    AutoCompleteContext.AppPackage => AR_APP_PACKAGE_SUGGEST,
    AutoCompleteContext.Variable => AR_VARIABLE_SUGGEST,
    AutoCompleteContext.ObjectMembers => AR_OBJECT_MEMBERS,
    AutoCompleteContext.SystemVariables => AR_SYSTEM_VARIABLE_SUGGEST,
    AutoCompleteContext.FunctionCallTip => AR_FUNCTION_CALL_TIP,
    AutoCompleteContext.FunctionNames => AR_FUNCTION_SUGGEST,
    _ => AR_VARIABLE_SUGGEST
};
```

Replace `DetectAutocompleteContext` (lines 102-126) with:

```csharp
/// <summary>
/// Scan backward from position to detect autocomplete context
/// </summary>
private (AutoCompleteContext context, int triggerPosition, char triggerChar)? DetectAutocompleteContext(
    string text, int position)
{
    // Scan backward from cursor
    for (int i = position - 1; i >= 0; i--)
    {
        char ch = text[i];

        // Continue through identifier characters
        if (char.IsLetterOrDigit(ch) || ch == '_')
            continue;

        // Found trigger character
        if (ch == '%') return (AutoCompleteContext.SystemVariables, i, ch);
        if (ch == '&') return (AutoCompleteContext.Variable, i, ch);
        if (ch == '.') return (AutoCompleteContext.ObjectMembers, i, ch);
        if (ch == ':') return (AutoCompleteContext.AppPackage, i, ch);
        if (ch == '(' || ch == ',') return (AutoCompleteContext.FunctionCallTip, i, ch);

        // Hit a non-trigger boundary: a plain identifier (no prefix) means
        // function-name suggestions; anything else falls back to the default
        return IdentifierContextOrNull(text, i + 1, position);
    }

    // Reached start of document — the whole prefix may be a plain identifier
    return IdentifierContextOrNull(text, 0, position);
}

/// <summary>
/// FunctionNames context when [identifierStart, position) is a non-empty run that
/// starts with a letter (PeopleCode identifiers cannot start with a digit);
/// otherwise null so Execute falls back to variable suggestions.
/// </summary>
private static (AutoCompleteContext context, int triggerPosition, char triggerChar)? IdentifierContextOrNull(
    string text, int identifierStart, int position)
{
    if (identifierStart < position && char.IsLetter(text[identifierStart]))
        return (AutoCompleteContext.FunctionNames, identifierStart, '\0');
    return null;
}
```

Note: the caller loop only advances past `[A-Za-z0-9_]`, so `identifierStart < position` guarantees a pure identifier run; the first-char letter check is the only extra condition needed.

- [ ] **Step 5: Compile-verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```powershell
git add AppRefiner/ScintillaEditor.cs AppRefiner/MainForm.cs AppRefiner/Commands/BuiltIn/InvokeAutocompleteCommand.cs AppRefinerHook/Common.h
git commit -m "feat: detect plain-identifier context on Ctrl+Space, add FunctionNames context"
```

---

### Task 4: ScintillaManager plumbing (prefix length + fillup chars)

**Files:**
- Modify: `AppRefiner/ScintillaManager.cs:2034-2109` (`CalculateLengthEntered`) and `:2126-2133` (`SetAutoCompleteContextCharacters`)

**Interfaces:**
- Consumes: `AutoCompleteContext.FunctionNames` from Task 3; existing privates `ReadDocumentRange`, `SCI_LINEFROMPOSITION`, `SCI_POSITIONFROMLINE`.
- Produces: `CalculateLengthEntered` returns the typed identifier length for `FunctionNames` (so Scintilla pre-filters to the prefix and the selection handler deletes the right amount); fillups `"(\t;"` so `(` accepts an entry and chains into the existing call-tip path.

- [ ] **Step 1: Add the FunctionNames branch to `CalculateLengthEntered`**

In `CalculateLengthEntered`, immediately after the initial guard (after line 2039, before the `triggerChar` switch):

```csharp
// FunctionNames has no trigger character — the typed prefix is the identifier itself
if (context == AutoCompleteContext.FunctionNames)
{
    return CountIdentifierBytesBeforePosition(editor, position);
}
```

Add the helper as a new private method after `CalculateLengthEntered` (after line 2109):

```csharp
/// <summary>
/// Counts identifier bytes ([A-Za-z0-9_]) immediately before the cursor on the
/// current line. Used by contexts with no trigger character (FunctionNames), where
/// the typed prefix is the identifier itself. Positions are UTF-8 byte offsets and
/// PeopleCode identifier characters are ASCII, so byte-wise counting is exact.
/// </summary>
private static int CountIdentifierBytesBeforePosition(ScintillaEditor editor, int position)
{
    int lineNumber = (int)editor.SendMessage(SCI_LINEFROMPOSITION, position, IntPtr.Zero);
    int lineStart = (int)editor.SendMessage(SCI_POSITIONFROMLINE, lineNumber, IntPtr.Zero);
    int scanLength = position - lineStart;
    if (scanLength <= 0)
    {
        return 0;
    }

    byte[]? lineBytes = ReadDocumentRange(editor, lineStart, scanLength);
    if (lineBytes == null)
    {
        return 0;
    }

    int lengthEntered = 0;
    for (int i = scanLength - 1; i >= 0; i--)
    {
        byte b = lineBytes[i];
        if (b < 0x80 && (char.IsLetterOrDigit((char)b) || b == (byte)'_'))
        {
            lengthEntered++;
        }
        else
        {
            break;
        }
    }

    Debug.Log($"CountIdentifierBytesBeforePosition: lengthEntered={lengthEntered}");
    return lengthEntered;
}
```

- [ ] **Step 2: Add the fillup case**

In `SetAutoCompleteContextCharacters`, add to the `fillups` switch (line 2126):

```csharp
string fillups = context switch
{
    AutoCompleteContext.Variable => ".\t;",        // '.' chains to object members
    AutoCompleteContext.ObjectMembers => "(\t;",   // '(' chains to parameters
    AutoCompleteContext.AppPackage => ":\t",      // ':' drills down into packages
    AutoCompleteContext.SystemVariables => "\t;",  // No chaining for system variables
    AutoCompleteContext.FunctionNames => "(\t;",   // '(' chains to the function call tip
    _ => "\t"
};
```

- [ ] **Step 3: Compile-verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add AppRefiner/ScintillaManager.cs
git commit -m "feat: prefix length and fillup handling for FunctionNames autocomplete context"
```

---

### Task 5: Suggestion building, message handler, and selection handling

**Files:**
- Modify: `AppRefiner/AutoCompleteService.cs` (`UserListType` at :168, new `ShowFunctionSuggestions` after `ShowSystemVariables` ~:795, new selection handler after `HandleSystemVariableListSelection` ~:1260, new case in `HandleUserListSelection` :1383)
- Modify: `AppRefiner/MainForm.cs` (new `WndProc` branch after the `AR_SYSTEM_VARIABLE_SUGGEST` block ending :3013; `SCN_AUTOCSELECTION` context map :2383)

**Interfaces:**
- Consumes: `FunctionVisibilityIndex` (Task 1), `AutoCompleteContext.FunctionNames` + `AR_FUNCTION_SUGGEST` (Task 3), existing `editor.GetParsedProgram()` (`ScintillaEditor.cs:362`), `PeopleCodeTypeDatabase.GetObject("System")` (namespace `PeopleCodeTypeInfo.Database`, already imported in AutoCompleteService), `AutoCompleteIcons.ExternalFunction` / `.ClassMethod` (`AppDesignerProcess.cs:262`), `ScintillaManager.ShowAutoComplete(editor, context, position, options, customOrder)`.
- Produces: `AutoCompleteService.ShowFunctionSuggestions(ScintillaEditor editor, int position)` (public void), `UserListType.FunctionNames = 6`.

- [ ] **Step 1: Add the UserListType value**

In `AppRefiner/AutoCompleteService.cs` (line 168):

```csharp
public enum UserListType
{
    AppPackage = 1,
    QuickFix = 2,
    Variable = 3,
    ObjectMembers = 4,
    SystemVariables = 5,
    FunctionNames = 6
}
```

- [ ] **Step 2: Add the builtin cache and `ShowFunctionSuggestions`**

Ensure `using PeopleCodeParser.SelfHosted;` is among AutoCompleteService's usings (it may only have `PeopleCodeParser.SelfHosted.Nodes` etc. — `FunctionVisibilityIndex` lives in the parent namespace). Then add after `ShowSystemVariables` (after line ~795):

```csharp
/// <summary>
/// Display entries for builtin global functions (methods of the "System" builtin
/// object), built once — the typeinfo catalog is immutable. Names are kept alongside
/// the formatted entries so user functions can shadow same-named builtins.
/// </summary>
private static readonly Lazy<List<(string Name, string Entry)>> BuiltinFunctionEntries = new(() =>
{
    var entries = new List<(string, string)>();
    var systemObj = PeopleCodeTypeDatabase.GetObject("System");
    if (systemObj == null)
    {
        Debug.Log("System builtin object not found; no builtin function suggestions");
        return entries;
    }

    foreach (var fn in systemObj.GetAllMethods()
                 .Where(m => !string.IsNullOrEmpty(m.Name) && m.Visibility <= MemberVisibility.Public)
                 .OrderBy(m => m.Name))
    {
        entries.Add((fn.Name, $"{fn.Name}?{(int)AutoCompleteIcons.ClassMethod}"));
    }
    return entries;
});

/// <summary>
/// Shows function-name suggestions at the cursor: user functions visible at this
/// position (declares anywhere; implementations only above — PeopleCode is
/// single-pass) followed by builtin global functions. Triggered by Ctrl+Space on a
/// plain identifier (no prefix character).
/// </summary>
public void ShowFunctionSuggestions(ScintillaEditor editor, int position)
{
    if (editor == null || !editor.IsValid()) return;

    try
    {
        var suggestions = new List<string>();
        var userNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var program = editor.GetParsedProgram();
        if (program != null)
        {
            // Positions from Scintilla are UTF-8 byte offsets, matching SourceSpan
            var visible = FunctionVisibilityIndex.Build(program).GetVisibleAt(position)
                .Where(fn => !string.IsNullOrEmpty(fn.Name))
                .OrderBy(fn => fn.Name);

            foreach (var fn in visible)
            {
                if (userNames.Add(fn.Name))
                {
                    suggestions.Add($"{fn.Name}?{(int)AutoCompleteIcons.ExternalFunction}");
                }
            }
        }

        // Builtins after user functions, minus any shadowed by a user function
        foreach (var (name, entry) in BuiltinFunctionEntries.Value)
        {
            if (!userNames.Contains(name))
            {
                suggestions.Add(entry);
            }
        }

        if (suggestions.Count > 0)
        {
            Debug.Log($"Showing {suggestions.Count} function suggestions at position {position}");
            // customOrder keeps user functions (alphabetical) above builtins (alphabetical)
            bool result = ScintillaManager.ShowAutoComplete(editor, AutoCompleteContext.FunctionNames, position, suggestions, customOrder: true);
            if (!result)
            {
                Debug.Log("Failed to show function name suggestions.");
            }
        }
        else
        {
            Debug.Log("No function suggestions available at this position.");
        }
    }
    catch (Exception ex)
    {
        Debug.LogException(ex, "Error showing function suggestions");
    }
}
```

- [ ] **Step 3: Add the selection handler and routing case**

Add after `HandleSystemVariableListSelection` (after line ~1260):

```csharp
private BaseRefactor? HandleFunctionNamesSelection(ScintillaEditor editor, string selection)
{
    // Entries are bare names (the "?icon" suffix is stripped by Scintilla before
    // the notification). Defensive: strip a " -> " decoration if the entry format
    // ever gains type info, mirroring the other handlers.
    string functionName = selection;
    var nameEndIndex = selection.IndexOf(" -> ");
    if (nameEndIndex > 0)
    {
        functionName = selection.Substring(0, nameEndIndex);
    }

    // The typed prefix was already removed by the SCN_AUTOCSELECTION handler.
    // Bare-name insertion: typing '(' afterwards fires the existing call tip.
    ScintillaManager.InsertTextAtCursor(editor, functionName);

    return null;
}
```

Add the case to the switch in `HandleUserListSelection` (line 1383):

```csharp
case UserListType.FunctionNames:
    return HandleFunctionNamesSelection(editor, selection);
```

- [ ] **Step 4: Add the MainForm message handler and selection mapping**

In `AppRefiner/MainForm.cs`, insert a new branch after the `AR_SYSTEM_VARIABLE_SUGGEST` block (its closing brace is at line 3013, before `else if (m.Msg == AR_SCINTILLA_ALREADY_LOADED)`):

```csharp
else if (m.Msg == AR_FUNCTION_SUGGEST)
{
    var editor = ResolveEditorFromHwnd(UnpackHwnd(m.LParam));
    if (editor == null || autoCompleteService == null) return;

    // Manual trigger only (Ctrl+Space on a plain identifier) — explicit user
    // intent, so no AutoSuggestSettings gate
    int position = m.WParam.ToInt32();

    try
    {
        // GetParsedProgram inside the service is hash-cached and needs no type
        // inference, unlike the char-typed suggest handlers above
        autoCompleteService.ShowFunctionSuggestions(editor, position);
    }
    catch (Exception ex)
    {
        Debug.LogException(ex, "Error processing function name suggestion");
    }
}
```

In the `SCN_AUTOCSELECTION` handler, add to the context→list-type map (line 2383):

```csharp
UserListType convertedListType = context switch
{
    AutoCompleteContext.AppPackage => UserListType.AppPackage,
    AutoCompleteContext.Variable => UserListType.Variable,
    AutoCompleteContext.ObjectMembers => UserListType.ObjectMembers,
    AutoCompleteContext.SystemVariables => UserListType.SystemVariables,
    AutoCompleteContext.FunctionNames => UserListType.FunctionNames,
    _ => UserListType.QuickFix  // Fallback (should never happen)
};
```

- [ ] **Step 5: Compile-verify**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: `Build succeeded. 0 Error(s)`

If `MemberVisibility` is unresolved in the Lazy initializer, use the same namespace import `ShowObjectMembers` relies on (check the file's existing usings — `ShowObjectMembers` at line 500 already takes a `MemberVisibility` parameter, so it is in scope; this note exists only in case the compiler says otherwise).

- [ ] **Step 6: Commit**

```powershell
git add AppRefiner/AutoCompleteService.cs AppRefiner/MainForm.cs
git commit -m "feat: Ctrl+Space function-name autosuggest (scoped user functions + builtins)"
```

---

### Task 6: What's New entry and manual verification handoff

**Files:**
- Modify: `AppRefiner/whats-new.txt` (NEW FEATURES section, currently starting line 13)

**Interfaces:**
- Consumes: the completed feature (Tasks 1–5).
- Produces: user-facing release note; manual test checklist for Tim.

- [ ] **Step 1: Add the What's New bullet**

In `AppRefiner/whats-new.txt`, add to the NEW FEATURES bullet list (alongside the existing entries, e.g. after the "Undeclared Function Detection" bullet at line 36):

```
• Function Name Autocomplete - Press Ctrl+Space while typing a plain identifier
  (no & or % prefix) to see the functions you can call there: functions defined
  or declared in the current program — honoring PeopleCode's rule that an
  implementation must appear above its use — plus every builtin function. Pick
  one and type ( to get its signature call tip.
```

- [ ] **Step 2: Commit**

```powershell
git add AppRefiner/whats-new.txt
git commit -m "docs: add function name autocomplete to what's new"
```

- [ ] **Step 3: Hand off to Tim for manual verification**

Report: **AppRefiner** is the project to rebuild (PeopleCodeParser.SelfHosted rebuilds as its dependency; the C++ hook needs no rebuild). Manual checklist inside Application Designer:

1. `Function DoAThing() ... End-Function;` above cursor, type `DoA` + Ctrl+Space → list shows `DoAThing` (function icon) pre-selected; Enter inserts `DoAThing` with no duplicated prefix.
2. Function implementation **below** the cursor → not offered; add a `Declare Function` for it at the top → offered.
3. `Le` + Ctrl+Space → builtin `Len` (method icon) among `Le…` entries; select it, type `(` → `Len` signature call tip appears.
4. User function named the same as a builtin → appears once, with the function icon.
5. Ctrl+Space after `&x`, `%Th`, `obj.`, `PKG:` → existing behaviors unchanged; Ctrl+Space on whitespace → variable suggestions (unchanged default).
6. Accept an entry by typing `(` (fillup) → name inserted and call tip fires.
7. Escape closes the popup; no stray space character in the document at any point.
8. Introduce a syntax error below the cursor → Ctrl+Space still lists functions (error-recovered AST).

---

## Self-Review Notes

- **Spec coverage:** trigger detection (Task 3), scope-honoring user functions (Tasks 1, 5), builtins (Task 5), prefix filter + deletion (Task 4 + existing SCN_AUTOCSELECTION), `(` call-tip chaining (Task 4 fillups), styler reuse (Task 2), no C++ (comment only), all four open-question resolutions encoded (variables default kept in Task 3's null path; custom order, bare-name insertion, no settings flag in Task 5). Phase-2 cross-program suggestions intentionally excluded per spec.
- **Type consistency:** `FunctionVisibilityIndex.Build/GetVisibleAt/Declarations/Implementations` used identically in Tasks 1, 2, 5; `AutoCompleteContext.FunctionNames`/`UserListType.FunctionNames` both `= 6`; `AR_FUNCTION_SUGGEST = 2524` in both files that define it.
- Line numbers reference the current state of `main` (commit 82c223a) and shift as tasks land — anchor by the quoted code, not the number.
