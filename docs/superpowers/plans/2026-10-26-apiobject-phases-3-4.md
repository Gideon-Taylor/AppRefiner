# ApiObject Phases 3–4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Infer `ApiObject` variable types from how they're used (phase 3), and infer the real return and property types of functions/methods declared `ApiObject`, `Record` or `Field`, both in the edited program and across programs (phase 4).

**Architecture:**
- **Phase 3:** a purely syntactic pre-pass (`UsageNarrowingAnalyzer`) computes, for each unannotated `ApiObject` variable, the candidate API types left after each direct member use. `TypeInferenceVisitor` seeds the result as a `Narrowing` refinement, and it stamps conflicts only on uses whose type actually came from narrowing.
- **Phase 4, same program:** inference collects each routine's `Return` types and writes agreeing ones into the program's own `FunctionInfo.RefinedReturnType` at the end of the body, so later calls read it.
- **Phase 4, cross program:** a shared `ReturnTypeRefiner` runs that inference on programs fetched by a resolver, with a per-thread cycle set and depth limit. Results cut short by the cycle set or depth limit are marked provisional and never cached.

**Tech Stack:** C# / .NET 8, xUnit. Projects: `PeopleCodeTypeInfo`, `PeopleCodeParser.SelfHosted`, their test projects, `AppRefiner`.

**Spec:** `docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md`. Relevant sections:
- §3 scope, §4 resolution order
- §8 narrowing, §9 cross-program returns
- §10 diagnostics, §11 editor experience
- §12 phases 3–4, §13 tests

## Global Constraints

