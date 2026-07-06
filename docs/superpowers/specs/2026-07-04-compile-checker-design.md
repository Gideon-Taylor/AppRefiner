# Design: PeopleCode Compile Checker

**Date:** 2026-07-04
**Status:** Approved (brainstorm) — pending implementation plan

## 1. Goal & shape

Introduce a single reusable component — `CompileChecker` — that answers *"will this
program compile when saved?"* It takes an already-parsed program (plus its parser
errors and an optional metadata resolver/context) and returns a flat, sorted list of
`CompileDiagnostic`s. It lives in the parser library, is UI- and database-agnostic,
and becomes the one place every "this won't compile" check is defined.

Today the compile-relevant checks are spread across ~11 independent `BaseStyler`
subclasses, each doing its own full AST traversal and each individually toggleable.
There is no coherent reason to enable some and not others — a user who wants to know
"is my code correct" wants all of them. This design consolidates them behind one
component and one styler toggle.

### Consumers

- **AppRefiner** — a single consolidated **"Compiler Errors"** styler that calls the
  checker and renders diagnostics as squiggles + quick-fixes.
- **Tests** — each check unit-tested in isolation against source snippets.
- **(Long-term, not this work)** — an MCP `will-this-compile` endpoint. Falls out
  almost for free because the checker is already headless. Explicit non-goal here.

## 2. Where it lives

New folder `PeopleCodeParser.SelfHosted/Compilation/`, namespace
`PeopleCodeParser.SelfHosted.Compilation`:

- `CompileDiagnostic`, `DiagnosticSeverity`, `DiagnosticCode` — the result model
- `ICompileCheck` — the check-unit interface
- `CompileChecker` — the driver
- `CompileCheckContext` — caller-supplied extras (expected class name, etc.)
- `Checks/` — one file per check unit

### Why the parser library is the correct home

The dependency graph already supports it — the required dependency inversion exists:

- `PeopleCodeTypeInfo` (foundational, no deps) defines `ITypeMetadataResolver` **and**
  `PeopleCodeTypeDatabase` (builtin type info).
- `PeopleCodeParser.SelfHosted` references `PeopleCodeTypeInfo` and already ships two
  semantic visitors — `TypeInferenceVisitor` and `TypeCheckerVisitor` — both taking an
  injected `ITypeMetadataResolver`. `TypeCheckerVisitor.Run(program, resolver, cache)`
  is exactly the shape a compile check wants.
- AppRefiner supplies the concrete, DB-backed `DatabaseTypeMetadataResolver` and
  injects it.

So the parser library can express "I need type metadata" through an interface without
knowing anything about Oracle, `IDataManager`, or Scintilla.

**Wrinkle:** the DB-backed *stylers* (e.g. `InvalidAppClass`) currently call
`IDataManager.CheckAppClassExists(...)` directly (an AppRefiner type), whereas
`TypeCheckerVisitor` uses `ITypeMetadataResolver`. Moving those checks into the library
means re-expressing them against `ITypeMetadataResolver` (e.g. "exists" =
`GetTypeMetadata(path) != null`). This is real work but a genuine improvement: it
unifies everything on one injectable contract and drops direct DB coupling. Pure-AST
checks need no resolver at all.

## 3. Diagnostic model

```csharp
public enum DiagnosticSeverity { Error, Warning }

public sealed record CompileDiagnostic(
    DiagnosticCode Code,        // stable enum, e.g. UnimportedClass, ClassNameMismatch
    DiagnosticSeverity Severity,
    SourceSpan Span,
    string Message,
    object? FixContext = null); // structured payload AppRefiner hands to a quick-fix refactor
```

- `DiagnosticCode` is a stable enum. It doubles as the machine-readable key for the
  future MCP endpoint and as the quick-fix routing key for AppRefiner.
- The library **never** references AppRefiner refactor types. Per the quick-fix
  decision: AppRefiner maps `Code` → quick-fix refactor(s) when building indicators,
  handing `FixContext` to the refactor verbatim. This mirrors the pattern
  `TypeErrorStyler` already uses today with `AssignToVariableContext`.

## 4. Check interface + composite driver

The driver **is** the `ScopedAstVisitor`. It computes scope / variable-registry
tracking exactly once and fans each visited node out to every registered check. This
is the key win over today's model, where every styler independently re-derives scope
on its own full traversal.

```csharp
public interface ICompileCheck
{
    DataManagerRequirement Requirement { get; }   // NotRequired | Optional | Required
    void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink);
    void Finish(CompileCheckContext ctx, IDiagnosticSink sink); // post-traversal analyses
}
```

- Each check keeps its own internal state across `OnNode` calls and emits via the sink.
- Checks that need scope read it from `ctx` (populated by the driver) rather than
  re-deriving it.
- The driver wraps each `OnNode`/`Finish` call in try/catch so one broken check cannot
  abort the shared traversal (parity with today's per-styler isolation in
  `StylerManager`).
- `Finish` covers post-traversal analyses that need the whole class/program collected
  first (e.g. missing method implementation, unimplemented abstract members).

## 5. Pipeline

```
CompileChecker.Check(program, parserErrors, resolver?, context):
  1. Collect parse-level diagnostics   <- parserErrors (syntax errors, missing semicolons)
  2. Type inference pass               <- TypeInferenceVisitor (only if resolver present)
  3. Type check pass                   <- TypeCheckerVisitor  -> type-error diagnostics
  4. Single composite traversal        <- all ICompileChecks whose Requirement is met
  5. Merge + sort by span, dedupe      -> IReadOnlyList<CompileDiagnostic>
```

