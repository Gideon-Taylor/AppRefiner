# ApiObject Type Resolution — Phases 1b and 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give types a generic name identity (`TypeIdentity`), and let developers state a variable's, parameter's, property's or return's concrete type with a `/* as T */` comment, honoured by inference, by cross-program metadata and in diagnostics.

**Architecture:**
- `TypeIdentity` is a flat record on `TypeInfo`, carried through metadata on `TypeWithDimensionality`.
- The parser records the two significant tokens before each comment, so an `AnnotationIndex` can match `/* as T */` comments to type nodes and assignment values by position.
- `TypeInferenceVisitor` applies declaration annotations as a lock when a variable is registered, and assignment annotations when refining.
- `TypeMetadataBuilder` stores header and `Function`-line annotations in new "refined" slots beside the declared types.
- Call sites and member checks read those slots.

**Tech Stack:** C# / .NET 8, xUnit (`PeopleCodeParser.SelfHosted.Tests`, `PeopleCodeTypeInfo.Tests`).

**Spec:** `docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md` (§4, §6, §7, §10, §12 phases 1b and 2, §13).

**Builds on:** phases 0/0b/1 (commits 4537609, 5e65d62, caf7e0c): `FunctionInfo.IsBuiltin`, `VariableInfo.Refine`/`RefinementSource`/`GetRefinementSource()`, `TypeCheckerVisitor.CheckApiObjectReassignment`.

## Global Constraints

- Annotation marker: a `/* */` comment (`TokenType.BlockComment`, never `ApiComment` `/+ +/`, `NestedComment` `<* *>` or `LineComment`/REM) whose whole body, trimmed, is `as T`. Case-insensitive. Anything else is not an annotation and is silently ignored.
- Allowed `T` forms and declared types (spec §7 table):

  | `T` | Meaning | Allowed on |
  |-----|---------|------------|
  | an API subtype name (`IsApiObjectSubtype`) | that subtype | `ApiObject` or `any` |
  | `CompIntfc.NAME` | `ComponentInterface` with `CompIntfcName` | `ApiObject` or `any` |
  | `Record.REC` | `Record` with `RecordName` | `Record` or `any` |
  | `Record.REC.FIELD` | `Field` with `RecordName` + `FieldName` | `Field` or `any` |
  | `Field.FIELD` | `Field` with only `FieldName` | `Field` or `any` |

- Placement: only two positions count, both "directly after X, same line, only whitespace between":
  1. Directly after a type node, in declarations without an initial value. That covers Local, Global, Component and instance variables, properties, parameters, and the return types on method headers and `Function` lines.
  2. Directly after the value of an assignment to a simple variable (`&x = …`), either side of the `;`. This includes `Local T &x = value`.

  Every other position is ignored with no warning. Methods are annotated only on their class-header declaration.
- `TypeIdentity` is not part of `TypeInfo.Equals` or `TypeWithDimensionality.Equals`, and is not written by the typeinfo.dat writer.
- Diagnostic texts (spec §10; `N` is `SourcePosition.Line + 1`):
  - `&x is annotated as Session (line 3) but is assigned ComponentInterface here.`
  - `The value is Session, but it is annotated as Tree. Using Tree.`
  - `Unknown type 'Foo' in annotation.`
  - `Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME?`
  - `Unknown type 'Record' in annotation. Did you mean Record.NAME?`
  - `Unknown type 'Field' in annotation. Did you mean Field.NAME or Record.REC.FIELD?`
  - `'as Session' needs an ApiObject declaration; &rs is Rowset.` The general form is `'as <T as written>' needs a <ApiObject|Record|Field> declaration; <name> is <declared type>.`
  - `Foo expects &s as Session (annotated); the argument is Tree.`
- Annotation and conflict warnings detected during inference are stamped by `TypeInferenceVisitor` with `SetTypeWarning`, each on a distinct node. Annotation-syntax warnings go on the declaring node (the `ParameterNode`, `ProgramVariableNode`, `PropertyNode`, `LocalVariableDeclarationNode`, `MethodNode` or `FunctionNode`). Value-conflict warnings go on the assignment's value expression node. The checker's one-type warning stays on the `AssignmentNode`.
- Tests use in-memory resolvers (`NullTypeMetadataResolver`, `FakeTypeMetadataResolver`). No database. Commit after each task, directly on `main`, no push.

## Review Focus

1. **Comments containing "as" that aren't annotations**: `/* store it as Session for later */` and commented-out code `/* &x = %Session; */` placed in an annotation position must have no effect and give no warning. Test in Task 3 (parser) and Task 4 (finder).
2. **An annotation on the line after its declaration** must not count. Test in Task 4.
3. **Annotations in a program with a parse error near them** must not crash the finder. It has to tolerate missing spans and null `FirstToken`/`LastToken`. Test in Task 4.
4. **A multi-variable declaration** `Local ApiObject /* as Session */ &c, &d;` must type both variables. Test in Task 5.
5. **A declaration annotation followed by the same type with more detail**, `/* as ComponentInterface */` then `&x = …GetCompIntfc(…) /* as CompIntfc.FOO */`, must not warn, and the variable must gain the CI name. Test in Task 6.

