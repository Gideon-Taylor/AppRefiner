# Component Interface Typing

Date: 2026-10-26. Follows `2026-10-25-apiobject-type-resolution-design.md` (phases 0–4, plus the
DataItem and ignored-return fixes). That design's §16 sketched this phase.

## Intent

AppRefiner treats every component interface (CI) as an opaque `ComponentInterface`. It can't say
what `&ci.UserID` is, what's inside `&ci.Roles`, or which custom methods a CI has. So it checks
nothing on a CI (any member is accepted) and suggests only the five standard methods.

This phase records which CI a value holds, reads that CI's definition from the database, and
uses the definition the way record definitions are already used:
- autosuggest for a CI's properties, collections and methods, and for those of any collection
  item at any depth;
- type checking and errors for members that don't exist;
- signatures and go-to-definition for the CI's custom methods.

Tim asked for the whole feature to ship together, not in slices.

## Goals

1. **Tag the CI name.** `%Session.GetCompIntfc(CompIntfc.USER_PROFILE)` (and the same call on any
   Session value) produces a ComponentInterface whose identity carries `USER_PROFILE`. This works
   the way `CreateRecord(Record.PSOPRDEFN)` produces a named record. The existing
   `/* as CompIntfc.X */` annotation keeps working. The name then flows through assignments,
   returns, parameters and cross-program refinement as identities already do.
1a. **Annotate collections and items (Tim).** Annotations can name a collection path, so an item
   or collection passed into a helper function stays typed. Syntax (my call):
   - `/* as CompIntfc.USER_PROFILE.Roles */` is an item of `Roles` (a DataItem). Items are what
     usually cross into helpers, so they get the short form.
   - `/* as CompIntfc.USER_PROFILE.Roles[] */` is the `Roles` collection itself (a
     DataCollection), read as "the collection of Roles items".
   - Deeper paths chain: `CompIntfc.USER_PROFILE.Roles.RouteControls`.

   They work everywhere `/* as T */` works today (locals, parameters, properties, returns,
   assignments), with the same rules:
   - an unknown CI or path is a warning, like an unknown type today;
   - a value that conflicts with the annotation warns and the annotation wins;
   - more detail is a refinement.
2. **Read the definition with one call.** Each data provider gets a single
   `GetComponentInterface(name)` that returns the whole definition as a structured object (see
   "The definition"). It's built for all three providers, SQL proxy first, then Oracle and
   SQL Server.
3. **Type collections and items.** `&ci.Roles` is a DataCollection that knows it is USER_PROFILE's
   `Roles` collection. Its items (`Item`, `ItemByKeys`, `CurrentItem`, `GetEffectiveItem`,
   `InsertItem`) are DataItems that know the same, so `&role.RoleName`, `&role.RouteControls` and
   their items work at any depth.
4. **Type properties.** A property's type is its record field's type: `&ci.UserID` is a string,
   and a date field is a date.
5. **Autosuggest.**
   - After a named CI: its properties and keys, its collections, the standard methods it
     enables, the standard methods every CI has, and its custom methods.
   - After an item of a known collection: that collection's properties and child collections,
     plus DataItem's own members (`ItemNum`).
6. **Hover.** A named CI shows as `ApiObject (CompIntfc.USER_PROFILE)`, and an item shows its CI
   and collection path. Hovering a property shows its type, its record and field, whether it's
   read-only, and its comment (the translate values, such as "Y for Yes, N for No").
7. **Custom methods.**
   - Signatures come from parsing the CI's PeopleCode, for type checking and the
     method-parameters tooltip.
   - Go-to-definition on a custom method call opens the CI's PeopleCode at that function.
8. **Errors**, all reported as errors (Tim's call), and only when the definition is known:
   - **Unknown member:** a property, collection or method that a named CI, or an item of a known
     collection, doesn't have.
   - **Read-only property:** an assignment to a read-only property (`BCACCESS = 2`).
   - **Disabled standard method:** a call to a standard method the CI doesn't enable (see
     "Standard methods").
   - **Unknown CI:** `CompIntfc.FOO` when no CI named FOO exists.
9. **Parser.** Function headers accept `NoExport`, and the function node records it.

## Non-goals

- Reading the component or its pages. The CI definition alone is enough (Tim).
- Typing CIs whose name isn't a `CompIntfc.X` reference or an annotation. For example,
  `GetCompIntfc(&name)` or `GetCompIntfc("USER_PROFILE")` stays an unnamed ComponentInterface.
