# Expression Refactors: Extract Local Variable, Inline Variable, Convert If ↔ Evaluate

**Date:** 2026-07-04
**Status:** Approved design, pending implementation plan

Three new visible refactors that move AppRefiner's refactoring story from hygiene
operations (imports, flower box, collect declarations) into true code transformation,
building on infrastructure that already exists: read/write-classified variable
references in `VariableRegistry`, `TypeInferenceVisitor`, and byte-indexed
`SourceSpan`s that share Scintilla's coordinate system.

---

## Shared plumbing

### Selection range on BaseRefactor

`BaseRefactor` gains selection support, captured in the constructor the same way
`CurrentPosition` is today:

```csharp
public int SelectionStart { get; }   // SCI_GETSELECTIONSTART
public int SelectionEnd { get; }     // SCI_GETSELECTIONEND
public bool HasSelection => SelectionEnd > SelectionStart;
```

Scintilla positions are UTF-8 byte offsets and `SourceSpan` tracks byte indexes,
so selection ↔ span comparison is direct with no char/byte conversion.

### Opt-in type inference in RefactorManager

`BaseRefactor` gains:

```csharp
public virtual bool RequiresTypeInference => false;
```

When true, `RefactorManager.ExecuteRefactor` runs type inference on the freshly
parsed program between `GetParsedProgramWithTokens` and `program.Accept(visitor)`.
The pipeline is the one `StylerManager.RunTypeInferenceForProgram` uses today
(qualified-name determination → `TypeMetadataBuilder.ExtractMetadata` →
`TypeInferenceVisitor.Run` with the process `TypeResolver`); that private method is
extracted into a shared helper (e.g. `Services/TypeInferenceRunner`) called by both
StylerManager and RefactorManager.

Inference **always runs** for opted-in refactors, even without a database
connection: when `AppDesignerProcess.TypeResolver` is null, the helper substitutes
the existing `NullTypeMetadataResolver` (`AppDesignerProcess.cs`). Builtin
functions and object methods/properties resolve through `PeopleCodeTypeDatabase`,
and literals and locally-declared types infer normally — only app class / record
metadata lookups come back empty, in which case the visitor yields `Any` per its
usual fallback. (StylerManager's current skip-when-null behavior is unchanged.)

This also creates a future path for `AssignToNewVariable` to drop its
styler-context type-name workaround (not in scope for this change).

---

## 1. Extract Local Variable

`ExtractLocalVariable : BaseRefactor` — visible, palette-only (`RegisterKeyboardShortcut
=> false` for v1), `RequiresUserInputDialog => true`, `DeferDialogUntilAfterVisitor
=> true`, `RequiresTypeInference => true`, `RunOnIncompleteParse => false`.

### Selection → expression matching

Selection is **required** — cursor position alone is ambiguous (in
`DoAThing(3 + (4 - (|6 * 2)))` the cursor cannot distinguish the three nested
candidate expressions). No selection → fail: *"Select the expression to extract."*

Matching algorithm:

1. Trim leading/trailing whitespace from the selection range.
2. Search the AST for an `ExpressionNode` whose `SourceSpan` byte range equals the
   trimmed range exactly. If multiple nodes share the identical span (wrapper
   nodes), take the outermost.
3. If no match and the trimmed selection starts with `(` and ends with `)`, strip
   one paren pair and go to 1 (loop).
4. No match after paren stripping → fail: *"Selection must cover exactly one
   complete expression."*

This rule inherently rejects selections spanning multiple statements or disparate
expressions (`3, 4 + 6` in an argument list matches no single node).

### Guards

Refuse (via `SetFailure`, with specific messages) when the matched expression:

- is not inside a statement within a method/function/property-getter/setter body
  (e.g. class-header constructs);
- is the assignment target (l-value) of an assignment;
- is a bare identifier / lone variable reference (nothing to extract);
- sits in a re-evaluated or loop-header position: `While` condition, `Repeat`
  condition, `For` from/to/step expressions, or an Evaluate `When` condition
  (hoisting changes re-evaluation semantics). `If` conditions and the Evaluate
  scrutinee itself are allowed.

### Transformation

- **Insertion point:** the nearest ancestor statement of the matched expression
  whose parent is a `BlockNode`. Insert before it, at its indentation (whitespace
  from line start to statement start):
  `Local <type> <name> = <expression text>;` + newline.
- **Type:** best-effort from `node.GetInferredType()` — inference always runs
  (see shared plumbing), so builtin function/method returns, literals, and
  locally-declared types resolve even without a DB connection. Declare `any` only
  when the inferred type is Unknown/Any/Invalid. Render the type name in
  declared-type syntax (arrays, app classes with fully qualified name — reuse
  whatever rendering `TypeErrorStyler`/`AssignToVariableContext` uses today).
- **Replacement:** the matched span is replaced with the variable name.

### Dialog

Shown after the visitor runs (so scope/type info is available). AppRefiner dialog
styling, modeled on the Rename dialog. Contents:

- Variable name textbox, pre-filled with a suggestion: function call → `&<funcName>`,
  member access → `&<memberName>`, else `&value`; uniquified with a numeric suffix
  against the scope chain (`VariableRegistry.FindVariableInScope`, checking both
  `&`-prefixed and bare forms, as `AssignToNewVariable` does).
- Read-only display of the inferred type.
- Checkbox **"Replace all N identical occurrences"**, shown only when N > 1.
- Live validation: name must be a legal PeopleCode user variable and not collide
  with a visible variable.

### Replace-all occurrences

Occurrences are `ExpressionNode`s whose normalized source text (case-insensitive,
whitespace-collapsed) equals the selected expression's, restricted to:

- the same method/function scope,
- located at or after the insertion point,
- within the insertion statement's containing block subtree (so the declaration
  dominates every replacement),
