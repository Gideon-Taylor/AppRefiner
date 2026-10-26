# Type System Weak Spots

Known structural weaknesses in type inference / type checking, found during a code read
on 2026-10-23. Items marked **unverified** are inferred from reading the code and have not
been reproduced with a test.

Pipeline for reference: `TypeMetadataBuilder` → `TypeInferenceVisitor` → `TypeCheckerVisitor`
→ `CompileChecker` AST checks.

## 1. One error slot per node

`AstNodeExtensions.SetTypeError` overwrites `node.Attributes[...]`.
Inference (array index, operator errors) and the checker (call validation) can both write
to the same argument node; the last write silently wins.

Warnings no longer have this problem (2026-10): `AddTypeWarning` appends to a per-node list,
skipping a message the node already holds, so a multi-variable declaration warns once per
variable and a re-run pass doesn't double up. Where one diagnostic is meant to replace
another (a declaration-annotation conflict outranks an assignment-annotation value conflict
on the same value), `TypeInferenceVisitor.TryRefineVariableFromExpression` decides that
explicitly instead of relying on overwrite.

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

## 3. TypeMetadataBuilder doesn't resolve imports

`TypeMetadataBuilder.ExtractTypeName` still returns the bare class name for `As MyClass`
(parameters, returns, properties, `BaseClassName`), while `TypeInferenceVisitor` qualifies the
same name to `PKG:MyClass` via `_importMap`.

`TypeInferenceVisitor` maps an app class's own short name to its `QualifiedName` (2026-10), so
`Local Svc &o = create Svc(&t);` inside `PKG:Svc` resolves to the program's own metadata and
its constructor is checked. When the metadata is extracted without a qualified name (the
`QualifiedName` is empty), the short name stays unresolved and the constructor's arguments
go unchecked. Metadata return and property types (`Returns Svc`, `property Svc Next`) are
qualified the same way when inference converts them (`ResolveAppClassPath`), and inference
stamps every app class type node with its import-resolved type, so declared return types
checked by `TypeCheckerVisitor` match too.

