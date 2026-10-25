# ApiObject Type Resolution — Design

**Date:** 2026-10-25
**Status:** Agreed design, not yet implemented.
**Area:** `PeopleCodeParser.SelfHosted`, `PeopleCodeTypeInfo`, AppRefiner resolver (`DatabaseTypeMetadataResolver`)
**Source:** design artifact https://claude.ai/artifact/AXVmm4pFEyDKdvLithQwtV (revised after code review)

How AppRefiner should work out the concrete PeopleSoft API type (Session, ComponentInterface, Tree, …) behind a variable declared `ApiObject`, so that member validation, hover and autosuggest work on it.

## 1. Summary

PeopleCode does not allow `Local Session &s`; API types must be declared `ApiObject`. Today AppRefiner only learns the concrete type when the variable is assigned in the same unit of work (the method body for locals, the program for instance variables). Parameters, and Globals/Components assigned in another program, stay `ApiObject`, so their members can't be checked or suggested.

The design adds these sources of concrete type information, in a fixed order:

1. **An explicit `/* as T */` comment** directly after a declaration's type or after an assignment's value. A declaration annotation always wins.
2. **Assignments** in the unit of work, as today, now also using **inferred return types** of external functions and other classes' methods.
3. **Usage narrowing**: the members used on the variable, read in source order, narrow the candidate API types until one is left.
4. Otherwise the variable stays `ApiObject`.

It also:

- fixes an existing bug where a refined variable is checked against its refined type instead of its declared type;
- adds diagnostics that explain what was inferred and where the code contradicts it;
- limits the built-in Record/Field naming heuristic to built-in functions (phase 0);
- widens the cross-program return-type refiner to named records, fields and later specific component interfaces (phase 4).

The name a type carries (which record, which record+field, which CI) lives in one generic, flat `TypeIdentity` attached to the type, not in a subclass per kind.

## 2. How it works today

### API subtypes

- API types are ordinary `PeopleCodeType` enum values (`Session`, `ComponentInterface`, …; `ApiObject = 35`) and resolve to `BuiltinObjectTypeInfo`.
- typeinfo.dat flags each API subtype (`IsApiObjectType`, read in `BuiltinObjectReaderStrategy`). `PeopleCodeTypeDatabase.IsApiObjectSubtype(name)` checks it.
- The flag makes subtypes non-declarable (`IsDeclarableTypeName`, with a type-checker error suggesting `ApiObject`), and lets `ApiObject` accept any subtype in `BuiltinObjectTypeInfo.IsAssignableFrom` (`IsApiObjectBase`).

### Refinement from assignments

- `TypeInferenceVisitor.TryRefineVariableFromExpression` runs after `Local … = expr` declarations and simple `&x = expr` assignments.
- If the RHS is an API subtype and the variable's current type is `any` or the `ApiObject` base, it calls `VariableInfo.SetInferredType`, which also overwrites `VariableInfo.Type`.
- Later identifier uses read `variable.InferredType`, so members resolve against the concrete type.
- Concrete types reach the RHS via system variables (`%Session`), signature return types (`GetCompIntfc(...) -> ComponentInterface`) and member access on already-concrete objects.

### Gaps

- **Values from outside the unit are never refined**: parameters, Globals/Components set elsewhere, instance variables used from elsewhere. Member lookups on `ApiObject` return `Any`: no errors, but no member info.
- **External return types stay `ApiObject`.** `DatabaseTypeMetadataResolver.ParseAndExtractMetadata` parses the full source, but `TypeMetadataBuilder` keeps only the declared signature.
- **Bug — reused variable gets a false error** (from reading code, not yet reproduced). After `Local ApiObject &x = %Session;`, `&x = &x.GetCompIntfc(...)` reports `Cannot assign type 'ComponentInterface' to 'Session'`:
  1. `TypeInferenceVisitor.VisitAssignment` visits the target first; `VisitIdentifier` stamps it with the current refined type, `Session`.
  2. `TryRefineVariableFromExpression` takes `variable.InferredType ?? …` as the "declared" type — `Session` — so the CI value is not refined.
  3. `TypeCheckerVisitor.VisitAssignment` compares against the stamped target type (`Session`); `IsAssignableFrom` fails.
- **Bug — false errors on CI properties** (from reading code). Once refined to `ComponentInterface`, `&ci.EMPLID` is looked up in typeinfo.dat and reported unknown. `InvalidMemberAccessCheck` exempts property access only on Record, Row and Grid.
- **Record and Field names are lost at function boundaries.** A user function/method declared `Returns Record`/`Returns Field` yields a plain `Record`/`Field`; nothing inspects its `Return` statements, and `TypeWithDimensionality` has no slot for a name.
- **Bug — the built-in naming heuristic applies to user code.** `SpecializeNamedInstanceReturn` names a returned record/field from the arguments, the receiver, or (zero-arg calls in record PeopleCode) the current record. Right for `CreateRecord(Record.X)`, `&row.GetRecord(Record.X)`, `GetRecord()`, but `VisitFunctionCall` runs it for every call. Likely mistyped today:
  - a zero-arg user function/method returning `Record`, called in record PeopleCode, is typed as the current record;
  - `CopyTo(Record.FOO)` (user function returning `Record`) is assumed to return FOO;
  - a user function returning `Field` called as `MyFunc("hello")` is typed as field `hello` on the default record.