- Use AppRefiner's `Debug.Log()`, never `Debug.WriteLine()`; never `MessageBox.Show()`.
- Commit on `main`, one commit per task, no push.
- Green after every task: `dotnet test PeopleCodeParser.SelfHosted.Tests`, `dotnet test PeopleCodeTypeInfo.Tests`, and `dotnet build AppRefiner/AppRefiner.csproj`. If AppRefiner is running and locks its DLLs, build with `-o <scratch dir>` and say so.
- One diagnostic per bad use (spec §13 "No duplicates").
- Narrowing and the one-type warning apply only to variables declared `ApiObject`. `any` and auto-declared variables keep their current behaviour (spec §3).
- All new diagnostics are **warnings** stamped with `AddTypeWarning`, with line numbers 1-based in messages.
- Message templates are exact. `{var}` is the variable name with its `&`. `{T}` is an API type name as `GetTypeName()` returns it.
  - **N1 (a use leaves none, several candidates before):** `'{member}' is not a member of {phrase}. Earlier uses of {var} ({uses}) suggested one of those types.`
  - **N2 (a use doesn't fit the settled type):** `{var} was inferred as {T} from {decider} on line {n}, but {T} has no member '{member}'.`
  - **N3 (the first use matches no API type):** `'{member}' is not a member of any API object type.`
  - **N4 (uses settled a type, then an assignment gives a different one):** `Uses of {var} suggest {T} ({decider} on line {n}), but it is assigned {R} on line {m}.`
  - **R1 (a routine returns different API types):** `{name} returns more than one API type ({T1} on line {a}, {T2} on line {b}).`
- How the template parts are built:
  - `{phrase}` lists the candidates in ordinal name order: two are `A or B`, three are `A, B or C`, and more than three are `one of {count} types (A, B, C, …)`. The last form uses the first three, then `…`.
  - `{decider}` is `Name()` for a method use and `Name` for a property use.
  - `{uses}` is the counted uses before this one, as `{decider} on line {n}` joined with `, `.
- Cross-program depth limit: `ReturnTypeRefiner.MaxDepth = 3`.

## Review Focus

1. **Mid-edit member names.** After `&x.GetCompIntfc(...)` settles `&x` as Session, a half-typed later use like `&x.Ge` must not change the type. Later uses still resolve as Session, and that use gets N2. Owned by Task 2: `Half_typed_member_after_settling_keeps_type`.
2. **Same-named variables in different methods narrow independently.** A local `&x` in `method A` used as a Session and a local `&x` in `method B` used as a Query must each get their own type. Owned by Task 1: `Same_name_in_two_methods_narrows_separately`.
3. **Self-recursive routines.** `Function F() Returns ApiObject; Return F(); End-Function;` must finish without refining and without a stack overflow. Owned by Task 4: `Self_recursive_function_is_not_refined`.
4. **Programs with parse errors, fetched by a resolver.** These must still produce (unrefined) metadata. A refiner exception must never turn metadata into null. Owned by Task 5: `Refiner_failure_keeps_unrefined_metadata`.
5. **A parameter with an inherited annotation and a conflicting use.** For example, `Sub.Use(&s As ApiObject)` overriding `Base.Use(&s As ApiObject /* as Session */)`, with `&s.Foo()` in the body. This must give the normal invalid-member error, not a narrowing warning. Owned by Task 2: `Inherited_annotation_beats_narrowing`.

---

### Task 1: Usage narrowing analyzer

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Visitors/Narrowing/UsageNarrowingAnalyzer.cs`, a `ScopedAstVisitor<object>` subclass with a static entry point.
- Create: `PeopleCodeParser.SelfHosted/Visitors/Narrowing/NarrowingResults.cs`, the result types.
- Test: `PeopleCodeParser.SelfHosted.Tests/Narrowing/UsageNarrowingAnalyzerTests.cs`

**Interfaces:**
- Produces, in namespace `PeopleCodeParser.SelfHosted.Visitors.Narrowing`:
  - `public sealed record NarrowingConflict(MemberAccessNode Use, string Message);`
  - `public sealed class NarrowingResult` has:
    - `string VariableName`;
    - `string? NarrowedType`: the API type name when exactly one candidate was reached;
    - `MemberAccessNode? DecidingUse`;
    - `IReadOnlyList<string> Candidates`: remaining candidates in ordinal order; a single entry when decided; empty when no use was counted;
    - `IReadOnlyList<NarrowingConflict> Conflicts`.
  - `public sealed class NarrowingResults` has:
    - `NarrowingResult? For(AstNode declarationNode, string variableName)`;
    - `NarrowingConflict? ConflictAt(MemberAccessNode use)`.
  - `public static NarrowingResults UsageNarrowingAnalyzer.Analyze(ProgramNode program)`
  - `public static string UsageNarrowingAnalyzer.CandidatePhrase(IReadOnlyList<string> candidates)`: the `{phrase}` rule from Global Constraints. N1 messages use it, and Task 3's hover reuses it.
- Consumes:
  - `PeopleCodeTypeDatabase.IsApiObjectSubtype`, `GetMethod` and `GetProperty`;
  - `ProgramNode.Annotations` (`ForType`, `ForValue`);
  - `ScopedAstVisitor.FindVariable`, and `VariableInfo.DeclarationNode`, `Type` and `IsAutoDeclared`.

Rules, all from spec §8:
- **Eligible variables:** declared `ApiObject` (case-insensitive) and not auto-declared. A variable is excluded entirely if it has an annotation anywhere: on its declaration (`Annotations.ForType(declaration type node)`) or on any assignment to it (`Annotations.ForValue(value)` where the assignment target is an identifier resolving to it).
- **Key:** each variable is keyed by `(DeclarationNode, Name)`, case-insensitive name. `FindVariable` gives each use its declaration, which keeps locals in different methods apart and lets instance, Global and Component variables span the program.
- **What counts as a use:** a `MemberAccessNode` whose `Target` is an `IdentifierNode` resolving to an eligible variable. It's a method use if its parent is a `FunctionCallNode` whose `Function` is this node; otherwise it's a property use, including assignment targets. Only the first link of a chain counts. Uses are processed in source order.
- **Candidates:** start from every `PeopleCodeType` value whose `GetTypeName()` passes `IsApiObjectSubtype`. Build this list once and cache it in a static.
- **Filtering:** a method keeps the candidates where `GetMethod` is non-null. A property keeps the candidates where `GetProperty` is non-null, plus `ComponentInterface` always. If a property filter would leave exactly `{ComponentInterface}` only through that exemption, the use is skipped: no change to the candidates, no conflict.
- **Settling:** when one candidate is left, `NarrowedType` and `DecidingUse` are set. Later uses are checked against that type: a mismatch gives an N2 conflict and the type doesn't change.
- **No candidates left:** an N1 conflict if more than one candidate remained before the use, or N3 if this was the first counted use. The use is ignored and processing continues.
- Exact message templates are in Global Constraints.

- [ ] **Step 1: Write the failing tests.** Each test parses a non-class program, runs `UsageNarrowingAnalyzer.Analyze`, and reads `For(declarationNode, "&x")`. The declaration node is the `LocalVariableDeclarationNode`, found with `FindDescendants`.
  - `One_distinguishing_method_settles_type`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);`
    - Expect `NarrowedType == "Session"`, `DecidingUse.MemberName == "GetCompIntfc"`, and no conflicts.
  - `Chained_members_do_not_count`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A).Save();`
    - Expect it settled on Session by `GetCompIntfc` only, with no conflict for `Save`.
  - `Annotated_variable_is_not_narrowed`:
    - Source: `Local ApiObject /* as Query */ &x;\n&x.GetCompIntfc(CompIntfc.A);`
    - Expect `For(...)` to return null.
  - `Assignment_annotation_excludes_variable`:
    - Source: `Local ApiObject &x;\n&x = %Session /* as Session */;\n&x.GetCompIntfc(CompIntfc.A);`
    - Expect null.
  - `Any_and_auto_declared_are_not_narrowed`:
    - Source: `Local any &a;\n&a.GetCompIntfc(CompIntfc.A);` gives null.
    - An auto-declared `&b.GetCompIntfc(CompIntfc.A);` also gives null.
  - `Use_matching_nothing_first_gives_N3`:
    - Source: `Local ApiObject &x;\n&x.Zzz();`
    - Expect one conflict with message `'Zzz' is not a member of any API object type.`, and `NarrowedType` null.
  - `Use_after_settling_gives_N2`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Zzz();`
    - Expect the message `&x was inferred as Session from GetCompIntfc() on line 2, but Session has no member 'Zzz'.`, still Session.
  - `Conflict_with_several_candidates_gives_N1`:
    - First, in the test body, find a member that exists on exactly two or three API types using `PeopleCodeTypeDatabase`. Assert that it does, then use it.
    - Uses: that member, then `Zzz()`.
    - Expect an N1 message built with the phrase rule, ending `Earlier uses of &x ({member}() on line 2) suggested one of those types.`
  - `Ci_dynamic_property_does_not_settle_ci`:
    - Source: `Local ApiObject &x;\n&y = &x.EMPLID;`
    - Expect no conflicts, `NarrowedType` null, and `Candidates` empty or unchanged from the start. Skipped uses leave no history.
  - `Ci_method_narrows_to_ci`:
    - Source: `Local ApiObject &x;\n&b = &x.Save();\n&b = &x.Get();`
    - Expect `ComponentInterface`. If `Save`+`Get` doesn't single it out, add `&x.GetPropertyByName("A")` and assert the actual distinguishing set. Record which one in the report.
  - `Same_name_in_two_methods_narrows_separately` (Review Focus 2):
    - Source: a class with methods A and B, each with `Local ApiObject &x;`. A uses `&x.GetCompIntfc(CompIntfc.A)`; B uses a Query-only method. Pick one with `PeopleCodeTypeDatabase` and assert it is Query-only.
    - Expect two separate results: Session and Query.
  - `Parameter_and_instance_variable_are_narrowed`:
    - A parameter `&p As ApiObject` and an `instance ApiObject &i;` each get settled by a Session-only method call in a method body.
