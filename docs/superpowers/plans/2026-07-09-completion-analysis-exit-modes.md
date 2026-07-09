# Completion Analysis: Exit-Mode Sets — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable `CompletionAnalyzer` over the PeopleCode AST that annotates every statement/block with how control leaves it (an `ExitMode` set), and use it to fix `ConvertIfEvaluate`'s fall-through detection.

**Architecture:** A pure, side-effect-light static analysis in `PeopleCodeParser.SelfHosted` walks a `BlockNode` post-order, computes each block's/statement's `[Flags] ExitMode` set from its children, and stores it on the node's `Attributes` dictionary (mirroring the existing `TypeInfo` convention). `ConvertIfEvaluate` swaps its last-statement `Break` heuristic for `Analyze(body).HasFlag(ExitMode.Normal)`.

**Tech Stack:** C# / .NET 8, xUnit (`PeopleCodeParser.SelfHosted.Tests`). Self-hosted recursive-descent parser AST.

## Global Constraints

- Target framework: **.NET 8**.
- Analysis lives in project **`PeopleCodeParser.SelfHosted`** (no dependency on AppRefiner / ScintillaEditor).
- Annotation storage MUST reuse the existing `Attributes` dictionary + extension-accessor pattern (like `AstNode.TypeInfoAttributeKey` / `GetInferredType()`), not new node properties.
- **Soundness rule:** the analysis only ever *over*-approximates `ExitMode.Normal` (never reports "cannot fall through" when it can). Loops therefore always keep `Normal`.
- `ExitMode` is a `[Flags]` enum; the value *is* the set (union `|`, membership `HasFlag`).
- Do **not** run `dotnet`/`msbuild` yourself for the AppRefiner C# app — the maintainer builds. You MAY run `dotnet test` on `PeopleCodeParser.SelfHosted.Tests` (pure library, fast).
- Existing `ContainsEvaluateBoundBreak` guards in `ConvertIfEvaluate` must be preserved.

---

## File Structure

- **Create** `PeopleCodeParser.SelfHosted/Analysis/ExitMode.cs` — the `[Flags] ExitMode` enum.
- **Create** `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs` — the analysis.
- **Modify** `PeopleCodeParser.SelfHosted/AstNode.cs` — add `ExitModeAttributeKey`.
- **Modify** `PeopleCodeParser.SelfHosted/AstNodeExtensions.cs` — add `GetExitMode`/`SetExitMode`.
- **Modify** `AppRefiner/Refactors/ConvertIfEvaluate.cs` — wire both directions to the analyzer.
- **Create** `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs` — unit tests.

---

### Task 1: `ExitMode` enum + AST annotation storage

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Analysis/ExitMode.cs`
- Modify: `PeopleCodeParser.SelfHosted/AstNode.cs` (add key near line 105, after `TypeWarningAttributeKey`)
- Modify: `PeopleCodeParser.SelfHosted/AstNodeExtensions.cs` (add a new region)
- Test: `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs`

**Interfaces:**
- Produces:
  - `enum ExitMode : int` with `[Flags]` — values `None=0, Normal=1, Return=2, Throw=4, Exit=8, Error=16, Break=32, Continue=64`, namespace `PeopleCodeParser.SelfHosted.Analysis`.
  - `const string AstNode.ExitModeAttributeKey = "ExitMode"`.
  - `ExitMode? AstNodeExtensions.GetExitMode(this AstNode node)`.
  - `void AstNodeExtensions.SetExitMode(this AstNode node, ExitMode mode)`.

- [ ] **Step 1: Write the failing test**

Create `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs`:

```csharp
using PeopleCodeParser.SelfHosted.Analysis;
using PeopleCodeParser.SelfHosted.Nodes;
using static PeopleCodeParser.SelfHosted.Tests.ParseTestHelper;

namespace PeopleCodeParser.SelfHosted.Tests.Analysis;