- Steps 2–3 stay separate passes: inference must fully populate the tree before any
  type-dependent check reads node type attributes, and `TypeCheckerVisitor` already
  exists as its own traversal. Folding the type checker into the composite is deferred
  and only worthwhile if the extra pass ever measurably matters.
- Parse-level checks (step 1) are **not** AST hooks — they come from `parser.Errors` /
  the token stream and are collected directly.
- DB-backed checks are silently skipped when no resolver is supplied, so the component
  degrades gracefully offline — the same behavior as today's `Optional` stylers.

## 6. Check inventory

| Check | Dependency class | Source today | Fix code? |
|---|---|---|---|
| Syntax errors | parse-level | `parser.Errors` (`SyntaxErrors`) | — |
| Missing semicolons | parse-level | `MissingSemicolon` | maybe |
| Redeclared variables | pure-AST (scope) | `RedeclaredVariables` | — |
| Undefined variables *(class code only)* | pure-AST (scope) | `UndefinedVariables` | — |
| Class name mismatch | pure-AST + context | `ClassNameMismatch` (uses `Editor.ClassPath`) | — |
| Type errors | inference + resolver | `TypeErrorStyler` | yes (assign-to-var) |
| Invalid app class | resolver | `InvalidAppClass` (currently `IDataManager` -> re-express on resolver) | — |
| Unimported class | resolver | `UnimportedClassStyler` | yes (add import) |
| Ambiguous class reference | resolver | `AmbiguousClassReferenceStyler` | yes |
| Invalid member access | inference + resolver | `InvalidMemberAccess` | — |
| Missing constructor | resolver | `MissingConstructor` | yes |
| Missing method implementation | resolver | `MissingMethodImplementation` | yes |
| Unimplemented abstract members | resolver | `UnimplementedAbstractMembersStyler` | yes |
| Undeclared functions | AST + builtin fn DB | `UndeclaredFunctionStyler` | — |

"Fix code?" marks checks whose corresponding styler offers a quick-fix today that must
be preserved via the `Code` -> refactor mapping. Exact codes are finalized during
implementation.

## 7. AppRefiner integration

- New `CompilerErrorsStyler : BaseStyler`, **`Active = true` by default** (matches the
  "show everything that's wrong" philosophy and today's `TypeErrorStyler` default).
  Its body does no traversal of its own: it calls `CompileChecker.Check(...)`, then for
  each returned diagnostic adds a squiggle indicator and maps `Code` -> quick-fix
  refactor(s), passing `FixContext` through unchanged.
- It fills `CompileCheckContext` from the editor (`ClassPath` -> expected class name,
  definition path, etc.) and passes `Editor.ParserErrors` and
  `AppDesignerProcess.TypeResolver`.
- `DataManagerRequirement.Optional` — AST/parse checks always run; resolver-backed
  checks light up when a database connection is present.
- **Settings migration:** the ~11 old compile stylers stop being registered in the
  styler grid; their persisted per-styler toggles become obsolete keys (ignored /
  cleaned up). One "Compiler Errors" row replaces them, defaulting on. `StylerManager`
  already runs type inference before stylers execute, so no orchestration change is
  required.
- **Performance note:** consolidating ~11 independent full traversals (plus their
  redundant scope re-derivations) into one shared traversal is a net win, not just an
  organizational one.

## 8. What stays a standalone cosmetic styler

- `UndefinedVariables` splits in two: the **class-scoped** undefined-variable case (a
  genuine compile error) moves into the checker; a surviving, independently toggleable
  styler keeps flagging undefined/undeclared variables in **non-class** code as a code
  smell.
- Genuinely cosmetic stylers are untouched: dead code, meaningless variable names,
  TODO/FIXME, reused for-iterator, property-as-variable, wrong exception variable, etc.

## 9. Phased implementation

Each phase leaves the application building and the consolidated styler working with a
growing set of checks.

1. **Framework** — diagnostic model, `ICompileCheck`, `CompileChecker`,
   `CompileCheckContext`, composite driver. Wire the two already-existing diagnostic
   sources: parser errors (syntax) and `TypeCheckerVisitor` (type errors). Add
   `CompilerErrorsStyler` rendering those. Delete `SyntaxErrors` and `TypeErrorStyler`.
2. **Pure-AST checks** — redeclared variables, undefined-in-class, class-name-mismatch,
   missing semicolons. Delete those stylers; split `UndefinedVariables` per §8.
3. **Resolver-backed checks** — invalid app class, unimported class, ambiguous class
   reference, invalid member access, undeclared functions. Re-express `InvalidAppClass`
   and peers against `ITypeMetadataResolver`.
4. **Structural checks** — missing constructor, missing method implementation,
   unimplemented abstract members (the `Finish`-phase checks). Delete remaining old
   stylers; wire the `Code` -> quick-fix refactor mapping for all fix-bearing checks.

## 10. Testing

- Per-check unit tests in `PeopleCodeParser.SelfHosted.Tests`: source snippet in ->
  expected diagnostics out. Resolver-backed checks use the existing
  `TestTypeMetadataResolver`.
- A few `CompileChecker`-level integration tests covering diagnostic ordering, dedupe,
  and graceful behavior when no resolver is supplied.

## 11. Non-goals

- MCP `will-this-compile` endpoint (deferred; the design keeps it cheap to add later).
- Changing or removing cosmetic stylers.
- Adding new checks beyond today's set.
- Folding `TypeCheckerVisitor` into the composite traversal (deferred optimization).
