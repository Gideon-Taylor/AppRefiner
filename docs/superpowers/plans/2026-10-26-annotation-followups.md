# Annotation Follow-ups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close two gaps left by phases 0–2: `create` calls skip the annotated-parameter check, and an AST node can hold only one type warning.

**Architecture:** Task 1 makes `TypeCheckerVisitor.CheckAnnotatedParameters` take an argument list so `VisitObjectCreation` can call it. Task 2 changes the type-warning attribute from one `TypeError` to a list, so every caller appends. It keeps one deliberate "pick one" rule: the declaration-annotation conflict on an assignment's value.

**Tech Stack:** C# / .NET 8, xUnit. Projects: `PeopleCodeParser.SelfHosted`, `PeopleCodeParser.SelfHosted.Tests`.

**Spec:** `docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md` (§7: annotations, and "one diagnostic per bad use").

## Global Constraints

- Use AppRefiner's `Debug.Log()`, never `Debug.WriteLine()`; never `MessageBox.Show()`.
- Commit on `main`, one commit per task, no push.
- Full suites must stay green: `dotnet test PeopleCodeParser.SelfHosted.Tests` and `dotnet test PeopleCodeTypeInfo.Tests`; `dotnet build AppRefiner/AppRefiner.csproj` must build.
- One diagnostic per bad use (spec): a single mistake must not produce two warnings saying the same thing.
- Type *errors* (`SetTypeError`) stay single-slot. That is out of scope.

## Review Focus

1. Constructor of a class in the **same program** as the `create` (not only cross-program): the check should still fire if `GetFunctionInfo()` is set there.
2. `create` of a class with no explicit constructor (default constructor, zero parameters): no warning and no exception.
3. The same visitor pass running twice over one AST, as AppRefiner re-runs inference on a cached tree: warnings must not double up.
4. A multi-variable declaration with a misfit annotation (`Local string /* as Session */ &a, &b;`): one warning per variable, each naming its own variable. Today only the last one survives.
5. The single-diagnostic rule from `Declaration_conflict_wins_over_assignment_conflict` must keep holding once the slot becomes a list.

---

### Task 1: Annotated-parameter check on constructor calls

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs` (`VisitObjectCreation`, `CheckAnnotatedParameters`)
- Test: `PeopleCodeParser.SelfHosted.Tests/AnnotatedParameterCallTests.cs`

**Interfaces:**
- Consumes: `ObjectCreationNode.GetFunctionInfo()`, which inference sets from `TypeMetadata.Constructor`. The constructor's `SingleParameter.RefinedParameterType` is already filled by `TypeMetadataBuilder`.
- Produces: `private void CheckAnnotatedParameters(AstNode callNode, IReadOnlyList<ExpressionNode> arguments, FunctionInfo functionInfo)`. `callNode` is the node to read `GetParameterAnnotations()` from; on an `ObjectCreationNode` that returns null, so the parameter's own `RefinedParameterType` is used. Constructors are not inherited, so no inheritance logic is added.

- [ ] **Step 1: Write the failing tests** in `AnnotatedParameterCallTests`. Use a `FakeTypeMetadataResolver` holding `PKG:Svc`, extracted from:
  ```
  class Svc
     method Svc(&s As ApiObject /* as Session */);
  end-class;
  ```
  - `Constructor_different_subtype_argument_warns`: program `Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\nLocal PKG:Svc &o = create PKG:Svc(&t);` → errors empty; exactly one warning, message `"Svc expects &s as Session (annotated); the argument is ComponentInterface."`, and the warning's node is an `IdentifierNode`.
  - `Constructor_matching_subtype_argument_is_silent`: `Local PKG:Svc &o = create PKG:Svc(%Session);` → no errors, no warnings.
  - `Constructor_plain_ApiObject_argument_is_silent`: `Local ApiObject &a;\nLocal PKG:Svc &o = create PKG:Svc(&a);` → no errors, no warnings.
  - `Default_constructor_create_is_silent`: resolver class `PKG:Empty` from `class Empty\nend-class;\n`; `Local PKG:Empty &o = create PKG:Empty();` → no errors, no warnings.
  - `Same_program_constructor_argument_warns` (Review Focus 1): one program declares `class Svc` with that constructor plus a method `method Make();`. The method body is `Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\nLocal Svc &o = create Svc(&t);`. Expect the same single warning. If inference does not set `FunctionInfo` for a same-program `create`, assert no warning instead. Name the test `Same_program_constructor_is_not_checked`, and record the gap in the report and in `docs/architecture/type-system-weak-spots.md` (do not widen inference in this task).
- [ ] **Step 2: Run** `dotnet test PeopleCodeParser.SelfHosted.Tests --filter AnnotatedParameterCallTests`. Expected: the cross-program warning test FAILS (no warning).
- [ ] **Step 3: Implement.** Change `CheckAnnotatedParameters` to the signature above. Update the `FunctionCallNode` call site to pass `(node, node.Arguments, functionInfo)`. Call it at the end of `VisitObjectCreation`, after the validator block, whether or not validation failed (the same order as `VisitFunctionCall`). Update the XML doc comment to mention constructor calls.
- [ ] **Step 4: Run** the full `PeopleCodeParser.SelfHosted.Tests` suite. Expected: all pass.
- [ ] **Step 5: Commit** `Check annotated constructor parameters at create calls`.

### Task 2: Multiple type warnings per node

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/AstNodeExtensions.cs` (type-warning region)
- Modify: every `SetTypeWarning` caller: `Visitors/TypeInferenceVisitor.cs` (ApplyAssignmentAnnotation, RefineAnnotatedVariable, OnVariableDeclared, CheckReturnAnnotation) and `Visitors/TypeCheckerVisitor.cs` (`RecordTypeWarning`)
- Modify: `docs/architecture/type-system-weak-spots.md`, if it mentions the single slot or the "names the last variable" limitation
- Test: new `PeopleCodeParser.SelfHosted.Tests/TypeWarningListTests.cs`, plus `DeclarationAnnotationTests.cs` and `AssignmentAnnotationTests.cs` as needed

