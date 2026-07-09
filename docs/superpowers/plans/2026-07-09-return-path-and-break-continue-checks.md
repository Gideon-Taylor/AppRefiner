# Not-All-Paths-Return & Invalid Break/Continue — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two compile checks — `NotAllPathsReturn` (value-returning functions/methods/getters must exit only via Return/Throw/Exit/Error on every path) and `InvalidBreakContinue` (Break binds to loop/Evaluate; Continue binds to loop only) — surfaced through the existing Compiler errors pipeline.

**Architecture:** Pure `ICompileCheck` units in `PeopleCodeParser.SelfHosted/Compilation/Checks/`. Return-path check calls `CompletionAnalyzer.Analyze(body)` then validates the body `ExitMode` set and emits a primary signature diagnostic plus one secondary on the innermost incomplete block. Break/Continue check parent-walks to find binders. Both register in `CompileChecker.CreateChecks()`; no AppRefiner changes required for squiggles (`CompilerErrorsStyler` already renders all compile diagnostics).

**Tech Stack:** C# / .NET 8, xUnit (`PeopleCodeParser.SelfHosted.Tests`), existing `CompletionAnalyzer` / `ExitMode`.

**Spec:** `docs/superpowers/specs/2026-07-09-return-path-and-break-continue-checks-design.md`

## Global Constraints

- Target framework: **.NET 8**.
- Checks live in **`PeopleCodeParser.SelfHosted`** (no AppRefiner dependency).
- `CheckRequirement.NotRequired` for both (no DB / resolver).
- Do **not** run `dotnet build` on AppRefiner unless needed; you MAY run `dotnet test PeopleCodeParser.SelfHosted.Tests`.
- Append `DiagnosticCode` members with **explicit next integers** (never renumber existing).
- Do not change `CompletionAnalyzer` semantics in this plan (over-approx Normal is intentional).
- Future work (return expr type, bare Return) stays out of scope — do not implement.

---

## File Structure

| Path | Action |
|---|---|
| `PeopleCodeParser.SelfHosted/Compilation/DiagnosticCode.cs` | Modify — add `NotAllPathsReturn = 15`, `InvalidBreakContinue = 16` |
| `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidBreakContinueCheck.cs` | **Create** |
| `PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs` | **Create** |
| `PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs` | Modify — register both checks |
| `PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidBreakContinueCheckTests.cs` | **Create** |
| `PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs` | **Create** |

No AppRefiner file changes in this plan (styler already consumes all codes).

---

### Task 1: Diagnostic codes

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Compilation/DiagnosticCode.cs`

- [ ] **Step 1: Append the two codes**

After `UndeclaredFunction = 14,` add:

```csharp
    NotAllPathsReturn = 15,
    InvalidBreakContinue = 16,
```

- [ ] **Step 2: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/DiagnosticCode.cs
git commit -m "feat(compile): add NotAllPathsReturn and InvalidBreakContinue diagnostic codes"
```

---