- [ ] **Step 2: Run** `dotnet test PeopleCodeParser.SelfHosted.Tests --filter UsageNarrowingAnalyzerTests`. Expected: compile failure.
- [ ] **Step 3: Implement** the analyzer and result types following the rules above. The visitor collects uses per key during the walk, then computes results after `program.Accept`.
- [ ] **Step 4: Run** the filtered tests, then the full suite. Expected: all pass.
- [ ] **Step 5: Commit** `Add usage narrowing analyzer for ApiObject variables`.

### Task 2: Seed narrowed types in inference and report conflicts

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`:
  - `Run`: call `UsageNarrowingAnalyzer.Analyze(program)` before `program.Accept`.
  - `OnVariableDeclared`: seed.
  - Identifier and member-access visits: stamp conflicts and candidates.
  - `TryRefineVariableFromExpression`: N4.
- Modify: `PeopleCodeParser.SelfHosted/AstNode.cs` and `AstNodeExtensions.cs` for the new attributes.
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs`, `CheckApiObjectReassignment`.
- Modify: `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs`: skip marked nodes.
- Test: `PeopleCodeParser.SelfHosted.Tests/Narrowing/NarrowingInferenceTests.cs`

**Interfaces:**
- Consumes Task 1's `NarrowingResults`.
- Produces:
  - `public static void MarkNarrowingConflict(this AstNode node)` and `public static bool HasNarrowingConflict(this AstNode node)`.
  - `public static void SetNarrowingCandidates(this AstNode node, IReadOnlyList<string> candidates)` and `public static IReadOnlyList<string>? GetNarrowingCandidates(this AstNode node)`. These are stamped on identifier uses of an undecided variable that has at least one counted use.
  - A refinement from narrowing has `RefinementSource(RefinementKind.Narrowing, decidingUse)`, where the node is the deciding `MemberAccessNode`.

