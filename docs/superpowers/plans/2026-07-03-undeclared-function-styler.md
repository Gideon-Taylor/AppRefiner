# Undeclared Function Styler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Red-squiggle unresolvable function calls with quick fixes that declare the function (from cache or pre-filled search dialog) or move a below-defined local function above its first use.

**Architecture:** A new `BaseStyler` does position-aware detection from the AST + bundled builtin database (no live DB). Quick fixes ride a new rich-context payload on quick-fix entries (`QuickFixEntry` struct replacing the `(Type, string)` tuple), so selections carry `FunctionSearchResult`/name objects instead of re-querying. Two new hidden refactors plus a marker type route selections; the existing `DeclareFunction` refactor and `DeclareFunctionDialog` are reused with small extensions.

**Tech Stack:** C# / .NET 8 WinForms, self-hosted PeopleCode parser AST, SQLite function cache. No automated test framework — verification is `dotnet build` per task plus the manual test matrix at the end (Tim runs App Designer tests).

**Spec:** `docs/superpowers/specs/2026-07-03-undeclared-function-styler-design.md`

## Global Constraints

- Work directly on `main`; exactly the three commits defined below (user request).
- Compile gate per task: `dotnet build AppRefiner/AppRefiner.csproj` → `Build succeeded. 0 Error(s)`. (Tim may prefer to run builds himself — ask before running.)
- All function-name comparisons `StringComparison.OrdinalIgnoreCase`.
- Styler ships enabled by default (`Active = true` in constructor).
- Use AppRefiner's `Debug.Log` (not System.Diagnostics).
- New refactors are hidden (`IsHidden = true`) with no keyboard shortcut (`RegisterKeyboardShortcut => false`), matching `AddImportQuickFix`.

---

### Task 1: QuickFixEntry payload plumbing (commit 1)

**Files:**
- Modify: `AppRefiner/Stylers/BaseStyler.cs` (Indicator struct, AddIndicator overloads, deferred resolver type)
- Modify: `AppRefiner/AutoCompleteService.cs:1168-1233` (`HandleQuickFixSelection`, `ExtractQuickFixContext` callers)
- Modify (mechanical retype only): `AppRefiner/Stylers/AmbiguousClassReferenceStyler.cs:212,223`, `AppRefiner/Stylers/MissingMethodImplementation.cs:45`, `AppRefiner/Stylers/MissingConstructor.cs:126`, `AppRefiner/Stylers/UndefinedVariables.cs:118`, `AppRefiner/Stylers/UnimplementedAbstractMembersStyler.cs:73`, `AppRefiner/Stylers/UnimportedClassStyler.cs:137,166`, `AppRefiner/Stylers/WrongExceptionVariableStyler.cs:43`

**Interfaces:**
- Produces: `AppRefiner.Stylers.QuickFixEntry` — `readonly struct { Type RefactorClass; string Description; object? Context; }` with ctor `(Type, string, object? = null)` and implicit conversions from `(Type, string)` and `(Type, string, object?)` tuples. `Indicator.QuickFixes : List<QuickFixEntry>`; deferred resolver type `Func<ScintillaEditor, int, object?, List<QuickFixEntry>>`. Selection behavior: `editor.QuickFixContext = entry.Context ?? ExtractQuickFixContext(description)`.

- [ ] **Step 1: Add `QuickFixEntry` and retype `Indicator`/`BaseStyler`**

In `BaseStyler.cs`, insert above the `Indicator` struct:

```csharp
    /// <summary>
    /// One quick-fix option attached to an indicator. Context, when set, is handed to the
    /// refactor via editor.QuickFixContext verbatim — richer than the description-string
    /// parsing fallback and avoids re-querying data the resolver already had in hand.
    /// </summary>
    public readonly struct QuickFixEntry
    {
        public Type RefactorClass { get; }
        public string Description { get; }
        public object? Context { get; }

        public QuickFixEntry(Type refactorClass, string description, object? context = null)
        {
            RefactorClass = refactorClass;
            Description = description;
            Context = context;
        }

        public static implicit operator QuickFixEntry((Type RefactorClass, string Description) t)
            => new(t.RefactorClass, t.Description);

        public static implicit operator QuickFixEntry((Type RefactorClass, string Description, object? Context) t)
            => new(t.RefactorClass, t.Description, t.Context);
    }
```