---

### Task 1: TypeIdentity on TypeInfo (spec phase 1b)

**Files:**
- Create: `PeopleCodeTypeInfo/Types/TypeIdentity.cs`
- Modify: `PeopleCodeTypeInfo/Types/PeopleCodeType.cs` (`TypeInfo` base, lines 64-147)
- Modify: `PeopleCodeTypeInfo/Types/RecordTypeInfo.cs`, `PeopleCodeTypeInfo/Types/FieldTypeInfo.cs` (override)
- Test: `PeopleCodeTypeInfo.Tests/TypeIdentityTests.cs` (new)

**Interfaces:**
- Produces:
  - `namespace PeopleCodeTypeInfo.Types; public sealed record TypeIdentity(string? RecordName = null, string? FieldName = null, string? CompIntfcName = null)`. Empty strings are treated as null in all comparisons, and names compare `OrdinalIgnoreCase`.
    - `bool IsRefinementOf(TypeIdentity? less)`: true when `less` is null, or when every non-null name in `less` equals the same name here.
    - `bool ConflictsWith(TypeIdentity? other)`: true when some name is non-null in both and differs.
    - `bool IsEmpty`
  - `TypeInfo.Identity` (`TypeIdentity?`, private set): copied in `WithAssignable`, `WithAutoDeclared` and `WithBufferField`.
  - `TypeInfo WithIdentity(TypeIdentity? identity)`: same shape as `WithBufferField`. It returns `this` when the identity is equal.
  - `virtual TypeIdentity? EffectiveIdentity => Identity`. `RecordTypeInfo` overrides it to `new TypeIdentity(RecordName: RecordName)`, and `FieldTypeInfo` to `new TypeIdentity(RecordName, FieldName)`. This adapter lets later tasks compare records and fields uniformly without moving them onto `TypeIdentity` (spec §6 defers that).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Refinement_adds_detail() // new(RecordName:"JOB", FieldName:"EMPLID").IsRefinementOf(new(RecordName:"JOB")) == true
[Fact] public void Null_or_empty_less_is_always_refined() // x.IsRefinementOf(null) and x.IsRefinementOf(new()) are true; new(RecordName:"") treated as empty
[Fact] public void Different_names_conflict() // new(CompIntfcName:"A").ConflictsWith(new(CompIntfcName:"b")) == true; "a" vs "A" == false
[Fact] public void Disjoint_names_do_not_conflict() // new(FieldName:"X").ConflictsWith(new(RecordName:"R")) == false
[Fact] public void Identity_survives_With_methods() // var t = TypeInfo.FromPeopleCodeType(PeopleCodeType.ComponentInterface).WithIdentity(new(CompIntfcName:"FOO"));
// t.WithAssignable(true).Identity, t.WithAutoDeclared(true).Identity, t.WithBufferField(null).Identity all == new(CompIntfcName:"FOO")
[Fact] public void Identity_does_not_affect_Equals() // CI with identity FOO .Equals(CI with identity BAR) == true
[Fact] public void Record_and_field_expose_effective_identity() // new RecordTypeInfo("JOB", null).EffectiveIdentity == new(RecordName:"JOB"); FieldTypeInfo("JOB","EMPLID",null) → (JOB, EMPLID)
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test PeopleCodeTypeInfo.Tests --filter FullyQualifiedName~TypeIdentityTests`. Expected: compile failure / FAIL.
- [ ] **Step 3: Implement** the interfaces above. Where `CloneWithState` overrides exist, nothing changes: the base `With*` methods copy `Identity` after cloning, exactly as they copy `BufferField`.
- [ ] **Step 4: Run** the `TypeIdentityTests` filter, then both full test projects. Expected: all PASS.
- [ ] **Step 5: Commit** — `git commit -m "Add TypeIdentity to TypeInfo"`

---

### Task 2: Identity and refined slots in metadata

**Files:**
- Modify: `PeopleCodeTypeInfo/Functions/Parameters.cs` (`TypeWithDimensionality`, `SingleParameter`)
- Modify: `PeopleCodeTypeInfo/Functions/FunctionInfo.cs`, `PeopleCodeTypeInfo/Functions/PropertyInfo.cs`
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs` — `ConvertTypeWithDimensionalityToTypeInfo`, `ConvertFunctionInfoToTypeInfo`, `ConvertPropertyInfoToTypeInfo`
- Test: `PeopleCodeParser.SelfHosted.Tests/RefinedMetadataTests.cs` (new)