Behaviour (spec §4, §8 "Where it runs", §10):
- **Seeding:** in `OnVariableDeclared`, after the existing annotation logic, if `variable.AnnotatedType == null` and the result has a `NarrowedType`, call `variable.Refine(<TypeInfo for that API name>, new RefinementSource(RefinementKind.Narrowing, result.DecidingUse))`. This covers inherited parameter annotations, which set `AnnotatedType`.
- **Conflicts:** when visiting a `MemberAccessNode` that `ConflictAt` returns a conflict for, `AddTypeWarning(conflict.Message)` it and `MarkNarrowingConflict()` it, but only if the target identifier's stamped refinement source is `Narrowing` or it has none. Uses typed by an annotation or assignment keep the normal invalid-member error.
- **Candidates:** a use of an undecided variable whose result has a non-empty `Candidates` gets `SetNarrowingCandidates`.
- **N4:** in `TryRefineVariableFromExpression`, when the variable's current `RefinementSource.Kind == Narrowing` and a concrete API subtype value differs from the narrowed type, `AddTypeWarning` N4 on the value expression. Then refine as usual: latest wins.
- **Checker:** `CheckApiObjectReassignment` skips the one-type warning when the target's refinement source is `Narrowing`, as it already does for `Annotation`. N4 is the single diagnostic.
- **`InvalidMemberAccessCheck`:** returns early for a member access with `HasNarrowingConflict()`.