Then replace every `List<(Type RefactorClass, string Description)>` in this file with `List<QuickFixEntry>`:
- `Indicator.QuickFixes` field (line 22)
- `DeferredQuickFixResolver` property type (line 29) → `Func<ScintillaEditor, int, object?, List<QuickFixEntry>>?`
- both `AddIndicator` overloads' `quickFixes` parameter (lines 100, 105)
- `AddIndicatorWithDeferredQuickFix`'s `deferredResolver` parameter (line 136)

The implicit tuple conversions keep existing `quickFixes.Add((typeof(X), "desc"))` and collection-initializer sites compiling unchanged.

- [ ] **Step 2: Retype the declaration sites in existing stylers**

At each file/line listed above, change the declared type only — element construction stays as tuples (converted implicitly):
- `private List<(Type RefactorClass, string Description)> GetImportOptionsResolver(` → `private List<QuickFixEntry> GetImportOptionsResolver(` (UnimportedClassStyler:137; same pattern AmbiguousClassReferenceStyler:212 `ResolveQualificationOptions`)
- `var quickFixes = new List<(Type, string)>();` → `var quickFixes = new List<QuickFixEntry>();` (UnimportedClassStyler:166, AmbiguousClassReferenceStyler:223)
- `var quickFixes = new List<(Type RefactorClass, string Description)> { ... }` → `var quickFixes = new List<QuickFixEntry> { ... }` (MissingMethodImplementation:45, MissingConstructor:126, UndefinedVariables:118, UnimplementedAbstractMembersStyler:73, WrongExceptionVariableStyler:43)

Then run `dotnet build` once and fix any remaining tuple-typed declaration the grep missed (the compiler finds them; `Add`/initializer sites need no changes).

- [ ] **Step 3: Use the context payload in `HandleQuickFixSelection`**

In `AutoCompleteService.cs` (~line 1177), the existing lookup:

```csharp
                if (indicator.QuickFixes != null && indicator.QuickFixes.Select(q => q.Description).Contains(selection))
                {
                    var quickFix = indicator.QuickFixes.Where(q => q.Description == selection).FirstOrDefault();
                    var refactorType = quickFix.RefactorClass;
```

stays shape-identical (`QuickFixEntry` has the same member names). Replace the context assignment:

```csharp
                    string context = ExtractQuickFixContext(selection);
                    editor.QuickFixContext = context;
```

with:

```csharp
                    // Rich payload attached by the resolver wins; description parsing is the
                    // legacy fallback for entries that carry no context object
                    editor.QuickFixContext = quickFix.Context ?? ExtractQuickFixContext(selection);
```

- [ ] **Step 4: Compile gate**

Run: `dotnet build AppRefiner/AppRefiner.csproj` → `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add -A AppRefiner
git commit -m "refactor: quick-fix entries carry an optional context payload" -m "Replaces the (Type, string) quick-fix tuple with QuickFixEntry (RefactorClass, Description, Context). Implicit tuple conversions keep existing stylers' construction sites unchanged; HandleQuickFixSelection passes the payload to editor.QuickFixContext when present, falling back to description parsing. Groundwork for the undeclared-function styler, whose fixes carry FunctionSearchResult objects instead of re-querying the cache on click."
```

---

### Task 2: Declare-function reuse surface + new refactors (commit 2)

