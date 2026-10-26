# Type System Weak Spots

Known structural weaknesses in type inference / type checking, found during a code read
on 2026-10-23. Items marked **unverified** are inferred from reading the code and have not
been reproduced with a test.

Pipeline for reference: `TypeMetadataBuilder` → `TypeInferenceVisitor` → `TypeCheckerVisitor`
→ `CompileChecker` AST checks.

## 1. One error slot per node

`AstNodeExtensions.SetTypeError` / `SetTypeWarning` overwrite `node.Attributes[...]`.
Inference (array index, operator errors) and the checker (call validation) can both write
to the same argument node; the last write silently wins.

The ApiObject annotation work (2026-10) works around this rather than fixing it: each
annotation or conflict warning goes on its own node (the declaring node, or the assignment's
value), and the checker's one-type warning stays on the `AssignmentNode`.

## 2. Assignability logic lives in three places

- `TypeInfo.IsAssignableFrom` overrides (`PeopleCodeTypeInfo/Types/PeopleCodeType.cs`)
- `TypeCheckerVisitor.AreTypesCompatible`
- `FunctionCallValidator.MatchSingle` / `MatchUnion` / `Accepts` (polymorphic-resolve block
  duplicated three times)

`AppClassTypeInfo.IsAssignableFrom` ignores inheritance, so each caller bolts on its own
`IsAppClassCompatible` check.

There is also a fourth copy: the union branch of `TryMatchOnePrefix` (autocomplete's
`GetAllowedNextTypes`) re-implements union matching. The field-name rule (`*` prefix, 2026-10)
had to be added to all four sites; it lives in one helper (`MatchFieldNameArgument`), which is
the pattern a consolidation should follow.

## 3. TypeMetadataBuilder doesn't resolve imports (unverified)

`TypeMetadataBuilder.ExtractTypeName` returns the bare class name for `As MyClass`, while
`TypeInferenceVisitor` qualifies the same name to `PKG:MyClass` via `_importMap`.
Passing a `PKG:MyClass` argument to a parameter declared with the imported short name may
fail the QualifiedName comparison. Same issue for `BaseClassName`.

## 4. Permissive sentinels mask failures

Unknown, Any, `UnionReturnTypeInfo` (`Kind == Any`) and dynamic `@` references are accepted
almost everywhere. `InvalidTypeInfo` is only used to build an error message — the node is
then typed Unknown, so invalidity never propagates. Many checks pass because resolution
failed, not because the code is correct.

**Partly addressed (2026-10):** `ApiObject` values the unit can't refine itself (parameters,
Globals/Components, properties, method returns) can now be typed with a `/* as T */`
annotation (`Local ApiObject /* as Session */ &s;`, `&s As ApiObject /* as Session */`).
Annotations on method headers flow to callers and are inherited by overrides, and calls
warn when an argument is a different API subtype from an annotated parameter. Unannotated
`ApiObject` values still resolve members to Any.

## 5. Checker validates calls without the receiver type

`TypeCheckerVisitor.VisitFunctionCall` calls `validator.Validate(functionInfo, arguments)`
without `objectType`, so `$same` / `$element` parameter types fall back to Any.

## 6. Variable typing is flow-insensitive

Refinement mutates the variable in traversal order. A refinement made inside one `If`
branch persists for everything visited afterward; narrowed auto-declared variables keep that
type for the rest of the pass.

**Partly addressed (2026-10):** declared and refined types are now separate. Declared
variables are refined with `VariableInfo.Refine`, which leaves `Type` as declared and records
a `RefinementSource`. Refinement compares against the declared type, so the latest assignment
wins, and a reassigned `ApiObject` no longer gets a false "Cannot assign" error. Only
auto-declared variables still overwrite `Type` (`SetInferredType`). Typing is still
flow-insensitive; scope-aware typing is deferred (spec §15 of
`docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md`).

## 7. Smaller gaps

- `ResolveMemberAccessReturnType` has no branch for array receivers, so `&arr.Len` infers
  as Any (array *methods* are handled in `ResolveFunctionInfo`).
- `TypeInfo.Equals` compares only `PeopleCodeType`, so `record(A).Equals(record(B))` and
  `field(A.X).Equals(field(B.Y))` are true. This is now deliberate: names live in
  `TypeIdentity` (`TypeInfo.Identity` / `EffectiveIdentity`, 2026-10), which code that cares
  compares explicitly with `IsRefinementOf` / `ConflictsWith`. Callers that use `Equals` still
  ignore names.
- `TypeInferenceVisitor.Run` setup is duplicated in `MainForm`, `AutoCompleteService` and
  `TypeErrorReporter` (which also has its own copy of `DetermineQualifiedName`) instead of
  going through `TypeInferenceRunner`.