- [ ] **Step 1: Write the failing tests.** Run inference and the checker as `AssignmentAnnotationTests.TypeCheck` does. For the no-duplicates tests, use `CompileChecker.Check` with a `FakeTypeMetadataResolver` so `InvalidMemberAccessCheck` runs.
  - `Narrowed_type_reaches_uses_before_the_deciding_one`:
    - Source: `Local ApiObject &x;\n&y = &x.ErrorPending;\n&x.GetCompIntfc(CompIntfc.A);`
    - The `&x` identifier on line 2 has an inferred type named Session and refinement source `Narrowing`. If `ErrorPending` is a property of several API types, the result is still Session after line 3.
  - `Conflict_is_one_warning_and_no_member_error`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Zzz();`
    - Expect exactly one diagnostic on line 3 (the N2 warning) and no `InvalidMemberAccess`.
  - `Assigned_variable_keeps_normal_member_error`:
    - Source: `Local ApiObject &x = %Session;\n&x.Zzz();`
    - Expect the `InvalidMemberAccess` error `... (&x is Session, assigned on line 1)` and no narrowing warning.
  - `Inherited_annotation_beats_narrowing` (Review Focus 5):
    - Use the `Base`/`Sub` setup from `AnnotationInheritanceTests`, with `&s.Zzz();` in `Sub.Use`.
    - Expect the normal invalid-member error that names Session as annotated, and no narrowing warning.
  - `Assignment_replaces_narrowed_type_with_N4`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x = %Session.GetQuery();`
    - Expect exactly one warning, `Uses of &x suggest Session (GetCompIntfc() on line 2), but it is assigned Query on line 3.`, and no "more than one API type" warning.
  - `Agreeing_assignment_is_silent`:
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x = %Session;`
    - Expect no warnings.
  - `Undecided_uses_carry_candidates`:
    - Use the two- or three-type member from Task 1's N1 test.
    - The identifier use carries `GetNarrowingCandidates()` equal to that set, and its inferred type is still `ApiObject`.
  - `Half_typed_member_after_settling_keeps_type` (Review Focus 1):
    - Source: `Local ApiObject &x;\n&x.GetCompIntfc(CompIntfc.A);\n&x.Ge;\n&x.GetCompIntfc(CompIntfc.B);`
    - If `&x.Ge;` doesn't parse cleanly, use the nearest form that does and say which in the report.
    - Expect the last use to still be Session, and exactly one warning (N2) on the `Ge` use.
- [ ] **Step 2: Run** the filtered tests. Expected: they fail.
- [ ] **Step 3: Implement** as described.
- [ ] **Step 4: Run** the full suites and the AppRefiner build. Expected: green. If an existing test now gets a narrowing type or warning it didn't expect, report it as a finding, with the test and why, rather than editing its assertion.
- [ ] **Step 5: Commit** `Seed narrowed ApiObject types and report narrowing conflicts`.

### Task 3: Show the type's source in hover

**Files:**
- Modify: `AppRefiner/TooltipProviders/InferredTypeTooltipProvider.cs`
- Modify: `AppRefiner/TooltipProviders/TypeDisplay.cs`

**Interfaces:**
- Consumes `GetRefinementSource()` (already on identifier uses) and Task 2's `GetNarrowingCandidates()`.

Spec §11. On top of the existing `Inferred Type: {TypeDisplay.Format(type)}`, the best node, when it's an identifier, gets a suffix:
- `, annotated on line {n}` for an `Annotation` source;
- `, assigned on line {n}` for `Assignment`;
- `, inferred from {decider} on line {n}` for `Narrowing`, with `{decider}` as in Global Constraints. The node is the deciding `MemberAccessNode`.

An undecided identifier with candidates shows `Inferred Type: ApiObject ({phrase})`, built with Task 1's `UsageNarrowingAnalyzer.CandidatePhrase`. Don't duplicate the rule.

- [ ] **Step 1:** Implement.
- [ ] **Step 2:** `dotnet build AppRefiner/AppRefiner.csproj` succeeds. There are no automated tests for tooltip providers; list the hover cases to check by hand in the report.
- [ ] **Step 3: Commit** `Show where an inferred type came from in the type hover`.

### Task 4: Same-program return refinement

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`: collect return types per routine; finalize after `Function` bodies, method implementations and property getters.
- Test: `PeopleCodeParser.SelfHosted.Tests/ReturnRefinementTests.cs`