**Files:**
- Modify: `AppRefiner/Refactors/DeclareFunction.cs` (`insertExampleCall` option)
- Modify: `AppRefiner/Dialogs/DeclareFunctionDialog.cs:72-78` (ctor gains `initialSearchTerm`)
- Modify: `AppRefiner/MainForm.cs:353` (static `FunctionCache`), `MainForm.cs:1575-1637` (`ShowDeclareFunctionDialog` optional params)
- Modify: `AppRefiner/AutoCompleteService.cs` (`HandleQuickFixSelection` marker interception)
- Create: `AppRefiner/Refactors/QuickFixes/DeclareFunctionQuickFix.cs`
- Create: `AppRefiner/Refactors/QuickFixes/OpenDeclareFunctionDialogQuickFix.cs`
- Create: `AppRefiner/Refactors/MoveFunctionAbove.cs`

**Interfaces:**
- Consumes: `QuickFixEntry.Context` → `editor.QuickFixContext` (Task 1).
- Produces:
  - `DeclareFunction(ScintillaEditor, FunctionSearchResult, bool insertExampleCall = true)`
  - `MainForm.FunctionCache : static FunctionCacheManager?`
  - `MainForm.ShowDeclareFunctionDialog(string? initialSearchTerm = null, bool insertExampleCall = true)`
  - `DeclareFunctionQuickFix` (reads `QuickFixContext as FunctionSearchResult`)
  - `OpenDeclareFunctionDialogQuickFix` (marker; never instantiated; context = function name string)
  - `MoveFunctionAbove` (reads `QuickFixContext as string` = function name)

- [ ] **Step 1: `DeclareFunction.insertExampleCall`**

Add field + ctor param:

```csharp
        private readonly bool _insertExampleCall;

        public DeclareFunction(AppRefiner.ScintillaEditor editor, FunctionSearchResult functionToDeclare,
            bool insertExampleCall = true) : base(editor)
        {
            if (functionToDeclare == null)
                throw new ArgumentException("Function to declare cannot be null", nameof(functionToDeclare));

            _functionToDeclare = functionToDeclare;
            _insertExampleCall = insertExampleCall;
        }
```

Replace the `if (insertIndex >= 0) { ... }` block (lines 92-110) with:

```csharp
                if (insertIndex >= 0)
                {
                    if (!_insertExampleCall)
                    {
                        // Quick-fix path: the call that triggered the fix already exists —
                        // only the declaration line is inserted
                        if (!string.IsNullOrEmpty(declarationString))
                        {
                            InsertText(insertIndex, declarationString, "Insert function declaration");
                        }
                        return;
                    }

                    var funcCallIndex = CurrentPosition;
                    var currentLineText = ScintillaManager.GetLineText(Editor, LineNumber);
                    var exampleCall = string.IsNullOrWhiteSpace(currentLineText)
                        ? _functionToDeclare.GetExampleCall()
                        : _functionToDeclare.GetFunctionCall();
                    if (CurrentPosition == insertIndex)
                    {
                        InsertText(funcCallIndex, exampleCall, "Insert example call of function");
                        InsertText(insertIndex, declarationString, "Insert function declaration");
                    }
                    else
                    {
                        InsertText(insertIndex, declarationString, "Insert function declaration");
                        InsertText(funcCallIndex, exampleCall, "Insert example call of function");
                    }
                }
```

- [ ] **Step 2: Dialog pre-fill**

`DeclareFunctionDialog` ctor: add trailing parameter `string? initialSearchTerm = null`; at the very end of the constructor body add:

```csharp
            if (!string.IsNullOrEmpty(initialSearchTerm))
            {
                // Fires TextChanged -> searchTimer; the cache-loaded handler re-runs the
                // search if the cache wasn't ready yet, so seeding here is sufficient
                searchBox.Text = initialSearchTerm;
            }
```

- [ ] **Step 3: MainForm surface**

At line 353 expose the cache statically (the deferred resolver reaches it without a MainForm reference):

```csharp
            // Instantiate FunctionCacheManager
            FunctionCache = functionCacheManager = FunctionCacheManager.CreateFromSettings();
```

with a new property near the field (line 73):

```csharp
        /// <summary>
        /// Static access to the function cache for quick-fix resolvers. The cache is local
        /// SQLite keyed by DBName — usable without a live database connection.
        /// </summary>
        public static FunctionCacheManager? FunctionCache { get; private set; }
```

