# Completion Analysis: Exit-Mode Sets

**Date:** 2026-07-09
**Status:** Approved design, pending implementation plan

A reusable static analysis over the PeopleCode AST that answers, for every statement
and block, **how control leaves it** — via `Return`/`Throw`/`Exit`/`Error`/`Break`/
`Continue`, or by reaching the end of the block ("normal completion", i.e.
fall-through). This is the classic normal-vs-abrupt completion / reachability
analysis (JLS §14.21-style), adapted to PeopleCode's control-flow statements.

It is motivated by a concrete bug in the `Convert If ↔ Evaluate` refactor (see
Background) but is designed as a general primitive: a second consumer — a future
"not all paths return a value" compiler check — reuses it unchanged.

---

## Background: the bug that motivated this

`ConvertIfEvaluate` converts an `Evaluate` to an `If`/`Else` chain only when no
`When` clause "falls through". PeopleCode `Evaluate` falls through after a matching
`When` unless control leaves the clause, so an `If`/`Else` chain (which never falls
through) is only equivalent when every `When` body terminates.

The original check required each non-empty `When` body's **last statement** to be a
`BreakStatementNode`. That is wrong in two directions:

- **False negative (the reported bug):** a body ending in `Return "x";` cannot fall
  through, yet was rejected. Real case:

  ```
  Evaluate &File_Name_Field
     When Field.MX_FILENAME1
        Return "TC";
     When Field.MX_FILENAME2
        Return "UT";
     ...
  End-Evaluate;
  ```

- **Still wrong after a naive fix:** widening the last-statement check to "any
  statement that transfers control" still misclassifies a body whose last statement
  is a *compound* statement all of whose branches terminate:

  ```
  When Field.FOO
     If &baz Then Return "a"; Else Return "b"; End-If;   /* never falls through */
  ```

  The last statement is an `If` (`DoesTransferControl == false`), so a
  last-statement peek rejects a genuinely-convertible body. Conversely:

  ```
  When Field.FOO
     If &baz Then Return "asdf"; End-If    /* &baz false: falls to next line */
     &thing = 3;                            /* ...then off the end — DOES fall through */
  ```

  falls through and must be rejected.

The correct question is **reachability**: *can control reach the end of the `When`
body?* That requires walking the body's control-flow structure, not inspecting one
statement.

---

## The primitive

### `ExitMode` — a `[Flags]` set

```csharp
[Flags]
public enum ExitMode
{
    None     = 0,
    Normal   = 1,   // control reaches the end of the block (falls off / fall-through)
    Return   = 2,
    Throw    = 4,
    Exit     = 8,
    Error    = 16,
    Break    = 32,
    Continue = 64,
}
```

A statement or block can complete in more than one way (an `If` with one branch
returning and one falling through), so the computed value is a **set**. A `[Flags]`
enum *is* the set: union is `|`, membership is `HasFlag`.

### `CompletionAnalyzer` — annotate-and-return

Lives in `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs`, beside the AST
it reasons over (not as per-node virtual properties — the value is traversal-derived,
not structural).

```csharp
public static class CompletionAnalyzer
{
    // Computes exit modes bottom-up, annotating every StatementNode and BlockNode
    // in the subtree, and returns the root's set as a summary.
    public static ExitMode Analyze(BlockNode root);
}
```

`Analyze` is a pure post-order walk: it recurses into each sub-block first (annotating
it), then computes and annotates the current node from its children's stored sets. It
does not mutate anything but the `Attributes` annotations.

### Storage — mirror the `TypeInfo` convention

Computed exit modes ride on the AST via the existing `Attributes` dictionary, exactly
as inferred types do (`AstNode.TypeInfoAttributeKey` + `GetInferredType()`):

- `AstNode.ExitModeAttributeKey = "ExitMode"`.
- Extension accessors in `AstNodeExtensions`: `node.GetExitMode()` →
  `ExitMode?` (null if not analyzed), `node.SetExitMode(ExitMode)`.

This keeps the node classes free of computed-state properties and stays consistent
with how semantic results already attach to the tree. **Both `StatementNode`s and
`BlockNode`s are annotated** — the block annotation is the "how does this block
complete" summary; the statement annotations let a diagnostic point at the exact
`Return`/branch, not just the enclosing block.

---

## The algorithm

`Analyze(block)` walks the block's statements in order, tracking `reachable` (can
control get to this statement):

- **Leaf terminator** (`Return`, `Throw`, `Exit`, `Error`, `Break`, `Continue`) →
  the statement's set is `{thatMode}`; it sets `reachable = false`. Statements after
  it are dead code — still visited and annotated with their own exit mode, but they
  do not contribute to the enclosing block's set.
- **Compound statement** (`If`, `Evaluate`, `Try`) → recurse into each sub-block,
  **union** their sets (with absorption, below). The statement's set is that union.
  It cuts off reachability (`reachable = false` for what follows) **iff `Normal` is
  not in the union**.
  - **`If`** keeps `Normal` if there is **no `Else`**, or either the `Then` or `Else`
    block keeps `Normal`.
  - **`Evaluate`** keeps `Normal` if there is **no `When-Other`** (an unmatched
    scrutinee falls through the whole statement), or any `When`/`When-Other` body
    keeps `Normal`. **Absorption:** a `Break` in the union coming from a `When` body
    binds to *this* `Evaluate`, so it is removed and folded into the `Evaluate`'s
    `Normal`.
  - **Loops** (`For`, `While`, `Repeat`) always keep `Normal` — conservatively, the
    loop may run zero times (`For`/`While`) or exit via its condition. **Absorption:**
    `Break`/`Continue` from the loop body bind to the loop and are folded into the
    loop's `Normal`; they do not escape.
  - **`Try`** → union of the try-block's set and every catch block's set.