**Interfaces:**
- Consumes: `TypeIdentity`, `TypeInfo.WithIdentity` (Task 1).
- Produces:
  - `TypeWithDimensionality.Identity` (`TypeIdentity?`, get/set). It is not included in `Equals`/`GetHashCode`/`ToString`.
  - `SingleParameter.RefinedParameterType` (`TypeWithDimensionality?`).
  - `FunctionInfo.RefinedReturnType` (`TypeWithDimensionality?`).
  - `PropertyInfo.RefinedType` (`TypeWithDimensionality?`).
  - `ConvertTypeWithDimensionalityToTypeInfo` attaches identity. For `Type == Record` with an `Identity.RecordName`, it returns `new RecordTypeInfo(name, _typeResolver)`. For `Type == Field` with an `Identity.FieldName`, it returns `new FieldTypeInfo(Identity.RecordName ?? "", Identity.FieldName, _typeResolver)`. Otherwise it returns `baseType.WithIdentity(twd.Identity)`. Arrays wrap as today.
  - `ConvertFunctionInfoToTypeInfo`: when `func.RefinedReturnType is { } r`, it returns `ConvertTypeWithDimensionalityToTypeInfo(r)` with no union, polymorphic or specialization handling. `ConvertPropertyInfoToTypeInfo` prefers `prop.RefinedType` the same way.

- [ ] **Step 1: Write the failing tests.** Use a `FakeTypeMetadataResolver` with class `PKG:Svc`. It has method `GetSess` (ReturnType `ApiObject`, RefinedReturnType `Session`), method `GetJob` (ReturnType `Record`, RefinedReturnType `Record` with `Identity = new(RecordName:"JOB")`), method `GetCI` (ReturnType `ApiObject`, RefinedReturnType `ComponentInterface` with `Identity = new(CompIntfcName:"FOO")`), and property `Sess` (Type `ApiObject`, RefinedType `Session`). Infer over `Local PKG:Svc &svc = create PKG:Svc();` followed by the line under test.

```csharp
[Fact] public void Refined_return_type_is_used_for_calls() // "Local ApiObject &x = &svc.GetSess();" → initial value type Name == "Session"
[Fact] public void Refined_record_return_carries_record_name() // "Local Record &r = &svc.GetJob();" → RecordTypeInfo, RecordName "JOB"
[Fact] public void Refined_ci_return_carries_identity() // "Local ApiObject &c = &svc.GetCI();" → Name "ComponentInterface", Identity == new(CompIntfcName:"FOO")
[Fact] public void Refined_property_type_is_used() // "Local ApiObject &p = &svc.Sess;" → "Session"
[Fact] public void Unrefined_return_is_unchanged() // a method with no RefinedReturnType returning ApiObject → "ApiObject"
[Fact] public void Identity_is_not_part_of_TypeWithDimensionality_equality() // two TWDs differing only in Identity are Equal
```

- [ ] **Step 2: Run to verify they fail.** Run: `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~RefinedMetadataTests`
- [ ] **Step 3: Implement** the interfaces above.
- [ ] **Step 4: Run** the focused tests, then both full projects. Expected: all PASS.
- [ ] **Step 5: Commit**: `git commit -m "Carry identity and refined types through function and property metadata"`

---

### Task 3: Annotation syntax parser

**Files:**
- Create: `PeopleCodeParser.SelfHosted/Annotations/TypeAnnotation.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Annotations/TypeAnnotationParserTests.cs` (new)

**Interfaces:**
- Consumes: `TypeIdentity` (Task 1), `PeopleCodeTypeDatabase.IsApiObjectSubtype`, `PeopleCodeType` name parsing (the `"apiobject" => PeopleCodeType.ApiObject` map in `PeopleCodeType.cs`; use whatever `FromString`-style lookup it exposes).
- Produces (namespace `PeopleCodeParser.SelfHosted.Annotations`):
  - `public sealed record AnnotationTarget(PeopleCodeType Type, TypeIdentity? Identity, string Written)`. `Written` is `T` exactly as typed, used in messages.
  - `public enum AnnotationCategory { ApiObject, Record, Field }`, plus `AnnotationTarget.Category`. `CompIntfc.X` maps to `ApiObject`.
  - `public sealed record ParsedAnnotation(Token Comment, AnnotationTarget? Target, string? Error)`. Exactly one of `Target`/`Error` is non-null.
  - `public static class TypeAnnotationParser { public static ParsedAnnotation? TryParse(Token comment); }`. It returns null when the token isn't a `BlockComment`, or when its trimmed body (between `/*` and `*/`) doesn't match `^as\s+(\S+)$`, ignoring case. `T` that matches but is unknown gives `Error` with the global-constraint texts. A `T` that names a `PeopleCodeType` which is not an API subtype and not one of the three reference forms is unknown. So `CompIntfc`, `Record`, `Field` and `Rowset` alone are all unknown: the first three get their "did you mean" text, and `Rowset` gets the plain text.

- [ ] **Step 1: Write the failing tests.** Build comment tokens with `Token.CreateTrivia(TokenType.BlockComment, text, span)`.