`ShowDeclareFunctionDialog` signature becomes `internal void ShowDeclareFunctionDialog(string? initialSearchTerm = null, bool insertExampleCall = true)`; pass `initialSearchTerm` into the `DeclareFunctionDialog` ctor (line 1610) and `insertExampleCall` into the refactor construction (line 1623):

```csharp
                        var refactorClass = new DeclareFunction(activeEditor, selectedFunction, insertExampleCall);
```

- [ ] **Step 4: New refactor files**

`AppRefiner/Refactors/QuickFixes/DeclareFunctionQuickFix.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// QuickFix wrapper that declares a specific function chosen from the cache.
    /// The full FunctionSearchResult rides in editor.QuickFixContext (attached by
    /// UndeclaredFunctionStyler's resolver), so no re-query or parsing happens here.
    /// </summary>
    public class DeclareFunctionQuickFix : BaseRefactor
    {
        public new static string RefactorName => "Declare Function (QuickFix)";
        public new static string RefactorDescription => "Adds a Declare Function statement via QuickFix selection";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly DeclareFunction _innerRefactor;

        public DeclareFunctionQuickFix(ScintillaEditor editor) : base(editor)
        {
            if (editor.QuickFixContext is not FunctionSearchResult functionToDeclare)
                throw new InvalidOperationException("QuickFix context does not contain a FunctionSearchResult");

            Debug.Log($"DeclareFunctionQuickFix: declaring {functionToDeclare.FunctionName} from {functionToDeclare.FunctionPath}");
            _innerRefactor = new DeclareFunction(editor, functionToDeclare, insertExampleCall: false);
        }

        public override void VisitProgram(ProgramNode node)
        {
            _innerRefactor.VisitProgram(node);
            foreach (var edit in _innerRefactor.GetEdits())
            {
                EditText(edit.StartIndex, edit.EndIndex, edit.NewText, edit.Description);
            }
        }
    }
}
```

`AppRefiner/Refactors/QuickFixes/OpenDeclareFunctionDialogQuickFix.cs`:

```csharp
namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// Marker type for the "Search for function 'X'..." quick-fix entry. Never
    /// instantiated: AutoCompleteService.HandleQuickFixSelection intercepts this type
    /// and opens the Declare Function dialog pre-filled with the function name that
    /// rides in the entry's context payload.
    /// </summary>
    public sealed class OpenDeclareFunctionDialogQuickFix
    {
        private OpenDeclareFunctionDialogQuickFix() { }
    }
}
```