- **Plain statement** → set `{Normal}` (control flows through); `reachable`
  unchanged.
- **End of block reached** with `reachable == true` → add `Normal` to the block's
  set.

**Soundness invariant:** the analysis only ever *over*-approximates `Normal` (e.g.
loops always keep it). A consumer that keys off `Normal` is therefore never wrong in
the dangerous direction — worst case it is over-cautious (rejects a convertible
`When`), never unsound (converts a falling-through one).

**Binding note:** `Break`/`Continue` bind to the innermost enclosing loop/`Evaluate`,
matching PeopleCode. Because absorption happens at that enclosing construct, a
`Break`/`Continue` nested inside a loop within a `When` body never appears in the
`When` body's own exit set — only a `Break` bound directly to the `Evaluate` does.

---

## Granular queries

After one `Analyze` pass, "which blocks complete normally?" is a walk, not a mystery:

```csharp
CompletionAnalyzer.Analyze(programBody);
var fallThrough = programBody.FindDescendants<BlockNode>()
    .Where(b => b.GetExitMode()?.HasFlag(ExitMode.Normal) == true);
```

The flat union off the root answers the simple "does anything in here fall through?";
the per-node annotations answer "*which* branch, and *how*".

---

## Consumer 1: `ConvertIfEvaluate` (this change)

Replaces the last-statement `BreakStatementNode` / `DoesTransferControl` peek in both
directions with the reachability result:

- **Evaluate → If:** a non-empty `When` body is fall-through — and therefore not
  convertible — iff `body.GetExitMode().HasFlag(ExitMode.Normal)`. This accepts the
  `Return`-terminated and all-branches-`Return` cases and still rejects the
  trailing-assignment case.
- **If → Evaluate:** append the synthetic `Break;` after a `When` body iff
  `thenBlock.GetExitMode().HasFlag(ExitMode.Normal)` — no unreachable `Break` after a
  body that already always terminates, so round-tripping stays a fixpoint.

The refactor runs `CompletionAnalyzer.Analyze` on the relevant construct before
querying.

**The existing `Break`-binding guards (`ContainsEvaluateBoundBreak`) stay unchanged.**
They answer a *different* question — "is a `Break` bound to this `Evaluate` reachable
somewhere it can't be reproduced in `If` form (i.e. not the single droppable trailing
`Break`)?" — which the fall-through check does not cover. Keeping both is the
low-risk, clearly-correct choice; re-expressing the guard in terms of the exit set
(`Break ∈ exits` plus a trailing-statement check) is a possible later cleanup, not
part of this change.

---

## Consumer 2: "not all paths return a value" check (future — not built now)

Reuses `CompletionAnalyzer` with **no new analysis code**. A value-returning function/
method is flagged when `Analyze(functionBody)` contains `Normal` (control can fall off
the end) or leaks `Break`/`Continue`, plus a trivial scan that no exiting `Return` is
value-less. The per-statement annotations let the diagnostic point at the exact block
or branch that fails to return. Notably App Designer's own compiler does not report
this. Listed here to justify the primitive's shape; it is explicitly **out of scope**
for this implementation (YAGNI).

---

## Testing

The analyzer is pure over the AST — no `ScintillaEditor`, no cross-process surface —
so it is **fully unit-testable in the existing `PeopleCodeParser.SelfHosted.Tests`
project**: parse a snippet, `Analyze`, assert the expected `ExitMode` on the target
block(s). This gives the load-bearing logic real TDD coverage even though the
refactor wiring itself (which needs a live editor) remains manual-verify in App
Designer.

Test matrix to cover:

- Each leaf terminator as a sole/trailing statement → its mode, no `Normal`.
- Straight-line body with no terminator → `Normal`.
- `If` with both branches terminating → no `Normal`; with only `Then` → `Normal`;
  with a fall-through `Else` → `Normal` plus the terminating branch's mode.
- Dead code after a terminator (annotated, not counted).
- `Evaluate` with/without `When-Other`; `Break` in a `When` absorbed to the
  `Evaluate`'s `Normal`.
- Loops always `Normal`; `Break`/`Continue` in a loop body absorbed, not escaping.
- `Try`/`Catch` union.
- The four `ConvertIfEvaluate` motivating snippets (Background) classify correctly.

---

## Files touched

- **New:** `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs`,
  `ExitMode` enum (same file or a sibling).
- **Edit:** `AstNode.cs` (`ExitModeAttributeKey`), `AstNodeExtensions.cs`
  (`GetExitMode`/`SetExitMode`).
- **Edit:** `AppRefiner/Refactors/ConvertIfEvaluate.cs` — swap both last-statement
  checks for `GetExitMode().HasFlag(ExitMode.Normal)`; keep `ContainsEvaluateBoundBreak`.
- **New:** completion-analysis tests in `PeopleCodeParser.SelfHosted.Tests`.