```csharp
[Theory]
[InlineData("/* as Session */", PeopleCodeType.Session, null, null, null)]
[InlineData("/*AS session*/", PeopleCodeType.Session, null, null, null)]
[InlineData("/* as CompIntfc.USER_PROFILE */", PeopleCodeType.ComponentInterface, null, null, "USER_PROFILE")]
[InlineData("/* as Record.JOB */", PeopleCodeType.Record, "JOB", null, null)]
[InlineData("/* as Record.JOB.EMPLID */", PeopleCodeType.Field, "JOB", "EMPLID", null)]
[InlineData("/* as Field.EMPLID */", PeopleCodeType.Field, null, "EMPLID", null)]
public void Parses_valid_annotations(string text, PeopleCodeType type, string? rec, string? fld, string? ci)

[Theory]
[InlineData("/* store it as Session for later */")]
[InlineData("/* &x = %Session; */")]
[InlineData("/* Session */")]
[InlineData("/* as */")]
public void Ignores_non_annotations(string text) // TryParse returns null

[Fact] public void Ignores_other_comment_kinds() // ApiComment "/+ as Session +/", NestedComment "<* as Session *>", LineComment "REM as Session;" → null

[Theory]
[InlineData("/* as Foo */", "Unknown type 'Foo' in annotation.")]
[InlineData("/* as CompIntfc */", "Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME?")]
[InlineData("/* as Record */", "Unknown type 'Record' in annotation. Did you mean Record.NAME?")]
[InlineData("/* as Field */", "Unknown type 'Field' in annotation. Did you mean Field.NAME or Record.REC.FIELD?")]
[InlineData("/* as Rowset */", "Unknown type 'Rowset' in annotation.")]
public void Reports_unknown_types(string text, string error)
```

- [ ] **Step 2: Run to verify they fail.** Run: `--filter FullyQualifiedName~TypeAnnotationParserTests`
- [ ] **Step 3: Implement.** Check the exact delimiters in `Token.Text` for block comments in `PeopleCodeLexer` (~line 671) and strip them.
- [ ] **Step 4: Run** the focused tests and the full project. Expected: PASS.
- [ ] **Step 5: Commit**: `git commit -m "Parse /* as T */ type annotation comments"`

---

### Task 4: Comment positions and the annotation index

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Nodes/ProgramNodes.cs` (`ProgramNode`, near `Comments` line 57)
- Modify: `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs` (`CollectComments`, ~5843)
- Create: `PeopleCodeParser.SelfHosted/Annotations/AnnotationIndex.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/Annotations/AnnotationIndexTests.cs` (new)

**Interfaces:**
- Consumes: `TypeAnnotationParser.TryParse`, `ParsedAnnotation` (Task 3).
- Produces:
  - `public sealed record CommentContext(Token Comment, Token? Previous, Token? BeforePrevious)` in `PeopleCodeParser.SelfHosted.Nodes`. `Previous` and `BeforePrevious` are the two nearest preceding non-trivia, non-EOF tokens in the parser's token stream.
  - `ProgramNode.CommentContexts` (`IReadOnlyList<CommentContext>`) and `internal void AddCommentContext(CommentContext)`. `CollectComments` fills it alongside `Comments`, tracking the last two significant tokens as it walks `_tokens`. A comment in the leading trivia of token *k* has the significant tokens before *k* as its context.
  - `public sealed class AnnotationIndex` (namespace `PeopleCodeParser.SelfHosted.Annotations`):
    - `static AnnotationIndex Build(ProgramNode program)`
    - `ParsedAnnotation? ForType(TypeNode? typeNode)`: the annotation whose comment starts on the type node's end line and whose `Previous` token ends at `typeNode.SourceSpan.End.Index`.
    - `ParsedAnnotation? ForValue(ExpressionNode? value)`: the same, matched against `value.SourceSpan.End`. It also matches when `Previous` is a `TokenType.Semicolon` and `BeforePrevious` ends at the value's end.

    Only comments for which `TryParse` returns non-null are indexed. Lookups on null nodes, or on nodes with an empty span, return null. The comment must be on the same line as the anchor's end.
  - `AnnotationIndex` is computed once per program. Cache it on `ProgramNode` with a lazy `public AnnotationIndex Annotations` property, so the visitor and metadata builder share it. Put the cache in `ProgramNode` only if that doesn't create a project-reference cycle. Otherwise expose it as an extension method `program.GetAnnotationIndex()` that caches in `program.Attributes`.

- [ ] **Step 1: Write the failing tests.** Parse with `ParseTestHelper.Parse`, find the node, then query `AnnotationIndex.Build(program)`.

```csharp
[Fact] public void Finds_annotation_after_local_type()            // "Local ApiObject /* as Session */ &x;" → ForType(decl.Type).Target.Type == Session
[Fact] public void Finds_annotation_after_parameter_type()        // "Function Foo(&a As ApiObject /* as Session */, &b As string)\nEnd-Function;" → ForType(param &a .Type) found; &b none
[Fact] public void Finds_annotation_after_function_return_type()  // "Function Foo() Returns ApiObject /* as Session */\nEnd-Function;"
[Fact] public void Finds_annotation_after_method_header_types()   // class header "method GetCI(&s As ApiObject /* as Session */) Returns ApiObject /* as CompIntfc.FOO */;" → both
[Fact] public void Finds_annotation_after_property_and_instance_types() // "property ApiObject /* as Session */ CurrentSession get;" and "instance ApiObject /* as Tree */ &t;"
[Fact] public void Finds_annotation_after_global_and_component_types()
[Fact] public void Finds_value_annotation_before_semicolon()      // "&ci = &s.GetCompIntfc(&n) /* as CompIntfc.FOO */;" → ForValue(assignment.Value)
[Fact] public void Finds_value_annotation_after_semicolon()       // "&d = GetIt(); /* as ComponentInterface */"
[Fact] public void Finds_value_annotation_on_local_with_initial_value() // "Local ApiObject &s = GetX() /* as Session */;" → ForValue(decl.InitialValue)
[Fact] public void Ignores_annotation_on_next_line()              // "Local ApiObject &x;\n/* as Session */" → ForType null
[Fact] public void Ignores_annotation_after_variable_name()       // "Local ApiObject &x /* as Session */;" → ForType null
[Fact] public void Ignores_annotation_inside_argument()           // "&x = Foo(Bar() /* as Session */);" → ForValue(assignment.Value) null
[Fact] public void Ignores_prose_and_commented_out_code()         // "Local ApiObject /* store it as Session for later */ &x;" → null
[Fact] public void Tolerates_parse_errors()                       // "Local ApiObject /* as Session */ &x\nLocal" (parse errors allowed) → Build does not throw
```

- [ ] **Step 2: Run to verify they fail.** Run: `--filter FullyQualifiedName~AnnotationIndexTests`
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the focused tests, then the full project. Expected: PASS. Existing comment tests must be unaffected.
- [ ] **Step 5: Commit**: `git commit -m "Index /* as T */ annotations by position"`

---

### Task 5: Declaration annotations in inference

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/Models/VariableInfo.cs`
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`: override `OnVariableDeclared`, `TryRefineVariableFromExpression`, plus a helper that converts an `AnnotationTarget` to `TypeInfo`
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs` (`CheckApiObjectReassignment`)
- Test: `PeopleCodeParser.SelfHosted.Tests/DeclarationAnnotationTests.cs` (new)