`AppRefiner/Refactors/MoveFunctionAbove.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Moves a local function implementation (with its leading comments) above the
    /// function containing the cursor — the quick fix for forward references, which
    /// PeopleCode does not allow. The function name rides in editor.QuickFixContext.
    /// Operates on whole lines in byte space (Scintilla/SourceSpan indices are UTF-8
    /// byte indices — never index the C# string directly with them).
    /// </summary>
    public class MoveFunctionAbove : BaseRefactor
    {
        public new static string RefactorName => "Move Function Above";
        public new static string RefactorDescription => "Moves a function implementation above its first use";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly string _functionName;

        public MoveFunctionAbove(ScintillaEditor editor) : base(editor)
        {
            if (editor.QuickFixContext is not string functionName || string.IsNullOrEmpty(functionName))
                throw new InvalidOperationException("QuickFix context does not contain the function name");

            _functionName = functionName;
        }

        public override void VisitProgram(ProgramNode node)
        {
            var target = node.Functions.FirstOrDefault(f => f.IsImplementation &&
                string.Equals(f.Name, _functionName, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                SetFailure($"Function '{_functionName}' implementation not found");
                return;
            }

            // Destination: the implementation containing the cursor (the call site the
            // quick fix was invoked from), else the start of main code
            var containing = node.Functions.FirstOrDefault(f => f.IsImplementation &&
                f != target &&
                f.SourceSpan.Start.ByteIndex <= CurrentPosition &&
                CurrentPosition <= f.SourceSpan.End.ByteIndex);

            int destLine;
            if (containing != null)
            {
                destLine = StartLineIncludingComments(containing);
            }
            else if (node.MainBlock != null)
            {
                destLine = node.MainBlock.SourceSpan.Start.Line;
            }
            else
            {
                SetFailure("Could not determine where to move the function");
                return;
            }

            int targetStartLine = StartLineIncludingComments(target);
            if (targetStartLine <= destLine)
            {
                SetFailure($"Function '{_functionName}' is already above this location");
                return;
            }

            // Whole-line byte ranges. End boundary = start of the line after End-Function,
            // which naturally carries the trailing line break with the block.
            int destIndex = ScintillaManager.GetLineStartIndex(Editor, destLine);
            int targetStart = ScintillaManager.GetLineStartIndex(Editor, targetStartLine);
            int targetEnd = ScintillaManager.GetLineStartIndex(Editor, target.SourceSpan.End.Line + 1);
            var contentBytes = Encoding.UTF8.GetBytes(ScintillaManager.GetScintillaText(Editor) ?? string.Empty);
            if (targetEnd < 0 || targetEnd > contentBytes.Length)
            {
                targetEnd = contentBytes.Length; // Function block ends at EOF
            }
            if (destIndex < 0 || targetStart < 0 || targetStart >= targetEnd)
            {
                SetFailure("Could not resolve the function block boundaries");
                return;
            }

            string blockText = Encoding.UTF8.GetString(contentBytes, targetStart, targetEnd - targetStart);
            if (!blockText.EndsWith("\n"))
            {
                blockText += Environment.NewLine; // EOF case: ensure separation from the block below
            }

            DeleteText(targetStart, targetEnd, $"Remove function '{_functionName}' from below");
            InsertText(destIndex, blockText, $"Insert function '{_functionName}' above its first use");
        }

        private static int StartLineIncludingComments(FunctionNode fn)
        {
            var firstComment = fn.GetLeadingComments().FirstOrDefault();
            return firstComment != null
                ? Math.Min(firstComment.SourceSpan.Start.Line, fn.SourceSpan.Start.Line)
                : fn.SourceSpan.Start.Line;
        }
    }
}
```

Implementer notes: confirm `DeleteText(startIndex, endIndex, …)` end-exclusivity against `BaseRefactor.cs:247` before relying on the boundary math (the intent is "delete `[targetStart, targetEnd)`", matching how `EditText` copies edits in `AddImportQuickFix`); if `GetLineStartIndex` for line `End.Line + 1` behaves differently at EOF than returning -1/out-of-range, adapt the clamp accordingly.

- [ ] **Step 5: Marker interception in `HandleQuickFixSelection`**

In `AutoCompleteService.cs`, immediately after `var refactorType = quickFix.RefactorClass;` and its null check, add:

```csharp
                    // "Search for function..." opens the pre-filled Declare Function dialog
                    // instead of executing a refactor
                    if (refactorType == typeof(Refactors.QuickFixes.OpenDeclareFunctionDialogQuickFix))
                    {
                        string functionName = quickFix.Context as string ?? string.Empty;
                        mainForm.BeginInvoke((Action)(() =>
                            mainForm.ShowDeclareFunctionDialog(functionName, insertExampleCall: false)));
                        return null;
                    }
```

- [ ] **Step 6: Compile gate**

Run: `dotnet build AppRefiner/AppRefiner.csproj` → `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add -A AppRefiner
git commit -m "feat: declare-function quick-fix refactors and pre-fillable search dialog" -m "DeclareFunction gains insertExampleCall (quick fixes suppress the example call since the triggering call already exists). DeclareFunctionDialog accepts an initial search term; ShowDeclareFunctionDialog passes it through, and MainForm.FunctionCache exposes the cache statically for resolvers. New hidden refactors: DeclareFunctionQuickFix (declares a FunctionSearchResult carried in the quick-fix context), MoveFunctionAbove (relocates a below-defined local function above its first use, comments included), and the OpenDeclareFunctionDialogQuickFix marker that HandleQuickFixSelection routes to the pre-filled dialog."
```

