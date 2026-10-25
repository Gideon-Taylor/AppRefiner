# ApiObject Type Resolution — Phases 0, 0b, 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the Record/Field naming heuristic from applying to user code, stop false errors on component-interface properties, and split a variable's declared type from its refined type so reassigning an `ApiObject` variable gives a style warning instead of a false type error.

**Architecture:** Phase 0 adds an origin flag to `FunctionInfo` and gates `SpecializeNamedInstanceReturn` on it. Phase 0b adds `ComponentInterface` to `InvalidMemberAccessCheck`'s dynamic-property exemption. Phase 1 stops refinement from overwriting `VariableInfo.Type`, records a `RefinementSource` on the variable and stamps it on each identifier use, and makes `TypeCheckerVisitor` check `ApiObject`/`any` targets against their declared type, emitting the one-type warning.

**Tech Stack:** C# / .NET 8, xUnit (`PeopleCodeParser.SelfHosted.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md` (§2 Gaps, §5, §12 phases 0/0b/1, §13).

## Global Constraints

- Tests use the in-memory resolvers already in the test project (`NullTypeMetadataResolver`, `FakeTypeMetadataResolver`). No database.
- Each phase ships on its own; commit after each task, directly on `main` (no feature branch, no push).
- Tim runs builds himself: confirm with him before the first `dotnet build`/`dotnet test` of the session.
- The one-type warning is only for `ApiObject`-declared variables, never an error. `any`-declared and auto-declared variables get no warning (spec §3).
- Auto-declared variables keep today's behaviour: typed on their first assignment only, via `UpdateType` (spec §5).
- `SourcePosition.Line` is zero-based; user-facing "line N" is `Line + 1`.
- Diagnostic text for the one-type warning (spec §10): `&x is assigned more than one API type (Session on line 4, ComponentInterface on line 9). Use a separate variable for each type.`
- Use AppRefiner's own `Debug.Log()` if logging is needed, never `Debug.WriteLine()`.

## Review Focus

1. **Reassigning the same subtype** (`&x = %Session; … &x = %Session;`) must give no warning. Test in Task 4.
2. **Assigning an unresolved `ApiObject` value** to a refined variable (e.g. the result of a user function declared `Returns ApiObject`) must give no error and no warning, and the variable keeps its previous refined type. Test in Task 4.
3. **`Record` variables reassigned to different named records** must still give no error and no warning (regression guard for the refinement change). Test in Task 4.
4. **Writes to CI properties** (`&ci.EMPLID = "x";`) must be exempt, not just reads. Test in Task 2.
5. **Case-insensitive declarations** (`Local apiobject &x`) must behave the same as `ApiObject`. Test in Task 4.

---

### Task 1: Phase 0 — limit the Record/Field naming heuristic to built-ins

Today `ConvertFunctionInfoToTypeInfo` always calls `SpecializeNamedInstanceReturn`, so user functions returning `Record`/`Field` get names guessed from their arguments or the default record.

**Files:**
- Modify: `PeopleCodeTypeInfo/Functions/FunctionInfo.cs` (add property)
- Modify: `PeopleCodeTypeInfo/Functions/BuiltinObjectReaderStrategy.cs:137` and `PeopleCodeTypeInfo/Functions/FunctionReaderStrategy.cs:46` (set it)
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs:285-288` (`ConvertFunctionInfoToTypeInfo`)
- Test: `PeopleCodeParser.SelfHosted.Tests/NamedInstanceReturnTests.cs` (new)

**Interfaces:**
- Produces: `FunctionInfo.IsBuiltin` (`bool`, settable, default `false`). True only for `FunctionInfo`s read from typeinfo.dat. Phase 4 relies on user functions having `false`.

This resolves the spec's "decide in phase 0" item: a flag set by the reader, not passing the origin from `ResolveFunctionInfo`. The flag also covers builtin methods reached through `LookupMethodInInheritanceChain` on classes with a builtin base, which `ResolveFunctionInfo` alone wouldn't distinguish.

- [ ] **Step 1: Write the failing tests**

Test helper: parse, `TypeMetadataBuilder.ExtractMetadata`, `TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance, defaultRecordName)`, then return the inferred type of the initial-value expression of the last `Local … = …` declaration (via `FindDescendants<LocalVariableDeclarationWithAssignmentNode>()`).

```csharp
[Fact] public void CreateRecord_with_record_reference_is_named()
// "Local Record &r = CreateRecord(Record.JOB);"  → Assert.IsType<RecordTypeInfo>; RecordName == "JOB"