- not an l-value.

---

## 2. Inline Variable

`InlineVariable : BaseRefactor` — visible, cursor-based (no selection needed, same
UX as Rename), no dialog, `RunOnIncompleteParse => false`.

### Target resolution

The variable whose declaration or reference is under the cursor, resolved through
the variable registry (same approach as `RenameLocalVariable`).

### Eligibility (refuse otherwise, with specific messages)

- Kind is `Local` (not instance, global, component, parameter, constant).
- Declared via `LocalVariableDeclarationWithAssignmentNode` (plain declaration +
  later assignment is out of scope for v1).
- Registry shows **exactly one write** — the initializer itself.
- At least one read (zero reads → point user at the Delete Unused Variable quick fix).

### Safety checks (refuse, per user decision — no warn-and-proceed)

1. **Side-effect duplication:** if the initializer subtree contains any call
   (function call, method call, `create`) and the variable has more than one read,
   refuse: *"The initializer calls a function and &x is read N times — inlining
   would evaluate it N times."*
2. **Stale value:** for every variable referenced inside the initializer, the
   registry must show **no writes** positioned between the declaration and the
   last read of the inlined variable. Otherwise refuse: *"&y is reassigned between
   the declaration and a use of &x — inlining would change the value observed."*

Known accepted limitation (documented in code): instance/global variables mutated
indirectly by calls between declaration and reads are not detected.

### Transformation

- Replace every read-reference span with the initializer text, wrapped in
  parentheses unless the initializer is atomic: literal, identifier, member
  access, array index, or function/method call. Binary/unary operations get parens.
- Delete the declaration statement, including its semicolon and the line itself
  if nothing else remains on it.

---

## 3. Convert If ↔ Evaluate

`ConvertIfEvaluate : BaseRefactor` — visible, single auto-detecting command
("Convert If ↔ Evaluate"), cursor-based, no dialog, `RunOnIncompleteParse => false`.

Direction is chosen from the statement containing the cursor: inside an If chain →
If→Evaluate; inside an `EvaluateStatementNode` → Evaluate→If; neither → fail with
*"Place the cursor inside an If/Else-If chain or an Evaluate statement."*

A single `If` (no chain) also converts — you may start writing an `If` and decide
partway that it should be an `Evaluate`. (2026-07-05: the original ≥2-comparison
minimum was dropped after manual testing.)