---

### Task 3: UndeclaredFunctionStyler + what's new (commit 3)

**Files:**
- Create: `AppRefiner/Stylers/UndeclaredFunctionStyler.cs`
- Modify: `AppRefiner/whats-new.txt` (NEW FEATURES section of 1.3.0)

**Interfaces:**
- Consumes: `QuickFixEntry` (Task 1); `DeclareFunctionQuickFix`, `OpenDeclareFunctionDialogQuickFix`, `MoveFunctionAbove`, `MainForm.FunctionCache` (Task 2); `PeopleCodeTypeDatabase.GetFunction` (existing).

- [ ] **Step 1: The styler**

```csharp
using AppRefiner.Database;
using AppRefiner.Refactors;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Database;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Flags calls to functions PeopleCode cannot resolve at the call site: not builtin,
    /// not Declare-d, and not implemented above the call (PeopleCode is single-pass, so
    /// forward references to later implementations are compile errors). Quick fixes:
    /// declare from the local function cache (works without a live DB), open the Declare
    /// Function search dialog pre-filled (connected only), or move a below-defined local
    /// function above its first use.
    /// </summary>
    public class UndeclaredFunctionStyler : BaseStyler
    {
        public override string Description => "Undeclared functions";
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        private const uint SQUIGGLE_COLOR = 0x0000FF; // Red
        private const int MAX_IMPORT_OPTIONS = 10;

        private readonly Dictionary<string, FunctionNode> _declarations = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FunctionNode> _implementations = new(StringComparer.OrdinalIgnoreCase);

        public UndeclaredFunctionStyler()
        {
            Active = true; // Ships enabled by default
        }

        public override void VisitProgram(ProgramNode node)
        {
            _declarations.Clear();
            _implementations.Clear();

            foreach (var fn in node.Functions)
            {
                if (fn.IsDeclaration)
                {
                    _declarations[fn.Name] = fn;
                }
                else if (fn.IsImplementation)
                {
                    // Keep the FIRST implementation: visibility is judged against the
                    // earliest definition of the name
                    _implementations.TryAdd(fn.Name, fn);
                }
            }

            base.VisitProgram(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            // Only bare-identifier calls: method calls, create expressions, and
            // %This.X() never have a plain IdentifierNode callee
            if (node.Function is not IdentifierNode ident)
                return;

            string name = ident.Name;

            // Declares must precede implementations and executable code, so existence
            // alone makes the name visible everywhere
            if (_declarations.ContainsKey(name))
                return;

            if (_implementations.TryGetValue(name, out var impl))
            {
                if (impl.SourceSpan.Start.ByteIndex < node.SourceSpan.Start.ByteIndex)
                    return; // Defined above the call — valid

                // Forward reference: the implementation exists but below this call
                var caller = node.FindAncestor<FunctionNode>();
                string description = caller != null
                    ? $"Move Function '{impl.Name}' above '{caller.Name}'"
                    : $"Move Function '{impl.Name}' above this statement";

                AddIndicator(ident.SourceSpan, IndicatorType.SQUIGGLE, SQUIGGLE_COLOR,
                    $"Function '{name}' is defined below its first use",
                    new List<QuickFixEntry> { new(typeof(MoveFunctionAbove), description, impl.Name) });
                return;
            }

            if (PeopleCodeTypeDatabase.GetFunction(name) != null)
                return; // Builtin

            AddIndicatorWithDeferredQuickFix(
                ident.SourceSpan,
                IndicatorType.SQUIGGLE,
                SQUIGGLE_COLOR,
                $"Function '{name}' is not declared or defined",
                ResolveUnknownFunctionFixes,
                name);
        }

        /// <summary>
        /// Deferred resolver (runs at Ctrl+. time): import options from the local function
        /// cache — usable without a live DB connection — plus a pre-filled search dialog
        /// entry when connected. Empty result = squiggle only, no popup.
        /// </summary>
        private List<QuickFixEntry> ResolveUnknownFunctionFixes(ScintillaEditor editor, int position, object? context)
        {
            var fixes = new List<QuickFixEntry>();
            if (context is not string functionName || string.IsNullOrEmpty(functionName))
                return fixes;

            var cache = MainForm.FunctionCache;
            var process = editor.AppDesignerProcess;
            if (cache != null && process != null)
            {
                var matches = cache.SearchFunctionCache(process, functionName)
                    .Where(r => string.Equals(r.FunctionName, functionName, StringComparison.OrdinalIgnoreCase))
                    .Take(MAX_IMPORT_OPTIONS);

                foreach (var match in matches)
                {
                    var parts = match.FunctionPath.Split(':');
                    string source = parts.Length >= 3
                        ? $"{parts[0]}.{parts[1]} ({parts[2]})"
                        : match.FunctionPath;
                    fixes.Add(new(typeof(DeclareFunctionQuickFix), $"Import '{match.FunctionName}' from {source}", match));
                }
            }

            if (editor.DataManager != null)
            {
                fixes.Add(new(typeof(OpenDeclareFunctionDialogQuickFix), $"Search for function '{functionName}'...", functionName));
            }

            return fixes;
        }
    }
}
```