**Interfaces:**
- Consumes: `AnnotationIndex.ForType` (Task 4), `AnnotationTarget` (Task 3), `TypeInfo.WithIdentity`/`EffectiveIdentity`/`TypeIdentity` (Task 1), `RefinementKind.Annotation` (existing enum value).
- Produces:
  - `VariableInfo.AnnotatedType` (`TypeInfo?`, private set), and `VariableInfo.Annotate(TypeInfo type, RefinementSource source)`. `Annotate` sets `AnnotatedType`, `InferredType` and `RefinementSource`, the last with `Kind = Annotation` and `Node` set to the declaring node.
  - `TypeInfo AnnotationToTypeInfo(AnnotationTarget t)` (private, in the visitor):
    - `Record` with a name gives `RecordTypeInfo`;
    - `Field` gives `FieldTypeInfo(rec ?? "", fld, _typeResolver)`;
    - anything else gives `TypeInfo.FromPeopleCodeType(t.Type).WithIdentity(t.Identity)`.
  - `bool AnnotationFits(AnnotationCategory c, string declaredTypeName)` (private). `any` always fits. Otherwise the declared type must be `apiobject`, `record` or `field` for its category (ordinal ignore-case).

Behaviour (spec §7 "Behaviour"):
- In `OnVariableDeclared(variable)`, find the type node of `variable.DeclarationNode`:
  - `ParameterNode.Type`;
  - `ProgramVariableNode.Type`;
  - `PropertyNode.Type`;
  - `LocalVariableDeclarationNode.Type`;
  - never `LocalVariableDeclarationWithAssignmentNode`.

  Query `ForType`, then:
  - **No annotation:** nothing.
  - **`Error`:** `SetTypeWarning` on the declaring node.
  - **Target that doesn't fit:** the misfit warning on the declaring node.
  - **Otherwise:** `variable.Annotate(AnnotationToTypeInfo(target), new RefinementSource(Annotation, declaringNode))`.

  Every variable in a multi-variable declaration shares the type node, so each gets annotated.
- Function and method return annotations don't create variables, so Task 7 handles them.
- In `TryRefineVariableFromExpression`, when `variable.AnnotatedType is { } annotated` and the RHS is a refinable type:
  - **RHS is `ApiObject` base or `any`:** no change.
  - **Same `PeopleCodeType` (for builtin objects, same `Name`), and the RHS identity refines the annotated one (`rhs.EffectiveIdentity.IsRefinementOf(annotated.EffectiveIdentity)` and the RHS adds detail):** `InferredType = rhs`. Keep `AnnotatedType` and source.
  - **Same type, less or equal detail:** no change.
  - **Otherwise (a conflict):** `SetTypeWarning` on the value node, `&x is annotated as <annotated.Name> (line N) but is assigned <rhs.Name> here.`, and no change.