**Builder's call:** each bug marked "from reading code" must get a failing test first; if the test doesn't fail, record that and adjust the phase.

## 3. Assumptions and scope

- **One concrete type per variable.** An `ApiObject` variable is assumed to hold one concrete type; reuse for different types is poor style and gets a warning.
- **In scope:** every `ApiObject`-declared variable — Local, Global, Component, instance variables, method/function parameters, header properties — and return types declared `Returns ApiObject`.
- **Out of scope for now:**
  - inferring parameter types from callers in other programs (no call index);
  - inferring from callers within the same program (private methods) — possible later;
  - variables declared `any` or auto-declared: they keep today's behaviour, including no one-type warning (they keep refining freely, as records do);
  - scope-aware (flow-sensitive) typing — deferred, see §15;
  - moving `RecordTypeInfo`/`FieldTypeInfo` onto `TypeIdentity`.

## 4. Resolution order

For each `ApiObject` variable, the first source that produces a type wins:

| # | Source | Strength | Can later sources replace it? |
|---|--------|----------|-------------------------------|
| 1 | Declaration annotation (`Local ApiObject /* as Session */ &x;`) | Authoritative for the scope | No. More detail can be added; a conflicting assignment warns. |
| 1a | Assignment annotation (`&x = … /* as Session */`) | Exact at that point | Replaces the value's type; latest wins afterwards. |
| 2 | Assignment in the unit of work, incl. inferred cross-program return types | Exact | A second, different assignment warns (one-type); later uses get the new type (latest wins). |
| 3 | Usage narrowing | Inferred; shown as such in hover | An assignment replaces it. |
| 4 | None: stays `ApiObject` | — | — |

Flow:

```
ApiObject variable
 ├─ declaration annotated "as T"?  yes → use T (locked)
 ├─ any annotation in scope?       yes → stays ApiObject (no narrowing)
 ├─ narrowing pre-pass finds one?  yes → seed narrowed type
 └─ otherwise                          → stays ApiObject
then main inference pass:
   assignment with concrete RHS, not locked → refine to RHS (or its "as T"); warn if it differs
   assignment with concrete RHS, locked     → warn if RHS differs from annotation
```

Narrowing is seeded *before* the main pass even though it ranks below assignments. It's purely syntactic (needs no inference), and seeding means the first use already has a type. Assignments in the main pass replace it; under the one-type assumption they agree, and when they don't, that's the warning case. This keeps it to **one** inference pass.

## 5. Declared vs. refined type

Every other piece depends on this; it also fixes the reassignment bug.

### VariableInfo

- **`Type` (string) stays the declared type.** Refinement stops calling `UpdateType`.
- **`InferredType` is the refined type**, with a new `RefinementSource` beside it: annotation, assignment or narrowing, plus the node/line that set it.
- **Auto-declared variables keep calling `UpdateType`.** That path (`VisitAssignment`) depends on `Type == "any"` to type the variable on its first assignment only.

### Refinement and checking

- **Refinement checks the declared type.** `TryRefineVariableFromExpression` compares against `ConvertTypeNameToTypeInfo(variable.Type)` (now truly declared), not `InferredType`. A variable refined to `Session` can then still be assigned `ComponentInterface`.
- **Latest assignment wins.** After a second, different assignment, later uses get the new type. Matches straight-line code, matches how records re-refine today, and steps toward §15. Keeping the first type was rejected: wrong for every use after reassignment, including an `Else` branch.
- **The type checker gets the declared type from its own registry.** `TypeCheckerVisitor` is a scoped visitor with its own `VariableInfo` objects that nothing overwrites. For identifier targets, `FindVariable` gives the declared type. If it's `ApiObject`, any API subtype passes the assignment check.
- **The one-type warning comes from the checker.** The target node's stamped type is the type before this assignment; a different API subtype on the value is the warning (never an error). Only `ApiObject`-declared variables get it.

### Source tracking

- **Kept on `VariableInfo`, not on `TypeIdentity`.** Identity says what the type is and takes part in comparisons; the source says why we think so and must never affect a comparison. It also mustn't travel with the value (the result of `&x.GetCompIntfc()` wasn't "refined on line 4").
- **Stamped onto each use.** `VisitIdentifier` attaches the source to the identifier node as a separate attribute (like `SetFunctionInfo`). Hover, the one-type warning ("Session on line 4") and narrowing conflicts read it from there.

**Risk, checked:** every scoped visitor builds its own `VariableInfo` objects (`ScopedAstVisitor`), and only `TypeInferenceVisitor` refines its own — including refactors that write `Local {variable.Type} …` back into source. Code outside the visitor sees only node-stamped types, with one exception: the visitor passes its live `VariableRegistry` to `_onUndefinedVariable`, and `ArrayExtensions.VariableTypeMatches` reads `InferredType` then `Type` from it. Arrays are never refined and `InferredType` is read first, so this is unaffected, but keep it in mind.

