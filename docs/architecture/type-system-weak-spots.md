# Type System Weak Spots

Known structural weaknesses in type inference / type checking, found during a code read
on 2026-10-23. Items marked **unverified** are inferred from reading the code and have not
been reproduced with a test.

Pipeline for reference: `TypeMetadataBuilder` → `TypeInferenceVisitor` → `TypeCheckerVisitor`
→ `CompileChecker` AST checks.

## 1. One error slot per node

**Fixed (2026-10):** errors and warnings are both per-node lists (`AddTypeError` /
`AddTypeWarning`, `GetTypeErrors` / `GetTypeWarnings`). When inference and the checker both
report on one node, each error is kept; a message the node already holds is skipped, so a
re-run pass doesn't double up. Where one diagnostic is meant to replace another (a
declaration-annotation conflict outranks an assignment-annotation value conflict on the same
value), `TypeInferenceVisitor.TryRefineVariableFromExpression` decides that explicitly.

## 2. Assignability logic lives in three places

**Fixed (2026-10):** `TypeInfo.IsAssignableFrom` is the one answer to "does this value fit this
type". `AppClassTypeInfo.IsAssignableFrom` follows the value's inheritance chain (base classes
and interfaces), comparing class names whole (§3). Arrays are invariant, as PeopleCode is:
`array of PKG:Base` rejects `array of PKG:Derived` (verified in App Designer), so
`ArrayTypeInfo` compares app class elements with `IsSameClass`. In `FunctionCallValidator`, one parameter
type is matched by `Fits` and resolved by `ExpectedType` (polymorphic types, app classes,
arrays); `MatchSingle`, `MatchUnion`, `Accepts` and autocomplete's `TryMatchOnePrefix` all use
them, so autocomplete's union matching now knows about inheritance too.
`TypeCheckerVisitor.AreTypesCompatible` adds only what's specific to assignments
(integer/number both ways, a field's data type).

## 3. TypeMetadataBuilder doesn't resolve imports

**Fixed (2026-10):** a short class name (`Local Config &c`) is an editor convenience: App
Designer expands every class name to its qualified form on save, through the program's
imports (a class's own short name expands without one). Saved programs, and so all metadata
fetched from the database, are already qualified; only the program being edited can hold short
names. `TypeMetadataBuilder` keeps names as written, and `TypeInferenceVisitor` qualifies that
program's own metadata (parameters, returns, properties, instance variables, extends and
implements) through its import map before inferring (`QualifyOwnClassNames`), so its signatures
match the qualified values passed to them. Class names are then compared whole
(`AppClassTypeInfo.Names`): the earlier rule that matched a bare name on the class name alone,
treating `PKG:Config` and `OTHER:Config` as one class, is gone. A short name no import resolves
stays bare and matches nothing qualified; App Designer rejects it on save and
`UnimportedClassCheck` reports it.

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

**Fixed (2026-10):** `TypeCheckerVisitor.VisitFunctionCall` passes the receiver of an array
method call, so `$element` parameters (`Push`, `Unshift`, `Find`, `Substitute`, `Replace`) are
checked against the element type, one dimension down: pushing a number into an
`array of string`, or a string into an `array of array of string`, is an error, and the
message names the resolved type ("should be string"), not `$element`. App Designer saves both,
but they fail or misbehave at run time, so they are errors by choice. Only `Array` uses
`$element`/`$same` parameters.

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

- `TypeInfo.Equals` compares only `PeopleCodeType`, so `record(A).Equals(record(B))` and
  `field(A.X).Equals(field(B.Y))` are true. This is now deliberate: names live in
  `TypeIdentity` (`TypeInfo.Identity` / `EffectiveIdentity`, 2026-10), which code that cares
  compares explicitly with `IsRefinementOf` / `ConflictsWith`. Callers that use `Equals` still
  ignore names.
- `MainForm` and `AutoCompleteService` still have their own `DetermineQualifiedName` copies for
  non-inference uses (member visibility, cache eviction, `%This` suggestions). Inference itself
  goes through `TypeInferenceRunner` everywhere (2026-10).

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
- **Global/Component redeclarations narrow per declaration.** A Global or Component variable
  declared again inside two method bodies is narrowed separately for each declaration, so the
  two can settle on different types although they are one variable at run time.

Fixed in 2026-10:
- A parenthesized `(Null)` return or member assignment is skipped like a bare `Null`.
- An annotation naming an unknown type is warned about but no longer turns narrowing off.

Fixed in 2026-10, from App Designer probes:
- `DataCollection.Item`, `ItemByKeys`, `CurrentItem`, `GetEffectiveItem` and `InsertItem` return
  a `DataItem` (they were `Row` or `void` in `typeinfo.dat`), and `DeleteItem` returns an
  ignorable `boolean`. A DataItem's properties are its fields, dynamic like a component
  interface's.
- Member access on a plain `ApiObject` that nothing has typed isn't checked, since it could be a
  CI with fields and methods of its own (it was a false error on CI code such as
  `&item.RoleName`).
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

## 9. Limits of component interface typing

Left open by the component interface typing work (spec
`docs/superpowers/specs/2026-10-26-component-interface-typing-design.md`):

- **An unnamed CI's own members are unchecked and untyped.** A CI named by `CompIntfc.X` (in
  `GetCompIntfc()` or a `/* as CompIntfc.X */` annotation) is typed and checked from its
  definition when the database can supply it (see `docs/demos/component-interfaces`). Without a
  name, or without the definition (no database, a failed lookup), `&ci.Roles` is untyped and the
  variable it's assigned to narrows from its own uses, field types on the CI and its items are
  untyped, and no method on a ComponentInterface is reported as unknown, since a CI can add
  methods of its own. Narrowing settles the API type but never the CI name. Still untyped:
  - `GetCompIntfc(&name)` and `GetCompIntfc("USER_PROFILE")`: only a `CompIntfc.X` reference
    names the CI; annotate the variable to type it;
  - what `Find()` returns: a plain DataCollection, not a collection of the CI's find results.
- **An annotation that conflicts with its declaration's own initializer isn't reported.**
  `Local ApiObject /* as CompIntfc.USER_PROFILE.Roles */ &r = &ci.EmailAddresses.Item(1);` gives no
  warning; only a later assignment is compared with the annotation. (An annotation naming a CI
  that doesn't exist, or a collection path the CI doesn't have, is a warning.)
- **Item properties also accept the ComponentInterface type's own properties.** `typeinfo.dat`'s
  DataItem declares only `ItemNum`, so a property on a known item is accepted when it's the
  collection's, DataItem's or one of the builtin ComponentInterface type's (`PropertyInfoCollection`,
  `InteractiveMode`, …). That lets a few names through on items that may not exist there.
- **A failed lookup is retried after a short pause.** When the database throws, the CI is
  unchecked (Unknown) and not queried again for `ITypeMetadataResolver.ComponentInterfaceFailureRetryInterval`
  (10 s), so a database that recovers is noticed only on the next lookup after that.
- **A very large CI is unchecked through the SQL proxy.** The proxy's items query is capped at
  10000 rows; a CI with that many items is treated as a failed lookup rather than checked
  against a possibly truncated definition. The Oracle and SQL Server managers aren't capped.
