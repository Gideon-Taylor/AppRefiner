# Extract Function / Method Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an "Extract Function/Method" refactor that turns a selected run of statements into a new function (non-app-class) or private method (app class), passing live-in variables as parameters and returning live-out variables via a single `Returns` value and/or `out` parameters.

**Architecture:** One `BaseRefactor` subclass, `ExtractFunctionMethod`, following the `ExtractLocalVariable` template: capture scope in `VisitBlock`, do all analysis in `OnExitGlobalScope`, then show a deferred dialog. Data-flow classification reads the `VariableRegistry` that `ScopedAstVisitor` populates during traversal. Generation emits `EditText`/`InsertText`/`DeleteText` edits that `BaseRefactor` applies in descending position order.

**Tech Stack:** C# / .NET 8 (`net8.0-windows7.0`), WinForms, the self-hosted `PeopleCodeParser.SelfHosted` AST + `ScopedAstVisitor` + `VariableRegistry`.

## Global Constraints

- All positions are **UTF-8 byte offsets**. Slice source only through `SourceBytes` / `GetSourceText`; never index the C# string with a `SourceSpan` byte index.
- Refactors are **auto-discovered by reflection** — no registration step. Just create the class under `AppRefiner/Refactors/`.
- **No unit tests.** Every task is verified manually in Application Designer (matching every existing refactor). Each task lists a concrete PeopleCode input, what to select, and the exact expected result.
- **Builds are run by Tim.** A task's build step means: tell Tim to run `dotnet build AppRefiner/AppRefiner.csproj` and confirm it succeeds; do not invoke the build yourself.
- Follow the existing patterns in `ExtractLocalVariable.cs` (scope capture, whitespace trimming, borderless dialog, edit ordering) and `MoveFunctionAbove.cs` (insert-before-delete ordering).
- PeopleCode syntax to emit verbatim:
  - Function: `Function <Name>(<params>) Returns <Type>` … body … `End-Function;` (omit `Returns <Type>` when void).
  - Method declaration (3-space indent, in class header): `   method <Name>(<params>) returns <Type>;` (omit `returns` when void).
  - Method implementation: `method <Name>` + one `   /+ &p as <type>[ out]<comma> +/` line per param + `   /+ Returns <Type> +/` (if any) + body + `end-method;`.
  - Parameter in a signature: `&p As <Type>` or `&p As <Type> out`.
  - Return statement: `Return &x;`. Self method call: `%This.<Name>(<args>)`.

---

### Task 1: Skeleton, statement-range location, and safety guards

**Files:**
- Create: `AppRefiner/Refactors/ExtractFunctionMethod.cs`

**Interfaces:**
- Produces (used by later tasks):
  - `private readonly List<StatementNode> selectedStatements` — the contiguous statements to extract, in source order.
  - `private BlockNode? containingBlock` — their parent block.
  - `private ScopeContext? containingScope` — enclosing method/function/getter/setter scope, captured in `VisitBlock`.
  - `private int RangeStart => selectedStatements[0].SourceSpan.Start.ByteIndex;`
  - `private int RangeEnd => selectedStatements[^1].SourceSpan.End.ByteIndex;`
  - `private bool InRange(SourceSpan s)`, `private bool AfterRange(SourceSpan s)`, `private bool BeforeRange(SourceSpan s)` — position helpers used by data-flow analysis.

- [ ] **Step 1: Create the class with metadata and the deferred-dialog flags**

Create `AppRefiner/Refactors/ExtractFunctionMethod.cs`:

```csharp
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Extracts a selected run of statements into a new function (non-app-class
    /// program / main block) or private method (app class). Live-in variables
    /// become parameters; live-out variables are returned via a single Returns
    /// value and/or `out` parameters. Requires a selection covering whole
    /// statements — cursor position alone cannot delimit the range.
    /// </summary>
    public class ExtractFunctionMethod : BaseRefactor
    {
        public new static string RefactorName => "Extract Function/Method";
        public new static string RefactorDescription => "Extracts the selected statements into a new function or method";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RequiresUserInputDialog => true;
        public override bool DeferDialogUntilAfterVisitor => true;
        public override bool RequiresTypeInference => true;
        public override bool RunOnIncompleteParse => false;

        private ScopeContext? containingScope;
        private BlockNode? containingBlock;
        private readonly List<StatementNode> selectedStatements = new();

        public ExtractFunctionMethod(ScintillaEditor editor) : base(editor) { }

        private int RangeStart => selectedStatements[0].SourceSpan.Start.ByteIndex;
        private int RangeEnd => selectedStatements[^1].SourceSpan.End.ByteIndex;

        private bool InRange(SourceSpan s) => s.Start.ByteIndex >= RangeStart && s.End.ByteIndex <= RangeEnd;
        private bool BeforeRange(SourceSpan s) => s.Start.ByteIndex < RangeStart;
        private bool AfterRange(SourceSpan s) => s.Start.ByteIndex >= RangeEnd;

        protected override void OnReset()
        {
            containingScope = null;
            containingBlock = null;
            selectedStatements.Clear();
        }
    }
}
```

- [ ] **Step 2: Capture the enclosing scope in `VisitBlock`**

Add inside the class (same trick as `ExtractLocalVariable.VisitBlock` — scope contexts are gone by `OnExitGlobalScope`):

```csharp
        public override void VisitBlock(BlockNode node)
        {
            if (HasSelection && node.SourceSpan.ContainsPosition(SelectionStart))
            {
                containingScope = GetCurrentScope();
            }
            base.VisitBlock(node);
        }
```

- [ ] **Step 3: Locate the contiguous statement range in `OnExitGlobalScope`**

Add the location logic. The deepest block whose span covers the trimmed selection wins; within it, take the sibling statements the selection touches and require the trimmed selection to align to their outer boundaries.

```csharp
        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            LocateStatementRange(node);
            if (selectedStatements.Count == 0) return; // SetFailure already called
            if (!PassesSafetyGuards()) return;         // SetFailure already called
            // Data-flow + generation added in later tasks.
        }

        private void LocateStatementRange(ProgramNode program)
        {
            if (!HasSelection)
            {
                SetFailure("Select the statements to extract. Extract Function/Method needs a selection because the cursor alone cannot delimit the range.");
                return;
            }
            if (containingScope == null)
            {
                SetFailure("Extract Function/Method only works inside a code block (method, function, getter/setter, or event body).");
                return;
            }

            int selStart = SelectionStart, selEnd = SelectionEnd;
            TrimWhitespace(ref selStart, ref selEnd);

            // Deepest block whose span covers the trimmed selection.
            BlockNode? best = null;
            foreach (var block in program.FindDescendants<BlockNode>())
            {
                if (block.SourceSpan.Start.ByteIndex <= selStart && block.SourceSpan.End.ByteIndex >= selEnd)
                {
                    if (best == null || block.SourceSpan.Start.ByteIndex >= best.SourceSpan.Start.ByteIndex)
                        best = block;
                }
            }
            if (best == null)
            {
                SetFailure("Selection must lie within a single statement block.");
                return;
            }

            // Sibling statements the selection overlaps, in source order.
            var touched = best.Statements
                .Where(s => s.SourceSpan.End.ByteIndex > selStart && s.SourceSpan.Start.ByteIndex < selEnd)
                .OrderBy(s => s.SourceSpan.Start.ByteIndex)
                .ToList();
            if (touched.Count == 0)
            {
                SetFailure("Selection must cover at least one whole statement.");
                return;
            }

            // Trimmed selection must align to the outer boundary of the touched statements.
            if (touched[0].SourceSpan.Start.ByteIndex < selStart || touched[^1].SourceSpan.End.ByteIndex > selEnd)
            {
                SetFailure("Selection must cover whole statements — it currently starts or ends inside a statement.");
                return;
            }

            containingBlock = best;
            selectedStatements.AddRange(touched);
        }
```

