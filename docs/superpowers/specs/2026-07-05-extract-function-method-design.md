# Extract Function / Method — Design

**Date:** 2026-07-05
**Status:** Approved design, ready for implementation planning
**Related:** `AppRefiner/Refactors/ExtractLocalVariable.cs` (sibling refactor, shared patterns)

## Goal

Add an "Extract Function/Method" refactor: take a selected contiguous run of
statements and replace it with a call to a newly-generated function (in a
non-app-class program or the main block) or method (in an app class). The
generated routine receives the selection's live-in variables as parameters and
propagates its live-out variables back via a single `Returns` value and/or `out`
parameters.

This is the statement-level analogue of `ExtractLocalVariable` (which operates on
a single expression). It reuses the same infrastructure: `BaseRefactor`,
`ScopedAstVisitor` scope/variable tracking, the `VariableRegistry`, byte-indexed
`SourceSpan` editing, and the deferred-dialog flow.

## Scope

- **Both targets from the start.** The refactor detects app-class vs. non-app-class
  and generates a method or a function accordingly.
- **Multi-output supported via `out` parameters.** PeopleCode models `out`
  parameters as pass-by-reference (`ParameterNode.IsOut`), so a value can flow in
  and back out on a single parameter. This removes the "functions return only one
  value" limitation.

## Refactor class

`ExtractFunctionMethod : BaseRefactor` in `AppRefiner/Refactors/`.

Property overrides:
- `RefactorName` = "Extract Function/Method"
- `RefactorDescription` = "Extracts the selected statements into a new function or method"
- `RegisterKeyboardShortcut` = false
- `RequiresUserInputDialog` = true
- `DeferDialogUntilAfterVisitor` = true (analysis must complete before the dialog)
- `RequiresTypeInference` = true (fallback for rendering parameter/return types)
- `RunOnIncompleteParse` = false (structure-sensitive)

Auto-discovered by reflection like all refactors; no registration step.

## Phase 1 — Locate the statement range

Mirrors `ExtractLocalVariable`'s expression-location approach, but the unit is a
run of statements.

1. Require `HasSelection`; otherwise `SetFailure("Select the statements to extract.")`.
2. In a `VisitBlock` override, when the block's span contains the (trimmed)
   selection start, capture `GetCurrentScope()` and the enclosing block/method/
   function node — scope contexts are gone by the time `OnExitGlobalScope` runs
   the location logic (same reason ExtractLocalVariable captures `containingScope`).
3. In `OnExitGlobalScope`:
   - Trim whitespace off the selection byte range (reuse the `TrimWhitespace`
     helper pattern).
   - Find the **deepest `BlockNode`** whose span covers the trimmed selection.
   - Within that block, gather the **contiguous sibling statements** whose combined
     span covers the selection.
   - Validate and fail clearly if:
     - the selection starts or ends inside a statement (must snap to statement
       boundaries after trimming): "Selection must cover whole statements.";
     - the selection spans more than one block / crosses a block boundary;
     - no statements are selected.
   - A single-statement selection is valid.

Record: the ordered list of selected statements, the containing block, the
enclosing scope, and whether the enclosing program is an app class (drives
function-vs-method).

## Phase 2 — Safety guards

Walk the selected statements' subtrees and block (with a specific message) when
relocating the code into a routine would change meaning:

- Any `ReturnStatementNode` in the selection → block ("selection contains a Return").
- Any `Break`/`Continue` whose nearest enclosing loop is **outside** the selection
  → block. (A loop fully contained in the selection, with its own break/continue,
  is fine.)
- `Exit` is **allowed**: it terminates PeopleCode execution regardless of location,
  so semantics are preserved.

## Phase 3 — Data-flow classification

Candidate variables = those with `Kind ∈ {Local, Parameter, Exception}`. Instance
variables, globals, component variables, constants, and `%This` members are
reachable from the extracted routine unchanged, so they are **never** parameters.
(This is what makes method extraction cheaper than function extraction.)

