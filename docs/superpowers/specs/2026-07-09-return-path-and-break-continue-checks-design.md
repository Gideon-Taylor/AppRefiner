# Design: Not-All-Paths-Return & Invalid Break/Continue Compile Checks

**Date:** 2026-07-09  
**Status:** Implemented  
**Depends on:** `CompletionAnalyzer` / `ExitMode` (`docs/superpowers/specs/2026-07-09-completion-analysis-exit-modes-design.md`)

## 1. Goal

PeopleCode’s real compiler requires *at least one* `Return` in a value-returning
function/method, but does **not** require that **all** control-flow paths exit with a
value. AppRefiner should close that gap with a compile check driven by exit-mode
analysis, and add a companion check that `Break` / `Continue` only appear where they
can bind (loop / Evaluate).

Both checks live in the existing `CompileChecker` pipeline and surface through the
**Compiler errors** styler (squiggles). No new AppRefiner styler toggle.

## 2. Approach (chosen)

**Approach 1:** Two focused `ICompileCheck` implementations that consume
`CompletionAnalyzer` (return-path) and AST parent walks (break/continue). The analyzer
stays a pure primitive; policy lives in the compile layer.

Rejected alternatives:

- Extending the analyzer with policy helpers (`IsCompleteReturnBody`, …) — blurs
  analysis vs. check policy.
- One mega “control-flow hygiene” check — harder to test, suppress, and route.

## 3. Check A — Not all paths return a value

### 3.1 Subjects

Any of the following that has a **declared return type** and a **body**:

| Construct | Return type source | Body |
|---|---|---|
| Function implementation | `FunctionNode.ReturnType` | `FunctionNode.Body` |
| Method implementation | `MethodNode.ReturnType` (via declaration / impl) | `MethodNode.Body` / `MethodImplNode.Body` |
| Property getter | Property return type on declaration | Getter `PropertyImplNode.Body` |

**Skip:**

- Function prototypes / declarations without a body  
- Abstract methods / methods without an implementation body  
- Procedures and methods/getters with **no** return type  
- Property setters (no return value)

### 3.2 Pass / fail criterion

```text
ValidExits = ExitMode.Return | ExitMode.Throw | ExitMode.Exit | ExitMode.Error
```

For each subject body:

```text
M = CompletionAnalyzer.Analyze(body)
```

**Pass** iff:

1. `M ≠ ExitMode.None`
2. `(M & ~ValidExits) == ExitMode.None`  
   (no `Normal`, no `Break`, no `Continue`)

**Fail** otherwise.

Rationale: PeopleCode can exit a returning routine via `Return`, `Throw`, `Exit`, or
`Error`. Falling off the end (`Normal`) is a bug. Unbound `Break`/`Continue` on the
body’s mode set is also not a valid value-producing exit (and is separately reported
by Check B when the statements are invalidly placed).

### 3.3 Analyzer assumptions (intentional)

`CompletionAnalyzer` **over-approximates** `Normal` (soundness rule from the exit-mode
design):

- `While` / `For` always contribute `Normal` (zero iterations possible).  
- `Repeat` currently also keeps `Normal` (precision gap; acceptable for this check).  
- One-armed `If` contributes `Normal`.  
- `Evaluate` without `When-Other` contributes `Normal`.

This means the check may flag bodies that *might* fall through; it must **never**
accept a body that can fall through. That matches the desired product direction
(stricter than PeopleCode’s real compiler in a useful way).

### 3.4 Diagnostics (two per failing body)

**Primary — signature**

| Field | Value |
|---|---|
| `DiagnosticCode` | `NotAllPathsReturn` (append next free enum value) |
| Severity | `Error` |
| Span | Header declaring the return: prefer name token through `Returns <type>` when available; else declaration name span |
| Message | e.g. `Not all paths return a value in function 'F'` (adjust “function” / “method” / “property getter”) |

**Secondary — one best incomplete region**

| Field | Value |
|---|---|
| `DiagnosticCode` | Same `NotAllPathsReturn` |
| Severity | `Error` |
| Span | Chosen incomplete block (or last-statement / body-end fallback) |
| Message | e.g. `This block can complete without returning a value` |
| Distinction | Optional `FixContext` flag / payload so tooling can tell primary vs secondary; v1 has no quick fix |

**Selecting the single secondary target**

After `Analyze(body)`:

1. Consider descendant **blocks** whose annotated exit mode still has `Normal` or any
   bit outside `ValidExits`.  
2. Prefer the **innermost** (deepest nesting).  
3. Tie-break: earliest source start position.  
4. If the only incomplete region is the body itself (straight-line fall-off): place
   secondary on the **last statement** of the body, or the body end span if empty.