[Fact] public void Builtin_GetRecord_with_no_args_in_record_peoplecode_uses_default_record()
// defaultRecordName "MY_REC"; "Local Record &r = GetRecord();" → RecordTypeInfo, RecordName == "MY_REC"

[Fact] public void Builtin_GetField_with_string_literal_is_named()
// "Local Field &f = GetField(\"OPRID\");" → FieldTypeInfo, FieldName == "OPRID"

[Fact] public void Record_GetField_with_field_reference_keeps_receiver_record()
// "Local Record &r = CreateRecord(Record.JOB); Local Field &f = &r.GetField(Field.EMPLID);"
// → FieldTypeInfo, RecordName == "JOB", FieldName == "EMPLID"

[Fact] public void Zero_arg_user_function_returning_record_is_not_named_after_default_record()
// defaultRecordName "MY_REC";
// "Function GetIt() Returns Record\n   Return CreateRecord(Record.JOB);\nEnd-Function;\nLocal Record &r = GetIt();"
// → Assert.IsNotType<RecordTypeInfo>; PeopleCodeType == PeopleCodeType.Record

[Fact] public void User_function_with_record_argument_is_not_named_after_argument()
// "Function CopyTo(&rec As Record) Returns Record\n   Return &rec;\nEnd-Function;\nLocal Record &r = CopyTo(Record.FOO);"
// → not RecordTypeInfo