Implementer notes: verify the styler settings persistence honors the constructor's `Active = true` for a styler with no saved setting (check how `StylerManager` restores Active state; if missing-from-settings defaults to disabled, make the default flow from the instance value instead). Verify `FunctionCallNode` exposes `Function` and `IdentifierNode` exposes `Name` (used by `FunctionParameterNames` precedent and the CLAUDE.md example).

- [ ] **Step 2: What's new entry**

Add to the NEW FEATURES section of the 1.3.0 block in `AppRefiner/whats-new.txt`:

```text
• Undeclared Function Detection - Calls to functions that aren't built in,
  declared, or defined above their first use are underlined as you type —
  including the classic "defined below its first use" mistake. Press Ctrl+.
  on the underline to import the function from your function cache (works
  even without a database connection), search for it in the Declare Function
  dialog, or move a below-defined function above its caller automatically.
```

- [ ] **Step 3: Compile gate**

Run: `dotnet build AppRefiner/AppRefiner.csproj` → `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add -A AppRefiner
git commit -m "feat: undeclared-function styler with declare/move quick fixes" -m "New UndeclaredFunctionStyler (enabled by default) red-squiggles bare-identifier calls that aren't builtins, Declare Function targets, or implementations defined above the call. Unknown functions offer cache-backed 'Import from REC.FIELD' fixes (no live DB needed) and a pre-filled Declare Function search dialog when connected; forward references to below-defined local functions offer an automatic 'Move Function above' fix."
```

---

## Manual test matrix (Tim, in App Designer — after deploying the build)

From the spec, all on a Tools instance with the enhanced editor:

1. `&x = DoAThing()` undeclared, DB connected, cache populated → squiggle; Ctrl+. shows `Import 'DoAThing' from REC.FIELD (Event)` entries + `Search for function 'DoAThing'...`; import inserts the declaration in the declare block, **no example call**, call site untouched.
2. Same, disconnected but cache populated → import entries only (no search entry).
3. Same, no cache rows → squiggle only, no popup.
4. Search entry → dialog opens pre-filled; picking a result declares without example call.
5. `CreateRecord(...)` (builtin) → no squiggle.
6. Already `Declare Function`'d call → no squiggle.
7. Local function called after its implementation → no squiggle.
8. `Function A` calling `Function B` defined below → "defined below" squiggle; Move fix relocates B (with comments) above A; saves clean.
9. `create IS_CO_BASE:JSON:JsonObject()` and `create JsonObject()` → no squiggle.
10. Unimported-class and ambiguous-class quick fixes still work (tuple change regression check).
11. Rowset default-method shorthand — `&rs(1).GetRecord(...)` on a rowset variable → no squiggle.