Parameters were verified to mismatch: `FunctionCallValidator` builds the expected type from
the bare metadata name, so passing a `PKG:Config` value to `&cfg As Config` reported
"Argument 1 should be Config, found PKG:Config". `IsAppClassCompatible` now matches a bare
expected type on the class name (the value's own name or any entry in its inheritance chain).
`AppClassTypeInfo.IsAssignableFrom` applies the same rule, so the leniency also holds inside
array types (`&cfgs As array of Config` accepts `array of PKG:Config`) and in assignment
compatibility. This is an approximation: two classes with the same short name in different
packages are treated as the same. Qualifying metadata names through the declaring program's
imports would remove it.

Still open: the reverse case. A parameter declared with a qualified type (`&cfg As
PKG:Config`) rejects an argument whose type stayed bare because the caller lacks the import
(inference can't qualify it). This predates the 2026-10 changes.

## 4. Permissive sentinels mask failures

Unknown, Any, `UnionReturnTypeInfo` (`Kind == Any`) and dynamic `@` references are accepted
almost everywhere. `InvalidTypeInfo` is only used to build an error message — the node is
then typed Unknown, so invalidity never propagates. Many checks pass because resolution
failed, not because the code is correct.

**Partly addressed (2026-10):** `ApiObject` values the unit can't refine itself (parameters,
Globals/Components, properties, method returns) can now be typed with a `/* as T */`
annotation (`Local ApiObject /* as Session */ &s;`, `&s As ApiObject /* as Session */`).
Annotations on method headers flow to callers and are inherited by overrides, and calls
warn when an argument is a different API subtype from an annotated parameter.

**Mostly addressed (2026-10, phases 3–4):** unannotated `ApiObject` values are now typed in two
more ways.
- **Usage narrowing:** a variable declared `ApiObject` (local, parameter, Global/Component,
  instance or property) with no annotation takes the one API type that has every member used
  directly on it, settled by the first use that leaves a single candidate
  (`Visitors/Narrowing/UsageNarrowingAnalyzer`). Uses that don't fit are reported once as
  warnings (N1–N4) instead of a member error.
- **Return refinement:** an unannotated method or function that returns `ApiObject`, `Record`
  or `Field` is typed from the values it returns when they agree (R1 warns when they don't),
  in the same program callee-first, and across programs through `ReturnTypeRefiner`, which the
  database resolver runs on every program it loads. Unannotated properties with a getter are
  refined from the getter's returns; those without one, and instance variables, from their
  assignments. Cached refinements are evicted along with what they were inferred from, so
  saving a class also drops the cached callers refined from it. Dependencies are recorded
  generously (every class a refinement looks up), so saving a widely used class drops most of
  the cache, and the next tooltip re-parses whatever it touches (no database round trips).

An `ApiObject` value still resolves members to Any when no annotation, assignment, use or
return decides it. See §8 for the remaining limits.

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

## 8. Limits of ApiObject narrowing and return refinement

Left open by phases 3–4 (spec `docs/superpowers/specs/2026-10-25-apiobject-type-resolution-design.md`):

- **Mutually recursive methods keep their declared return type.** Callee-first ordering can't
  order a cycle, so `A` returning `B()` and `B` returning `A()` stay `ApiObject`. Across
  programs, a cycle or a chain deeper than `ReturnTypeRefiner.MaxDepth` (3) leaves the metadata
  `IsProvisional`. A cycle member refined at the top level is cached with whatever its cycle
  partners could give it, so a return that depends on a partner through the cycle may stay
  `ApiObject` until that class is evicted (on save) or re-fetched. The nested, cut-short members
  are evicted when the top-level refinement finishes.
- **Callers don't type parameters.** Same-program callers aren't used to infer a parameter's
  type (spec §3); an unannotated parameter is typed only by narrowing on its own uses.
- **Multi-name instance declarations share one `PropertyInfo`.** `instance ApiObject &a, &b;`
  extracts one `PropertyInfo` for both names, so refining one from its assignments refines both.
- **A library's own `Declare` sees the unrefined copy.** A function library that `Declare`s
  one of its own functions gets that function's extracted (unrefined) signature through the
  declaration, not the refined one.
- **A parenthesized `(Null)` counts as a value.** The agreement rules skip a bare `Null` return
  or assignment, but not `(Null)`, which then disagrees with the other values.
- **An annotation with an unknown type still excludes the variable from narrowing.**
  `Local ApiObject /* as Sesion */ &x;` warns about the unknown type and then leaves `&x` as plain
  `ApiObject`, even if its uses would settle it.
- **Global/Component redeclarations narrow per declaration.** A Global or Component variable
  declared again inside two method bodies is narrowed separately for each declaration, so the
  two can settle on different types although they are one variable at run time.
- **A CI's own properties are untyped.** `&ci.Roles` is a DataCollection at run time because
  Roles is a scroll in the CI's definition, but nothing reads that definition yet, so `&ci.Roles`
  is untyped and the variable it's assigned to narrows from its own uses. Field types on a CI or a
  DataItem are untyped too. Both wait on reading CI definitions (spec §16).

Fixed in 2026-10, from App Designer probes:
- `DataCollection.Item`, `ItemByKeys`, `CurrentItem`, `GetEffectiveItem` and `InsertItem` return
  a `DataItem` (they were `Row` or `void` in `typeinfo.dat`), and `DeleteItem` returns an
  ignorable `boolean`. A DataItem's properties are its fields, dynamic like a component
  interface's.
- Member access on a plain `ApiObject` that nothing has typed is checked only for methods no API
  type has (it was a false error on CI code such as `&item.RoleName`).
- In narrowing, a property some API type declares narrows to its declarers (`ItemNum` settles
  DataItem, `CurrentItemNumber` DataCollection). One nobody declares may be a CI or item field,
  or a typo, so it counts only after something else has narrowed the list. Accepted trade-off: a
  CI field named like a declared property (`NAME`, `COUNT`) on a variable not yet typed as a CI
  narrows away from ComponentInterface. CI variables are nearly always typed by their
  `GetCompIntfc()` assignment or a CI method first; otherwise annotate them.
- An ignored return value is an error only for user-written functions and methods and for typed
  builtin methods not marked optional. App Designer accepts an ignored return from any API
  object method, even on `%Session`, because those calls are late-bound.

A call to the class's own method without `%This` (`Get()` rather than `%This.Get()`) isn't
valid PeopleCode, so treating it as an undeclared function call is correct, not a limit.