### Chain model

PeopleCode has no `ElseIf`; a chain link is an `IfStatementNode` whose `ElseBlock`
contains **exactly one** statement which is itself an `IfStatementNode`. The
refactor walks up from the cursor's If to the topmost chain member, then down to
collect all links. A trailing `ElseBlock` that isn't a chain link becomes the
`When-Other` body.

### If → Evaluate

Convertible when **all** of the following hold (else fail with the failing reason):

- Every condition is a single `BinaryOperationNode` with operator in
  `{=, <>, <, <=, >, >=}` (no `And`/`Or` in v1; `Or`-of-comparisons → stacked
  When clauses is noted as a future enhancement).
- One operand of every comparison is the same **scrutinee**, matched by normalized
  source text (case-insensitive, whitespace-collapsed). The scrutinee may be on
  either side; when on the right, the operator is mirrored (`5 < &x` → `When > 5`).
- No Then/Else body contains a `Break` that would bind to the generated Evaluate —
  i.e. a `BreakStatementNode` not nested inside a loop or Evaluate *within that
  body*. (Such a Break currently targets an enclosing loop; wrapping it in an
  Evaluate would silently retarget it.)

Generation — the whole chain's span is replaced with:

```
Evaluate <scrutinee>
When <op> <value>
   <body statements>
   Break;
...
When-Other
   <else body>
End-Evaluate;
```

- **Semantics note (the critical PeopleCode detail):** Evaluate falls through —
  after a matching When body executes, subsequent When conditions are still
  tested. Every generated When body therefore ends with `Break;` to preserve
  if/else-if semantics. `When-Other` needs no Break.
- Equality is rendered explicitly as `When = <value>`.
- Body text is captured verbatim from source, dedented to its own base
  indentation, and re-indented one level under the When (chain bodies sit at
  increasing depths in the original nested-If form, so per-body normalization is
  required).

### Evaluate → If

Convertible when **all** of the following hold:

- Every non-empty When body's last top-level statement is `Break;` (intentional
  fall-through cannot be represented as if/else — fail with an explanatory
  message).
- No other `Break` in any When body binds to the Evaluate (same nesting rule as
  above).

Generation:

- Consecutive empty-bodied When clauses merge into the next non-empty one as
  `Or`-joined conditions: `When = "A" When = "B" <body>` →
  `If &x = "A" Or &x = "B" Then <body>`.
- Trailing `Break;` statements are dropped.
- `When-Other` → final `Else`.
- Output is the standard nested form: `If … Then … Else If … Then … End-If;`
  chain with matching `End-If`s, re-indented per level.

---

## Error handling

All refusals go through the existing `SetFailure` → `MessageBoxDialog` path in
`RefactorManager.ExecuteRefactor`. Every guard has a distinct, actionable message
(listed above) — no generic "cannot refactor" failures.

## Testing

- Unit-style tests where feasible via `BaseRefactor.GetEdits()` (internal
  accessor already exists): feed source + selection/cursor, assert produced edits.
- Manual test plan additions (following the project's manual-test convention):
  - Extract: nested-expression selections, paren-wrapped selections, multi-statement
    and disparate-expression selections (must refuse), loop-header refusals,
    replace-all across nested blocks, no-DB inference (builtin call → concrete
    type, app class expression → `any`).
  - Inline: single-read/multi-read with and without calls, stale-value refusal,
    parenthesization around binary initializers.
  - If↔Evaluate: round-trip conversion is a fixpoint (convert → convert back →
    original semantics), Break-binding refusals both directions, right-side
    scrutinee with mirrored operators, stacked-When ↔ Or merging, fall-through
    Evaluate refusal.

## Out of scope (explicitly deferred)

- Extract Method (planned as a future headline feature).
- `Or`-of-comparisons → stacked When clauses in If→Evaluate.
- Inlining `Local &x;` + separate first assignment.
- Warn-and-proceed mode for Inline safety hazards.
- Migrating `AssignToNewVariable` onto `RequiresTypeInference`.