- [ ] **Step 4: Add the whitespace helpers (copied from ExtractLocalVariable)**

```csharp
        private void TrimWhitespace(ref int start, ref int end)
        {
            while (start < end && IsWhitespaceByte(SourceBytes[start])) start++;
            while (end > start && IsWhitespaceByte(SourceBytes[end - 1])) end--;
        }

        private static bool IsWhitespaceByte(byte b)
            => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
```

- [ ] **Step 5: Add the safety guards**

Block relocation-unsafe selections. `Break`/`Continue` are safe only when their nearest enclosing loop is itself inside the selection.

```csharp
        private bool PassesSafetyGuards()
        {
            foreach (var stmt in selectedStatements)
            {
                if (stmt.FindDescendants<ReturnStatementNode>().Any() || stmt is ReturnStatementNode)
                {
                    SetFailure("Cannot extract a selection that contains a Return — it would return from the new routine, not the original.");
                    return false;
                }

                foreach (var brk in DescendantsAndSelf(stmt).OfType<BreakStatementNode>())
                    if (!LoopEnclosesWithinSelection(brk))
                    { SetFailure("Cannot extract a Break that targets a loop outside the selection."); return false; }

                foreach (var cont in DescendantsAndSelf(stmt).OfType<ContinueStatementNode>())
                    if (!LoopEnclosesWithinSelection(cont))
                    { SetFailure("Cannot extract a Continue that targets a loop outside the selection."); return false; }
            }
            return true;
        }

        private static IEnumerable<AstNode> DescendantsAndSelf(AstNode node)
            => new[] { node }.Concat(node.FindDescendants<AstNode>());

        // True when the nearest enclosing loop of `node` is inside the selected range.
        private bool LoopEnclosesWithinSelection(AstNode node)
        {
            for (AstNode? cur = node.Parent; cur != null; cur = cur.Parent)
            {
                if (cur is ForStatementNode or WhileStatementNode or RepeatStatementNode)
                    return cur.SourceSpan.Start.ByteIndex >= RangeStart && cur.SourceSpan.End.ByteIndex <= RangeEnd;
            }
            return false; // no enclosing loop at all
        }
```

Note for the implementer: verify the exact node type names against `PeopleCodeParser.SelfHosted/Nodes/StatementNodes.cs` (`ReturnStatementNode`, `BreakStatementNode`, `ContinueStatementNode`, `ForStatementNode`, `WhileStatementNode`, `RepeatStatementNode`). If a `Break`/`Continue` node type is absent (some grammars fold them into a generic node), adjust the guard to match the actual AST and keep the same semantics.

- [ ] **Step 6: Build**

Ask Tim to run `dotnet build AppRefiner/AppRefiner.csproj` and confirm it compiles.

- [ ] **Step 7: Manual verification in Application Designer**

Because there is no generation yet, a valid selection succeeds as a **no-op** (zero edits is a valid success in `BaseRefactor`), and invalid selections show the failure message.

Input (a Function-based program or Component PeopleCode):
```
Local number &a = 1;
Local number &b = 2;
Local number &c;
&c = &a + &b;
MessageBox(0, "", 0, 0, "" | &c);
```
Verify:
1. Select the whole two lines `&c = &a + &b;` and the `MessageBox(...)` line → run "Extract Function/Method" → **no error, no change** (no-op).
2. Select only `&a + &b` (part of a statement) → run → failure: *"Selection must cover whole statements…"*.
3. Wrap the two lines in a `For &i = 1 To 3 … End-For;` and select a `Break;` inside plus a statement, but not the `For` → failure about Break. (If your parser models `Break`, otherwise skip.)

- [ ] **Step 8: Commit**

```bash
git add AppRefiner/Refactors/ExtractFunctionMethod.cs
git commit -m "feat(refactor): Extract Function/Method — statement-range location and safety guards"
```

---

### Task 2: Data-flow classification + function generation (void + single Return)

Generates a real `Function` for non-app-class programs. Placeholder name `ExtractedFunction` (dialog comes in Task 4). Handles **inputs**, **void**, and **a single pure output as `Returns`** (both declared-before and declared-inside). If 2+ outputs are found, block for now (out-params land in Task 3).

**Files:**
- Modify: `AppRefiner/Refactors/ExtractFunctionMethod.cs`

**Interfaces:**
- Produces (used by Tasks 3–5):
  - `private enum ParamRole { Value, InOut, Out }`
  - `private sealed class ParamPlan { VariableInfo Var; ParamRole Role; int OrderKey; bool DeclaredInside; }`
  - `private readonly List<ParamPlan> paramPlans` — ordered parameter list (value/in-out first by first-use, then out).
  - `private readonly List<ParamPlan> returnCandidates` — pure outputs eligible to be the `Returns` value.
  - `private ParamPlan? returnChoice` — the chosen return (defaulted here, overridable by the dialog in Task 4).
  - `private bool isAppClass` — set in Task 5.
  - `private string routineName` — placeholder now, set by dialog in Task 4.
  - `private void AnalyzeDataFlow()`, `private void GenerateFunction()`, `private string BuildSignatureParams()`, `private string ReindentBody(string raw, string blockIndent, string bodyIndent)`.

- [ ] **Step 1: Add the classification data structures and fields**

```csharp
        private enum ParamRole { Value, InOut, Out }

        private sealed class ParamPlan
        {
            public required VariableInfo Var;
            public required ParamRole Role;
            public int OrderKey;
            public bool DeclaredInside;
            public string Name => Var.Name.StartsWith('&') ? Var.Name : "&" + Var.Name;
            public string TypeName => string.IsNullOrWhiteSpace(Var.Type) ? "any" : Var.Type;
        }

        private readonly List<ParamPlan> paramPlans = new();
        private readonly List<ParamPlan> returnCandidates = new();
        private ParamPlan? returnChoice;
        private string routineName = "ExtractedFunction";
```

Extend `OnReset()` to clear `paramPlans`, `returnCandidates`, and set `returnChoice = null`, `routineName = "ExtractedFunction"`.

- [ ] **Step 2: Implement data-flow classification**