### Task 2: `InvalidBreakContinueCheck`

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidBreakContinueCheck.cs`
- Create: `PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidBreakContinueCheckTests.cs`
- Modify: `PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs` (register only this check first, or both after Task 3 — prefer register both at end of Task 3; for TDD register this check alone here)

**Semantics:**
- `Break` valid if ancestor is `ForStatementNode` | `WhileStatementNode` | `RepeatStatementNode` | `EvaluateStatementNode` before leaving a routine boundary.
- `Continue` valid only for For/While/Repeat (not Evaluate).
- Routine boundary: stop walk at `FunctionNode`, `MethodImplNode`, `PropertyImplNode`, `ProgramNode` (do not search outside).

- [ ] **Step 1: Write failing tests**

Create `PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidBreakContinueCheckTests.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class InvalidBreakContinueCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    [Fact]
    public void Break_outside_loop_or_evaluate_is_error()
    {
        var diags = Check(@"
Function F()
   Break;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Continue_outside_loop_is_error()
    {
        var diags = Check(@"
Function F()
   Continue;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Continue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Break_in_evaluate_when_is_ok()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         Break;
   End-Evaluate;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }

    [Fact]
    public void Continue_in_evaluate_when_without_loop_is_error()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         Continue;
   End-Evaluate;
End-Function;
");
        Assert.Contains(diags, d =>
            d.Code == DiagnosticCode.InvalidBreakContinue &&
            d.Message.Contains("Continue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Break_and_continue_in_while_are_ok()
    {
        var diags = Check(@"
Function F()
   While &x
      If &y Then
         Break;
      End-If;
      Continue;
   End-While;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }

    [Fact]
    public void Break_in_loop_inside_evaluate_is_ok()
    {
        var diags = Check(@"
Function F()
   Evaluate &x
      When = 1
         While &y
            Break;
         End-While;
   End-Evaluate;
End-Function;
");
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidBreakContinue);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~InvalidBreakContinueCheckTests
```

Expected: FAIL (code/check missing or no diagnostics).

- [ ] **Step 3: Implement the check**

Create `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidBreakContinueCheck.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports Break/Continue statements that are not inside a valid binder.
/// Break binds to For/While/Repeat/Evaluate; Continue binds only to For/While/Repeat.
/// </summary>
public sealed class InvalidBreakContinueCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        switch (node)
        {
            case BreakStatementNode br when !HasBreakBinder(br):
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.InvalidBreakContinue,
                    DiagnosticSeverity.Error,
                    br.SourceSpan,
                    "Break is not inside a loop or Evaluate."));
                break;

            case ContinueStatementNode cont when !HasContinueBinder(cont):
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.InvalidBreakContinue,
                    DiagnosticSeverity.Error,
                    cont.SourceSpan,
                    "Continue is not inside a loop."));
                break;
        }
    }

    private static bool HasBreakBinder(AstNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (IsRoutineBoundary(current))
                return false;
            if (current is ForStatementNode or WhileStatementNode or RepeatStatementNode
                or EvaluateStatementNode)
                return true;
        }
        return false;
    }

    private static bool HasContinueBinder(AstNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (IsRoutineBoundary(current))
                return false;
            if (current is ForStatementNode or WhileStatementNode or RepeatStatementNode)
                return true;
            // Evaluate is not a Continue binder — keep walking past it.
        }
        return false;
    }

    private static bool IsRoutineBoundary(AstNode node) =>
        node is FunctionNode or MethodImplNode or PropertyImplNode or ProgramNode;
}
```

- [ ] **Step 4: Register the check**

In `CompileChecker.CreateChecks()`, after `MissingConstructorCheck(),` add:

```csharp
        new Checks.InvalidBreakContinueCheck(),
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~InvalidBreakContinueCheckTests
```

Expected: PASS. If a snippet fails to parse, adjust whitespace/keywords only.

- [ ] **Step 6: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidBreakContinueCheck.cs \
        PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs \
        PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidBreakContinueCheckTests.cs
git commit -m "feat(compile): InvalidBreakContinue check for unbound Break/Continue"
```

---

### Task 3: `NotAllPathsReturnCheck` — core pass/fail + primary diagnostic

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs`
- Create: `PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs`
- Modify: `PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs`

**ValidExits:**

```csharp
private static readonly ExitMode ValidExits =
    ExitMode.Return | ExitMode.Throw | ExitMode.Exit | ExitMode.Error;