[Fact] public void User_function_returning_field_with_string_argument_is_not_named()
// "Function MyFunc(&s As string) Returns Field\n   Return GetField(\"X\");\nEnd-Function;\nLocal Field &f = MyFunc(\"hello\");"
// → Assert.IsNotType<FieldTypeInfo>; PeopleCodeType == PeopleCodeType.Field
```

- [ ] **Step 2: Run tests to verify the three user-function tests fail and the four builtin tests pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~NamedInstanceReturnTests`
Expected: 3 FAIL (`Zero_arg_…`, `User_function_with_record_…`, `User_function_returning_field_…`), 4 PASS. If a user-function test already passes, note it in the commit message (the spec's bug list came from reading code) and keep the test.

- [ ] **Step 3: Implement**

Add `public bool IsBuiltin { get; set; }` to `FunctionInfo` with a doc comment ("True when read from typeinfo.dat"). Set `IsBuiltin = true` in both reader strategies' object initializers. In `ConvertFunctionInfoToTypeInfo`, return `returnType` unchanged when `!func.IsBuiltin`, and only call `SpecializeNamedInstanceReturn` otherwise. Update the comment above it to say builtins only.

- [ ] **Step 4: Run tests to verify they pass, then the whole test project**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests` and `dotnet test PeopleCodeTypeInfo.Tests`
Expected: all PASS. If an existing test relied on user-function naming, it encoded the bug; update it and say so in the commit.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeTypeInfo/Functions PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs PeopleCodeParser.SelfHosted.Tests/NamedInstanceReturnTests.cs
git commit -m "Limit Record/Field return naming to builtin functions"
```

---

### Task 2: Phase 0b — stop false errors on CI properties

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs:62-66`
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidMemberAccessCheckTests.cs` (add tests, use existing `Run` helper)

**Interfaces:** none new.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Does_not_report_dynamic_property_read_on_component_interface()
// "Local ApiObject &s = %Session;\nLocal ApiObject &ci = &s.GetCompIntfc(CompIntfc.USER_PROFILE);\nLocal string &e = &ci.EMPLID;"
// Assert.DoesNotContain(diags, d => d.Code == DiagnosticCode.InvalidMemberAccess && d.Message.Contains("EMPLID"));

[Fact] public void Does_not_report_dynamic_property_write_on_component_interface()
// same setup, then "&ci.EMPLID = \"X\";"  → no InvalidMemberAccess mentioning EMPLID

[Fact] public void Still_reports_unknown_method_on_component_interface()
// same setup, then "&ci.TotallyBogusMethod();"
// Assert.Contains InvalidMemberAccess, Message contains "TotallyBogusMethod" and "method"
```

- [ ] **Step 2: Run to verify the two property tests fail and the method test passes**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~InvalidMemberAccessCheckTests`
Expected: the two `Does_not_report_dynamic_property_…` tests FAIL with a diagnostic mentioning EMPLID. If they pass, `&ci` isn't being refined to `ComponentInterface`; investigate before changing the check.

- [ ] **Step 3: Implement**

Add `PeopleCodeType.ComponentInterface` to the `Record or Row or Grid` pattern on the property-access exemption, and extend the comment above it: CI properties are the CI's own keys and fields, resolved at runtime; only methods are validated. (Spec §16: a named CI will later be checked against its definition.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~InvalidMemberAccessCheckTests`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidMemberAccessCheckTests.cs
git commit -m "Skip property validation on ComponentInterface like Record/Row/Grid"
```

---

### Task 3: Phase 1 (part 1) — declared vs. refined type and refinement source

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Visitors/Models/RefinementSource.cs`
- Modify: `PeopleCodeParser.SelfHosted/Visitors/Models/VariableInfo.cs:73, 190-194`
- Modify: `PeopleCodeParser.SelfHosted/AstNodeExtensions.cs` (new region next to FunctionInfo Extensions)
- Modify: `PeopleCodeParser.SelfHosted/AstNode.cs:~99` (attribute key)
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs` — `TryRefineVariableFromExpression` (530-576), its two callers (1089, 1186), `VisitIdentifier` (1310-1337)
- Test: `PeopleCodeParser.SelfHosted.Tests/ApiObjectRefinementTests.cs` (new)

**Interfaces:**
- Produces:
  - `public enum RefinementKind { Assignment, Annotation, Narrowing }` (only `Assignment` is used in this phase; the other two are phase 2 and 3).
  - `public sealed record RefinementSource(RefinementKind Kind, AstNode Node)` with `public int Line => Node.SourceSpan.Start.Line;` (zero-based). Namespace `PeopleCodeParser.SelfHosted.Visitors.Models`.
  - `VariableInfo.RefinementSource` (`RefinementSource?`, private set).
  - `VariableInfo.Refine(TypeInfo type, RefinementSource source)`: sets `InferredType` and `RefinementSource`; does **not** touch `Type`.
  - `VariableInfo.SetInferredType(TypeInfo)` keeps its current behaviour (sets `InferredType` and calls `UpdateType`) and is now used only by the auto-declared path. Add a doc comment saying so.
  - `AstNode.RefinementSourceAttributeKey = "RefinementSource"`; `AstNodeExtensions.GetRefinementSource(this AstNode) : RefinementSource?` and `SetRefinementSource(this AstNode, RefinementSource)`.
  - `TryRefineVariableFromExpression(string variableName, ExpressionNode? expression, AstNode sourceNode)`. Callers pass the declaration node (1089) or the `AssignmentNode` (1186).

- [ ] **Step 1: Write the failing tests**

Helper: parse, `ExtractMetadata`, `TypeInferenceVisitor.Run(program, metadata, NullTypeMetadataResolver.Instance)`; `LastUse(program, "&x")` returns the last `IdentifierNode` named `&x` in source order (`FindDescendants<IdentifierNode>()`, ordered by `SourceSpan.Start`).

```csharp
[Fact] public void Reassigned_ApiObject_variable_takes_latest_type()
// "Local ApiObject &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);\n&x.Save();"
// Assert.Equal("ComponentInterface", LastUse(p, "&x").GetInferredType()!.Name, ignoreCase: true)

[Fact] public void Use_after_assignment_carries_refinement_source()
// "Local ApiObject &x;\n&x = %Session;\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);"
// var src = LastUse(p, "&x").GetRefinementSource();
// Assert.NotNull(src); Assert.Equal(RefinementKind.Assignment, src!.Kind); Assert.Equal(1, src.Line);

[Fact] public void Any_declared_variable_keeps_refining()
// "Local any &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);\n&x.Save();"
// last use type == ComponentInterface

[Fact] public void Auto_declared_variable_is_typed_on_first_assignment_only()
// TypeInferenceVisitor.Run(..., inferAutoDeclaredTypes: true):
// "&n = 1;\n&n = \"text\";\n&m = &n;"  → last use of &n is integer (matches today's behaviour)
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~ApiObjectRefinementTests`
Expected: `Reassigned_…` FAIL (type is Session), `Use_after_…` FAIL (doesn't compile until the new types exist; create the types first if needed so the failure is an assertion), `Any_declared_…` FAIL (today `Type` is overwritten to "Session", so `any` stops refining), `Auto_declared_…` PASS (regression guard).

- [ ] **Step 3: Implement**

- `TryRefineVariableFromExpression`: compute `declared` from `ConvertTypeNameToTypeInfo(variable.Type)` only (drop `variable.InferredType ??`). Replace the three `variable.SetInferredType(...)` calls with `variable.Refine(rhsType.WithAssignable(true), new RefinementSource(RefinementKind.Assignment, sourceNode))`.
- `VisitIdentifier`: in both the real-variable and auto-declared branches, after computing the type, `if (variable.RefinementSource is { } src) node.SetRefinementSource(src);`.
- The auto-declared path in `VisitAssignment` (1181) keeps `variable.SetInferredType(rightHandType)`.

- [ ] **Step 4: Run tests to verify they pass, then the whole project**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted PeopleCodeParser.SelfHosted.Tests/ApiObjectRefinementTests.cs
git commit -m "Keep declared variable type separate from refined type"
```

---

### Task 4: Phase 1 (part 2) — type checker uses declared type; one-type warning

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs:85-106` (`VisitAssignment`)
- Test: `PeopleCodeParser.SelfHosted.Tests/ApiObjectReassignmentCheckTests.cs` (new)

**Interfaces:**
- Consumes: `AstNodeExtensions.GetRefinementSource()` and `RefinementSource.Line` (Task 3); `ScopedAstVisitor.FindVariable(string)` (checker's own registry, whose `Type` is the declared type string).

- [ ] **Step 1: Write the failing tests**

Helper as in `ReturnTypeCheckTests.TypeCheck`, returning both `program.GetAllTypeErrors()` and `program.GetAllTypeWarnings()`.

```csharp
[Fact] public void Reassigning_ApiObject_to_another_subtype_is_not_an_error()
// "Local ApiObject &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);"
// Assert.DoesNotContain(errors, e => e.Message.Contains("Cannot assign"))

[Fact] public void Reassigning_ApiObject_to_another_subtype_warns_with_both_types_and_lines()
// "Local ApiObject &x;\n&x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);"
// Assert.Contains(warnings, w => w.Message ==
//   "&x is assigned more than one API type (Session on line 2, ComponentInterface on line 3). Use a separate variable for each type.")

[Fact] public void Reassigning_same_subtype_does_not_warn()
// "Local ApiObject &x = %Session;\n&x = %Session;"  → no warning containing "more than one API type"

[Fact] public void First_assignment_does_not_warn()
// "Local ApiObject &x;\n&x = %Session;"  → no such warning

[Fact] public void Lowercase_apiobject_declaration_behaves_the_same()
// "Local apiobject &x;\n&x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);"
// → no "Cannot assign" error; one-type warning present

[Fact] public void Any_declared_variable_gets_neither_error_nor_warning()
// "Local any &x = %Session;\n&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);"

[Fact] public void Assigning_unresolved_ApiObject_value_keeps_type_and_is_silent()
// "Function GetThing() Returns ApiObject\nEnd-Function;\nLocal ApiObject &x = %Session;\n&x = GetThing();\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);"
// → no error, no one-type warning; last use of &x still Session

[Fact] public void Record_reassigned_to_different_named_record_is_silent()
// "Local Record &r = CreateRecord(Record.JOB);\n&r = CreateRecord(Record.PERSONAL_DATA);"
// → no error, no warning
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~ApiObjectReassignmentCheckTests`
Expected: `Reassigning_…_not_an_error`, `…_warns_…`, `Lowercase_…` and `Any_declared_…` FAIL. The first of these confirms the spec §2 bug. If it passes, record that in the commit and keep the test. The rest PASS as regression guards.

- [ ] **Step 3: Implement**

At the top of `VisitAssignment`, after computing `leftType`/`rightType`: if `node.Target is IdentifierNode id && FindVariable(id.Name) is { IsAutoDeclared: false } v` and `v.Type` equals `"apiobject"` or `"any"` (ordinal ignore-case) and `rightType` is a `BuiltinObjectTypeInfo` whose name `PeopleCodeTypeDatabase.IsApiObjectSubtype`:
- skip the compatibility check (return after the warning logic below);
- if `v.Type` is `apiobject`, `leftType` is also an API subtype, and its name differs from `rightType`'s (ordinal ignore-case), call `RecordTypeWarning` with the constraint's message. The first line is `id.GetRefinementSource()?.Line + 1`, and the second is `node.SourceSpan.Start.Line + 1`. If there's no source, drop " on line N" for the first type.

Everything else falls through to the existing logic unchanged. Values that aren't API subtypes (`ApiObject` base, `any`) are already compatible.

- [ ] **Step 4: Run tests to verify they pass, then all test projects**

Run: `dotnet test PeopleCodeParser.SelfHosted.Tests` and `dotnet test PeopleCodeTypeInfo.Tests`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs PeopleCodeParser.SelfHosted.Tests/ApiObjectReassignmentCheckTests.cs
git commit -m "Warn instead of erroring when an ApiObject variable changes API type"
```

---

After Task 4: name the projects for Tim to rebuild (`PeopleCodeTypeInfo`, `PeopleCodeParser.SelfHosted`, `AppRefiner`) for a manual check in App Designer. In particular, check the false "Cannot assign" error and the CI property errors are gone. Then plan spec phase 1b (`TypeIdentity`) and phase 2.