Reference buckets come straight from `VariableInfo.References` (each has `ReferenceType` + `SourceSpan`). Candidate vars are locals/params/exception vars accessible from `containingScope`. Instance/global/component/constant vars are reachable from the extracted routine unchanged, so they are never parameters.

```csharp
        private void AnalyzeDataFlow()
        {
            var candidates = VariableRegistry.GetAccessibleVariables(containingScope!)
                .Where(v => v.Kind is VariableKind.Local or VariableKind.Parameter or VariableKind.Exception);

            foreach (var v in candidates)
            {
                var reads  = v.References.Where(r => r.ReferenceType == ReferenceType.Read).ToList();
                var writes = v.References.Where(r => r.ReferenceType == ReferenceType.Write).ToList();

                bool readInside  = reads.Any(r => InRange(r.SourceSpan));
                bool writeInside = writes.Any(r => InRange(r.SourceSpan));
                bool readAfter   = reads.Any(r => AfterRange(r.SourceSpan));

                int firstWriteInside = writes.Where(r => InRange(r.SourceSpan))
                                             .Select(r => r.SourceSpan.Start.ByteIndex)
                                             .DefaultIfEmpty(int.MaxValue).Min();
                bool readInsideBeforeFirstWrite = reads.Any(r => InRange(r.SourceSpan)
                                             && r.SourceSpan.Start.ByteIndex < firstWriteInside);

                var declSpan = v.DeclarationReference?.SourceSpan;
                bool declaredInside = declSpan.HasValue && InRange(declSpan.Value);
                bool definedBefore = v.Kind == VariableKind.Parameter
                                     || (declSpan.HasValue && declSpan.Value.Start.ByteIndex < RangeStart)
                                     || writes.Any(r => BeforeRange(r.SourceSpan))
                                     || reads.Any(r => BeforeRange(r.SourceSpan));

                bool needsInput = definedBefore && readInsideBeforeFirstWrite;
                bool isOutput = writeInside && readAfter;

                int order = v.References.Where(r => InRange(r.SourceSpan))
                             .Select(r => r.SourceSpan.Start.ByteIndex)
                             .DefaultIfEmpty(int.MaxValue).Min();

                if (needsInput && isOutput)
                    paramPlans.Add(new ParamPlan { Var = v, Role = ParamRole.InOut, OrderKey = order, DeclaredInside = declaredInside });
                else if (needsInput)
                    paramPlans.Add(new ParamPlan { Var = v, Role = ParamRole.Value, OrderKey = order, DeclaredInside = declaredInside });
                else if (isOutput)
                    returnCandidates.Add(new ParamPlan { Var = v, Role = ParamRole.Out, OrderKey = order, DeclaredInside = declaredInside });
                // else: internal-only, ignored.
            }

            // Deterministic order: value/in-out params by first use inside the range.
            paramPlans.Sort((x, y) => x.OrderKey.CompareTo(y.OrderKey));
            returnCandidates.Sort((x, y) => x.OrderKey.CompareTo(y.OrderKey));

            // Default return: prefer a candidate declared inside the range, else the first.
            returnChoice = returnCandidates.FirstOrDefault(c => c.DeclaredInside) ?? returnCandidates.FirstOrDefault();
        }
```

Implementer note: confirm `VariableRegistry.GetAccessibleVariables(ScopeContext)` exists (see `PeopleCodeParser.SelfHosted/Visitors/Models/VariableRegistry.cs` around line 120, "Gets all variables accessible from a specific scope"). If the method name differs, use the actual accessor; the intent is "all locals/params visible in `containingScope`, including its parents."

- [ ] **Step 3: Temporarily block 2+ outputs (removed in Task 3)**

At the end of `OnExitGlobalScope`, after `AnalyzeDataFlow()`:

```csharp
            AnalyzeDataFlow();

            var nonReturnOutputs = returnCandidates.Where(c => c != returnChoice).ToList();
            if (nonReturnOutputs.Count > 0)
            {
                SetFailure("This selection produces more than one output ("
                    + string.Join(", ", returnCandidates.Select(c => c.Name))
                    + "). Multi-output extraction via `out` parameters is not available yet.");
                return;
            }

            GenerateFunction();
```

- [ ] **Step 4: Implement the re-indent helper**

```csharp
        private string ReindentBody(string raw, string blockIndent, string bodyIndent)
        {
            var lines = raw.Replace("\r\n", "\n").Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (i == 0)
                {
                    sb.Append(bodyIndent).Append(line.TrimStart());
                }
                else if (line.Length == 0)
                {
                    // preserve blank lines
                }
                else
                {
                    string stripped = line.StartsWith(blockIndent) ? line.Substring(blockIndent.Length) : line.TrimStart();
                    sb.Append(bodyIndent).Append(stripped);
                }
                if (i < lines.Length - 1) sb.Append(NewLine);
            }
            return sb.ToString();
        }
```

- [ ] **Step 5: Implement signature-parameter rendering**

```csharp
        // Renders the parenthesised parameter list for a signature, e.g.
        // "&a As number, &acc As string out". Out/in-out params get the ` out` suffix.
        private string BuildSignatureParams()
        {
            return string.Join(", ", paramPlans.Select(p =>
                $"{p.Name} As {p.TypeName}{(p.Role == ParamRole.Value ? "" : " out")}"));
        }

        // Comma-joined argument names for the call site, in the same order.
        private string BuildCallArgs()
            => string.Join(", ", paramPlans.Select(p => p.Name));
```

- [ ] **Step 6: Implement function generation (void + single Return)**

```csharp
        private void GenerateFunction()
        {
            string blockIndent = GetLineIndent(RangeStart);
            string bodyIndent = "   "; // functions live at column 0; body is one level in
            string retType = returnChoice != null ? returnChoice.TypeName : null;

            // --- Function body ---
            string rawBody = GetSourceText(RangeStart, RangeEnd);
            var body = new System.Text.StringBuilder();

            // A pure-output Return var declared BEFORE the range needs a fresh local inside.
            if (returnChoice != null && !returnChoice.DeclaredInside)
                body.Append(bodyIndent).Append($"Local {returnChoice.TypeName} {returnChoice.Name};").Append(NewLine);

            body.Append(ReindentBody(rawBody, blockIndent, bodyIndent));

            if (returnChoice != null)
                body.Append(NewLine).Append(bodyIndent).Append($"Return {returnChoice.Name};");

            // --- Function block ---
            string returnsClause = retType != null ? $" Returns {retType}" : "";
            string funcText =
                $"Function {routineName}({BuildSignatureParams()}){returnsClause}{NewLine}" +
                body + NewLine +
                $"End-Function;{NewLine}{NewLine}";

            // --- Call site ---
            string call = $"{routineName}({BuildCallArgs()})";
            string callStmt = returnChoice == null
                ? $"{call};"
                : returnChoice.DeclaredInside
                    ? $"Local {returnChoice.TypeName} {returnChoice.Name} = {call};"
                    : $"{returnChoice.Name} = {call};";

            // Replace the selected statements with the call, then insert the function above.
            EditText(RangeStart, RangeEnd, callStmt, "Replace statements with function call");
            int insertAt = FunctionInsertionIndex();
            InsertText(insertAt, funcText, $"Insert function '{routineName}'");
        }
```