- `CheckApiObjectReassignment`: when the target's `GetRefinementSource()?.Kind == RefinementKind.Annotation`, don't emit the one-type warning. Inference already reported the conflict, so a second warning would break "exactly one diagnostic per bad use". Still return handled=true so there's no "Cannot assign" error.

- [ ] **Step 1: Write the failing tests.** Run inference plus the checker, as in `ApiObjectReassignmentCheckTests`. Helpers: `LastUse`, `Warnings`, `Errors`.

```csharp
[Fact] public void Local_annotation_types_the_variable()          // "Local ApiObject /* as Session */ &x;\n&x.GetCompIntfc(CompIntfc.USER_PROFILE);" → last use "Session", source Kind Annotation, Line 0
[Fact] public void Multi_variable_declaration_types_every_variable() // "Local ApiObject /* as Session */ &c, &d;\n&c.GetCompIntfc(CompIntfc.A);\n&d.GetCompIntfc(CompIntfc.B);" → both "Session"
[Fact] public void Parameter_annotation_types_parameter_in_body()  // "Function Foo(&s As ApiObject /* as Session */)\n   &s.GetCompIntfc(CompIntfc.A);\nEnd-Function;" → "Session"
[Fact] public void Global_and_instance_annotations_apply()          // Global; and a class with "instance ApiObject /* as Session */ &s;" used in a method body
[Fact] public void Conflicting_assignment_warns_and_annotation_stays() // "Local ApiObject /* as Session */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.A);\n&x.GetCompIntfc(CompIntfc.B);"
//   warning == "&x is annotated as Session (line 1) but is assigned ComponentInterface here."; exactly one warning mentioning &x; last use "Session"; no "Cannot assign" error
[Fact] public void More_detailed_assignment_adds_detail()           // "Local ApiObject /* as ComponentInterface */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.USER_PROFILE);" → no warning (identity comes later in spec §16; same type → no warning)
[Fact] public void Less_detailed_assignment_keeps_annotation()      // "Local ApiObject /* as CompIntfc.FOO */ &x;\n&x = %Session.GetCompIntfc(CompIntfc.A);\n&x.Save();" → last use Identity == new(CompIntfcName:"FOO"); no warning
[Fact] public void Misfit_annotation_warns()                        // "Local Rowset /* as Session */ &rs;" → warning "'as Session' needs an ApiObject declaration; &rs is Rowset." (declared type as the registry holds it; match case-insensitively)
[Fact] public void Unknown_annotation_warns()                       // "Local ApiObject /* as CompIntfc */ &x;" → the CompIntfc did-you-mean text
[Fact] public void Record_annotation_on_record_variable()           // "Local Record /* as Record.JOB */ &r;\n&r.GetField(Field.EMPLID);" → last use RecordTypeInfo "JOB"
[Fact] public void Any_declared_variable_accepts_annotation()       // "Local any /* as Session */ &x;" → "Session"
```

- [ ] **Step 2: Run to verify they fail.** Run: `--filter FullyQualifiedName~DeclarationAnnotationTests`
- [ ] **Step 3: Implement.** The `TypeInferenceVisitor` constructor or `Run` builds the program's `AnnotationIndex` once (Task 4's cached accessor).
- [ ] **Step 4: Run** the focused tests, then both full projects. Expected: PASS.
- [ ] **Step 5: Commit**: `git commit -m "Apply declaration type annotations during inference"`

---

### Task 6: Assignment annotations in inference

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs` (`TryRefineVariableFromExpression` and its two callers)
- Test: `PeopleCodeParser.SelfHosted.Tests/AssignmentAnnotationTests.cs` (new)

**Interfaces:**
- Consumes: `AnnotationIndex.ForValue` (Task 4); `AnnotationToTypeInfo`, `AnnotationFits` and the annotated-variable rules (Task 5).

Behaviour (spec §7): before refining, look up `ForValue(expression)`, then:
- **`Error`:** warning on the value node, and refine as if there were no annotation.
- **Doesn't fit the variable's declared type:** the misfit warning on the value node.
- **Otherwise:** the annotation type `A` replaces the RHS type for this refinement. If the RHS is a concrete type that conflicts with `A` (different type, or `ConflictsWith` on identity), warn on the value node: `The value is <rhs.Name>, but it is annotated as <A.Name>. Using <A.Name>.` The value expression's own stamped type is not changed.

Then the Task 5 rules apply, using `A` as the RHS: a declaration-annotated variable may still warn about a conflict. Latest-wins and the checker's one-type warning are unchanged. The checker compares the target's stamped type with the value node's stamped type, so it still sees the original RHS type. That's acceptable, and the test below pins it.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Annotation_types_unresolved_value()        // "Function GetIt() Returns ApiObject\nEnd-Function;\nLocal ApiObject &d = GetIt() /* as ComponentInterface */;\n&d.Save();" → last use "ComponentInterface"; no warning
[Fact] public void Annotation_after_semicolon_counts()        // "Local ApiObject &d;\n&d = GetIt(); /* as Session */\n&d.GetCompIntfc(CompIntfc.A);" → "Session"
[Fact] public void Annotation_adds_ci_name()                  // "Local ApiObject &ci = %Session.GetCompIntfc(&n) /* as CompIntfc.FOO */;" → last use Identity CompIntfcName FOO, no warning
[Fact] public void Conflicting_value_warns_and_annotation_wins() // "Local ApiObject &x = %Session /* as Tree */;\n&x.Foo();" → warning "The value is Session, but it is annotated as Tree. Using Tree."; last use "Tree"
[Fact] public void Latest_wins_after_annotated_assignment()   // "Local ApiObject &x = GetIt() /* as Session */;\n&x = GetIt() /* as Tree */;" → last use Tree; one-type warning "(Session on line N, Tree on line M)" is NOT expected because the value nodes are ApiObject → assert no "Cannot assign" error only
[Fact] public void Declaration_annotation_still_governs()     // "Local ApiObject /* as Session */ &x;\n&x = GetIt() /* as Tree */;" → "&x is annotated as Session (line 1) but is assigned Tree here."; last use Session
[Fact] public void Annotation_on_property_target_is_ignored()  // in a class: "&obj.P = GetIt() /* as Session */;" → no warning, no effect (no refinement for non-identifier targets)
```