**Interfaces:**
- Produces (replacing `SetTypeWarning` and `GetTypeWarning`, which are deleted; no callers remain):
  - `public static void AddTypeWarning(this AstNode node, TypeError warning)`: appends to a `List<TypeError>` stored under `AstNode.TypeWarningAttributeKey`. It does nothing if the node already holds a warning with the same `Message` (ordinal). This dedupe is what keeps a re-run pass from doubling warnings.
  - `public static IReadOnlyList<TypeError> GetTypeWarnings(this AstNode node)`: an empty list when there are none.
  - `HasTypeWarning` is unchanged in meaning. `GetAllTypeWarnings` yields every warning on every node, in insertion order per node, with the same traversal as today.
- `CompileChecker` already consumes `GetAllTypeWarnings()`. Verify it compiles unchanged.

- [ ] **Step 1: Write the failing tests** in `TypeWarningListTests`. Parse any small program, take its root and nodes, and run inference and checking as the other annotation tests do.
  - `Two_different_warnings_on_one_node_are_both_kept`: on any node, `AddTypeWarning` messages "a" then "b" → `GetTypeWarnings` returns ["a", "b"], and `GetAllTypeWarnings` contains both.
  - `Same_message_twice_is_kept_once`: add "a" twice → one entry.
  - `Rerunning_inference_does_not_duplicate_warnings` (Review Focus 3): program `Local string /* as Session */ &a;`. Run `TypeInferenceVisitor.Run` twice on the same `ProgramNode` → `GetAllTypeWarnings()` has exactly one entry.
  - `Multi_variable_misfit_warns_per_variable` (Review Focus 4): `Local string /* as Session */ &a, &b;` → exactly two warnings. Their messages are `MisfitMessage` for `&a` and for `&b` (`"'as Session' needs an ApiObject declaration; &a is string."` and the same for `&b`). Check the exact text against `MisfitMessage` in `TypeInferenceVisitor` and keep its current wording.
- [ ] **Step 2: Run** them. Expected: compile failure (`AddTypeWarning` not defined).
- [ ] **Step 3: Implement** the extensions. Replace every `SetTypeWarning(...)` call with `AddTypeWarning(...)`.
- [ ] **Step 4: Keep the deliberate single diagnostic** (Review Focus 5). In `TryRefineVariableFromExpression`, an assignment can hit both an assignment-annotation value conflict (in `ApplyAssignmentAnnotation`: "The value is X, but it is annotated as Y…") and a declaration-annotation conflict on the same expression (in `RefineAnnotatedVariable`: "&x is annotated as … but is assigned … here."). Only the declaration warning may be recorded. Today this works because the later write overwrites the earlier one. Make it explicit:
  - `ApplyAssignmentAnnotation` returns its value-conflict message instead of recording it (for example, an `out string? conflictWarning`).
  - `RefineAnnotatedVariable` reports whether it recorded a warning.
  - The value-conflict message is recorded only when `RefineAnnotatedVariable` did not warn, or when the variable has no declaration annotation.

  The unknown-annotation and misfit warnings in `ApplyAssignmentAnnotation` are unaffected, because they return before any conflict. `Declaration_conflict_wins_over_assignment_conflict` must pass unchanged. Then grep for any other place that relied on overwrite, and list in the report each `AddTypeWarning` site and whether it can coincide with another warning on the same node:
  - the checker's `RecordTypeWarning` on assignment nodes: the auto-declared change and the one-type warning are mutually exclusive branches;
  - the checker's argument warnings against inference's warnings on the same argument node.
- [ ] **Step 5: Run** the full `PeopleCodeParser.SelfHosted.Tests` and `PeopleCodeTypeInfo.Tests` suites, and `dotnet build AppRefiner/AppRefiner.csproj`. Expected: all green. A test that asserted a single warning where two distinct, legitimate warnings now appear is a finding: report it, and do not change its assertion to fit.
- [ ] **Step 6: Update** `docs/architecture/type-system-weak-spots.md` to remove or adjust the single-slot entry, if present. **Commit** `Allow multiple type warnings per node`.