public class CompletionAnalyzerTests
{
    [Fact]
    public void ExitMode_RoundTripsThroughAttributes()
    {
        var (program, _) = Parse("Function F()\nReturn;\nEnd-Function;");
        var body = program.Functions.Single().Body!;

        Assert.Null(body.GetExitMode());

        body.SetExitMode(ExitMode.Return | ExitMode.Normal);

        Assert.Equal(ExitMode.Return | ExitMode.Normal, body.GetExitMode());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests.ExitMode_RoundTripsThroughAttributes`
Expected: FAIL — `ExitMode`, `GetExitMode`, `SetExitMode` do not exist (compile error).

- [ ] **Step 3: Create the `ExitMode` enum**

Create `PeopleCodeParser.SelfHosted/Analysis/ExitMode.cs`:

```csharp
namespace PeopleCodeParser.SelfHosted.Analysis;

/// <summary>
/// The set of ways control can leave a statement or block. A statement/block can
/// complete in more than one way (e.g. an If whose Then returns and whose Else falls
/// through), so this is a set — the [Flags] value IS the set.
/// <para>
/// <see cref="Normal"/> means control can reach the end of the block (fall-through /
/// "normal completion"). All other members are abrupt completions.
/// </para>
/// </summary>
[Flags]
public enum ExitMode
{
    None     = 0,
    Normal   = 1,   // control reaches the end of the block (falls off)
    Return   = 2,
    Throw    = 4,
    Exit     = 8,
    Error    = 16,
    Break    = 32,
    Continue = 64,
}
```

- [ ] **Step 4: Add the attribute key**

In `PeopleCodeParser.SelfHosted/AstNode.cs`, after the line
`public const string TypeWarningAttributeKey = "TypeWarning";` (currently line 105), add:

```csharp
    /// <summary>
    /// Attribute key for storing completion analysis results (ExitMode)
    /// </summary>
    public const string ExitModeAttributeKey = "ExitMode";
```

- [ ] **Step 5: Add the extension accessors**

In `PeopleCodeParser.SelfHosted/AstNodeExtensions.cs`, add `using PeopleCodeParser.SelfHosted.Analysis;` to the top with the other usings, then add this region before the final closing brace of the class (after the `#endregion` that closes TypeError Extensions):

```csharp
    #region ExitMode Extensions

    /// <summary>
    /// Gets the completion analysis result for this node, if
    /// <see cref="Analysis.CompletionAnalyzer"/> has been run over it.
    /// </summary>
    /// <returns>The ExitMode set, or null if completion analysis has not been performed.</returns>
    public static ExitMode? GetExitMode(this AstNode node)
    {
        if (node.Attributes.TryGetValue(AstNode.ExitModeAttributeKey, out var mode))
        {
            return (ExitMode)mode;
        }
        return null;
    }

    /// <summary>
    /// Stores the completion analysis result for this node.
    /// </summary>
    public static void SetExitMode(this AstNode node, ExitMode mode)
    {
        node.Attributes[AstNode.ExitModeAttributeKey] = mode;
    }

    #endregion
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests.ExitMode_RoundTripsThroughAttributes`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Analysis/ExitMode.cs \
        PeopleCodeParser.SelfHosted/AstNode.cs \
        PeopleCodeParser.SelfHosted/AstNodeExtensions.cs \
        PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs
git commit -m "feat(parser): add ExitMode flags enum and AST annotation accessors"
```

---

### Task 2: `CompletionAnalyzer` core — leaf terminators, blocks, `If`

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs` (add tests)

**Interfaces:**
- Consumes: `ExitMode`, `GetExitMode`/`SetExitMode` (Task 1); AST nodes `BlockNode`, `StatementNode`, `ReturnStatementNode`, `ThrowStatementNode`, `ExitStatementNode`, `ErrorStatementNode`, `BreakStatementNode`, `ContinueStatementNode`, `IfStatementNode` (`ThenBlock`, `ElseBlock`).
- Produces: `static ExitMode CompletionAnalyzer.Analyze(BlockNode root)` — annotates every descendant statement/block and returns the root block's set. Also (used by later tasks internally) it dispatches on `EvaluateStatementNode`, `TryStatementNode`, `ForStatementNode`, `WhileStatementNode`, `RepeatStatementNode`, which Task 2 stubs to fall-through and Task 3 completes.

- [ ] **Step 1: Write the failing tests**

Add to `CompletionAnalyzerTests.cs` (inside the class), including a shared helper:

```csharp
    // Wraps a body in a param-less function and analyzes the function body block.
    private static ExitMode AnalyzeBody(string body)
    {
        var (program, errors) = Parse($"Function F()\n{body}\nEnd-Function;");
        Assert.Empty(errors);
        return CompletionAnalyzer.Analyze(program.Functions.Single().Body!);
    }

    [Fact]
    public void Return_ExitsViaReturn_NotNormal()
        => Assert.Equal(ExitMode.Return, AnalyzeBody("Return;"));

    [Fact]
    public void Throw_ExitsViaThrow()
        => Assert.Equal(ExitMode.Throw, AnalyzeBody("Throw &e;"));

    [Fact]
    public void PlainStatement_FallsThrough()
        => Assert.Equal(ExitMode.Normal, AnalyzeBody("&x = 1;"));

    [Fact]
    public void EmptyBody_FallsThrough()
        => Assert.Equal(ExitMode.Normal, AnalyzeBody(""));

    [Fact]
    public void IfWithBothBranchesReturn_DoesNotFallThrough()
        => Assert.Equal(ExitMode.Return,
            AnalyzeBody("If &b Then\nReturn;\nElse\nReturn;\nEnd-If;"));

    [Fact]
    public void IfWithOnlyThen_CanFallThrough()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal,
            AnalyzeBody("If &b Then\nReturn;\nEnd-If;"));

    [Fact]
    public void IfThenReturns_ElseFallsThrough_UnionsBoth()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal,
            AnalyzeBody("If &b Then\nReturn;\nElse\n&x = 1;\nEnd-If;"));

    [Fact]
    public void StatementAfterReturn_IsDeadButAnnotated()
    {
        var (program, _) = Parse("Function F()\nReturn;\n&x = 1;\nEnd-Function;");
        var body = program.Functions.Single().Body!;

        Assert.Equal(ExitMode.Return, CompletionAnalyzer.Analyze(body));
        // The dead statement is still visited and carries its own exit mode...
        Assert.Equal(ExitMode.Normal, body.Statements[1].GetExitMode());
        // ...but it did not add Normal to the block's set.
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests`
Expected: FAIL — `CompletionAnalyzer` does not exist (compile error).

- [ ] **Step 3: Create the analyzer**

Create `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs`. (Task 3 fills in the `Evaluate`/loop/`Try` bodies of the marked methods; keep those method stubs exactly as shown so Task 3 only edits their internals.)

```csharp
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Analysis;

/// <summary>
/// Computes, for every statement and block in a subtree, the set of ways control can
/// leave it (<see cref="ExitMode"/>) — the classic normal-vs-abrupt completion /
/// reachability analysis, adapted to PeopleCode. Results are annotated onto each node
/// via <see cref="AstNodeExtensions.SetExitMode"/>.
///
/// Soundness: the analysis only ever OVER-approximates <see cref="ExitMode.Normal"/>,
/// so a consumer keying off Normal is never wrong in the dangerous direction.
/// </summary>
public static class CompletionAnalyzer
{
    /// <summary>
    /// Analyzes <paramref name="root"/>, annotating it and every descendant
    /// statement/block, and returns the root block's exit-mode set.
    /// </summary>
    public static ExitMode Analyze(BlockNode root) => AnalyzeBlock(root);

    private static ExitMode AnalyzeBlock(BlockNode block)
    {
        ExitMode result = ExitMode.None;
        bool reachable = true;

        foreach (var statement in block.Statements)
        {
            // Always visit so nested/dead structure is annotated too.
            ExitMode stmtModes = AnalyzeStatement(statement);
            if (!reachable)
                continue; // dead code: annotated, but does not affect the block

            // Abrupt modes escape the block; Normal only means "can reach next stmt".
            result |= (stmtModes & ~ExitMode.Normal);
            if (!stmtModes.HasFlag(ExitMode.Normal))
                reachable = false;
        }

        if (reachable)
            result |= ExitMode.Normal; // fell off the end

        block.SetExitMode(result);
        return result;
    }

    private static ExitMode AnalyzeStatement(StatementNode statement)
    {
        ExitMode modes = statement switch
        {
            ReturnStatementNode => ExitMode.Return,
            ThrowStatementNode => ExitMode.Throw,
            ExitStatementNode => ExitMode.Exit,
            ErrorStatementNode => ExitMode.Error,
            BreakStatementNode => ExitMode.Break,
            ContinueStatementNode => ExitMode.Continue,
            IfStatementNode ifNode => AnalyzeIf(ifNode),
            EvaluateStatementNode evalNode => AnalyzeEvaluate(evalNode),
            TryStatementNode tryNode => AnalyzeTry(tryNode),
            ForStatementNode forNode => AnalyzeLoop(forNode.Body),
            WhileStatementNode whileNode => AnalyzeLoop(whileNode.Body),
            RepeatStatementNode repeatNode => AnalyzeLoop(repeatNode.Body),
            BlockNode block => AnalyzeBlock(block),
            _ => ExitMode.Normal, // plain statements fall through
        };

        statement.SetExitMode(modes);
        return modes;
    }

    private static ExitMode AnalyzeIf(IfStatementNode node)
    {
        ExitMode then = AnalyzeBlock(node.ThenBlock);
        if (node.ElseBlock == null)
            return then | ExitMode.Normal; // the untaken (no-else) path falls through
        return then | AnalyzeBlock(node.ElseBlock);
    }

    // ---- Completed in Task 3; stubbed here to fall through conservatively. ----

    private static ExitMode AnalyzeEvaluate(EvaluateStatementNode node)
    {
        foreach (var whenClause in node.WhenClauses)
            AnalyzeBlock(whenClause.Body);
        if (node.WhenOtherBlock != null)
            AnalyzeBlock(node.WhenOtherBlock);
        return ExitMode.Normal;
    }

    private static ExitMode AnalyzeTry(TryStatementNode node)
    {
        AnalyzeBlock(node.TryBlock);
        foreach (var catchClause in node.CatchClauses)
            AnalyzeBlock(catchClause.Body);
        return ExitMode.Normal;
    }

    private static ExitMode AnalyzeLoop(BlockNode body)
    {
        AnalyzeBlock(body);
        return ExitMode.Normal;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests`
Expected: PASS (all Task 1 + Task 2 tests). If `Throw &e;` or `&x = 1;` produce parse errors, adjust the snippet whitespace/newlines only — the assertions stay.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs \
        PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs
git commit -m "feat(parser): CompletionAnalyzer core (leaf terminators, blocks, If)"
```

---

### Task 3: `CompletionAnalyzer` — `Evaluate`, loops, `Try` with Break/Continue absorption

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs` (fill in `AnalyzeEvaluate`, `AnalyzeTry`, `AnalyzeLoop`; add two absorption helpers)
- Test: `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs` (add tests)

**Interfaces:**
- Consumes: everything from Task 2, plus `EvaluateStatementNode` (`WhenClauses` → `WhenClause.Body`, `WhenOtherBlock`), `TryStatementNode` (`TryBlock`, `CatchClauses` → `CatchStatementNode.Body`), loop `Body` blocks.
- Produces: no new public surface — completes the internal semantics of `Analyze`.

**Semantics being implemented:**
- `Evaluate` keeps `Normal` if there is no `When-Other` (unmatched scrutinee falls through) or any branch keeps `Normal`. A `Break` from a `When` body binds to the `Evaluate`, so it is absorbed into the `Evaluate`'s `Normal`.
- Loops always keep `Normal` (may run zero times / exit via condition). `Break`/`Continue` from the loop body bind to the loop and are absorbed into `Normal`.
- `Try` = union of the try block and every catch block.

- [ ] **Step 1: Write the failing tests**

Add to `CompletionAnalyzerTests.cs`:

```csharp
    [Fact]
    public void EvaluateWithoutWhenOther_CanFallThrough()
        => Assert.True(AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nEnd-Evaluate;")
            .HasFlag(ExitMode.Normal));

    [Fact]
    public void EvaluateAllReturn_WithWhenOther_DoesNotFallThrough()
        => Assert.Equal(ExitMode.Return, AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nWhen = 2\nReturn;\nWhen-Other\nReturn;\nEnd-Evaluate;"));

    [Fact]
    public void EvaluateOneWhenFallsThrough_WithWhenOther_KeepsNormal()
        => Assert.Equal(ExitMode.Return | ExitMode.Normal, AnalyzeBody(
            "Evaluate &x\nWhen = 1\nReturn;\nWhen = 2\n&y = 1;\nWhen-Other\nReturn;\nEnd-Evaluate;"));

    [Fact]
    public void BreakInWhen_AbsorbedIntoEvaluateNormal()
    {
        var mode = AnalyzeBody(
            "Evaluate &x\nWhen = 1\nBreak;\nWhen-Other\nBreak;\nEnd-Evaluate;");
        Assert.False(mode.HasFlag(ExitMode.Break)); // Break bound to the Evaluate, absorbed
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void Loop_AlwaysFallsThrough_AndAbsorbsBreak()
    {
        var mode = AnalyzeBody("While &x\nBreak;\nEnd-While;");
        Assert.False(mode.HasFlag(ExitMode.Break)); // Break bound to the loop, absorbed
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void Loop_AbsorbsContinue()
    {
        var mode = AnalyzeBody("While &x\nContinue;\nEnd-While;");
        Assert.False(mode.HasFlag(ExitMode.Continue));
        Assert.True(mode.HasFlag(ExitMode.Normal));
    }

    [Fact]
    public void ReturnInLoop_PropagatesReturn_ButStaysNormal()
    {
        var mode = AnalyzeBody("While &x\nReturn;\nEnd-While;");
        Assert.True(mode.HasFlag(ExitMode.Return)); // Return escapes the loop
        Assert.True(mode.HasFlag(ExitMode.Normal)); // loop may run zero times
    }

    [Fact]
    public void TryCatch_UnionsBothPaths()
    {
        var mode = AnalyzeBody(
            "try\nReturn;\ncatch Exception &e\n&x = 1;\nend-try;");
        Assert.True(mode.HasFlag(ExitMode.Return)); // try path returns
        Assert.True(mode.HasFlag(ExitMode.Normal)); // catch path falls through
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests`
Expected: FAIL — e.g. `EvaluateAllReturn_WithWhenOther_DoesNotFallThrough` expects `Return` but the stub returns `Normal`; `BreakInWhen_AbsorbedIntoEvaluateNormal` sees `Break` present.

- [ ] **Step 3: Implement the three methods + absorption helpers**

In `CompletionAnalyzer.cs`, replace the three stubbed methods (`AnalyzeEvaluate`, `AnalyzeTry`, `AnalyzeLoop`) and the `// ---- Completed in Task 3 ...` comment with:

```csharp
    private static ExitMode AnalyzeEvaluate(EvaluateStatementNode node)
    {
        ExitMode union = ExitMode.None;
        foreach (var whenClause in node.WhenClauses)
            union |= AnalyzeBlock(whenClause.Body);

        if (node.WhenOtherBlock != null)
            union |= AnalyzeBlock(node.WhenOtherBlock);
        else
            union |= ExitMode.Normal; // unmatched scrutinee falls through the Evaluate

        // A Break in a When/When-Other body binds to THIS Evaluate: from the
        // Evaluate's perspective that is normal completion.
        return Absorb(union, ExitMode.Break);
    }

    private static ExitMode AnalyzeTry(TryStatementNode node)
    {
        ExitMode union = AnalyzeBlock(node.TryBlock);
        foreach (var catchClause in node.CatchClauses)
            union |= AnalyzeBlock(catchClause.Body);
        return union;
    }

    private static ExitMode AnalyzeLoop(BlockNode body)
    {
        ExitMode inner = AnalyzeBlock(body);
        // Break/Continue bind to the loop (absorbed); the loop can always complete
        // normally (may run zero times or exit via its condition).
        return Absorb(inner, ExitMode.Break | ExitMode.Continue) | ExitMode.Normal;
    }

    /// <summary>
    /// Removes <paramref name="bound"/> modes from the set; if any were present, folds
    /// them into <see cref="ExitMode.Normal"/> (they bound to the construct being
    /// analyzed, so control resumes normally after it).
    /// </summary>
    private static ExitMode Absorb(ExitMode modes, ExitMode bound)
    {
        if ((modes & bound) != ExitMode.None)
            modes = (modes & ~bound) | ExitMode.Normal;
        return modes;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests`
Expected: PASS (all tests). If a snippet fails to parse, adjust only its whitespace/keywords (e.g. `try`/`catch Exception &e`/`end-try` casing) — assertions stay.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs \
        PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs
git commit -m "feat(parser): CompletionAnalyzer Evaluate/loop/Try with Break-Continue absorption"
```

---

### Task 4: Lock the `ConvertIfEvaluate` motivating cases at the analyzer boundary

**Files:**
- Test: `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs` (add tests)

**Interfaces:**
- Consumes: `CompletionAnalyzer.Analyze`, `EvaluateStatementNode`, `FindDescendants<T>()`, `GetExitMode()`.
- Produces: nothing — these tests pin the exact behavior the refactor relies on, so Task 5's wiring is mechanical.

- [ ] **Step 1: Write the tests**

Add to `CompletionAnalyzerTests.cs`:

```csharp
    // The reported bug: every When returns, so no When body falls through.
    [Fact]
    public void MotivatingCase_AllWhensReturn_NoWhenBodyFallsThrough()
    {
        var source = """
            Function Calculate_File_Type(&Field As string) Returns string
               Evaluate &Field
                  When = "A"
                     Return "TC";
                  When = "B"
                     Return "UT";
                  When = "C"
                     Return "STL";
               End-Evaluate;
            End-Function;
            """;
        var (program, errors) = Parse(source);
        Assert.Empty(errors);

        var evaluate = program.FindDescendants<EvaluateStatementNode>().Single();
        CompletionAnalyzer.Analyze(program.Functions.Single().Body!);

        foreach (var whenClause in evaluate.WhenClauses)
            Assert.False(whenClause.Body.GetExitMode()!.Value.HasFlag(ExitMode.Normal));
    }

    // All branches return via a nested If — must NOT be seen as fall-through.
    [Fact]
    public void MotivatingCase_NestedIfBothBranchesReturn_DoesNotFallThrough()
        => Assert.False(AnalyzeBody("If &b Then\nReturn \"a\";\nElse\nReturn \"b\";\nEnd-If;")
            .HasFlag(ExitMode.Normal));

    // Trailing assignment after a one-armed If DOES fall through.
    [Fact]
    public void MotivatingCase_IfThenReturn_ThenAssignment_FallsThrough()
        => Assert.True(AnalyzeBody("If &b Then\nReturn \"a\";\nEnd-If;\n&thing = 3;")
            .HasFlag(ExitMode.Normal));
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~CompletionAnalyzerTests`
Expected: PASS. (These exercise already-implemented behavior; if the `Function ... Returns string` header or `FindDescendants` usage doesn't parse/resolve, fix the snippet/navigation only.)

- [ ] **Step 3: Commit**

```bash
git add PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs
git commit -m "test(parser): pin ConvertIfEvaluate motivating cases at analyzer boundary"
```

---

### Task 5: Wire `CompletionAnalyzer` into `ConvertIfEvaluate` (both directions)

**Files:**
- Modify: `AppRefiner/Refactors/ConvertIfEvaluate.cs`

**Interfaces:**
- Consumes: `CompletionAnalyzer.Analyze(BlockNode)`, `ExitMode` (namespace `PeopleCodeParser.SelfHosted.Analysis`).
- Produces: nothing new; behavior change only.

**No automated test:** this refactor is coupled to a live `ScintillaEditor` and is verified by build + manual App Designer check (see Step 5). The decision logic it now calls is covered by Tasks 2–4.

- [ ] **Step 1: Add the using**

At the top of `AppRefiner/Refactors/ConvertIfEvaluate.cs`, with the other `using` lines, add:

```csharp
using PeopleCodeParser.SelfHosted.Analysis;
```

- [ ] **Step 2: Replace the Evaluate→If fall-through check**

In `ConvertEvaluateToIf`, find this block (inside the `foreach (var whenClause in ev.WhenClauses)` loop, right after the empty-body `continue`):

```csharp
                if (whenClause.Body.Statements[^1] is not BreakStatementNode)
                {
                    SetFailure("A When clause falls through (its body does not end with Break) — intentional fall-through cannot be expressed as If/Else and will not be converted.");
                    return;
                }
```

Replace it with:

```csharp
                // Fall-through = control can reach the end of the body. A trailing
                // Return/Throw/Exit/Error/Break/Continue (or an If whose every branch
                // does so) prevents it; a plain trailing statement does not.
                if (CompletionAnalyzer.Analyze(whenClause.Body).HasFlag(ExitMode.Normal))
                {
                    SetFailure("A When clause falls through (control can reach the end of its body) — intentional fall-through cannot be expressed as If/Else and will not be converted.");
                    return;
                }
```

Leave the following `ContainsEvaluateBoundBreak(whenClause.Body, ignoreTrailing: true)` check unchanged — it guards a different hazard (a Break bound to the Evaluate that can't be reproduced in If form).

- [ ] **Step 3: Replace the If→Evaluate synthetic-Break emission**

In `BuildEvaluateText`, find the `for` loop body:

```csharp
                sb.Append($"{indent}When {whens[i].OpSymbol} {GetSourceText(whens[i].Value.SourceSpan)}{NewLine}");
                sb.Append(RenderBody(links[i].ThenBlock, indent + unit, dropTrailingBreak: false));
                // Evaluate falls through: every When body must Break to preserve
                // if/else-if semantics (even an empty body)
                sb.Append($"{indent + unit}Break;{NewLine}");
```

Replace it with:

```csharp
                var thenBlock = links[i].ThenBlock;
                sb.Append($"{indent}When {whens[i].OpSymbol} {GetSourceText(whens[i].Value.SourceSpan)}{NewLine}");
                sb.Append(RenderBody(thenBlock, indent + unit, dropTrailingBreak: false));
                // Evaluate falls through: add a trailing Break only when the body can
                // reach its end. A body that already always terminates (Return/Throw/
                // etc.) needs none — an added Break would be unreachable, and omitting
                // it keeps If<->Evaluate round-trips a fixpoint. Empty bodies fall
                // through, so they still get one.
                if (CompletionAnalyzer.Analyze(thenBlock).HasFlag(ExitMode.Normal))
                    sb.Append($"{indent + unit}Break;{NewLine}");
```

- [ ] **Step 4: Update the class doc comment**

At the top of the file, replace the `<summary>` block:

```csharp
    /// <summary>
    /// Converts between an If/Else-If chain and an Evaluate statement, whichever
    /// direction applies at the cursor. Semantics note: PeopleCode Evaluate falls
    /// through after a matching When unless Break, so generated When bodies always
    /// end with Break; and only Break-terminated Evaluates convert back to If.
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// Converts between an If/Else-If chain and an Evaluate statement, whichever
    /// direction applies at the cursor. Convertibility uses CompletionAnalyzer:
    /// PeopleCode Evaluate falls through after a matching When unless control leaves
    /// the clause, so a generated When body gets a trailing Break only when its body
    /// can reach its end, and an Evaluate converts back to If only when no When body
    /// can complete normally.
    /// </summary>
```

- [ ] **Step 5: Verify (build + manual)**

The maintainer rebuilds `AppRefiner/AppRefiner.csproj`, then in App Designer confirms:
- The `Calculate_File_Type` Evaluate (every `When` ends in `Return`) now converts to a nested If/Else chain — no longer "Refactoring Failed".
- Converting that If chain back yields the original Evaluate with **no** stray `Break;` after the `Return`s (round-trip fixpoint).
- A nested `If &b Then Return "a"; Else Return "b"; End-If;` inside a `When` converts.
- Regression: a classic `When … Break;` Evaluate still converts as before (trailing `Break` dropped); a `When` whose body ends in a plain assignment still refuses with the fall-through message.

- [ ] **Step 6: Commit**

```bash
git add AppRefiner/Refactors/ConvertIfEvaluate.cs
git commit -m "fix(refactor): use CompletionAnalyzer for ConvertIfEvaluate fall-through detection"
```

---

## Self-Review

**Spec coverage:**
- `ExitMode` flags enum → Task 1. ✓
- `CompletionAnalyzer.Analyze` annotate-and-return, `Attributes` storage, `GetExitMode`/`SetExitMode` mirroring TypeInfo → Task 1 (storage) + Task 2 (analyzer). ✓
- Algorithm: leaf terminators, block reachability + dead-code annotation, `If` → Task 2; `Evaluate`/loops/`Try` + Break/Continue absorption → Task 3. ✓
- Soundness (over-approx Normal; loops keep Normal) → Task 3 `AnalyzeLoop`, tested `ReturnInLoop_PropagatesReturn_ButStaysNormal`. ✓
- Granular per-block queries → Task 4 iterates `evaluate.WhenClauses[..].Body.GetExitMode()`. ✓
- Consumer 1 wiring (both directions), keep `ContainsEvaluateBoundBreak` → Task 5. ✓
- Testing in `PeopleCodeParser.SelfHosted.Tests` → Tasks 1–4. ✓
- Consumer 2 (future check) → out of scope by spec; no task. ✓ (intentional)

**Placeholder scan:** No TBD/TODO; every code step shows complete code; the Task 2 stubs are explicitly labeled and replaced verbatim in Task 3.

**Type consistency:** `Analyze(BlockNode) → ExitMode` used identically in Tasks 2–5. `GetExitMode()` returns `ExitMode?` — Task 4 uses `.GetExitMode()!.Value.HasFlag(...)` (nullable) while Task 5 uses the non-null `Analyze(...).HasFlag(...)` return value; consistent. `Absorb(ExitMode, ExitMode)` introduced and used only within Task 3. Enum members (`Normal/Return/Throw/Exit/Error/Break/Continue`) match the Task 1 definition throughout.