- [ ] **Step 2: Run to verify they fail.** Run: `--filter FullyQualifiedName~AssignmentAnnotationTests`
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run** the focused tests, then the full projects. Expected: PASS.
- [ ] **Step 5: Commit**: `git commit -m "Apply assignment type annotations during inference"`

---

### Task 7: Header and Function-line annotations in metadata

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeMetadataBuilder.cs` (`ExtractMetadata`, `BuildFunctionInfo` ×2, `BuildParameter`, `BuildPropertyInfo` ×2)
- Test: `PeopleCodeParser.SelfHosted.Tests/AnnotationMetadataTests.cs` (new)

**Interfaces:**
- Consumes: `AnnotationIndex.ForType`, `AnnotationTarget`, `AnnotationCategory` (Tasks 3-4); refined slots (Task 2).
- Produces: `TypeMetadataBuilder` fills in the following when an annotation fits the declared type:
  - `SingleParameter.RefinedParameterType`;
  - `FunctionInfo.RefinedReturnType`;
  - `PropertyInfo.RefinedType`, for properties and instance variables;
  - all of them as `new TypeWithDimensionality(target.Type) { Identity = target.Identity }`.

  Misfits and errors are skipped here, because inference reports them. `ExtractMetadata` gets the program's cached `AnnotationIndex`.
- Same-program use: functions declared in the program resolve through `_programMetadata.Methods`, so after this task a call to a local `Function Foo() Returns ApiObject /* as Session */` returns `Session` through Task 2's `ConvertFunctionInfoToTypeInfo` change.

- [ ] **Step 1: Write the failing tests.**
  - Run `ExtractMetadata` on a class source with annotated method parameters, return, property and instance variable. Assert the refined slots: `Type` and `Identity`.
  - Run it on a `Function` line. Assert `RefinedReturnType.Type == Session`.
  - Assert that an unannotated or misfit declaration leaves the slots null.
  - Same-program call: `"Function Foo() Returns ApiObject /* as Session */\nEnd-Function;\nLocal ApiObject &x = Foo();\n&x.GetCompIntfc(CompIntfc.A);"` gives a last use of "Session".
  - Cross-program: register the extracted metadata of `PKG:Svc` (annotated `method GetSess() Returns ApiObject /* as Session */;`) in a `FakeTypeMetadataResolver`. Infer a caller program, `Local PKG:Svc &s = create PKG:Svc();\nLocal ApiObject &x = &s.GetSess();`. Assert the initial value is "Session".
- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run** the focused tests, then the full projects.
- [ ] **Step 5: Commit**: `git commit -m "Store header and Function-line annotations in type metadata"`

---

### Task 8: Call-site warning for annotated parameters

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeCheckerVisitor.cs` (`VisitFunctionCall`, ~line 344)
- Test: `PeopleCodeParser.SelfHosted.Tests/AnnotatedParameterCallTests.cs` (new)

**Interfaces:**
- Consumes: `SingleParameter.RefinedParameterType` (Tasks 2, 7), `node.GetFunctionInfo()`.

Behaviour (spec §7 "Parameters keep a declared-vs-refined split"):
- After the existing validation, take the first overload's parameter list (`functionInfo.Parameters`). For each argument *i* whose parameter is a `SingleParameter` with `RefinedParameterType` set to an API subtype, look at the argument's inferred type:
  - plain `ApiObject`, `any` or unknown passes silently;
  - the same subtype passes;
  - a different API subtype gets a warning on the argument node: `<FunctionName> expects <param.Name> as <refined type name> (annotated); the argument is <arg type name>.`