At most **one** secondary per subject body (plus one primary).

### 3.5 Requirement

`CheckRequirement.NotRequired` — pure AST + analyzer; no database / type resolver.

## 4. Check B — Invalid Break / Continue

### 4.1 Binding rules

Walk from the `BreakStatementNode` / `ContinueStatementNode` toward the root. The first
matching binder wins. Stop when leaving the enclosing function / method / getter /
program main block (do not treat an outer construct in another compilation unit).

| Statement | Valid binders |
|---|---|
| **Break** | `ForStatementNode`, `WhileStatementNode`, `RepeatStatementNode`, or `EvaluateStatementNode` |
| **Continue** | `For` / `While` / `Repeat` only (**not** `Evaluate`) |

**Invalid** if no valid binder is found before leaving the enclosing routine.

Do **not** decide validity from post-absorption exit-mode flags alone: absorption
already folds Break into Evaluate/loop `Normal`. Binding is a **parent walk**.

### 4.2 Examples

| Snippet | Result |
|---|---|
| `Break` inside `When` of `Evaluate` | Valid (binds to Evaluate) |
| `Continue` inside `When` only | Invalid |
| `Break` inside `While` inside `Evaluate` | Valid (binds to While) |
| `Break` as only statement in a method | Invalid Break + likely NotAllPathsReturn if method returns a value |

### 4.3 Diagnostics

| Field | Value |
|---|---|
| `DiagnosticCode` | `InvalidBreakContinue` (append next free enum value) |
| Severity | `Error` |
| Span | The Break / Continue statement |
| Message | `Break is not inside a loop or Evaluate` / `Continue is not inside a loop` |
| Count | One diagnostic per invalid statement |

### 4.4 Requirement

`CheckRequirement.NotRequired`.

### 4.5 Optional DRY note (non-blocking)

`ConvertIfEvaluate` already walks for Evaluate-bound Break. Implementation **may**
extract a shared binder helper later; not required for v1 if the check’s parent walk
is small and local.

## 5. Registration & editor surface

1. Append both checks to `CompileChecker.CreateChecks()`.  
2. Add `DiagnosticCode` members with explicit next integer values (stable wire contract).  
3. Surface via existing `CompilerErrorsStyler` — no new styler, no linter.  
4. Quick fixes: **none in v1** (no map entries required unless we add no-op placeholders).

## 6. Testing

Unit tests in `PeopleCodeParser.SelfHosted.Tests/Compilation/`:

### NotAllPathsReturn

- Function with return type, only assignment → primary + secondary  
- Function, both If branches `Return` → no diagnostic  
- Function, one-armed If with `Return` → fail (Normal)  
- Function, only `Throw` / `Exit` / `Error` → pass  
- Function, `Return` only inside `While` → fail (loop Normal)  
- Method with return type, same patterns  
- Property getter missing path → fail  
- Procedure (no return type) with fall-through → **no** diagnostic  
- Body is only unbound `Break` → fail NotAllPathsReturn (and InvalidBreakContinue)

### InvalidBreakContinue

- Break outside any loop/Evaluate → error  
- Continue outside loop → error  
- Break in Evaluate When → ok  
- Continue in Evaluate When (no loop) → error  
- Break/Continue in While → ok  

No AppRefiner integration tests required for v1.

## 7. Future work (explicitly out of this plan)

Tracked for **after** this plan ships:

1. **Return expression type matches declared return type** — type-checker / compile-check
   that each `Return <expr>` is assignment-compatible with the routine’s return type.  
2. **Return must carry an expression** — value-returning function/method/getter must not
   use bare `Return;` if PeopleCode requires a value.  
3. Optional: tighten `CompletionAnalyzer` Repeat-loop `Normal` precision.  
4. Optional: quick fix “add `Return` stub” on incomplete paths.

## 8. Non-goals

- Changing PeopleCode runtime or the real Application Designer compiler.  
- Suppressing individual compile checks via the linter grid (compile checks are all-or-nothing under Compiler errors).  
- Full CFG / SSA; exit-mode sets are sufficient.  
- Fixing ConvertIfEvaluate (already done via CompletionAnalyzer).

## 9. Success criteria

- Returning functions/methods/getters that can fall through report **Error** with
  signature + one in-body marker.  
- Bodies that only exit via Return/Throw/Exit/Error on all paths stay clean.  
- Break/Continue outside valid binders report **Error** on the statement.  
- Offline / no-DB editing still runs both checks (no resolver required).  
- All new behavior covered by `PeopleCodeParser.SelfHosted.Tests` facts.