- [ ] **Step 7: Implement the function insertion point**

Insert above the function/main block that contains the selection (mirrors `MoveFunctionAbove`'s destination logic). Add near the other helpers:

```csharp
        // Byte index of the line where the new Function block should be inserted:
        // the start line (incl. leading comments) of the enclosing function
        // implementation, or the start of the main block otherwise.
        private int FunctionInsertionIndex()
        {
            var program = selectedStatements[0].GetRoot() as ProgramNode;
            var enclosingFunc = program?.Functions.FirstOrDefault(f => f.IsImplementation
                && f.SourceSpan.Start.ByteIndex <= RangeStart
                && f.SourceSpan.End.ByteIndex >= RangeEnd);

            int line;
            if (enclosingFunc != null)
            {
                var firstComment = enclosingFunc.GetLeadingComments().FirstOrDefault();
                line = firstComment != null
                    ? Math.Min(firstComment.SourceSpan.Start.Line, enclosingFunc.SourceSpan.Start.Line)
                    : enclosingFunc.SourceSpan.Start.Line;
            }
            else if (program?.MainBlock != null)
            {
                line = program.MainBlock.SourceSpan.Start.Line;
            }
            else
            {
                line = selectedStatements[0].SourceSpan.Start.Line;
            }
            return ScintillaManager.GetLineStartIndex(Editor, line);
        }
```

Implementer note: `EditText` (replace) and `InsertText` must not collide. Here the replace span `[RangeStart, RangeEnd)` and the insert index (a line start at/above the enclosing function) never share a byte, and `BaseRefactor` sorts edits by descending `StartIndex`, so ordering is safe. Keep an eye on the `InsertText` dedup shortcut noted in `MoveFunctionAbove` if you later add a same-position insert.

- [ ] **Step 8: Build**

Ask Tim to build `AppRefiner/AppRefiner.csproj`.

- [ ] **Step 9: Manual verification**

**Scenario A — inputs + void.** Input:
```
Function DoWork()
   Local number &a = 1;
   Local number &b = 2;
   MessageBox(0, "", 0, 0, "sum=" | (&a + &b));
End-Function;
```
Select the `MessageBox(...)` line → extract. Expected: a new `Function ExtractedFunction(&a As number, &b As number)` above `DoWork` whose body is the MessageBox line, and the call site becomes `ExtractedFunction(&a, &b);`.

**Scenario B — single Return, declared inside.** Input body:
```
   Local number &a = 1;
   Local number &b = 2;
   Local number &c = &a + &b;
   MessageBox(0, "", 0, 0, "" | &c);
```
Select the single line `Local number &c = &a + &b;`. Expected: `Function ExtractedFunction(&a As number, &b As number) Returns number` with body `Local number &c = &a + &b;` + `Return &c;`, and the call site becomes `Local number &c = ExtractedFunction(&a, &b);`.

**Scenario C — single Return, declared before.** Input body:
```
   Local number &a = 1;
   Local number &total;
   &total = &a * 10;
   MessageBox(0, "", 0, 0, "" | &total);
```
Select `&total = &a * 10;`. Expected: `Function ExtractedFunction(&a As number) Returns number` whose body is `Local number &total;` + `&total = &a * 10;` + `Return &total;`; call site `&total = ExtractedFunction(&a);`. The original `Local number &total;` declaration stays in the caller.

**Scenario D — multi-output block.** Select both `&total = &a * 10;` and a second `&other = &a + 1;` (with `&other` read later). Expected: failure naming `&total, &other`.

- [ ] **Step 10: Commit**

```bash
git add AppRefiner/Refactors/ExtractFunctionMethod.cs
git commit -m "feat(refactor): function extraction with inputs and single Return"
```

---

### Task 3: `out` parameters and caller-side declaration relocation

Lift the multi-output block. Non-return outputs become `out` parameters; in/out variables are already `out`. Apply the declaration-relocation matrix.

**Files:**
- Modify: `AppRefiner/Refactors/ExtractFunctionMethod.cs`

**Interfaces:**
- Consumes: `paramPlans`, `returnCandidates`, `returnChoice`, `BuildSignatureParams`, `BuildCallArgs`, `GenerateFunction` (Task 2).
- Produces: revised `GenerateFunction` handling out-params; `MoveOutputsIntoParams()`; body-decl stripping for out-params declared inside.

Declaration-relocation matrix (implement exactly):

| Var declared | Chosen role | Function body | Caller |
|---|---|---|---|
| before range | Return | prepend `Local T &x;`, append `Return &x;` | `&x = Foo(...)` |
| before range | out param | `&x` is a param — no body decl | `Foo(..., &x)` (already declared) |
| inside range | Return | keep its `Local T &x = …;`, append `Return &x;` | `Local T &x = Foo(...)` |
| inside range | out param | strip `Local T ` from its declaration in the body | add `Local T &x;` before the call |

- [ ] **Step 1: Remove the multi-output block and fold outputs into params**

Replace the block added in Task 2 Step 3 with:

```csharp
            AnalyzeDataFlow();
            MoveOutputsIntoParams();
            GenerateFunction();
```

Add:

```csharp
        // Every output that isn't the chosen Return becomes an `out` parameter,
        // appended after the value/in-out params in first-use order.
        private void MoveOutputsIntoParams()
        {
            foreach (var outVar in returnCandidates.Where(c => c != returnChoice).OrderBy(c => c.OrderKey))
                paramPlans.Add(new ParamPlan
                {
                    Var = outVar.Var,
                    Role = ParamRole.Out,
                    OrderKey = outVar.OrderKey,
                    DeclaredInside = outVar.DeclaredInside
                });
        }
```

`BuildSignatureParams` already renders `out` for non-`Value` roles, and value/in-out params sort ahead of appended out params because they were sorted in `AnalyzeDataFlow`; the appended out params keep source order among themselves. (If you want a strict "value, then in/out, then out" grouping, sort `paramPlans` by `(Role == Value ? 0 : Role == InOut ? 1 : 2, OrderKey)` at the end of `MoveOutputsIntoParams`.)

- [ ] **Step 2: Strip `Local T ` from out-param declarations inside the body, and declare them at the caller**

Update `GenerateFunction`. After computing `rawBody`, transform it so out-params declared inside the range lose their `Local <type> ` prefix (they are parameters now). Then emit caller-side `Local T &x;` lines before the call.

Replace the body/caller portions of `GenerateFunction` with:

```csharp
            // Out-params declared inside the range: their `Local <type> ` prefix must go.
            var outParamsDeclaredInside = paramPlans
                .Where(p => p.Role != ParamRole.Value && p.DeclaredInside && p != returnChoice)
                .ToList();

            string rawBody = StripLocalPrefixes(GetSourceText(RangeStart, RangeEnd), outParamsDeclaredInside);

            var body = new System.Text.StringBuilder();
            if (returnChoice != null && !returnChoice.DeclaredInside)
                body.Append(bodyIndent).Append($"Local {returnChoice.TypeName} {returnChoice.Name};").Append(NewLine);
            body.Append(ReindentBody(rawBody, blockIndent, bodyIndent));
            if (returnChoice != null)
                body.Append(NewLine).Append(bodyIndent).Append($"Return {returnChoice.Name};");
```

And the call site:

```csharp
            string call = $"{routineName}({BuildCallArgs()})";
            string callStmt = returnChoice == null
                ? $"{call};"
                : returnChoice.DeclaredInside
                    ? $"Local {returnChoice.TypeName} {returnChoice.Name} = {call};"
                    : $"{returnChoice.Name} = {call};";

            // Caller must declare out-params that were originally declared inside the range.
            var prefix = new System.Text.StringBuilder();
            foreach (var p in outParamsDeclaredInside)
                prefix.Append($"Local {p.TypeName} {p.Name};{NewLine}{blockIndent}");

            EditText(RangeStart, RangeEnd, prefix + callStmt, "Replace statements with function call");
```

Add the helper. It rewrites `Local <type> &x [= …];` to `&x [= …];` for each named out-param, operating on the raw body slice (before re-indent). Match at the start of a line (after optional whitespace).

```csharp
        // Removes the `Local <type> ` keyword+type prefix from the in-body declaration
        // of each given variable, leaving the assignment (or bare name) intact.
        private static string StripLocalPrefixes(string rawBody, List<ParamPlan> vars)
        {
            foreach (var p in vars)
            {
                string name = System.Text.RegularExpressions.Regex.Escape(p.Name);
                // e.g. "   Local number &x = ..."  ->  "   &x = ..."
                var rx = new System.Text.RegularExpressions.Regex(
                    @"(?im)^(\s*)Local\s+[^;&]+?\s+(" + name + @"\b)");
                rawBody = rx.Replace(rawBody, "$1$2", 1);
            }
            return rawBody;
        }
```

Implementer note: PeopleCode local declarations can combine multiple names (`Local number &x, &y;`). If an out-param shares a combined declaration, the simple prefix strip is wrong. For v1 this is an accepted limitation — add a guard: if `StripLocalPrefixes` would touch a declaration node with more than one name, `SetFailure("Cannot extract: output variable '<name>' shares a combined Local declaration. Split it first.")`. Detect via the AST: find the `LocalVariableDeclarationNode` (or `LocalVariableDeclarationWithAssignmentNode`) for the var inside the range and check its declared-name count before generating.

- [ ] **Step 3: Build**

Ask Tim to build.

- [ ] **Step 4: Manual verification**

**Scenario A — two outputs, one Return + one out (declared before).** Input body:
```
   Local number &a = 1;
   Local number &sum;
   Local number &prod;
   &sum = &a + 10;
   &prod = &a * 10;
   MessageBox(0, "", 0, 0, "" | &sum | &prod);
```
Select the two assignment lines. Expected signature `Function ExtractedFunction(&a As number, &prod As number out) Returns number` (default Return = `&sum`, first candidate), body assigns both + `Return &sum;`, call site `&sum = ExtractedFunction(&a, &prod);`. Both `&sum`/`&prod` keep their caller declarations.

**Scenario B — out param declared inside.** Input body:
```
   Local number &a = 1;
   Local number &sum;
   &sum = &a + 10;
   Local number &prod = &a * 10;
   MessageBox(0, "", 0, 0, "" | &sum | &prod);
```
Select `&sum = &a + 10;` through `Local number &prod = &a * 10;`. With Return defaulting to `&prod` (declared inside), verify `&sum` becomes the `out` param: body strips nothing for `&prod` (it's the Return) — actually confirm the default prefers declared-inside `&prod` as Return; then `&sum` is out (declared before → no relocation). To exercise the strip path, re-run picking Return = `&sum` (Task 4 dialog) — then `&prod` is an out param declared inside, so the body line becomes `&prod = &a * 10;` and the caller gains `Local number &prod;` before the call. (For this task, temporarily force `returnChoice` to `&sum` to verify, then revert.)

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/Refactors/ExtractFunctionMethod.cs
git commit -m "feat(refactor): out parameters and caller-side declaration relocation"
```

---

### Task 4: Extraction dialog (name, Return picker, signature preview)

Adds the deferred dialog: routine name, a Return-value dropdown, and a live signature preview. Visibility dropdown is added in Task 5 (methods only) — build the dialog now so it already has the slot.

**Files:**
- Modify: `AppRefiner/Refactors/ExtractFunctionMethod.cs`

**Interfaces:**
- Consumes: `paramPlans`, `returnCandidates`, `returnChoice`, `routineName`, `BuildSignatureParams`.
- Produces: `ShowRefactorDialog()` override; nested `ExtractRoutineDialog : Form`. Sets `routineName` and `returnChoice`, then calls the Task 2/3 generation.

- [ ] **Step 1: Restructure so generation runs after the dialog**

The dialog needs the analysis but generation must use the user's choices. Change `OnExitGlobalScope` to stop after analysis; move `MoveOutputsIntoParams()` + `GenerateFunction()` into `ShowRefactorDialog()` after the dialog returns OK.

```csharp
        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            LocateStatementRange(node);
            if (selectedStatements.Count == 0) return;
            if (!PassesSafetyGuards()) return;
            AnalyzeDataFlow();
        }

        public override bool ShowRefactorDialog()
        {
            if (selectedStatements.Count == 0) return false; // failure already set

            using var dialog = new ExtractRoutineDialog(routineName, returnCandidates, returnChoice,
                p => BuildPreview(p), IsRoutineNameTaken);
            var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
            if (dialog.ShowDialog(wrapper) != DialogResult.OK) return false;

            routineName = dialog.RoutineName;
            returnChoice = dialog.ReturnChoice;

            MoveOutputsIntoParams();
            GenerateFunction();
            return true;
        }
```

- [ ] **Step 2: Add the preview + name-collision helpers**

```csharp
        // Live signature preview for the dialog. `preview` is the tentative return choice.
        private string BuildPreview(ParamPlan? preview)
        {
            // Recompute params for the tentative choice without mutating state.
            var tentative = paramPlans.ToList();
            foreach (var o in returnCandidates.Where(c => c != preview))
                tentative.Add(new ParamPlan { Var = o.Var, Role = ParamRole.Out, OrderKey = o.OrderKey, DeclaredInside = o.DeclaredInside });
            string prms = string.Join(", ", tentative.Select(p =>
                $"{p.Name} As {p.TypeName}{(p.Role == ParamRole.Value ? "" : " out")}"));
            string ret = preview != null ? $" Returns {preview.TypeName}" : "";
            return $"Function {dialogName}({prms}){ret}";
        }
```

Note: `BuildPreview` reads a display name. Pass the current textbox value from the dialog into the preview delegate instead of a field — simplest is to have the dialog compute `Function <name>(...)` itself by combining the name box with the params string the delegate returns. To keep the refactor as the single source of the params string, change the delegate to `Func<ParamPlan?, string>` returning only the `(<params>) Returns <T>` tail, and let the dialog prepend `Function `/`method ` + name. Implement whichever is cleaner; the goal is a preview that updates on name and return changes.

```csharp
        private bool IsRoutineNameTaken(string name)
        {
            var program = selectedStatements[0].GetRoot() as ProgramNode;
            if (program == null) return false;
            bool fnClash = program.Functions.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            bool mClash = program.AppClass?.Methods.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false;
            return fnClash || mClash;
        }
```

- [ ] **Step 3: Add the dialog class**

Model closely on `ExtractLocalVariable.ExtractVariableDialog` (borderless, dark header, Enter/Escape handling). Controls: name textbox, a Return `ComboBox` (items = candidates + "(none — all via out params)"), a **Visibility** `ComboBox` (created but hidden; shown in Task 5), a preview `Label`, OK/Cancel. Nest it inside `ExtractFunctionMethod`.

```csharp
        private sealed class ExtractRoutineDialog : Form
        {
            private readonly TextBox txtName = new();
            private readonly ComboBox cboReturn = new();
            private readonly ComboBox cboVisibility = new();
            private readonly Label lblPreview = new();
            private readonly Label lblError = new();
            private readonly Button btnOk = new();
            private readonly Button btnCancel = new();
            private readonly Panel headerPanel = new();
            private readonly Label headerLabel = new();

            private readonly List<ParamPlan> candidates;
            private readonly Func<string, bool> isNameTaken;
            private readonly Func<ParamPlan?, string> buildTail; // returns "(<params>) Returns <T>"

            public string RoutineName { get; private set; }
            public ParamPlan? ReturnChoice { get; private set; }
            public bool ShowVisibility { get; set; }               // set true by Task 5
            public PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier Visibility { get; private set; }
                = PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Private;

            public ExtractRoutineDialog(string suggestedName, List<ParamPlan> returnCandidates,
                ParamPlan? defaultReturn, Func<ParamPlan?, string> buildTail, Func<string, bool> isNameTaken)
            {
                this.candidates = returnCandidates;
                this.isNameTaken = isNameTaken;
                this.buildTail = buildTail;
                RoutineName = suggestedName;
                ReturnChoice = defaultReturn;
                InitializeComponent();
                txtName.Text = suggestedName;
                PopulateReturn(defaultReturn);
                ActiveControl = txtName;
                txtName.SelectAll();
                UpdatePreview();
            }

            private void PopulateReturn(ParamPlan? defaultReturn)
            {
                cboReturn.Items.Add("(none — all via out params)");
                foreach (var c in candidates) cboReturn.Items.Add(c.Name + " As " + c.TypeName);
                cboReturn.SelectedIndex = defaultReturn == null ? 0 : candidates.IndexOf(defaultReturn) + 1;
            }

            private void UpdatePreview()
            {
                ReturnChoice = cboReturn.SelectedIndex <= 0 ? null : candidates[cboReturn.SelectedIndex - 1];
                string kind = ShowVisibility
                    ? $"{cboVisibility.SelectedItem} method" : "Function";
                lblPreview.Text = $"{kind} {txtName.Text}{buildTail(ReturnChoice)}";
            }

            private void InitializeComponent()
            {
                SuspendLayout();
                headerPanel.BackColor = Color.FromArgb(50, 50, 60);
                headerPanel.Dock = DockStyle.Top; headerPanel.Height = 30; headerPanel.Controls.Add(headerLabel);
                headerLabel.Text = "Extract Function/Method"; headerLabel.ForeColor = Color.White;
                headerLabel.Dock = DockStyle.Fill; headerLabel.TextAlign = ContentAlignment.MiddleCenter;
                headerLabel.Font = new Font("Segoe UI", 9F);

                var lblName = new Label { AutoSize = true, Location = new Point(12, 40), Text = "Function/method name:" };
                txtName.BorderStyle = BorderStyle.FixedSingle; txtName.Location = new Point(12, 60);
                txtName.Size = new Size(320, 23); txtName.Font = new Font("Segoe UI", 11F);
                txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };
                txtName.TextChanged += (s, e) => UpdatePreview();

                var lblVis = new Label { AutoSize = true, Location = new Point(12, 90), Text = "Visibility:" };
                cboVisibility.DropDownStyle = ComboBoxStyle.DropDownList; cboVisibility.Location = new Point(80, 87);
                cboVisibility.Size = new Size(120, 23);
                cboVisibility.Items.AddRange(new object[] { "Private", "Protected", "Public" });
                cboVisibility.SelectedIndex = 0;
                cboVisibility.SelectedIndexChanged += (s, e) =>
                {
                    Visibility = cboVisibility.SelectedIndex switch
                    {
                        1 => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Protected,
                        2 => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Public,
                        _ => PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Private,
                    };
                    UpdatePreview();
                };

                var lblRet = new Label { AutoSize = true, Location = new Point(12, 120), Text = "Return value:" };
                cboReturn.DropDownStyle = ComboBoxStyle.DropDownList; cboReturn.Location = new Point(90, 117);
                cboReturn.Size = new Size(242, 23);
                cboReturn.SelectedIndexChanged += (s, e) => UpdatePreview();

                lblPreview.AutoSize = false; lblPreview.Location = new Point(12, 150); lblPreview.Size = new Size(320, 40);
                lblPreview.ForeColor = Color.FromArgb(90, 90, 100); lblPreview.Font = new Font("Consolas", 8.5F);

                lblError.AutoSize = true; lblError.Location = new Point(12, 195); lblError.ForeColor = Color.Firebrick;

                btnOk.Text = "&OK"; btnOk.Location = new Point(176, 216); btnOk.Size = new Size(75, 28);
                btnOk.Click += BtnOk_Click;
                btnCancel.Text = "&Cancel"; btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(257, 216); btnCancel.Size = new Size(75, 28);

                AcceptButton = btnOk; CancelButton = btnCancel;
                ClientSize = new Size(344, 256);
                Controls.AddRange(new Control[] { headerPanel, lblName, txtName, lblVis, cboVisibility,
                    lblRet, cboReturn, lblPreview, lblError, btnOk, btnCancel });
                FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent;
                ShowInTaskbar = false; MaximizeBox = false; MinimizeBox = false;

                // Visibility row hidden by default (functions); Task 5 sets ShowVisibility=true for methods.
                lblVis.Visible = cboVisibility.Visible = false;
                Load += (s, e) => { lblVis.Visible = cboVisibility.Visible = ShowVisibility; UpdatePreview(); };

                ResumeLayout(false); PerformLayout();
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                var name = txtName.Text.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$"))
                { lblError.Text = "Not a valid function/method name."; return; }
                if (isNameTaken(name))
                { lblError.Text = $"'{name}' is already declared in this program."; return; }
                RoutineName = name;
                ReturnChoice = cboReturn.SelectedIndex <= 0 ? null : candidates[cboReturn.SelectedIndex - 1];
                DialogResult = DialogResult.OK; Close();
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
```

Implementer note on `buildTail`: change the refactor's `BuildPreview` to return only the tail `"(<params>) Returns <T>"` (no leading `Function`/name), so the dialog owns the `Function`/`method` + name prefix. Update the `ShowRefactorDialog` construction to pass `p => BuildPreviewTail(p)`.

- [ ] **Step 4: Add `BuildPreviewTail`**

```csharp
        private string BuildPreviewTail(ParamPlan? preview)
        {
            var tentative = paramPlans.ToList();
            foreach (var o in returnCandidates.Where(c => c != preview))
                tentative.Add(new ParamPlan { Var = o.Var, Role = ParamRole.Out, OrderKey = o.OrderKey, DeclaredInside = o.DeclaredInside });
            string prms = string.Join(", ", tentative.Select(p =>
                $"{p.Name} As {p.TypeName}{(p.Role == ParamRole.Value ? "" : " out")}"));
            string ret = preview != null ? $" Returns {preview.TypeName}" : "";
            return $"({prms}){ret}";
        }
```

Remove the earlier `BuildPreview` sketch from Step 2 (superseded by `BuildPreviewTail`).

- [ ] **Step 5: Build**

Ask Tim to build.

- [ ] **Step 6: Manual verification**

Re-run Task 2 Scenario B: extract → dialog appears with name `ExtractedFunction`, Return preselected to `&c`, preview shows `Function ExtractedFunction(&a As number, &b As number) Returns number`. Rename to `ComputeSum`, press OK → generated function and call use `ComputeSum`. Re-run and set Return to "(none)" → verify `&c` becomes an `out` param and the call is `ComputeSum(&a, &b, &c);` with `&c` declared appropriately. Enter a name that collides with an existing function → inline error, no close.

- [ ] **Step 7: Commit**

```bash
git add AppRefiner/Refactors/ExtractFunctionMethod.cs
git commit -m "feat(refactor): extraction dialog with name, return picker, and signature preview"
```

---

### Task 5: Method extraction for app classes

Detect app-class context and emit a private (or chosen-visibility) method: a declaration in the class header and an implementation appended after the last method impl, with a `%This.` call site. Instance vars / `%This` are already excluded from params by Task 2's candidate filter.

**Files:**
- Modify: `AppRefiner/Refactors/ExtractFunctionMethod.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–4.
- Produces: `isAppClass`, `AppClassNode? appClass`, `VisibilityModifier chosenVisibility`, `GenerateMethod()`, method decl/impl insertion helpers.

- [ ] **Step 1: Detect app-class context**

In `OnExitGlobalScope`, after `LocateStatementRange` succeeds, set the app-class fields from the program:

```csharp
            var program = node;
            appClass = program.AppClass;
            isAppClass = appClass != null;
```

Add fields: `private bool isAppClass; private AppClassNode? appClass; private PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier chosenVisibility = PeopleCodeParser.SelfHosted.Nodes.VisibilityModifier.Private;`. Reset them in `OnReset`. Default `routineName` to `"ExtractedMethod"` when `isAppClass`.

- [ ] **Step 2: Show visibility in the dialog and branch generation**

In `ShowRefactorDialog`, set `dialog.ShowVisibility = isAppClass;` before `ShowDialog`, read `chosenVisibility = dialog.Visibility;` after OK, and branch:

```csharp
            routineName = dialog.RoutineName;
            returnChoice = dialog.ReturnChoice;
            chosenVisibility = dialog.Visibility;

            MoveOutputsIntoParams();
            if (isAppClass) GenerateMethod();
            else GenerateFunction();
            return true;
```

- [ ] **Step 3: Implement method generation**

Body/return logic is identical to `GenerateFunction`; only the wrapper (declaration + implementation) and call site differ. Factor the shared body build into `BuildRoutineBody(out string blockIndent)` and reuse it in both. Method call site uses `%This.`.

```csharp
        // Shared body builder used by both function and method generation.
        // Returns the fully re-indented body text (incl. any Local decl and Return),
        // and reports the out-params that were declared inside the range (for caller decls).
        private string BuildRoutineBody(string bodyIndent, out string blockIndent, out List<ParamPlan> outParamsDeclaredInside)
        {
            blockIndent = GetLineIndent(RangeStart);
            outParamsDeclaredInside = paramPlans
                .Where(p => p.Role != ParamRole.Value && p.DeclaredInside && p != returnChoice)
                .ToList();

            string rawBody = StripLocalPrefixes(GetSourceText(RangeStart, RangeEnd), outParamsDeclaredInside);
            var body = new System.Text.StringBuilder();
            if (returnChoice != null && !returnChoice.DeclaredInside)
                body.Append(bodyIndent).Append($"Local {returnChoice.TypeName} {returnChoice.Name};").Append(NewLine);
            body.Append(ReindentBody(rawBody, blockIndent, bodyIndent));
            if (returnChoice != null)
                body.Append(NewLine).Append(bodyIndent).Append($"Return {returnChoice.Name};");
            return body.ToString();
        }
```

Refactor `GenerateFunction` to call `BuildRoutineBody("   ", out var blockIndent, out var outDecls)` and use `outDecls` for the caller prefix (same call-site code as Task 3). Then add:

```csharp
        private void GenerateMethod()
        {
            string body = BuildRoutineBody("   ", out string blockIndent, out var outParamsDeclaredInside);
            string retType = returnChoice?.TypeName;

            // --- Declaration line in the class header ---
            string returnsDecl = retType != null ? $" returns {retType}" : "";
            string declLine = $"   {VisibilityKeyword()}method {routineName}({BuildSignatureParams()}){returnsDecl};{NewLine}";
            // Note: PeopleCode groups members under `public`/`protected`/`private` section
            // headers, not per-member keywords. VisibilityKeyword() returns "" — see Step 4.

            // --- Implementation (after the last method impl) ---
            var annotations = new System.Text.StringBuilder();
            for (int i = 0; i < paramPlans.Count; i++)
            {
                var p = paramPlans[i];
                string outMod = p.Role == ParamRole.Value ? "" : " out";
                string comma = i < paramPlans.Count - 1 ? "," : "";
                annotations.Append($"   /+ {p.Name} as {p.TypeName}{outMod}{comma} +/").Append(NewLine);
            }
            if (retType != null) annotations.Append($"   /+ Returns {retType} +/").Append(NewLine);

            string impl =
                $"{NewLine}method {routineName}{NewLine}" +
                annotations +
                body + NewLine +
                $"end-method;{NewLine}";

            // --- Call site (%This.) ---
            string call = $"%This.{routineName}({BuildCallArgs()})";
            string callStmt = returnChoice == null
                ? $"{call};"
                : returnChoice.DeclaredInside
                    ? $"Local {returnChoice.TypeName} {returnChoice.Name} = {call};"
                    : $"{returnChoice.Name} = {call};";
            var prefix = new System.Text.StringBuilder();
            foreach (var p in outParamsDeclaredInside)
                prefix.Append($"Local {p.TypeName} {p.Name};{NewLine}{blockIndent}");

            EditText(RangeStart, RangeEnd, prefix + callStmt, "Replace statements with method call");
            InsertText(MethodDeclInsertionIndex(), declLine, $"Insert method declaration '{routineName}'");
            InsertText(MethodImplInsertionIndex(), impl, $"Insert method implementation '{routineName}'");
        }
```

- [ ] **Step 4: Implement visibility placement and insertion points**

PeopleCode class headers group members under `public`/`protected`/`private` **section headers**, not per-member keywords, so a member's visibility is determined by which section it sits in. Insert the declaration line at the end of the chosen visibility section; create the section header if absent.

```csharp
        // Members carry no per-line visibility keyword; the section they live in decides it.
        private string VisibilityKeyword() => "";

        // Byte index at which to insert the new method declaration: end of the chosen
        // visibility section within the class header. Uses AppClassNode.VisibilitySections.
        private int MethodDeclInsertionIndex()
        {
            var section = appClass!.VisibilitySections.TryGetValue(chosenVisibility, out var members) ? members : null;
            if (section != null && section.Count > 0)
            {
                var last = section.OrderBy(m => m.SourceSpan.End.ByteIndex).Last();
                return ScintillaManager.GetLineStartIndex(Editor, last.SourceSpan.End.Line + 1);
            }
            // No existing section: for Private, App Designer convention is a `private`
            // header near the end of the class block. Insert a section header + the decl.
            // (See implementer note below.)
            int classEndLine = appClass.SourceSpan.End.Line; // line of `end-class;`
            return ScintillaManager.GetLineStartIndex(Editor, classEndLine);
        }

        // Byte index after the last existing method implementation (or class end).
        private int MethodImplInsertionIndex()
        {
            var lastImpl = appClass!.Methods
                .Where(m => m.IsImplementation && m.Implementation != null)
                .OrderBy(m => m.Implementation!.SourceSpan.End.ByteIndex)
                .LastOrDefault();
            if (lastImpl?.Implementation != null)
                return lastImpl.Implementation.SourceSpan.End.ByteIndex + 1;
            return appClass.SourceSpan.End.ByteIndex + 1;
        }
```

Implementer notes:
- Confirm `AppClassNode.VisibilitySections` keys are `VisibilityModifier` and values are member-node lists (see `ProgramNodes.cs:389`). Confirm `AppClassNode.Methods[i].Implementation` and `.IsImplementation` exist (used in `GenerateBaseConstructor.FindImplementationInsertionPosition`).
- When the chosen visibility section is absent, emit the section header keyword line too (`private`/`protected`) before the declaration. Detect presence via `VisibilitySections[chosenVisibility].Count`. Keep this minimal: if the section is missing, prepend `{NewLine}   {keyword.ToLower()}{NewLine}` (e.g. `   private`) before `declLine`. `public` needs no header (default), but a fresh `public` decl can go right after the class/extends line — reuse `GenerateBaseConstructor.FindHeaderInsertionPosition` as a reference.
- Watch multi-edit ordering: three edits here (one replace + two inserts) at distinct byte positions; `BaseRefactor` sorts descending. Verify the impl insert index (near class end / after last impl) and the decl insert index (inside header) don't coincide.

- [ ] **Step 5: Build**

Ask Tim to build.

- [ ] **Step 6: Manual verification**

Input (app class):
```
class Sample
   method Run();
end-class;

method Run
   Local number &a = 1;
   Local number &b = 2;
   Local number &c = &a + &b;
   MessageBox(0, "", 0, 0, "" | &c);
end-method;
```
Select `Local number &c = &a + &b;` → dialog shows the **Visibility** dropdown (default Private) and preview `Private method ExtractedMethod(&a As number, &b As number) Returns number`. OK. Expected:
- A declaration `   method ExtractedMethod(&a As number, &b As number) returns number;` inserted in the class header (private section — create one if absent).
- An implementation appended after `Run`:
  ```
  method ExtractedMethod
     /+ &a as number +/
     /+ &b as number +/
     /+ Returns number +/
     Local number &c = &a + &b;
     Return &c;
  end-method;
  ```
- Call site in `Run`: `Local number &c = %This.ExtractedMethod(&a, &b);`.

Also verify: extracting in a non-app-class program still produces a `Function` with **no** visibility dropdown (Task 4 path unchanged).

- [ ] **Step 7: Commit**

```bash
git add AppRefiner/Refactors/ExtractFunctionMethod.cs
git commit -m "feat(refactor): app-class method extraction with visibility and %This call"
```

---

### Task 6: whats-new entry and edge-case verification pass

**Files:**
- Modify: `AppRefiner/whats-new.txt`

- [ ] **Step 1: Add a whats-new line**

Read `AppRefiner/whats-new.txt`, match its existing format, and add an entry announcing "Extract Function/Method" refactor (statement selection → new function or private method, with input parameters, a chosen return value, and `out` parameters for additional outputs).

- [ ] **Step 2: Build**

Ask Tim to build.

- [ ] **Step 3: Edge-case manual verification pass**

Run each and confirm sensible behavior (correct generation or a clear failure — never a crash or malformed code):
1. Single-statement selection (one `MessageBox(...)` with two inputs) → void function, call replaces the line.
2. Selection with **no** inputs and **no** outputs (e.g. two `MessageBox` lines using only literals) → `Function Name()` void, call `Name();`.
3. In/out variable: `&x` read then reassigned inside, read after → `&x As T out`, call passes `&x`, no Return.
4. Object mutation only (`&rs.Fill();` where `&rs` read after) → `&rs` is a value param (mutation is a Read), **not** an out param.
5. Combined `Local number &x, &y;` where one becomes an out param declared inside → clear failure ("shares a combined Local declaration"), not malformed output.
6. Method extraction where the private section already exists → decl appended to it, not duplicated.

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/whats-new.txt
git commit -m "docs: whats-new entry for Extract Function/Method"
```

---

## Notes for the implementer

- The riskiest, least-mechanical logic is in Tasks 2–3 (data-flow classification and declaration relocation). Verify each scenario by eye against the tables — the compiler will not catch a wrong classification.
- Node-type names (`ReturnStatementNode`, `BreakStatementNode`, `ContinueStatementNode`, `ForStatementNode`, `WhileStatementNode`, `RepeatStatementNode`, `LocalVariableDeclarationNode`) and registry/AST accessor names (`VariableRegistry.GetAccessibleVariables`, `AppClassNode.VisibilitySections`, `AppClassNode.Methods[i].Implementation`) must be confirmed against the actual source before relying on them; adjust to the real names, preserving the described semantics.
- All string generation must use `NewLine` (the document's own convention), not `Environment.NewLine`, to match `BaseRefactor` helpers and the surrounding file's line endings.
```