```
Local ApiObject &x = %Session;          /* refined: Session (assignment, line 1) */
&x = &x.GetCompIntfc(CompIntfc.USER_PROFILE);
/* before: error "Cannot assign type 'ComponentInterface' to 'Session'" */
/* after:  warning "&x is assigned more than one API type …"            */
```

## 6. Type identity

```csharp
public sealed record TypeIdentity(string? RecordName = null, string? FieldName = null, string? CompIntfcName = null)
{
    public bool IsRefinementOf(TypeIdentity? less);  // detail added: fine
    public bool ConflictsWith(TypeIdentity? other);  // different names: one-type warning
}
```

### Decisions

- **Flat, typed record**, not a bag of untyped attributes. A Field with only its field name known is a partial identity.
- **On the base `TypeInfo`**: an `Identity` property, copied in `WithAssignable`, `WithAutoDeclared` and `WithBufferField` (as `BufferField` is today).
- **Travels with the value.** Unlike `BufferField` (dropped on purpose), identity survives storage in variables and return from calls.
- **Not part of `Equals`.** `TypeInfo.Equals` compares only `PeopleCodeType` (`record(A).Equals(record(B))` is true), and caches may depend on that. Identity comparisons use `IsRefinementOf`/`ConflictsWith` explicitly.
- **Component interfaces use it directly.** A CI stays a plain `BuiltinObjectTypeInfo("ComponentInterface")` with an identity. No `ComponentInterfaceTypeInfo` subclass, so `FromPeopleCodeType`, `IsApiObjectSubtype`, `IsApiObjectBase` need no special cases.
- **`RecordTypeInfo`/`FieldTypeInfo` stay as they are** (they hold behaviour like `DirectRecordAccess` and the field data-type lookup, ~40 uses). Backing their names with `TypeIdentity` is deferred.

### Carrying it through metadata

- **Precedent:** `TypeWithDimensionality.AppClassPath` (copied into `PropertyInfo.AppClassPath`) already stores a name beside a type for one kind. Identity generalizes it.
- **`TypeWithDimensionality` and `PropertyInfo`** get an optional `Identity`. Not written by the typeinfo.dat binary writer: built-ins never carry one; user `TypeMetadata` lives in the in-memory `TypeCache`.
- **`FunctionInfo`** gets a `RefinedReturnType` beside the declared `ReturnType` (declared-vs-refined split for returns, so hover can still show `Returns ApiObject`).
- **Main conversion point.** `ConvertTypeWithDimensionalityToTypeInfo` is where the inference path turns metadata types into `TypeInfo`; it attaches the identity, producing `RecordTypeInfo`/`FieldTypeInfo` for records/fields and a CI with identity otherwise. Other conversion sites exist and drop identity unless updated: `PeopleCodeTypeDatabase.ToTypeInfo`, `UnionReturnTypeInfo.FromTypeWithDimensionality`, and five sites in `FunctionCallValidator`. Builtins never carry identity, so `ToTypeInfo` is safe; the others must be checked when identity starts flowing through user metadata (phase 4).

### Where identity gets lost (guard these)

1. **Types rebuilt from scratch** from a `PeopleCodeType` or name string. The main case, `ConvertTypeNameToTypeInfo(variable.Type)` in refinement, goes away with phase 1.
2. **Equality and merging.** `GetCommonType(record(A), record(B))` returns `record(A)`. Merging disagreeing returns/branches must compare identities explicitly.
3. **`CloneWithState` overrides** copy only their own fields; base-class identity is safe only if every base `With*` copies it.
4. **Name strings.** `VariableInfo.Type` holds `typeInfo.Name` (`"record"` for every record). New code must not rely on it for identity.

### Later uses

Names flowing through chains: `CreateRowset(Record.JOB)` → `rowset(JOB)` → `&rs(1)` → `row(JOB)` → `GetRecord(Record.JOB)`. Type-specific logic, no new storage.

## 7. Type annotations: `/* as T */`

An explicit comment giving a variable, parameter, property or return a more specific type than PeopleCode lets you declare. Uses `as`, PeopleCode's cast keyword, echoing `&x As T` in parameter lists and App Designer's `/+ &p as ApiObject +/`.

### Syntax

A `/* */` comment whose whole body is `as T`. Case-insensitive.

| `T` | Meaning | Allowed on |
|-----|---------|------------|
| `Session`, `ComponentInterface`, … | That API subtype | `ApiObject` or `any` |
| `CompIntfc.CI_NAME` | `ComponentInterface` with identity `CI_NAME` | `ApiObject` or `any` |
| `Record.REC` | `Record` with identity `REC` | `Record` or `any` |
| `Record.REC.FIELD` | `Field` with record+field identity | `Field` or `any` |
| `Field.FIELD` | `Field` with only its field name | `Field` or `any` |