- Typing what `Find()` returns beyond a plain DataCollection. Find results aren't typed in this
  phase.
- Key markers on properties. The CI viewer shows key icons that aren't stored in PSBCITEM, and
  typing doesn't need them.
- Editing or creating CIs.

## Anti-goals

- **No new false errors when the definition is missing.** Without a database connection, or
  without a CI name, behavior stays exactly as it is today: CI members are unchecked and `any`.
  Unchecked must never turn into reported.
- **No unknown-CI error unless the CI is known to be missing.** A failed or unavailable lookup
  is not the same as "doesn't exist".
- **No noticeably slower tooltips or autosuggest.** Definitions are cached, and each
  CI is fetched in one round trip.

## Constraints

- **Project conventions** (CLAUDE.md):
  - Database-dependent components declare `DataManagerRequirement` and check
    `HasDatabaseConnection()`.
  - Logging uses AppRefiner's `Debug.Log()`.
  - Dialogs never use `MessageBox.Show`.
- **SQL proxy queries** are Meta-SQL constants in `SqlProxyQueries/SqlProxyQueryCatalog.cs`, with
  positional binds, and each needs its `MetaSQL/conversions/meta/<Method>__<variant>.sql.md` doc
  file. Oracle uses named `:x` parameters. SQL Server is ODBC with positional `?` parameters only,
  never `@named` (see memory `sqlserver-odbc-positional-params-only`).
- **The type system can't see AppRefiner's models.** `ITypeMetadataResolver` lives in
  PeopleCodeTypeInfo and can't reference AppRefiner's database models, so it gets its own contract
  type for the definition, mapped from the data provider's model.
- **Demos** use delivered metadata only: `CompIntfc.USER_PROFILE` and `Record.PSOPRDEFN`.
- **Existing behavior for unnamed CIs stays:**
  - methods aren't checked, because a CI can have custom methods;
  - narrowing's dynamic-property rule for ComponentInterface and DataItem stays;
  - an ignored return value on any API object method is allowed.

## Where the definition lives (from Tim)

### PSBCDEFN

One row per CI, with `BCNAME` = the CI name. `BCSTDMETHODS` is a bit mask of the enabled standard
methods:

| Method | Bit |
|---|---|
| Cancel | 1 |
| Create | 2 |
| Find | 4 |
| Get | 8 |
| Save | 16 |

### PSBCITEM

All the tree data, one row per item, keyed by `BCNAME`.

| Column | Meaning |
|---|---|
| `BCTYPE` | 1 GETKEYS entry, 2 CREATEKEYS entry, 3 collection, 4 property, 5 FINDKEYS entry |
| `BCITEMNAME` | The item's name (`UserID`, `Roles`) |
| `BCITEMPARENT` | `PS_ROOT` for the CI itself, otherwise the parent collection's `BCITEMNAME` |
| `SEQUENCE_NBR_6` | Display order |
| `BCACCESS` | 1 read/write, 2 read-only |
| `RECNAME` / `FIELDNAME` | A property's record and field. For a collection, its scroll's main record (`FIELDNAME` blank) |
| `COMMENTSHORT` | Comment, typically translate values (`Y for Yes, N for No`); blank is a single space |
| `BCSCROLL`, `BCSCROLLNUM`, `BCSCROLLNAME` | Scroll level, occurs number, and scroll path (`00-00-01-05-02-00`). Not needed for typing |
| `SUBRECNAME` | Set when the field comes from a subrecord. Not needed for typing |

Notes:
- A key appears both as a key row and as a property row of the same name (`UserID` appears three
  times in USER_PROFILE). A key that has no property row is still accessible as a property of the
  CI.
- Parents are referenced by name. The design assumes collection names are unique within a CI. If
  they aren't, `BCSCROLLNAME` tells same-named collections apart, and the builder should use it.
- A property's type comes from joining `FIELDNAME` to PSDBFIELD (`FIELDTYPE`), in the same query.

Reference: USER_PROFILE's rows, as exported by Tim, include:
- properties at the root: `UserID`, `UserIDAlias`, `UserDescription`, …;
- the collections `EmailAddresses` (PSUSEREMAIL) and `IDTypes` (PSOPRALIAS), with
  `IDTypes` → `Attributes` (PSOPRALIASFIELD);
- `Roles` (PSROLEUSER_VW) with a read-only `Dynamic` property and the child collection
  `RouteControls` (RTE_CNTL_USERVW) → `RouteControlProfile`.