**Interfaces:**
- Produces: after a routine body is visited, the routine's `FunctionInfo` in `_programMetadata.Methods`, or the getter's `PropertyInfo` in `_programMetadata.Properties`, gets `RefinedReturnType` or `RefinedType`. The value is a `TypeWithDimensionality(PeopleCodeType.X)` with an `Identity` for Record (`RecordName`), Field (`RecordName`, `FieldName`) and named CIs.
- This only happens when:
  - the declared return or property type is scalar `ApiObject`, `Record` or `Field`;
  - there is no return annotation: `HasReturnAnnotation` is false and `RefinedReturnType`/`RefinedType` is null;
  - every `Return` with a value agrees:
    - **API:** the same non-base API subtype. A named CI with the same name keeps that identity; mixed names fall back to plain `ComponentInterface`.
    - **Record:** every return is a `RecordTypeInfo` with the same non-empty `RecordName`.
    - **Field:** the same record and field.
- Any `Any`, `Unknown`, plain `ApiObject`, or missing return type means no refinement and no warning.
- Disagreeing API subtypes mean no refinement and one R1 warning. It goes on the first `Return` value whose type differs from the first return's. The routine name is the function or method name.
- Disagreeing Record/Field returns stay plain, with no warning (spec §9).
- Calls visited after the body pick the refinement up through the existing `ConvertFunctionInfoToTypeInfo` path. Function bodies are visited in source order, before the main block. Methods are visited in header declaration order (`ScopedAstVisitor.VisitMethod` → `Implementation`). A call reached before the body uses the declared or annotated type (spec §9, "Builder's call").
- Check first that `_programMetadata` is a fresh object per inference run in every AppRefiner caller (`TypeInferenceRunner`, `TypeErrorReporter`, `MainForm`, `AutoCompleteService`) and in tests. Mutating a cached `TypeMetadata` would leak a refinement into later runs. If any caller passes cached metadata, report it before writing to it.

- [ ] **Step 1: Write the failing tests.** These are same-program tests, using `NullTypeMetadataResolver` except where noted.
  - `Function_called_after_definition_gets_refined_return`:
    - Source: `Function GetS() Returns ApiObject\n   Return %Session;\nEnd-Function;\nLocal ApiObject &s = GetS();\n&s.GetCompIntfc(CompIntfc.A);`
    - Expect the type on the last line's `&s` to be Session, with source `Assignment`.
  - `Record_function_gets_named_return`:
    - Source: `Function GetJ() Returns Record\n   Return CreateRecord(Record.JOB);\nEnd-Function;\nLocal Record &r = GetJ();`
    - Expect `&r` to be a `RecordTypeInfo` with `RecordName == "JOB"`.
  - `Field_function_gets_named_return`: the same with `Returns Field` and `Return GetRecord(Record.JOB).EMPLID;`, or the nearest builtin form inference names. Assert `Record.JOB.EMPLID`.
  - `Disagreeing_api_returns_warn_and_stay_apiobject`:
    - A function with `If ... Return %Session; Else Return %Session.GetQuery(); End-If;`.
    - Expect R1 with lines matching the two `Return`s, and the caller's variable stays `ApiObject`.
  - `Disagreeing_record_returns_stay_plain_silently`.
  - `Annotated_return_is_not_overridden`:
    - Header `Returns ApiObject /* as Query */` with `Return %Session;`.
    - The existing annotation behaviour is unchanged, and there is no R1.
  - `Method_called_from_later_body_gets_refined_return`:
    - A class declaring `method GetS() Returns ApiObject;` before `method Use();`.
    - `Use` calls `%This.GetS()` into a local. Expect Session.
  - `Method_called_before_its_body_uses_declared_type`:
    - The header declares `Use` before `GetS`, so `GetS`'s body is visited later.
    - Expect plain `ApiObject`, with no warning.
  - `Getter_property_gets_refined_type`:
    - `property ApiObject Sess get;` with a getter body returning `%Session`.
    - A later method's `%This.Sess` resolves to Session. Use the property-get form `PropertyGetterWiringTests` uses.
  - `Pass_through_parameter_return_uses_parameter_type`:
    - `Function P(&s As ApiObject /* as Session */) Returns ApiObject\n   Return &s;\nEnd-Function;`
    - Expect callers to get Session.
  - `Self_recursive_function_is_not_refined` (Review Focus 3):
    - `Function F() Returns ApiObject\n   Return F();\nEnd-Function;` followed by `Local ApiObject &f = F();`
    - Expect no exception, `&f` stays `ApiObject`, and no warning.