- Validation against the declared `ApiObject` in `FunctionCallValidator` is unchanged.

- [ ] **Step 1: Write the failing tests.** All three cases are in the same program, using `Function Foo(&s As ApiObject /* as Session */)` defined above the calls:

```csharp
[Fact] public void Plain_ApiObject_argument_is_silent()      // "Local ApiObject &a;\nFoo(&a);" → no warning, no error
[Fact] public void Matching_subtype_argument_is_silent()     // "Foo(%Session);"
[Fact] public void Different_subtype_argument_warns()        // "Local ApiObject &t = %Session.GetCompIntfc(CompIntfc.A);\nFoo(&t);" → "Foo expects &s as Session (annotated); the argument is ComponentInterface."
[Fact] public void Cross_program_annotated_parameter_warns()  // FakeTypeMetadataResolver class PKG:Svc with method Use(&s As ApiObject, refined Session); "&svc.Use(&t)" with &t a CI → warning names Use
```

- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run** the focused tests and the full projects.
- [ ] **Step 5: Commit**: `git commit -m "Warn when an argument's API type differs from an annotated parameter"`

---

### Task 9: Member errors name the type's source

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs` (NotFound message, ~line 75)
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/InvalidMemberAccessCheckTests.cs` (add)

**Interfaces:**
- Consumes: `GetRefinementSource()` (phase 1), `RefinementKind.Annotation`/`Assignment`.

Behaviour (spec §7 "Normal member checks", §10): when the member target is an `IdentifierNode` with a refinement source, append a suffix to the existing message, `'<m>' is not a known <kind> on '<type>'`:
- for an annotation source: ` (&x is <type>, annotated on line N)`;
- for an assignment source: ` (&x is <type>, assigned on line N)`.

Other messages are unchanged.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Unknown_member_on_annotated_variable_names_annotation() // "Local ApiObject /* as Session */ &x;\n&x.Bogus();" → message ends with "(&x is Session, annotated on line 1)"
[Fact] public void Unknown_member_on_assigned_variable_names_assignment()  // "Local ApiObject &x;\n&x = %Session;\n&x.Bogus();" → ends with "(&x is Session, assigned on line 2)"
[Fact] public void Unknown_member_on_declared_variable_has_no_suffix()     // "Local Rowset &rs;\n&rs.Bogus();" → message unchanged (existing test text)
```

- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run** `InvalidMemberAccessCheckTests`, then the full project.
- [ ] **Step 5: Commit**: `git commit -m "Name the annotation or assignment behind an invalid member's type"`

---

### Task 10: Subclasses inherit base declaration annotations

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs` (`ConvertFunctionInfoToTypeInfo` call sites for app-class methods; `OnVariableDeclared` for method parameters)
- Test: `PeopleCodeParser.SelfHosted.Tests/AnnotationInheritanceTests.cs` (new)

**Interfaces:**
- Consumes: `LookupMethodInInheritanceChain`, `_programMetadata.BaseClassName`, the resolver's base-class `TypeMetadata`, refined slots (Task 2).

Behaviour (spec §7 "Inheritance"): a subclass method inherits the base declaration's annotations, unless its own header declaration has its own.
- **Callers:** when an app-class method resolves to a `FunctionInfo` without `RefinedReturnType`, walk the base chain (the same walk `LookupMethodInInheritanceChain` does) for the same-named method. Use the first `RefinedReturnType` found.
- **Inside the subclass body:** when a method parameter has no annotation of its own and the class has a base class, use the base method's parameter *i* `RefinedParameterType`. Base metadata comes from the resolver, so a null resolver or missing base means no inheritance. Apply it with `Annotate` (source node: the parameter). Stop at the first class that has the method.

- [ ] **Step 1: Write the failing tests.** Use a `FakeTypeMetadataResolver` holding base `PKG:Base` (extracted from source with `method Get() Returns ApiObject /* as Session */;` and `method Use(&s As ApiObject /* as Session */);`).
  - **Caller:** with subclass `PKG:Sub extends PKG:Base` overriding both without annotations, `Local PKG:Sub &o = create PKG:Sub();\nLocal ApiObject &x = &o.Get();` gives "Session".
  - **Body:** in `PKG:Sub`'s `method Use` body, `&s` has type "Session".
  - **Override wins:** a subclass header with its own `/* as Tree */` gives "Tree".
- [ ] **Step 2: Run to verify they fail.**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run** the focused tests, then both full projects.
- [ ] **Step 5: Commit**: `git commit -m "Inherit base method annotations in subclasses"`

---

After Task 10, list the rebuild (`PeopleCodeTypeInfo`, `PeopleCodeParser.SelfHosted`, `AppRefiner`) for Tim's manual App Designer check. Check that annotations on a `Function` line, a method header and a `Local` change the member errors and autosuggest. Then plan phase 3 (usage narrowing). Narrowing must skip any variable with an annotation in its scope: `VariableInfo.AnnotatedType != null`, or any `ForValue` hit on its assignments.