### Custom methods

These exist only as functions in the CI's PeopleCode program: PSPCMPROG with `OBJECTID1 = 74`,
`OBJECTVALUE1 = <CI name>`, `OBJECTID2 = 12` (METHOD), `OBJECTVALUE2 = 'Methods'`. Every function
in that program is a method, except one declared `NoExport`:

```
Function Foo() NoExport
End-Function;
```

Function header grammar (PeopleTools):
`Id [ '(' InternalParameters ')' ] [ 'returns' PeopleCodeType ] [ 'noexport' ] [ 'doc' String ] TopStList 'end-function'`.
The parser handles `Doc` today but not `NoExport`.

## The definition

`GetComponentInterface(name)` returns null when the CI doesn't exist. Its shape:

- **Name**, and the **enabled standard methods** from `BCSTDMETHODS`.
- **GetKeys, CreateKeys, FindKeys**: lists of properties.
- **Properties**, each with:
  - name, record, field and field type;
  - read-only flag;
  - comment.
- **Collections**, each with:
  - name and record;
  - its properties;
  - its child collections, recursively.
- **Custom methods**: each method's name and signature (parameters with types, return type),
  excluding `NoExport` functions, plus what go-to-definition needs to open the CI's PeopleCode at
  that function.

How the provider and resolver divide the work is my call: the provider returns the structural
rows plus the Methods program's source text, and the resolver (which can parse) builds the
signatures. It parses with the same parser and metadata extraction used for function libraries,
so signatures and `/* as T */` annotations behave the same.

### Caching

- Definitions are cached per CI name and per connection, including the answer "doesn't exist".
- The cache is cleared on connect and disconnect, like the app class catalog.

**Purging a changed CI (Tim).** A CI definition has no editor that AppRefiner hooks, so a save
can't be seen the way a PeopleCode save is. Two mechanisms:

1. **"Refresh CI cache" command (required).** It re-fetches every CI currently in the cache and
   updates the entries; one no longer found becomes "doesn't exist". It also evicts the
   type-side entries refined from those CIs, so the open program's checks, hovers and
   autosuggest pick up the change. It's a command-palette command declaring
   `DataManagerRequirement`, and it does nothing without a connection.
2. **Automatic version check.** PSBCDEFN has a VERSION column (Tim). A lookup compares it to the
   cached entry's version first and re-fetches on a change. That's one cheap query, the same
   way `PSRECDEFN.VERSION` keys record fields today. An edit to the CI's Methods PeopleCode
   changes PSPCMPROG, which may not bump PSBCDEFN.VERSION. If PSPCMPROG has a reliable marker
   (its last-update timestamp), the check covers the Methods program too; otherwise method
   edits rely on the refresh command.
3. **Save hook for the CI's PeopleCode (Tim).** AppRefiner already updates its function cache
   when a PeopleCode editor is saved. When the saved editor is a CI's PeopleCode
   (`(Component Interface PeopleCode)`), the same hook evicts that CI's entry and the type
   entries refined from it.

## Standard methods

| Methods | Rule |
|---|---|
| Cancel, Create, Find, Get, Save | Exist only if `BCSTDMETHODS` enables them. A call to a disabled one is an error, and they aren't suggested |
| GetPropertyByName, SetPropertyByName, CopyRowset, CopyRowsetDelta, CopySetupRowset, CopySetupRowsetDelta | Available on every CI. They aren't in the bit mask, and typeinfo.dat declares them |
| Custom methods | Come from the Methods program |

Standard methods keep their typeinfo.dat signatures.

## Type behavior

### Identity

A named CI is `BuiltinObjectTypeInfo("ComponentInterface")` with `TypeIdentity.CompIntfcName`, as
spec §16 set out (no subclass). Collections and items need more: `TypeIdentity` gains a collection
path (my call: one field holding the path from the CI root, such as `Roles/RouteControls`), set on
DataCollection and DataItem values. `IsRefinementOf` and `ConflictsWith` cover the new field:
- more detail is a refinement;
- two different paths, or two different CI names, conflict. The existing one-type warning applies
  to conflicting CI names.

### Where types come from