```

**Subjects (OnNode):**

1. `FunctionNode` where `ReturnType != null` and `Body != null`
2. `MethodImplNode` where effective return type is non-null (`Declaration?.ReturnType ?? ReturnTypeAnnotation`) and `Body != null`
3. `PropertyImplNode` where `IsGetter` and body non-null and parent `PropertyNode.Type` exists (all properties have a type)

**Pass:** `M = CompletionAnalyzer.Analyze(body)`, then `M != None && (M & ~ValidExits) == None`.

- [ ] **Step 1: Write failing tests (primary only first)**

Create `PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Compilation;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class NotAllPathsReturnCheckTests
{
    private static IReadOnlyList<CompileDiagnostic> Check(string source)
    {
        var (program, errors) = ParseTestHelper.Parse(source);
        Assert.Empty(errors);
        return CompileChecker.Check(program, errors, resolver: null, new CompileCheckContextInput(null));
    }

    private static IEnumerable<CompileDiagnostic> PathDiags(IReadOnlyList<CompileDiagnostic> diags) =>
        diags.Where(d => d.Code == DiagnosticCode.NotAllPathsReturn);

    [Fact]
    public void Function_with_fallthrough_reports()
    {
        var diags = Check(@"
Function F() Returns number
   Local number &x;
   &x = 1;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
        Assert.Contains(PathDiags(diags), d => d.Message.Contains("F"));
    }

    [Fact]
    public void Function_all_paths_return_is_clean()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return \"a\";
   Else
      Return \"b\";
   End-If;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Function_one_armed_if_return_reports()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return \"a\";
   End-If;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Function_throw_only_is_clean()
    {
        var diags = Check(@"
Function F() Returns number
   Throw CreateException(0, 0, \"x\");
End-Function;
");
        // If Throw CreateException fails to parse, use: Throw &e;
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Function_return_only_inside_while_reports()
    {
        var diags = Check(@"
Function F() Returns number
   While &x
      Return 1;
   End-While;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Procedure_without_return_type_is_not_checked()
    {
        var diags = Check(@"
Function F()
   Local number &x;
   &x = 1;
End-Function;
");
        Assert.Empty(PathDiags(diags));
    }

    [Fact]
    public void Method_with_fallthrough_reports()
    {
        var diags = Check(@"
class Sample
   method GetX() Returns number;
end-class;

method GetX
   Local number &x;
   &x = 1;
end-method;
");
        Assert.NotEmpty(PathDiags(diags));
        Assert.Contains(PathDiags(diags), d => d.Message.Contains("GetX"));
    }

    [Fact]
    public void Property_getter_with_fallthrough_reports()
    {
        var diags = Check(@"
class Sample
   property number Foo get;
end-class;

get Foo
   Local number &x;
   &x = 1;
end-get;
");
        Assert.NotEmpty(PathDiags(diags));
    }

    [Fact]
    public void Function_only_unbound_break_reports_not_all_paths()
    {
        var diags = Check(@"
Function F() Returns number
   Break;
End-Function;
");
        Assert.NotEmpty(PathDiags(diags));
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~NotAllPathsReturnCheckTests
```

- [ ] **Step 3: Implement check (primary + secondary)**

Create `PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// For functions/methods/property getters with a return type, requires every path to
/// exit via Return, Throw, Exit, or Error (no Normal / Break / Continue on the body).
/// Emits a primary diagnostic on the signature and one secondary on the innermost
/// incomplete block.
/// </summary>
public sealed class NotAllPathsReturnCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    private static readonly ExitMode ValidExits =
        ExitMode.Return | ExitMode.Throw | ExitMode.Exit | ExitMode.Error;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        switch (node)
        {
            case FunctionNode fn when fn.ReturnType != null && fn.Body != null:
                CheckBody(fn.Body, fn.Name, "function", SignatureSpan(fn.NameToken, fn.ReturnType), sink);
                break;

            case MethodImplNode mi when mi.Body != null:
            {
                var returnType = mi.Declaration?.ReturnType ?? mi.ReturnTypeAnnotation;
                if (returnType == null)
                    break;
                CheckBody(mi.Body, mi.Name, "method", SignatureSpan(mi.NameToken, returnType), sink);
                break;
            }

            case PropertyImplNode pi when pi.IsGetter && pi.Body != null:
            {
                // Getter always returns the property type.
                var prop = pi.Parent as PropertyNode ?? pi.FindAncestor<PropertyNode>();
                if (prop == null)
                    break;
                CheckBody(pi.Body, pi.Name, "property getter", pi.NameToken.SourceSpan, sink);
                break;
            }
        }
    }

    private static void CheckBody(
        BlockNode body,
        string name,
        string kind,
        SourceSpan signatureSpan,
        IDiagnosticSink sink)
    {
        ExitMode modes = CompletionAnalyzer.Analyze(body);
        if (IsComplete(modes))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.NotAllPathsReturn,
            DiagnosticSeverity.Error,
            signatureSpan,
            $"Not all paths return a value in {kind} '{name}'."));

        var secondarySpan = FindBestIncompleteSpan(body) ?? body.SourceSpan;
        // Avoid duplicate identical span noise when secondary collapses to signature-only empty body.
        if (secondarySpan.Start.ByteIndex != signatureSpan.Start.ByteIndex
            || secondarySpan.End.ByteIndex != signatureSpan.End.ByteIndex)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.NotAllPathsReturn,
                DiagnosticSeverity.Error,
                secondarySpan,
                "This block can complete without returning a value."));
        }
    }

    private static bool IsComplete(ExitMode modes) =>
        modes != ExitMode.None && (modes & ~ValidExits) == ExitMode.None;

    private static bool IsIncomplete(ExitMode? modes) =>
        modes is null || !IsComplete(modes.Value);

    /// <summary>
    /// Innermost block that still has Normal or an invalid mode; tie-break earliest start.
    /// Falls back to last statement span of the root body.
    /// </summary>
    private static SourceSpan? FindBestIncompleteSpan(BlockNode root)
    {
        BlockNode? best = null;
        var bestDepth = -1;

        void Consider(BlockNode block, int depth)
        {
            var mode = block.GetExitMode();
            if (!IsIncomplete(mode))
                return;

            if (best == null
                || depth > bestDepth
                || (depth == bestDepth
                    && block.SourceSpan.Start.ByteIndex < best.SourceSpan.Start.ByteIndex))
            {
                best = block;
                bestDepth = depth;
            }
        }

        void Walk(BlockNode block, int depth)
        {
            Consider(block, depth);
            foreach (var stmt in block.Statements)
            {
                foreach (var childBlock in stmt.FindDescendants<BlockNode>())
                {
                    // FindDescendants is relative to stmt; recompute depth via parent chain
                    // for accuracy. Simpler approach: walk structure explicitly.
                }
            }

            foreach (var stmt in block.Statements)
                WalkStatement(stmt, depth + 1);
        }

        void WalkStatement(StatementNode stmt, int depth)
        {
            switch (stmt)
            {
                case IfStatementNode ifn:
                    Walk(ifn.ThenBlock, depth);
                    if (ifn.ElseBlock != null)
                        Walk(ifn.ElseBlock, depth);
                    break;
                case EvaluateStatementNode ev:
                    foreach (var w in ev.WhenClauses)
                        Walk(w.Body, depth);
                    if (ev.WhenOtherBlock != null)
                        Walk(ev.WhenOtherBlock, depth);
                    break;
                case ForStatementNode f:
                    Walk(f.Body, depth);
                    break;
                case WhileStatementNode w:
                    Walk(w.Body, depth);
                    break;
                case RepeatStatementNode r:
                    Walk(r.Body, depth);
                    break;
                case TryStatementNode t:
                    Walk(t.TryBlock, depth);
                    foreach (var c in t.CatchClauses)
                        Walk(c.Body, depth);
                    break;
                case BlockNode b:
                    Walk(b, depth);
                    break;
            }
        }

        Walk(root, 0);

        if (best == null)
            return null;

        // Straight-line body: prefer last statement for a visible in-body marker.
        if (ReferenceEquals(best, root) && root.Statements.Count > 0)
            return root.Statements[^1].SourceSpan;

        return best.SourceSpan;
    }

    private static SourceSpan SignatureSpan(Token nameToken, TypeNode returnType) =>
        new SourceSpan(nameToken.SourceSpan.Start, returnType.SourceSpan.End);
}
```

**Note for implementer:** The sketch above needs `using PeopleCodeParser.SelfHosted.Lexing` for `Token`. Clean up the empty `FindDescendants` loop body (left as a comment remnant) — keep only `Walk` / `WalkStatement`. If `Throw CreateException(...)` fails tests, use `Throw &e;` for the throw-only case.

- [ ] **Step 4: Register the check**

In `CompileChecker.CreateChecks()`, add:

```csharp
        new Checks.NotAllPathsReturnCheck(),
```

(near `InvalidBreakContinueCheck`)

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~NotAllPathsReturnCheckTests
```

Fix parse snippets if needed. Assert at least one primary (message contains name). Optionally assert count >= 1; secondary is covered in Task 4.

- [ ] **Step 6: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs \
        PeopleCodeParser.SelfHosted/Compilation/CompileChecker.cs \
        PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs
git commit -m "feat(compile): NotAllPathsReturn check for value-returning routines"
```

---

### Task 4: Secondary marker tests + polish

**Files:**
- Modify: `PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs`
- Modify: `PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs` (only if secondary selection is wrong)

- [ ] **Step 1: Add secondary-span tests**

```csharp
    [Fact]
    public void Reports_primary_and_secondary_for_incomplete_if_branch()
    {
        var diags = Check(@"
Function F(&b As boolean) Returns string
   If &b Then
      Return \"a\";
   Else
      Local string &s;
      &s = \"x\";
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn).ToList();

        Assert.True(diags.Count >= 2, "expected primary signature + secondary block");
        Assert.Contains(diags, d => d.Message.Contains("Not all paths return"));
        Assert.Contains(diags, d => d.Message.Contains("This block can complete"));
    }

    [Fact]
    public void Secondary_prefers_innermost_incomplete_block()
    {
        // Outer If incomplete only because inner Else falls through.
        var diags = Check(@"
Function F(&b As boolean, &c As boolean) Returns number
   If &b Then
      If &c Then
         Return 1;
      Else
         Local number &n;
         &n = 2;
      End-If;
   Else
      Return 0;
   End-If;
End-Function;
").Where(d => d.Code == DiagnosticCode.NotAllPathsReturn).ToList();

        Assert.Contains(diags, d => d.Message.Contains("This block can complete"));
        // Secondary should not be the entire function body only — span should be
        // inside the else branch (line after Return 1's sibling). Soft check:
        var secondary = diags.First(d => d.Message.Contains("This block can complete"));
        Assert.True(secondary.Span.Start.Line >= 4);
    }
```

- [ ] **Step 2: Run full compile-check suite**

```bash
dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~Compilation
```

Expected: all PASS (including prior InvalidAppClass / etc.).

- [ ] **Step 3: Commit**

```bash
git add PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs \
        PeopleCodeParser.SelfHosted/Compilation/Checks/NotAllPathsReturnCheck.cs
git commit -m "test(compile): pin NotAllPathsReturn secondary incomplete-block markers"
```

---

### Task 5: Spec status note (optional docs)

**Files:**
- Modify: `docs/superpowers/specs/2026-07-09-return-path-and-break-continue-checks-design.md` — set Status to Implemented when done.

- [ ] **Step 1: Update status line**

Change:

```markdown
**Status:** Approved (brainstorm) — pending implementation plan
```

to:

```markdown
**Status:** Implemented
```

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/specs/2026-07-09-return-path-and-break-continue-checks-design.md
git commit -m "docs: mark return-path and break/continue checks design implemented"
```

---

## Self-Review (plan vs spec)

| Spec requirement | Task |
|---|---|
| Functions/methods/getters with return type | Task 3 |
| ValidExits = Return\|Throw\|Exit\|Error; no Normal/Break/Continue | Task 3 |
| Primary on signature | Task 3 |
| One secondary innermost incomplete block | Task 3–4 |
| InvalidBreakContinue parent walk; Continue ≠ Evaluate | Task 2 |
| Register in CompileChecker | Tasks 2–3 |
| NotRequired / no DB | both checks |
| Unit tests | Tasks 2–4 |
| Future return-type / bare Return | Out of scope (spec §7) |
| No CompletionAnalyzer semantic change | Confirmed |

**Placeholder scan:** Implementation sketch in Task 3 is complete enough to type; implementer must remove the empty FindDescendants remnant and fix usings (`Lexing.Token`, `Analysis`).

**Type consistency:** `DiagnosticCode.NotAllPathsReturn = 15`, `InvalidBreakContinue = 16`; `CompileDiagnostic` record unchanged; `CompletionAnalyzer.Analyze(BlockNode) → ExitMode`.

---

## Execution

Plan complete and saved to `docs/superpowers/plans/2026-07-09-return-path-and-break-continue-checks.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — this session with executing-plans checkpoints  

Which approach?