- [ ] **Step 2: Run** them. Expected: they fail.
- [ ] **Step 3: Implement.** Keep a stack of per-routine return collectors: push on entering a function, method implementation or getter, add in `VisitReturn` after `base.VisitReturn`, and pop and finalize after the body. Agreement uses `TypeIdentity` comparisons (`IsRefinementOf`/`ConflictsWith` where they fit), not `GetCommonType`. The spec warns that keeps the first name.
- [ ] **Step 4: Run** the full suites and the AppRefiner build. Expected: green. Report any existing test that changed, as in Task 2.
- [ ] **Step 5: Commit** `Refine same-program ApiObject, Record and Field return types from their returns`.

### Task 5: Cross-program return refiner

**Files:**
- Modify: `PeopleCodeTypeInfo/Inference/TypeMetadata.cs`: add `public bool IsProvisional { get; set; }`, documented as "refinement was cut short by a cycle or the depth limit; never cache".
- Modify: `PeopleCodeTypeInfo/Contracts/ITypeMetadataResolver.cs`: `GetTypeMetadata` and `GetTypeMetadataAsync` don't cache provisional metadata.
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`: the two `_typeCache.Set` sites (around lines 128 and 1157) skip provisional metadata.
- Create: `PeopleCodeParser.SelfHosted/Visitors/ReturnTypeRefiner.cs`
- Create: `PeopleCodeParser.SelfHosted.Tests/Compilation/SourceTypeMetadataResolver.cs`, a test resolver. It holds `name → source`, and `GetTypeMetadataCore` parses, calls `TypeMetadataBuilder.ExtractMetadata`, then `ReturnTypeRefiner.Refine`. It counts parses per name and exposes them for tests.
- Test: `PeopleCodeParser.SelfHosted.Tests/CrossProgramReturnTests.cs`

**Interfaces:**
- Produces: `public static class ReturnTypeRefiner` with:
  - `public const int MaxDepth = 3;`
  - `public static void Refine(ProgramNode program, TypeMetadata metadata, ITypeMetadataResolver resolver)`.
- `Refine` behaviour:
  - It sets `metadata.IsProvisional` when this refinement, or any nested one, was cut short.
  - **Opt-in:** it returns immediately when the metadata has no scalar `ApiObject`, `Record` or `Field` return (method or function), property or instance variable that is still unrefined.
  - **Inference:** otherwise it runs `TypeInferenceVisitor.Run(program, metadata, resolver)`. Task 4 writes the return and getter refinements into `metadata`.
  - **Properties and instance variables:** afterwards it refines `ApiObject`, `Record` and `Field` properties and instance variables that are still unrefined. It collects every assignment in the program whose target is that member, either an identifier resolving to the instance variable or property, or `%This.Name`. It reads the assigned value's inferred type, using the same agreement rule as Task 4, and writes `PropertyInfo.RefinedType` when they agree. If they disagree it stays unrefined, with no warning.
  - **Guard:** a `[ThreadStatic]` stack of qualified names (`metadata.QualifiedName`) being refined, plus a `[ThreadStatic]` cut-short counter. If the name is already on the stack, or the stack depth is `MaxDepth`, it doesn't run inference: it increments the counter, sets `IsProvisional`, and returns. Each frame compares the counter before and after its inference run, and sets `IsProvisional` if it changed.
  - **Errors:** any exception inside `Refine` is caught. The metadata stays as extracted and is not provisional. The library has no logging, so the exception is swallowed with a comment, as `CompileChecker` does.

- [ ] **Step 1: Write the failing tests.** Use `SourceTypeMetadataResolver`; the caller program is checked with `TypeInferenceVisitor.Run`.
  - `External_method_returning_one_subtype_refines_caller`:
    - `PKG:Svc` has `method Get() Returns ApiObject;` returning `%Session`.
    - The caller `Local ApiObject &s = &svc.Get();` gets Session.
  - `Declared_function_library_return_refines_caller`:
    - A library `REC.FLD.FieldFormula` with `Function GetS() Returns ApiObject` returning `%Session`.
    - The caller's `Declare Function GetS PeopleCode REC.FLD FieldFormula;` and `Local ApiObject &s = GetS();` give Session.
  - `Disagreeing_external_returns_stay_apiobject`.
  - `External_record_return_carries_name`: `Returns Record` with `Return CreateRecord(Record.JOB);` gives the caller `record(JOB)`.
  - `External_property_assigned_in_constructor_is_refined`:
    - `property ApiObject Sess;`, set in the constructor to `%Session`.
    - The caller's `&svc.Sess` gives Session.
  - `Annotation_overrides_inference`:
    - `Returns ApiObject /* as Query */` with `Return %Session;`.
    - The caller gets Query.
  - `Cycle_returns_apiobject_and_is_not_cached`:
    - `PKG:A.Get()` returns `create PKG:B().Get()`, and `PKG:B.Get()` returns `create PKG:A().Get()`. Both are declared `Returns ApiObject`.
    - Resolving `PKG:A` finishes with no exception and an unrefined `Get`.
    - Its metadata `IsProvisional == true`.
    - Resolving `PKG:A` a second time parses it again (the parse count rises), because it wasn't cached.
  - `Depth_limit_cuts_chain`: a chain `PKG:C1 → C2 → C3 → C4 → C5`, each `Get()` returning the next's `Get()`, with C5 returning `%Session`. Resolving C1 is provisional and its `Get` stays unrefined.
  - `Complete_refinement_is_cached`: resolving a non-cyclic class twice parses it once.
  - `Refiner_failure_keeps_unrefined_metadata` (Review Focus 4):
    - A class source with a syntax error inside one method body, plus `method Get() Returns ApiObject` returning `%Session` in a well-formed body.
    - Expect non-null metadata, and no exception.
    - Assert whichever of these holds (refined or unrefined), and record which in the report.
- [ ] **Step 2: Run** them. Expected: they fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the full suites and the AppRefiner build. Expected: green.
- [ ] **Step 5: Commit** `Refine return and property types of programs loaded by the resolver`.

### Task 6: Hook the refiner into the database resolver

**Files:**
- Modify: `AppRefiner/Database/DatabaseTypeMetadataResolver.cs`, `ParseAndExtractMetadata`
- Modify: `docs/architecture/type-system-weak-spots.md`
- Modify: `docs/demos/apiobject-annotations/` (new demo file and README rows)

- [ ] **Step 1:** After `TypeMetadataBuilder.ExtractMetadata`, call `ReturnTypeRefiner.Refine(program, metadata, this)`. Leave the existing try/catch as is; `Refine` never throws. Add a `Debug.Log` line when the metadata comes back provisional.
- [ ] **Step 2:** Update the weak-spots doc. Mark the items phase 3/4 resolve as fixed, and add any limitation the implementation leaves:
  - methods called before their body is visited;
  - same-program callers aren't used to infer parameter types (§3 scope).
- [ ] **Step 3:** Add a demo program, `04_Narrowing.pcode`, covering:
  - settling;
  - N1, N2 and N3 conflicts;
  - N4;
  - an undecided hover.

  Add `Get` and `Sess` to the existing demo app package's classes to show cross-program refinement:
  - `ZZ_AR_DEMO:Svc` with an unannotated `method Make2() Returns ApiObject;` returning `%Session`;
  - a `Caller` use of it.

  Update the README's expected-results table. Verify each expected line against the real checker, using a scratch runner like the one used for the existing demos: parse every file, register class metadata, then run inference and `CompileChecker`.
- [ ] **Step 4:** `dotnet build AppRefiner/AppRefiner.csproj` and both test suites stay green.
- [ ] **Step 5: Commit** `Refine cross-program return types in the database resolver`.