- **Reuses existing forms.** `Record.X`, `CompIntfc.X`, `Field.X` are definition-reference forms, and each maps onto a `TypeIdentity`. `ReferenceTypeInfo.IsSpecialReferenceKeyword` recognises the keywords, but `TryParseDefinitionReferenceString` (private, `TypeInferenceVisitor`) rejects multi-segment names, so the annotation parser needs its own small parser for `Record.REC.FIELD` (reuse what it can).
- **Why `Record.JOB.EMPLID`, not `JOB.EMPLID`:** `JOB.EMPLID` is the field's *value*; `Record.REC.FIELD` is already promoted to a Field object by inference. Requiring the keyword also keeps `Field.EMPLID` unambiguous.
- **Annotation vs. prose.** The whole body must be `as T`. Anything else (`/* store it as Session for later */`, commented-out code) is ignored, no warning. Requiring `as` stops `/* Tree */` from counting.
- **Only `/* */`** — not `/+ +/` (App Designer's), `<* *>`, or `REM`.
- **Checks:** `T` must fit the declared type per the table. API names are validated with `IsApiObjectSubtype`, not just `FromString` (`CompIntfc` is itself a `PeopleCodeType`, value 137). Unknown or misfit names warn.

### Where it counts

Only two places, both meaning "directly after X, same line, only whitespace between".

**1. Directly after a type in a declaration without an initial value**

```
Function Foo(&a As ApiObject /* as Session */, &b As string) Returns ApiObject /* as Session */
method GetCI(&s As ApiObject /* as Session */) Returns ApiObject /* as CompIntfc.FOO */;
property ApiObject /* as Session */ CurrentSession get;
Local ApiObject /* as Session */ &c, &d;   /* applies to every variable in the declaration */
Global ApiObject /* as Session */ &gSession;
```

- Parameters, returns and properties in class headers and on `Function` lines.
- Local, Global, Component and instance variables declared without an initial value.
- No exported/local distinction: a header or `Function`-line annotation is part of the signature, so `TypeMetadataBuilder` carries it to other programs like the declared type.
- **Parameters keep a declared-vs-refined split** (decided). The parameter's declared type stays `ApiObject` for call validation; the annotation is stored beside it, as `RefinedReturnType` is for returns. Inside the callee, `&s` has the annotated type. At call sites:
  - a plain `ApiObject` argument (not resolvable to a subtype) is accepted silently: no error, no warning;
  - an argument of the annotated subtype (or the same subtype with more detail) is accepted;
  - an argument of a **different** API subtype gets a warning, e.g. "Foo expects &s as Session (annotated); the argument is Tree."
  - `FunctionCallValidator` keeps validating against the declared `ApiObject`; the subtype warning is a separate check on top of it.
- Moves with the declaration: sorting refactors can't separate it.
- Multi-variable declarations: the annotation types every variable. Split the declaration or annotate assignments to type them differently.

**2. Directly after the value of an assignment to a simple variable**

```
&ci = &sess.GetCompIntfc(&ciName) /* as CompIntfc.FOO */;
&d = GetSomethingReturnsApiObject(); /* as ComponentInterface */
Local ApiObject &s = GetX() /* as Session */;
```

- Reads as "&x gets a value as a T"; the cast applies to the variable.
- Either side of the `;`, same line.
- Declarations with an initial value use this form only; their type position is not checked.
- Targets must be simple variables (`&x = …`); property and array targets aren't refinable.

**Everywhere else: silently ignored** — function arguments, operands, `Return` expressions, after a variable name, above statements, next line. A "has no effect here" warning was rejected (cheap to add later).

### Finding them

- **Not via `GetLeadingComments()`.** The lexer attaches every comment as leading trivia of the *next* token (`PeopleCodeLexer.NextToken`); `AddTrailingTrivia` is never called. So `&a As ApiObject /* as Session */,` stores the comment on the `,`, and `Returns ApiObject /* as Session */` on the first token of the body.
- **By position**, using the comment list `CollectComments` already puts on `ProgramNode`. A type annotation is the comment starting right after a type node ends, same line. An assignment annotation is the comment starting right after the value ends, or right after its `;`, same line.

### Behaviour

- **A declaration annotation acts as the declared type PeopleCode wouldn't let you write**, for the whole scope. A later assignment may add detail (annotated `ComponentInterface`, assigned `CompIntfc.FOO`). Less detail (plain `ComponentInterface` after `as CompIntfc.FOO`) doesn't erase it. A conflicting value warns; the annotation stays.
- **An assignment annotation is a refinement at that point.** `T` replaces the value's inferred type when refining. `ApiObject`, `any`, or same-subtype-less-detail values refine to `T` quietly; a conflicting value warns and `T` wins. The value expression's own stamped type is unchanged. Latest-wins and the one-type warning apply afterwards.
- **Overrides narrowing.** Any annotation for a variable in its scope turns narrowing off for it.
- **Normal member checks**, with the error naming the annotation as the type's source.
- **Per program.** Globals/Components are redeclared per program; an annotation applies only where written.
- **Inheritance.** A subclass method inherits the base declaration's annotations unless its own header declaration has its own.
- **App Designer's `/+ … +/` annotations** on method implementations are not used (App Designer regenerates them from the header).

**Verified:** App Designer preserves inline `/* as T */` comments in all the placements above (inside parameter lists, after `Returns T`, between a type and a variable name) when it reformats on save, as well as comment blocks above declarations.

## 8. Usage narrowing

When nothing else gives a type, the members used on the variable do. Goal: one distinguishing call (e.g. `&x.GetCompIntfc(...)`) is enough for autosuggest to work on every later use.

### What counts as a use

- **Counted:** direct member accesses on the variable: property reads/writes (`&x.Name`) and method calls (`&x.Name(...)`).
- **Not counted:** members further along a chain (`&x.GetCompIntfc(...).Save()` counts only `GetCompIntfc`).
- **Scope:** method/function body for locals and parameters; whole program for instance, Global and Component variables.
- **Order:** source order within the scope.

### Algorithm

1. Start with every API subtype. typeinfo.dat can't be enumerated (it's a hash table keyed by name hash; names aren't stored), so build the set from `PeopleCodeType` enum values filtered by `IsApiObjectSubtype(GetTypeName())`, computed once and cached. An API object in the dat without an enum value is unreachable; that's accepted.
2. For each use in source order, keep only candidates that have that member. Record line, member and remaining candidates as a history entry.
3. **One candidate left:** that's the type. Later uses are checked against it but can't change it.
4. **A use would leave none:** report the conflict diagnostic on that use, ignore it, keep the previous set, continue.
5. **All uses checked, several remain:** stays `ApiObject`; remaining candidates kept for hover/autosuggest.

Why stop at the first single candidate rather than intersect all uses: same result for consistent code, but code is usually mid-edit during autosuggest. A half-typed `&x.Ge` would empty a full intersection and drop the type exactly when needed. Fixing early keeps it stable and gives contradicting uses a precise error on their own line.

### Component interface rule

- CI properties are dynamic (the CI's keys/fields, e.g. `&ci.EMPLID`), not in typeinfo.dat, so a **property** not found on `ComponentInterface` doesn't remove it from candidates.
- **Methods** (`Get`, `Find`, `Create`, `Save`, `Cancel`, `GetPropertyByName`, …) narrow normally.
- An unknown property alone isn't enough to settle on `ComponentInterface`.
- `InvalidMemberAccessCheck` follows the same rule (phase 0b).

### Where it runs

- A pre-pass in `TypeInferenceVisitor.Run`, before `program.Accept(visitor)`.
- For variables with no `/* as T */` annotation anywhere in their scope.
- Seeds the narrowed type as the refined type, source = narrowing. Main-pass assignments replace it.
- Narrowing conflicts replace the generic invalid-member error, so a use is never reported twice. When the type came from an annotation or assignment, the normal error applies.

## 9. Cross-program return types

Covers `Local ApiObject &a = ThatFunction();` where `ThatFunction` is a declared external function, or another class's method, returning `ApiObject`. If the real return type is known, existing assignment refinement handles the rest.

### Mechanism

1. **Where:** after `TypeMetadataBuilder.ExtractMetadata` in `DatabaseTypeMetadataResolver.ParseAndExtractMetadata`, run a refiner on the already-parsed AST. Put it in a shared helper in `PeopleCodeParser.SelfHosted` so tests and other resolvers can use it.
2. **Opt-in:** only if the program declares an `ApiObject`, `Record` or `Field` return type, property or instance variable.
3. **Annotation first:** `Returns ApiObject /* as T */` decides; inference skipped.
4. **Inference:** otherwise run `TypeInferenceVisitor` on the program (including its annotations and narrowing) and collect the inferred types of every `Return` expression in the method/function (and property `get` blocks).
5. **Result:** if all returns agree on one API subtype, write it to `FunctionInfo.RefinedReturnType` as `TypeWithDimensionality(PeopleCodeType.X, 0)` with identity if any. Declared `ReturnType` unchanged.
6. **Properties:** the same pass refines `ApiObject` properties and instance variables, so `&obj.SomeApiProp` resolves elsewhere. **Builder's call:** this covers assignments in any method of the class, not just the constructor, with the same rule as returns: if all assignments agree on one subtype, use it; if they disagree, it stays `ApiObject`.

### Named instance types: Record, Field, later CI

- **Record:** `Returns Record` whose returns all give the same `RecordTypeInfo` (e.g. `Return CreateRecord(Record.JOB);`) stores that record name.
- **Field:** same for `Returns Field` / `FieldTypeInfo` (record + field).
- **CI:** once CI identity lands, same for `ComponentInterface` with a CI name.
- **Storage:** `FunctionInfo.RefinedReturnType`, a `TypeWithDimensionality` with optional `TypeIdentity`; `ConvertTypeWithDimensionalityToTypeInfo` turns it into `RecordTypeInfo`/`FieldTypeInfo`/CI-with-identity. Callers' `TryRefineVariableFromExpression` works unchanged.
- **Same-program calls:** refined during the edited program's single inference pass, in source order, so a local `GetJobRec()` returns `record(JOB)`. No extra pass.
  - **Functions** must be defined above their use in PeopleCode, so by the time a call is visited, the function's body has been visited and its refined return type is known. When the visitor finishes a `Function` body, it writes the refined return into that function's `FunctionInfo.RefinedReturnType` in the program's metadata, and later calls read it.
  - **Methods**: the header declaration always comes first, but implementation bodies can be in any order. A call to a method whose body has already been visited uses its refined return. **Builder's call:** a call to a method whose body comes later uses the declared type (or the header's `/* as T */` annotation). Users can annotate the header if they need it. A deferred fix-up pass is not worth a second inference run.
- **Disagreeing Record/Field returns:** stay plain `Record`/`Field`, no warning. Compare identities explicitly (`GetCommonType` would keep the first name).

### Rules and guards

- **Cycles:** a set of programs being refined plus a depth limit (e.g. 3). On hit, return `ApiObject`, never an error.
- **Caching:** refined metadata goes into the existing `TypeCache`; each program refined at most once per cache generation. **Builder's call:** metadata whose refinement was cut short by the cycle set or depth limit is not cached as final, so it doesn't stay wrong until eviction.
- **Pass-through returns:** `Return &param;` uses the parameter's resolved type (annotation or narrowing) in that program. "Returns the type of argument N" is left for later.
- **Disagreeing API returns:** stay `ApiObject`; the source program gets a warning on the method.
- **Inheritance:** the type comes from the class whose method was actually resolved. Abstract/interface methods rely on a `Returns ApiObject /* as T */` header annotation.
- **Threading (decided):** refinement runs inside `GetTypeMetadata` on whatever thread calls it, including the existing UI-thread callers (tooltips, autocomplete, `UnimplementedAbstractMembersStyler`). This is accepted: those paths already fetch and parse other programs synchronously today. The cost is bounded by the depth limit and paid once per program per cache generation. There's no per-caller opt-in and no separate unrefined cache.

## 10. Diagnostics

Reported as node type warnings/errors (`SetTypeWarning` / `SetTypeError`), the same storage the type checker's `RecordTypeWarning`/`RecordTypeError` use. **Builder's call:** diagnostics detected during inference or the narrowing pre-pass (narrowing conflicts, annotation parse/misfit, assignment-annotation conflicts) are stamped by inference itself; the assignment-time ones (one-type, annotation conflict) come from the checker. Collection stays as today: `CompileChecker` gathers type warnings only when a resolver is present, so there's no change to offline behaviour. "Replaces the generic invalid-member error" is done with a node marker that `InvalidMemberAccessCheck` reads and skips. Wording is a draft; each message must include the inferred type or candidates, the use or annotation that produced it, and the line.

| Situation | Severity | Message (template) |
|-----------|----------|--------------------|
| Narrowing conflict, several candidates left | Warning | 'Foo' is not a member of Session or Tree. Earlier uses of &x (GetQuery on line 12, Count on line 15) suggested one of those types. |
| Narrowing conflict, one candidate left | Warning | &x was inferred as Session from GetCompIntfc() on line 12, but Session has no member 'Foo'. |
| First use matches no API type | Warning | 'Foo' is not a member of any API object type. |
| Many candidates (> 3) | — | … one of 6 types (Session, Tree, Query, …) … |
| Two assignments of different subtypes | Warning | &x is assigned more than one API type (Session on line 4, ComponentInterface on line 9). Use a separate variable for each type. |
| Uses suggest one type, assignment gives another | Warning | Uses of &x suggest Session (GetCompIntfc on line 12), but it is assigned ComponentInterface on line 20. |
| Assignment conflicts with a declaration annotation | Warning | &x is annotated as Session (line 3) but is assigned ComponentInterface here. |
| Assignment annotation conflicts with the value | Warning | The value is Session, but it is annotated as Tree. Using Tree. |
| Member not on an annotated or assigned type | Error | Session has no member 'Foo' (&x is Session, annotated on line 3). |
| Unknown type name in an annotation | Warning | Unknown type 'CompIntfc' in annotation. Did you mean ComponentInterface or CompIntfc.NAME? |
| Annotation doesn't fit the declared type | Warning | 'as Session' needs an ApiObject declaration; &rs is Rowset. |
| Argument is a different subtype than an annotated parameter | Warning | Foo expects &s as Session (annotated); the argument is Tree. |
| Method returns different API subtypes | Warning | GetThing returns more than one API type (Session on line 30, Tree on line 34). |

## 11. Editor experience

- **Hover shows type and source:** `&x: Session (inferred from GetCompIntfc() on line 12)`, `&x: Session (annotated on line 3)`, undecided: `&x: ApiObject (Session or Tree)`.
- **Autosuggest:** once decided, the concrete type's members.
- **Optional, later:** while undecided, combined members of remaining candidates, labelled by type; picking a unique one settles the type.
- **Optional, later:** quick fix "Add `/* as Session */`" on a narrowed variable, written after the declaration's type.

## 12. Implementation phases

Each phase ships on its own with its own tests.

### Phase 0 — limit the Record/Field naming heuristic to built-ins
- `TypeInferenceVisitor.SpecializeNamedInstanceReturn`: apply argument, receiver and default-record rules only when the `FunctionInfo` came from typeinfo.dat.
- `FunctionInfo` doesn't record its origin today: add an `IsBuiltin` flag set by the typeinfo.dat reader, or pass the fact from `ResolveFunctionInfo` (decide in this phase).
- User functions/methods then return plain `Record`/`Field` until phase 4.
- Independent; can ship first.

### Phase 0b — stop false errors on CI properties
- `InvalidMemberAccessCheck`: skip property access (not method calls) on `ComponentInterface`, alongside Record/Row/Grid.
- Once CI identity lands, a named CI is checked against its definition.
- Independent.

### Phase 1 — declared vs. refined split and the one-type warning
- `VariableInfo`: `Type` stays declared (auto-declared path keeps `UpdateType`); `InferredType` refined, plus `RefinementSource`.
- `TryRefineVariableFromExpression`: compare against declared type; latest wins.
- `VisitIdentifier`: stamp refinement source on the identifier node.
- `TypeCheckerVisitor.VisitAssignment`: declared type from the checker's registry; any API subtype passes for `ApiObject` targets; one-type warning (`ApiObject`-declared only) using the target's stamped type and source.

### Phase 1b — TypeIdentity
- Add `TypeIdentity` and `TypeInfo.Identity`; copy it in every base `With*`.
- Not needed by phase 1; lands before phases 2 and 4.

### Phase 2 — `/* as T */` annotations
- Annotation finder over `ProgramNode`'s comment list by position (after a declaration's type node; after an assignment's value, either side of `;`; same line). Parses `as T` into type + `TypeIdentity`.
- `TypeInferenceVisitor`: declaration annotations become the variable's annotated type for its scope; assignment annotations replace the value's type when refining. Annotated variables are excluded from narrowing.
- `TypeMetadataBuilder`: store header and `Function`-line annotations as refined types beside the declared parameter/return types in `FunctionInfo`, and on `PropertyInfo`.
- Call sites: warn when an argument is a different API subtype than an annotated parameter; plain `ApiObject` arguments pass silently.
- Annotation diagnostics.

### Phase 3 — usage narrowing
- Narrowing analyzer: direct member uses per variable and scope, candidate history, CI rule.
- Pre-pass in `TypeInferenceVisitor.Run` seeding narrowed types.
- Conflict diagnostics; suppress `InvalidMemberAccessCheck` duplicates for narrowed variables.
- Show the type's source in hover.

### Phase 4 — cross-program return types
- Return-type refiner in `PeopleCodeParser.SelfHosted`: opt-in check, collect return types, refine constructor-set properties.
- Hook into `DatabaseTypeMetadataResolver.ParseAndExtractMetadata`; cycle set and depth limit; declared and refined return types side by side in `FunctionInfo`.
- Named `Record`/`Field` returns as well as `ApiObject`: optional `Identity` on `TypeWithDimensionality` and `PropertyInfo`, `FunctionInfo.RefinedReturnType`, identity attached in `ConvertTypeWithDimensionalityToTypeInfo`.
- Run the refiner on the edited program's own functions/methods too.

### Phase 5 (optional)
- Autosuggest with combined members of remaining candidates.
- "Add `/* as Session */`" quick fix.
- Inferring from callers within the same program (private methods).

## 13. Testing

Tests go in `PeopleCodeParser.SelfHosted.Tests` with an in-memory `ITypeMetadataResolver` — no database.

- **Reassignment:** reassigning to another subtype gives the one-type warning, not a type error; later uses get the latest type; `any` variables get no warning; auto-declared variables still typed on first assignment only.
- **Phase 0b:** `&ci.EMPLID` on `ComponentInterface` gives no error; an unknown CI *method* still does.
- **Identity:** survives storage in a variable and return from a call; survives `WithAssignable`/`WithAutoDeclared`/`WithBufferField`; doesn't change `Equals`.
- **Annotation placement:** after the type on Local/Global/Component/instance declarations, properties, multi-variable declarations, method header parameters and returns, `Function` parameters and returns; after an assignment's value, before and after `;`; after the value of a `Local` with an initial value.
- **Ignored placements:** type position in a `Local` with an initial value; function arguments; operands; `Return` expressions; after a variable name; above a statement; next line; property and array targets. No effect, no warning.
- **Comment matching:** only whole-body `as T`; prose with "as" and commented-out code ignored; `/+ +/`, `<* *>`, `REM` ignored; case-insensitive; non-canonical names give the "did you mean" warning; `Record.REC.FIELD`, `Field.FIELD`, `CompIntfc.X` give the right identities.
- **Annotated parameters at call sites:** plain `ApiObject` argument passes with no diagnostic; matching subtype passes; different subtype warns; same-program and cross-program.
- **Same-program returns:** a function called after its definition gets the refined return; a method called from a body visited after its own body gets it; a method called before its body is visited gets the declared/annotated type.
- **Declaration annotation behaviour:** holds for the scope; less detail doesn't erase it; more detail added; conflicting assignment warns, annotation stays.
- **Assignment annotation behaviour:** overrides plain `ComponentInterface` with `CompIntfc.FOO`; conflicting value warns, annotation wins; latest-wins afterwards.
- **Narrowing:** one distinguishing call settles the type; uses before the deciding one also get it; chains don't count.
- **Narrowing conflicts:** several candidates, one candidate, first use matching nothing, continuing after a conflict.
- **CI rule:** dynamic properties don't remove `ComponentInterface`; CI methods narrow to it.
- **Precedence:** declaration annotation over assignment (warning); any annotation disables narrowing; assignment over narrowing (replaced, warning if different).
- **Cross-program:** external function returning one subtype; disagreeing returns; pass-through `Return &param`; cycle A→B→A; depth limit; cache reuse; annotation overriding inference.
- **No duplicates:** exactly one diagnostic per bad use.
- **Phase 0:** built-ins (`CreateRecord(Record.X)`, `GetRecord()` in record PeopleCode, `&rec.GetField(Field.X)`, `GetField("OPRID")`) still get names; zero-arg user function, `CopyTo(Record.FOO)`, `MyFunc("hello")` no longer do.
- **Named returns:** a user function returning one record gets that name, same-program and cross-program; disagreeing returns stay plain `Record`; same for fields.

## 14. Open items

| Item | Status |
|------|--------|
| App Designer keeps comment blocks above declarations on save | Confirmed: yes |
| App Designer keeps inline comments inside parameter lists, after `Returns T`, between type and variable name | Confirmed: yes |
| `InvalidMemberAccessCheck` on dynamic CI properties | Answered: false errors; fix in phase 0b |
| Annotation syntax and placement | Decided: `/* as T */` after a type or an assignment's value |
| Syntax for naming a specific CI | Decided: `CompIntfc.CI_NAME` |
| "Has no effect here" warning | Decided: no (cheap to add later) |
| Carrying names across programs | Decided: `TypeIdentity` on `TypeWithDimensionality`/`PropertyInfo` |
| Subclass per kind vs. generic attributes | Decided: flat `TypeIdentity` |
| Where refinement source lives | Decided: `VariableInfo`, stamped on uses |
| Type after a second, different assignment | Decided: latest |
| `any` variables get the one-type warning | Decided: no |
| `VariableInfo.Type` callers depending on the overwrite | Checked: private to `TypeInferenceVisitor` |
| Backing `RecordTypeInfo`/`FieldTypeInfo` with `TypeIdentity` | Deferred |
| Scope-aware typing | Deferred (§15) |
| Built-in vs. user `FunctionInfo` (flag vs. pass from `ResolveFunctionInfo`) | Decide in phase 0 |
| Declaration-level warning when narrowing never reaches one type | Decide later |
| Cross-program depth limit value | Tune in phase 4 |
| Annotated parameters at call sites | Decided: declared stays `ApiObject`; plain `ApiObject` passes silently; different subtype warns |
| Same-program return refinement | Decided: single pass in source order; forward method calls use declared/annotated type |
| Refinement on UI-thread callers | Decided: accepted, bounded by depth limit and cache |
| Inferring from same-program callers | Deferred |
| Inferring parameter types from other programs' callers | Out of scope |

## 15. Deferred: scope-aware typing

Not part of this work; also weak spot #6 in `docs/architecture/type-system-weak-spots.md` ("Variable typing is flow-insensitive").

Goal: use the latest refinement in the current or an enclosing block, not the latest in traversal order.

```
Local Record &r;
If &lang = "en" Then
   &r = GetRecord(Record.FOO);       /* FOO here */
Else
   &r = GetRecord(Record.FOO_LANG);  /* FOO_LANG here */
End-If;
/* plain Record here */
```

| Where | Latest (phase 1) | First | Scope-aware |
|-------|------------------|-------|-------------|
| Inside `If` | FOO ✓ | FOO ✓ | FOO |
| Inside `Else` | FOO_LANG ✓ | FOO ✗ | FOO_LANG |
| After `End-If` | FOO_LANG ✗ | FOO ✗ | plain Record |

Phase 1 doesn't block it: each use is stamped with the type and source at visit time, so scope-aware typing only changes how `VariableInfo`'s refined type + source are saved/restored around blocks. Sketch: save/restore per `If`/`Else`/`Evaluate` branch and merge by explicit identity comparison; loops drop refinements assigned inside on entry (cheap) or iterate to a fixed point (proper); branches ending in `Return`/`Exit`/`Break`/`Throw` don't join. An `ApiObject` assigned different subtypes in two branches still gets the one-type warning — it's a style rule. `Record` variables never get it.

## 16. Next: component interface identity

The phase after this design records *which* CI a variable holds, enabling checks and suggestions for `&ci.EMPLID`. Phases 0–4 must not make it harder:

- **Representation:** plain `BuiltinObjectTypeInfo("ComponentInterface")` with/without `TypeIdentity.CompIntfcName`. No subclass.
- **One-type rule:** plain → named CI is added detail (`IsRefinementOf`), no warning; two different names warn (`ConflictsWith`).
- **Name source:** `&sess.GetCompIntfc(CompIntfc.USER_PROFILE)` fits the `SpecializeNamedInstanceReturn` pattern.
- **Annotation syntax:** `/* as CompIntfc.USER_PROFILE */`; phase 2 already parses and stores it.
- **Narrowing:** the CI rule stays; once named, properties are checked against the CI definition.
- **Cross-program:** carried via `Identity` on `TypeWithDimensionality`/`PropertyInfo` (phase 4).
- **Member checks:** named CI → checked against its definition; unnamed → unchecked.
- **Database access:** CI property definitions come from the database; check `HasDatabaseConnection()` and fall back to plain `ComponentInterface` without one.