| Expression | Type |
|---|---|
| `GetCompIntfc(CompIntfc.X)` | ComponentInterface tagged X. This hooks in where `CreateRecord(Record.X)` is specialized (`SpecializeNamedInstanceReturn`) |
| A property on a named CI or a known item | Its field's type (string, number, date, …) |
| A collection on a named CI or a known item | DataCollection with the CI name and path |
| `Item`, `ItemByKeys`, `CurrentItem`, `GetEffectiveItem`, `InsertItem` on a known collection | DataItem with the same CI name and path |
| A custom method call | Its declared return type, after annotation and refinement |

### Fallback

Without a definition (no database connection, an unnamed CI, a lookup failure), every
expression above keeps today's behavior.

### Assignments to properties

A value assigned to a property is checked against the property's field type, the same way
assignments to other typed properties are checked.

## Checks

All of these are errors and apply only when the definition is known.

| Situation | Example message |
|---|---|
| Unknown property or collection on a named CI or a known item | `'Bogus' is not a property of CompIntfc.USER_PROFILE` (or of `USER_PROFILE.Roles`) |
| Unknown method on a named CI | `'Bogus' is not a method of CompIntfc.USER_PROFILE` |
| Assignment to a read-only property | `'WorklistEntriesCount' is read-only on CompIntfc.USER_PROFILE` |
| Call to a disabled standard method | `CompIntfc.X doesn't enable Find()` |
| `CompIntfc.FOO` where FOO doesn't exist (any use, not only in `GetCompIntfc`) | `Component interface 'FOO' doesn't exist` |

The exact wording is left to the builder, in the style of the existing messages. The checks
follow the existing pattern: `InvalidMemberAccessCheck` with resolver access, and
`UnknownRecordFieldCheck` as the template for definition-backed checks. Where an error names the
CI, it says where the type came from, as current messages already do.

Narrowing settles the API type but never a CI name, so a CI typed only by narrowing stays unnamed
and unchecked.

## Editor features

- **Autosuggest**: added next to the existing record-field block in
  `AutoCompleteService.ShowObjectMembers`, in the same format, on top of the builtin members
  already listed. Hidden members (disabled standard methods) are left out.
- **Hover**:
  - `TypeDisplay` shows the CI name (already supported) and item paths.
  - A member hover on a CI property shows type, record.field, read-only and comment.
  - The declared-name hover (just added) works as for any variable.
- **Method parameters tooltip**: a custom method call shows its signature, as for app class
  methods.
- **Go-to-definition**: a custom method call opens the CI's Methods PeopleCode at the function
  (the OpenTarget is 74 = CI, METHOD = "Methods").

## Also in scope

- **Parser**:
  - `NoExport` is accepted in a function header after `Returns` and before `Doc`, only in that
    position, so identifiers named NoExport keep working.
  - The function node gets a flag for it.
  - Function libraries accept it too.
- **Opening CI PeopleCode is broken today (Tim confirmed)**, and go-to-definition depends on it.
  - `IDataManager.cs:537` maps `ComponentInterfacePeopleCode` to object ID 75
    (`COMPINTFCINTERFACE`), so the open string is `COMPINTFCINTERFACE.<CI>.METHOD.Methods`.
    Everything else uses 74 (`COMPONENTINTERFACE`).
  - The fix starts with finding the open string App Designer accepts for CI PeopleCode, by
    trying it in App Designer with Tim. The likely answer is `COMPONENTINTERFACE`, matching the
    PSPCMPROG keys, but it isn't verified.
  - Opening from the open dialog and from go-to-definition must both work.
- **Demos**: a new demo set with USER_PROFILE that shows:
  - typed properties;
  - nested collections down to `RouteControls`;
  - each error. USER_PROFILE enables all its standard methods, so for the disabled-method error
    the README tells the tester to turn one off in the CI's definition (Tim will toggle the
    flags himself during validation);
  - a custom method's signature and go-to-definition (USER_PROFILE has custom methods such as
    `SetPassword`).

  The README lists expected results and hovers, as earlier demos do.
- **Weak-spots doc**: update `docs/architecture/type-system-weak-spots.md`: the "A CI's own members
  are unchecked and untyped" limit becomes "only unnamed CIs".

## Left to the builder

- The exact SQL, and whether PSBCITEM and PSDBFIELD are read in one query or two per CI.
- Contract type names, and where the definition-to-type mapping lives.
- Exact error wording and diagnostic codes.
- Autosuggest icons and ordering.
- How the collection path is stored on `TypeIdentity` (a string field or a list), within the
  equality and refinement rules above.
- Test fixtures: a fake data provider or fake resolver returning a USER_PROFILE-shaped definition.