For each candidate, partition its `Read`/`Write` references (ignore `Declaration`
and `ParameterAnnotation` for liveness, but track the declaration's position) into
**before / inside / after** the selected byte range. Note that the reference
tracker already records `&obj.Method(...)` as a **Read** of `&obj`, so pure object
mutation never looks like a write — only rebinding assignments, for-loop
iterators, and property writes are `Write` references.

Derived facts per variable:
- `definedBefore` = declared before range, OR written before range, OR `Kind == Parameter`.
- `readInside`, `writeInside`, `readAfter`.
- `declaredInside` = declaration position falls within the range.

Classification:

| Condition | Role |
|---|---|
| `readInside` and `definedBefore`, not reassigned inside | **value parameter** `&p As T` |
| `readInside` and reassigned inside and `readAfter` | **in/out parameter** `&p As T out` |
| reassigned inside (not read inside before first write) and `readAfter` | **output** — Return candidate or `out` param |
| declared and used only inside the range | internal local — ignored |

"Outputs" = variables reassigned inside and read after. In/out variables are a
subset that must be `out` params (they carry a value in). Pure outputs (including
variables declared inside the range and read after) are Return candidates.

## Phase 4 — Dialog (deferred until after the visitor)

A WinForms dialog in the same borderless AppRefiner style as `ExtractVariableDialog`:

- **Name** textbox. Validate: legal routine identifier (no leading `&`), and no
  collision with an existing function/method name in the program.
- **Visibility dropdown** — methods only: Private / Protected / Public, default
  **Private**. Hidden for function extraction.
- **Return value dropdown** — the Return candidates plus "(none — all via out
  params)". Smart default:
  - exactly one Return candidate → preselect it;
  - prefer a candidate that is **declared inside** the selection (the most natural
    return);
  - otherwise "(none)".
  Whatever is not chosen as the Return, if it is an output, becomes an `out` param.
- **Live signature preview** label that updates as the name / visibility / return
  choice change, e.g. `Function Foo(&a As string, &b As number out) Returns date`
  or `private Method Foo(&a As string) Returns boolean`.
- OK / Cancel, Enter/Escape handling, matching `ExtractVariableDialog`.

## Phase 5 — Generation

### Parameter list order
Value + in/out params in order of first reference within the selection, then pure
`out` params, in the same order. The Return variable (if any) is not a parameter.

### Types
Render from `VariableInfo.Type` (the declared type string — available without a DB
connection); fall back to `Services.TypeInferenceRunner.RenderDeclaredType(node.GetInferredType())`
when the declared type is absent.

### Body
The exact source text of the selected statements, re-indented one level relative to
the routine header. Append `Return &ret;` when a Return variable is chosen. Emit
`Local T &x;` at the top of the body for any output-only Return variable that was
**declared before** the range (inside the routine it is a fresh local).

### Call site
Replace the selected statements' full byte span with one call statement:
- void: `Foo(&a, &b);`
- return: `&ret = Foo(&a, &b);`, or `Local T &ret = Foo(&a, &b);` when `&ret` was
  originally **declared inside** the range (the caller now needs the declaration).

Plus caller-side `Local T &o;` declarations, immediately before the call, for any
`out` param whose variable was **declared inside** the range (its declaration moves
out of the extracted body to the caller).

### Declaration relocation — case matrix for output variables

| Declared | Chosen role | Body | Caller |
|---|---|---|---|
| before range | Return | add `Local T &x;` at top, `Return &x` | `&x = Foo(...)` |
| before range | out param | `&x` is a param, no body decl | `Foo(..., &x)` (already declared) |
| inside range | Return | keep its `Local T &x = ...;`, `Return &x` | `Local T &x = Foo(...)` |
| inside range | out param | strip the `Local T` from its decl (now a param) | add `Local T &x;` before the call |

### Insertion point
- **Function**: insert the `Function … End-Function` block above the function/main
  block containing the selection (reuse the destination logic from
  `MoveFunctionAbove`).
- **Method**: insert the method **declaration** into the chosen visibility section
  of the class header (create the section if absent), and append the method
  **implementation** after the last existing method implementation (or at the end
  of the program if none). Uses `AppClassNode.VisibilitySections` /
  `VisibilityModifier`.

### Editing mechanics
Collect all edits via `EditText` / `InsertText` / `DeleteText`; `BaseRefactor`
applies them in descending position order. Watch the same insert-before-delete
ordering hazards documented in `MoveFunctionAbove` and `ExtractLocalVariable`. All
indices are UTF-8 byte offsets — slice through `SourceBytes`, never the C# string.

## Non-goals (v1)

- Bundling multiple outputs into a synthesized array/object (not needed — `out`
  params cover it).
- Extracting across block boundaries or partial statements.
- Auto-naming the routine from the selection's content (a static default suggestion
  is fine; the user names it).
- Extracting selections that contain a `Return` or a boundary-crossing
  `Break`/`Continue` (blocked, not rewritten).

## Reused patterns / references

- `ExtractLocalVariable.cs` — scope capture in `VisitBlock`, `OnExitGlobalScope`
  location, whitespace trimming, name suggestion/validation, borderless dialog,
  edit ordering.
- `MoveFunctionAbove.cs` — whole-block insertion above a target, insert-before-
  delete ordering, leading-comment handling.
- `VariableRegistry` / `VariableInfo` / `VariableReference` — reference buckets,
  declared type string, `ReferenceType`.
- `AppClassNode.VisibilitySections`, `VisibilityModifier`, `ParameterNode.IsOut`.
