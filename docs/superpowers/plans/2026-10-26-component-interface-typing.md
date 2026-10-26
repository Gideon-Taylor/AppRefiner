# Component Interface Typing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tag component interface values with their CI name (and collection path), read the CI's definition from the database through one data-provider call, and use it for typing, errors, autosuggest, hovers, signatures and go-to-definition.

**Architecture:**
- **Data.** The definition is a contract type in PeopleCodeTypeInfo. A pure builder in PeopleCodeParser.SelfHosted turns PSBCITEM rows plus the CI's "Methods" PeopleCode into that definition, so it's testable. The three data managers only run SQL and call the builder.
- **Lookups.** `ITypeMetadataResolver` gains a cached, three-state CI lookup (found / missing / unknown).
- **Typing.** Inference attaches `TypeIdentity.CompIntfcName` plus a new `CompIntfcPath`, and the checks, editor features and cache purge read the definition through the resolver.

**Tech Stack:** C# / .NET 8, xUnit, PeopleCode self-hosted parser, SQL proxy Meta-SQL, Oracle and SQL Server (ODBC) data managers.

**Spec:** `docs/superpowers/specs/2026-10-26-component-interface-typing-design.md`

## Global Constraints

- **Errors** (all severity Error, and only when the definition is known):
  - unknown member on a named CI or a known item;
  - assignment to a read-only property (`BCACCESS = 2`);
  - call to a disabled standard method;
  - `CompIntfc.FOO` for a CI that doesn't exist.
- **No new diagnostics without a definition.** No database, no CI name, or a failed lookup means today's behavior exactly: CI members are unchecked and `any`. A failed lookup is never "doesn't exist".
- **`BCSTDMETHODS` bits:** Cancel = 1, Create = 2, Find = 4, Get = 8, Save = 16. `GetPropertyByName`, `SetPropertyByName`, `CopyRowset`, `CopyRowsetDelta`, `CopySetupRowset` and `CopySetupRowsetDelta` are on every CI.
- **`PSBCITEM.BCTYPE`:** 1 GETKEYS, 2 CREATEKEYS, 3 collection, 4 property, 5 FINDKEYS. `BCITEMPARENT = 'PS_ROOT'` is the CI root. `BCACCESS` is 1 read/write, 2 read-only.
- **The CI's PeopleCode** is PSPCMPROG with `OBJECTID1 = 74` (`PSCLASSID.COMPONENTINTERFACE`), `OBJECTVALUE1 = <CI>`, `OBJECTID2 = 12` (`PSCLASSID.METHOD`), `OBJECTVALUE2 = 'Methods'`. Functions marked `NoExport` aren't methods. The source isn't kept after parsing.
- **Annotation syntax:**
  - `CompIntfc.X` is the CI;
  - `CompIntfc.X.Coll[.Child…]` is an item (DataItem) of that collection;
  - a trailing `[]` (`CompIntfc.X.Coll[]`) is the collection (DataCollection).
- **AppRefiner conventions:**
  - `Debug.Log()`, never `Debug.WriteLine()`;
  - never `MessageBox.Show` (use the `MessageBoxDialog` pattern in CLAUDE.md);
  - database-dependent components declare `DataManagerRequirement` and check `HasDatabaseConnection()`.
- **SQL:**
  - SQL proxy queries are `const string` Meta-SQL in `SqlProxyQueryCatalog` with positional `:n` binds, and each needs a `MetaSQL/conversions/meta/<Method>__<variant>.sql.md` doc file.
  - Oracle uses named `:x` parameters.
  - SQL Server is ODBC with positional `?` only, never `@named`. Dictionary insertion order is bind order.
- **Demos** use only `CompIntfc.USER_PROFILE` and `Record.PSOPRDEFN`.
- **Git:** commit directly on `main`, never push.
- **Tests:**
  - `dotnet test PeopleCodeParser.SelfHosted.Tests` and `dotnet test PeopleCodeTypeInfo.Tests` must pass after every task.
  - AppRefiner must build: `dotnet build AppRefiner/AppRefiner.csproj -o <scratch dir>`.

## Review Focus

1. **Case.** A CI name written in a different case (`CompIntfc.user_profile`, or a definition fetched as `USER_PROFILE`) resolves the same; lookups are case-insensitive. Test owned by Task 2.
2. **CI without a Methods program.** Its definition simply has no custom methods. Nothing is reported, and standard methods still work. Test owned by Task 5.
3. **Property field missing from PSDBFIELD.** The outer join yields a null `FIELDTYPE`, and the property types as `any` without crashing. Test owned by Task 5.
4. **Lookup that throws** (connection dropped mid-session). This is "unknown", not "missing": no unknown-CI error and no member errors. Test owned by Task 2 (resolver) and Task 4 (check).
5. **A named CI passed to a parameter annotated with a different CI.** The existing conflict warning names both CIs. Test owned by Task 3.

## File Structure

| File | Responsibility |
|---|---|
| `PeopleCodeTypeInfo/Contracts/ComponentInterfaces.cs` (new) | Contract types: `CompIntfcDefinition`, `CompIntfcNode`, `CompIntfcCollection`, `CompIntfcProperty`, `CompIntfcMethod`, `CompIntfcStandardMethods`, `CompIntfcItemRow`, `CompIntfcLookup` |
| `PeopleCodeTypeInfo/Contracts/ITypeMetadataResolver.cs` | Cached `GetComponentInterface` and `InvalidateComponentInterface` |
| `PeopleCodeTypeInfo/Types/TypeIdentity.cs` | `CompIntfcPath` |
| `PeopleCodeParser.SelfHosted/ComponentInterfaces/CompIntfcDefinitionBuilder.cs` (new) | Rows plus methods source → definition |
| `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs`, `Nodes/DeclarationNodes.cs` | `NoExport` |
| `PeopleCodeParser.SelfHosted/Annotations/TypeAnnotation.cs` | CI path annotations |
| `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs` | Tagging and member typing |
| `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs`, `ComponentInterfaceCheck.cs` (new) | Errors |
| `AppRefiner/Database/*` | Queries, data manager methods, field-type mapping, resolver core |
| `AppRefiner/AutoCompleteService.cs`, `TooltipProviders/*`, `GotoDefintion.cs`, `Database/IDataManager.cs:537` | Editor features and the open-target fix |
| `AppRefiner/Commands/BuiltIn/RefreshComponentInterfaceCacheCommand.cs` (new), `MainForm.cs` (save hook) | Cache purge |
| `docs/demos/component-interfaces/*` (new), `docs/architecture/type-system-weak-spots.md` | Demos and docs |

---

### Task 1: Parser accepts `NoExport`

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs` (function header, after the `RETURNS` block near line 3141, before `DOC`)
- Modify: `PeopleCodeParser.SelfHosted/Nodes/DeclarationNodes.cs` (`FunctionNode`)
- Test: `PeopleCodeParser.SelfHosted.Tests/FunctionNoExportTests.cs` (new)

**Interfaces:**
- Produces: `FunctionNode.IsNoExport` (`bool`, settable).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void NoExport_is_recorded()
{   // "Function Foo() NoExport\nEnd-Function;" parses with no errors
    // and the single FunctionNode has IsNoExport == true
}
[Fact] public void NoExport_after_returns_and_before_doc()
{   // "Function Foo() Returns string NoExport Doc \"d\"\n   Return \"x\";\nEnd-Function;"
    // no errors, IsNoExport, ReturnType is string, Documentation == "d"
}
[Fact] public void Function_without_NoExport_is_exported()
{   // "Function Foo()\nEnd-Function;" → IsNoExport == false
}
[Fact] public void NoExport_named_variable_still_parses()
{   // "Local string &NoExport;\n&NoExport = \"x\";" and "Function NoExport()\nEnd-Function;"
    // both parse with no errors
}
```

- [ ] **Step 2: Run them.** `dotnet test PeopleCodeParser.SelfHosted.Tests --filter FullyQualifiedName~FunctionNoExportTests`. Expected: the first two fail.
- [ ] **Step 3: Implement.** Match `NoExport` contextually: a generic identifier token whose text equals `NoExport` (case-insensitive), only at this header position. Don't add a lexer keyword, so identifiers named NoExport keep working. Set `functionNode.IsNoExport = true`.
- [ ] **Step 4: Run the full parser suite.** Expected: all pass.
- [ ] **Step 5: Commit.** `git commit -m "Parse NoExport on function headers"`

### Task 2: Contract types, identity path, and the resolver's CI lookup

**Files:**
- Create: `PeopleCodeTypeInfo/Contracts/ComponentInterfaces.cs`
- Modify: `PeopleCodeTypeInfo/Types/TypeIdentity.cs`, `PeopleCodeTypeInfo/Contracts/ITypeMetadataResolver.cs`, `PeopleCodeTypeInfo/Inference/TypeCache.cs` (only if `Remove` of a key with no entry of its own doesn't already evict its dependents)
- Modify: `PeopleCodeParser.SelfHosted.Tests/Compilation/FakeTypeMetadataResolver.cs`
- Test: `PeopleCodeTypeInfo.Tests/ComponentInterfaceLookupTests.cs`, `PeopleCodeTypeInfo.Tests/TypeIdentityTests.cs`

**Interfaces (Produces):**

```csharp
namespace PeopleCodeTypeInfo.Contracts;

[Flags] public enum CompIntfcStandardMethods { None = 0, Cancel = 1, Create = 2, Find = 4, Get = 8, Save = 16 }

public sealed record CompIntfcProperty(string Name, string RecordName, string FieldName,
    PeopleCodeType FieldType, bool IsReadOnly, string? Comment);

public abstract class CompIntfcNode            // the CI root or a collection
{
    public string Name { get; init; }           // CI name for the root, collection name otherwise
    public string RecordName { get; init; }     // root: "" ; collection: scroll record
    public IReadOnlyDictionary<string, CompIntfcProperty> Properties { get; init; }   // OrdinalIgnoreCase
    public IReadOnlyDictionary<string, CompIntfcCollection> Collections { get; init; } // OrdinalIgnoreCase
}
public sealed class CompIntfcCollection : CompIntfcNode { }

public sealed record CompIntfcMethod(string Name, FunctionInfo Signature);

public sealed class CompIntfcDefinition : CompIntfcNode
{
    public int Version { get; init; }
    public CompIntfcStandardMethods StandardMethods { get; init; }
    public IReadOnlyList<CompIntfcProperty> GetKeys { get; init; }
    public IReadOnlyList<CompIntfcProperty> CreateKeys { get; init; }
    public IReadOnlyList<CompIntfcProperty> FindKeys { get; init; }
    public IReadOnlyDictionary<string, CompIntfcMethod> CustomMethods { get; init; }    // OrdinalIgnoreCase
    public CompIntfcNode? FindNode(string? path);          // null/"" = root; "Roles.RouteControls" walks collections
    public CompIntfcProperty? FindRootProperty(string name); // Properties, then the three key lists
    public bool IsStandardMethodEnabled(string methodName); // false only for a gated name whose bit is clear
}

public sealed record CompIntfcItemRow(int BcType, string Parent, string Name, int Sequence, int Access,
    string RecordName, string FieldName, string? Comment, PeopleCodeType FieldType);

public enum CompIntfcLookupStatus { Found, Missing, Unknown }
public readonly record struct CompIntfcLookup(CompIntfcLookupStatus Status, CompIntfcDefinition? Definition)
{
    public static CompIntfcLookup Unknown { get; }
    public static CompIntfcLookup Missing { get; }
    public static CompIntfcLookup Found(CompIntfcDefinition definition);
}
```

- `TypeIdentity`:
  - gains a fourth positional parameter `string? CompIntfcPath = null`, a dotted path from the CI root (`Roles.RouteControls`);
  - `IsEmpty`, `IsRefinementOf` and `ConflictsWith` include it.
- `ITypeMetadataResolver` gains:
  - `public CompIntfcLookup GetComponentInterface(string name)`, cached per upper-cased name;
  - `public void InvalidateComponentInterface(string? name)`, where `null` means all;
  - `public IReadOnlyCollection<string> CachedComponentInterfaces`;
  - `protected virtual CompIntfcLookup GetComponentInterfaceCore(string name) => CompIntfcLookup.Unknown`;
  - `public static TimeSpan ComponentInterfaceRecheckInterval { get; set; } = TimeSpan.FromSeconds(30)`.

  Each `GetComponentInterface` call records `RefinementDependencyCollector.Record("CompIntfc:" + upperName)`. A Found or Missing entry older than the recheck interval is re-fetched through Core. Unknown is never cached. `InvalidateComponentInterface` drops entries and calls `Cache.Remove("CompIntfc:" + name)` so dependents are evicted.
- `FakeTypeMetadataResolver` gains `AddComponentInterface(CompIntfcDefinition definition)` and `SetComponentInterfaceUnknown(bool)`.

- [ ] **Step 1: Write the failing tests**

`TypeIdentityTests`:
```csharp
[Fact] public void Collection_path_more_detail_is_refinement()
    // new TypeIdentity(CompIntfcName:"USER_PROFILE", CompIntfcPath:"Roles")
    //   .IsRefinementOf(new TypeIdentity(CompIntfcName:"USER_PROFILE")) == true
[Fact] public void Different_paths_conflict()
    // ...Path "Roles" ConflictsWith Path "EmailAddresses" == true; paths compare case-insensitively
```

`ComponentInterfaceLookupTests`:
- These use a test subclass of `ITypeMetadataResolver` that counts Core calls and returns a scripted result.
- `Found_is_cached`: two calls give one Core call.
- `Lookup_is_case_insensitive`: `user_profile` then `USER_PROFILE` gives one Core call.
- `Missing_is_cached_and_reported`: Status Missing.
- `Unknown_is_not_cached`: two calls give two Core calls.
- `Core_exception_is_unknown`: Core throws, Status Unknown, no exception escapes.
- `Recheck_after_interval`: set the interval to `TimeSpan.Zero`, two calls give two Core calls.
- `Invalidate_one_and_all`: after `InvalidateComponentInterface("USER_PROFILE")` the next call reaches Core. `null` clears every entry, and `CachedComponentInterfaces` is empty.
- `Invalidate_evicts_dependent_type_metadata`: inside a refinement frame (`RefinementDependencyCollector`), a lookup is recorded and metadata `PKG:A` is cached. After `InvalidateComponentInterface("USER_PROFILE")`, `Cache.Contains("PKG:A")` is false.
- `FindNode_walks_paths`, `FindRootProperty_includes_key_only_names`, `IsStandardMethodEnabled_gates_only_the_five`: these build a USER_PROFILE-shaped definition by hand.

- [ ] **Step 2: Run them.** Expected: compile failures, then failures.
- [ ] **Step 3: Implement** the types and members above. `IsStandardMethodEnabled` returns true for any name other than Cancel, Create, Find, Get and Save.
- [ ] **Step 4: Run both suites.** Expected: all pass.
- [ ] **Step 5: Commit.** `git commit -m "Add component interface definition contract and cached resolver lookup"`

### Task 3: Annotations and display for CI paths

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Annotations/TypeAnnotation.cs` (`ParseTarget`)
- Modify: `AppRefiner/TooltipProviders/TypeDisplay.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/AnnotationMetadataTests.cs` (extend) and `PeopleCodeParser.SelfHosted.Tests/ComponentInterfaceAnnotationTests.cs` (new)

**Interfaces:**
- Consumes: `TypeIdentity.CompIntfcPath` (Task 2).
- Produces:
  - `CompIntfc.X.A.B` → `AnnotationTarget(PeopleCodeType.DataItem, new TypeIdentity(CompIntfcName:"X", CompIntfcPath:"A.B"), written)`;
  - `CompIntfc.X.A[]` → `PeopleCodeType.DataCollection` with path `"A"`;
  - `CompIntfc.X[]` is unknown (a warning, as for any unknown annotation).
- `TypeDisplay.Detail`:
  - CI → `CompIntfc.X`;
  - DataItem with identity → `CompIntfc.X.A.B`;
  - DataCollection with identity → `CompIntfc.X.A.B[]`.

  Both overloads (`TypeInfo` and `TypeWithDimensionality`).

- [ ] **Step 1: Write the failing tests.**
  - Parsing each form above, and `CompIntfc.X.` (empty segment) staying unknown.
  - `Local ApiObject /* as CompIntfc.USER_PROFILE.Roles */ &r;` types `&r` as DataItem with that identity, with source Annotation.
  - Review Focus 5: given `Function F(&c As ApiObject /* as CompIntfc.USER_PROFILE */)`, the call `F(%Session.GetCompIntfc(CompIntfc.OTHER_CI))` gives the existing annotated-parameter warning, naming `CompIntfc.USER_PROFILE` and `CompIntfc.OTHER_CI`. This test passes only after Task 4's tagging, so mark it `[Fact(Skip = "enabled in Task 4")]` here, and Task 4 removes the Skip.
- [ ] **Step 2: Run them.** Expected: fail.
- [ ] **Step 3: Implement.** In `ParseTarget`, handle `CompIntfc` with three or more parts, and the `[]` suffix on the last part.
- [ ] **Step 4: Run the suites and build AppRefiner.** Expected: pass.
- [ ] **Step 5: Commit.** `git commit -m "Annotate component interface collections and items"`

### Task 4: Inference: tagging and member typing

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Visitors/TypeInferenceVisitor.cs`. The places to change:
  - `SpecializeNamedInstanceReturn` (~331);
  - `ResolveMemberAccessReturnType` (~1040);
  - `ResolveFunctionInfo`'s builtin member branch (~1470);
  - add a `FindCompIntfcNameFromArgs` beside `FindRecordNameFromArgs`.
- Test: `PeopleCodeParser.SelfHosted.Tests/ComponentInterfaceInferenceTests.cs` (new)

**Interfaces:**
- Consumes:
  - `ITypeMetadataResolver.GetComponentInterface`, `CompIntfcDefinition.FindNode`/`FindRootProperty`/`CustomMethods` (Task 2);
  - `FakeTypeMetadataResolver.AddComponentInterface` (Task 2).
- Produces the following types (`TypeIdentity` abbreviated to `(CI, path)`):

| Expression | Result |
|---|---|
| `GetCompIntfc(CompIntfc.X)` | ComponentInterface `.WithIdentity((X, null))` |
| Property on a named CI or a known DataItem | Primitive type of `FieldType` (`Any` for `Unknown`), assignable |
| Collection on a named CI or a known DataItem | DataCollection with `(CI, path)` |
| `Item`/`ItemByKeys`/`CurrentItem`/`GetEffectiveItem`/`InsertItem` on a DataCollection with identity | DataItem with the same identity |
| Custom method on a named CI | Its `FunctionInfo` (signature, annotations and argument checking flow through `ConvertFunctionInfoToTypeInfo`) |

  An unknown name, an Unknown or Missing lookup, or no identity falls through to today's behavior (`any`).

- [ ] **Step 1: Write the failing tests.** Use the fake resolver with a USER_PROFILE definition built from the spec's rows:
  - root properties `UserID` (string, PSOPRDEFN.OPRID), `WorklistEntriesCount` (number, read-only) and `AccountLockDate` (date);
  - collections `Roles` (properties `RoleName`, `Dynamic` read-only; child `RouteControls` → `RouteControlProfile`) and `EmailAddresses`;
  - custom method `SetPassword(&pwd As string) Returns boolean`;
  - standard methods all enabled.

  ```csharp
  Ci_from_GetCompIntfc_is_tagged      // &ci identity CompIntfcName == "USER_PROFILE"
  Property_has_field_type             // &s = &ci.UserID → string; &d = &ci.AccountLockDate → date
  Collection_and_item_carry_path      // &ci.Roles → DataCollection path "Roles"; &ci.Roles.Item(1) → DataItem path "Roles"
  Nested_collection_path              // &ci.Roles.Item(1).RouteControls.Item(1) → DataItem path "Roles.RouteControls"
  Item_property_type                  // &role.RoleName → string
  Custom_method_returns_declared_type // &b = &ci.SetPassword("x") → boolean
  Annotated_item_parameter_is_typed   // Function F(&r As ApiObject /* as CompIntfc.USER_PROFILE.Roles */) … &r.RoleName → string
  Unknown_lookup_keeps_any            // SetComponentInterfaceUnknown(true): &ci.UserID → Any, &ci still tagged
  Unnamed_ci_unchanged                // GetCompIntfc(&name) → ComponentInterface with no CompIntfcName; &ci.UserID → Any
  ```
  Also remove the Skip from Task 3's Review Focus 5 test.
- [ ] **Step 2: Run them.** Expected: fail.
- [ ] **Step 3: Implement.** Map `FieldType` with the existing PeopleCodeType → TypeInfo conversion used for primitives.
- [ ] **Step 4: Run both suites.** Expected: all pass, with existing tests unchanged.
- [ ] **Step 5: Commit.** `git commit -m "Type component interface members from their definition"`

### Task 5: Definition builder

**Files:**
- Create: `PeopleCodeParser.SelfHosted/ComponentInterfaces/CompIntfcDefinitionBuilder.cs`
- Test: `PeopleCodeParser.SelfHosted.Tests/ComponentInterfaces/CompIntfcDefinitionBuilderTests.cs`

**Interfaces:**
- Consumes: the contract types (Task 2), `FunctionNode.IsNoExport` (Task 1), `TypeMetadataBuilder.ExtractMetadata`.
- Produces:
  ```csharp
  public static CompIntfcDefinition Build(string ciName, int version, int standardMethodBits,
      IEnumerable<CompIntfcItemRow> rows, string? methodsSource)
  ```
  - Rows are attached by `Parent`: `PS_ROOT` is the root, otherwise the collection whose `Name` matches. Rows are ordered by `Sequence`.
  - BcType 1/2/5 rows go to GetKeys/CreateKeys/FindKeys, 3 makes a collection, 4 makes a property.
  - `Access == 2` means read-only. A blank or single-space `Comment` becomes null.
  - `methodsSource` is parsed, and every implemented function that isn't `NoExport` becomes a `CompIntfcMethod` with the `FunctionInfo` from `ExtractMetadata(program).Methods[name]`. The source isn't stored.

- [ ] **Step 1: Write the failing tests.** Use the USER_PROFILE rows from the spec's reference section, as `CompIntfcItemRow`s typed in the test.
  ```csharp
  Builds_tree_from_parents          // Roles.Collections["RouteControls"].Properties["RouteControlProfile"].FieldName == "RTE_CNTL_PROFILE"
  Keys_are_listed                   // GetKeys/CreateKeys each have UserID (PSOPRDEFN_SRCH.OPRID); FindKeys empty
  Access_2_is_read_only             // Properties["WorklistEntriesCount"].IsReadOnly; Roles "Dynamic" too
  Blank_comment_is_null             // UserID.Comment == null; MultiLanguageEnabled.Comment == "1 for True, 0 for False"
  Standard_method_bits              // bits 4|8 → IsStandardMethodEnabled("Find") && !…("Save")
  Methods_from_source_skip_NoExport // "Function SetPassword(&p As string) Returns boolean\n   Return True;\nEnd-Function;\nFunction Helper() NoExport\nEnd-Function;" → only SetPassword, returns boolean, one string param
  No_methods_source_means_none      // methodsSource null → CustomMethods empty            (Review Focus 2)
  Unknown_field_type_is_any         // a row with FieldType Unknown → property FieldType Unknown, built without error (Review Focus 3)
  Orphan_row_is_skipped             // a row whose Parent names no collection is ignored, no exception
  ```
- [ ] **Step 2: Run them.** Expected: fail.
- [ ] **Step 3: Implement** `Build`.
- [ ] **Step 4: Run the suites.** Expected: pass.
- [ ] **Step 5: Commit.** `git commit -m "Build component interface definitions from PSBCITEM rows and the Methods program"`

### Task 6: Checks

**Files:**
- Modify: `PeopleCodeParser.SelfHosted/Compilation/Checks/InvalidMemberAccessCheck.cs`
- Create: `PeopleCodeParser.SelfHosted/Compilation/Checks/ComponentInterfaceCheck.cs` (registered like the other checks)
- Test: `PeopleCodeParser.SelfHosted.Tests/Compilation/ComponentInterfaceCheckTests.cs`

**Interfaces:**
- Consumes: Tasks 2 and 4.
- Produces: diagnostics.
  - **InvalidMemberAccessCheck:** the CI early return stays for unnamed CIs and non-Found lookups. A named CI with Found checks members. A DataItem with identity checks its property and collection names.
  - **ComponentInterfaceCheck:** read-only assignments, disabled standard methods, and unknown `CompIntfc.X` references (any node inferred as a `ReferenceTypeInfo` with category CompIntfc, only when the lookup is Missing).

  Messages (exact):
  - `'{name}' is not a property of CompIntfc.{CI}`, or `of CompIntfc.{CI}.{path}` for items;
  - `'{name}' is not a method of CompIntfc.{CI}`;
  - `'{name}' is read-only on CompIntfc.{CI}` (or `.{path}`);
  - `CompIntfc.{CI} doesn't enable {Method}()`;
  - `Component interface '{CI}' doesn't exist`.

  Unknown-member messages keep the existing refinement suffix (`(&ci is ComponentInterface, assigned on line N)`).

- [ ] **Step 1: Write the failing tests.** Use the USER_PROFILE fake from Task 4 and assert each message:
  ```csharp
  Unknown_property_on_named_ci        // &ci.Bogus → property error
  Unknown_method_on_named_ci          // &ci.Bogus() → method error
  Custom_and_standard_methods_ok      // &ci.SetPassword("x"), &ci.Get(), &ci.GetPropertyByName("UserID") → no errors
  Key_only_name_is_a_property         // a key with no property row → no error
  Unknown_property_on_item            // &role.Bogus → "of CompIntfc.USER_PROFILE.Roles"
  Read_only_assignment                // &ci.WorklistEntriesCount = 1 → error; reading it → none
  Read_only_on_item                   // &role.Dynamic = "Y" → error
  Disabled_standard_method            // definition with bits 1|2|8|16 (no Find): &ci.Find() → "doesn't enable Find()"
  Unknown_ci_reference                // CompIntfc.NO_SUCH (lookup Missing) → error
  Unknown_lookup_reports_nothing      // SetComponentInterfaceUnknown(true): every case above → no CI diagnostics (Review Focus 4)
  Unnamed_ci_unchanged                // GetCompIntfc(&name): &ci.Bogus() → nothing
  Property_type_mismatch_on_assign    // &ci.AccountLockDate = "x" (string into date) → the existing assignment type error
  ```
- [ ] **Step 2: Run them.** Expected: fail.
- [ ] **Step 3: Implement** the two checks.
- [ ] **Step 4: Run both suites.** Fix any existing tests that change because named CIs are now checked, and note each one in the report.
- [ ] **Step 5: Commit.** `git commit -m "Check component interface members against their definition"`

### Task 7: Data layer, SQL proxy, and the database resolver

**Files:**
- Modify: `AppRefiner/Database/IDataManager.cs`, `AppRefiner/Database/SqlProxyDataManager.cs`, `AppRefiner/Database/SqlProxyQueries/SqlProxyQueryCatalog.cs`, `AppRefiner/Database/DatabaseTypeMetadataResolver.cs`
- Create: `AppRefiner/Database/FieldTypes.cs`, plus three `MetaSQL/conversions/meta/GetComponentInterface__{defn,items,methods}.sql.md` files (copy an existing `.sql.md` file's layout)

**Interfaces:**
- Consumes: `CompIntfcDefinitionBuilder.Build` (Task 5), the contract types (Task 2).
- Produces:
  - `IDataManager.CompIntfcDefinition? GetComponentInterface(string ciName)`:
    - null only when PSBCDEFN has no row; query failures throw;
    - cached per upper-cased name, keyed by `PSBCDEFN.VERSION`, so each call runs the version query and re-fetches when the version changed.
  - `IDataManager.void InvalidateComponentInterface(string? ciName)` (`null` means all).
  - `IDataManager.IReadOnlyCollection<string> CachedComponentInterfaces { get; }`.
  - `FieldTypes.ToPeopleCodeType(int fieldType)`: 0/1 → String, 2/3 → Number, 4 → Date, 5 → Time, 6 → DateTime, 8/9 → Any, else Unknown. Copy the mapping from `SqlProxyDataManager.GetFieldType`; existing call sites may stay as they are.
  - `DatabaseTypeMetadataResolver.GetComponentInterfaceCore`:
    - not connected → Unknown;
    - null → Missing;
    - exception → Unknown, after `Debug.Log`;
    - otherwise Found.

  SQL proxy queries (Meta-SQL; Oracle and SQL Server in Task 8 mirror them):
  ```sql
  -- GetComponentInterfaceDefn
  SELECT VERSION, BCSTDMETHODS FROM %Table(PSBCDEFN) WHERE BCNAME = :1
  -- GetComponentInterfaceItems
  SELECT i.BCTYPE, i.BCITEMPARENT, i.BCITEMNAME, i.SEQUENCE_NBR_6, i.BCACCESS,
         i.RECNAME, i.FIELDNAME, i.COMMENTSHORT, f.FIELDTYPE
    FROM %Table(PSBCITEM) i
    LEFT OUTER JOIN %Table(PSDBFIELD) f ON f.FIELDNAME = i.FIELDNAME
   WHERE i.BCNAME = :1
   ORDER BY i.SEQUENCE_NBR_6
  ```
  The Methods program reuses the existing PSPCMPROG content load, with pairs `(74, CI)`, `(12, "Methods")` and blank slots after. A null `FIELDTYPE` maps to Unknown.

- [ ] **Step 1: Implement** the interface members, the SQL proxy implementation (cache cleared on Connect/Disconnect beside the app class cache), `FieldTypes`, the queries, the doc files and the resolver core. Oracle and SQL Server get `NotImplementedException` stubs only until Task 8, so the solution builds.
- [ ] **Step 2: Build AppRefiner and run both suites.** Expected: build succeeds, tests pass. There's no AppRefiner test project, so the builder logic is already covered by Task 5.
- [ ] **Step 3: Commit.** `git commit -m "Fetch component interface definitions through the SQL proxy"`
- [ ] **Step 4: Manual check (Tim, at the end; record in the ledger).** Against a SQL proxy connection, hover `&ci.UserID` in `06_` style code and see `string`.

### Task 8: Oracle and SQL Server data managers

**Files:**
- Modify: `AppRefiner/Database/OraclePeopleSoftDataManager.cs`, `AppRefiner/Database/SqlServerPeopleSoftDataManager.cs`

**Interfaces:**
- Consumes: Task 7's interface and queries.
- Produces: the same behavior as SQL proxy, with the same caching and the same null/throw contract.
  - The queries are inline SQL with plain table names (no `%Table`): Oracle `:name` binds, SQL Server positional `?` binds in insertion order.
  - The Methods program loads through each manager's existing PSPCMPROG content path.

- [ ] **Step 1: Implement both.**
- [ ] **Step 2: Build AppRefiner.** Expected: success, no `NotImplementedException` left (`grep -n NotImplementedException AppRefiner/Database/*Manager.cs` shows none for CI).
- [ ] **Step 3: Commit.** `git commit -m "Fetch component interface definitions on Oracle and SQL Server"`

### Task 9: Editor features and the CI PeopleCode open target

**Files:**
- Modify:
  - `AppRefiner/AutoCompleteService.cs` (next to the RecordTypeInfo block, ~636);
  - `AppRefiner/TooltipProviders/MethodParametersTooltipProvider.cs` (after the builtin miss, ~113);
  - `AppRefiner/GotoDefintion.cs` (inferred-type fallback, ~251);
  - `AppRefiner/Database/IDataManager.cs:537`;
  - `AppRefiner/TooltipProviders/TypeDisplay.cs` (only if Task 3 left anything).
- Create: `AppRefiner/TooltipProviders/ComponentInterfaceTooltipProvider.cs`

**Interfaces:**
- Consumes: the resolver lookup (Task 2), typed identities (Task 4), and the definition's custom methods (Task 5).
- Produces:
  - **Autosuggest.** For a named CI with Found:
    - properties and key-only names: `"{Name} -> {type} (Property)?{Property icon}"`;
    - collections: `"{Name} -> DataCollection (Collection)?{Property icon}"`;
    - custom methods, in the same format as builtin methods;
    - disabled standard methods are removed from the builtin list.

    A DataItem with identity gets its node's properties and collections, plus `ItemNum`.
  - **`ComponentInterfaceTooltipProvider`.** Priority 75, `DataManagerRequirement.NotRequired`, using the editor's `TypeResolver`. Hovering a CI or DataItem property shows:
    ```
    {Name}: {type}
    Record field: {RECNAME}.{FIELDNAME}
    Read-only            (only when read-only)
    {comment}            (when present)
    ```
  - **Method-parameters tooltip.** A custom method call shows the signature, formatted like a builtin.
  - **Go-to-definition.** A custom method call opens the OpenTarget `(COMPONENTINTERFACE, CI)`, `(METHOD, "Methods")`, parses it with `GetParsedProgram`, and selects the `FunctionNode` with that name. This follows the app class pattern; no source position is cached.
  - **`IDataManager.cs:537`.** Map `ComponentInterfacePeopleCode` to `PSCLASSID.COMPONENTINTERFACE` (74).

- [ ] **Step 1: Implement** the items above.
- [ ] **Step 2: Build AppRefiner and run both suites.** Expected: success.
- [ ] **Step 3: Commit.** `git commit -m "Autosuggest, hovers, signatures and go-to-definition for component interfaces"`
- [ ] **Step 4: Manual check with Tim (blocking for the open target).** Open a CI's PeopleCode from AppRefiner's open dialog, and go-to-definition on `&ci.SetPassword(...)`.
  - If App Designer rejects the `COMPONENTINTERFACE.<CI>.METHOD.Methods` string, the fix is to find the accepted form with Tim and change `BuildOpenTargetString`'s output for this type only. The PSPCMPROG lookup keeps using 74.
  - Record the result in the ledger.

### Task 10: Cache purge (command and save hook)

**Files:**
- Create: `AppRefiner/Commands/BuiltIn/RefreshComponentInterfaceCacheCommand.cs`
- Modify: `AppRefiner/MainForm.cs` (`InvalidateTypeCacheForEditor`, ~4613)

**Interfaces:**
- Consumes:
  - `IDataManager.InvalidateComponentInterface`/`GetComponentInterface`/`CachedComponentInterfaces` (Task 7);
  - `ITypeMetadataResolver.InvalidateComponentInterface` (Task 2);
  - `OpenTargetBuilder` (an existing caption parse).
- Produces:
  - **The command** `"Refresh Component Interface Cache"`, which requires a database connection.
    - For each name in the data manager's `CachedComponentInterfaces`: invalidate it, re-fetch it with `GetComponentInterface`, and call `resolver.InvalidateComponentInterface(name)`. Then re-run stylers on the active editor.
    - It ends with the CLAUDE.md `MessageBoxDialog` pattern: `Refreshed {n} component interface(s).`
    - With no connection it shows the same dialog pattern with `Connect to a database to refresh component interfaces.` and returns.
  - **The save hook.** If the saved editor's OpenTarget (from its caption) is `ComponentInterfacePeopleCode`, call both invalidations for that CI name. The existing `editor.Type != EditorType.PeopleCode` early return must still let CI PeopleCode through. Confirm CI PeopleCode editors are `EditorType.PeopleCode`; if they aren't, handle them before that return.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Build AppRefiner.** Expected: success.
- [ ] **Step 3: Commit.** `git commit -m "Refresh component interface definitions on command and on CI PeopleCode save"`

### Task 11: Demos and docs

**Files:**
- Create: `docs/demos/component-interfaces/01_Typing.pcode`, `02_Errors.pcode`, `03_Methods.pcode`, `README.md`
- Modify: `docs/architecture/type-system-weak-spots.md`

**Content:**
- **`01_Typing`:**
  - typed properties (`UserID`, `AccountLockDate`);
  - `Roles` → items → `RouteControls` → items;
  - an annotated helper `Function ProcessRole(&role As ApiObject /* as CompIntfc.USER_PROFILE.Roles */)`;
  - `[]` collection annotations.
- **`02_Errors`:** one line per error. For the disabled-method line, the README tells the tester to clear a bit in USER_PROFILE's standard methods first (Tim toggles them).
- **`03_Methods`:** a `SetPassword` call for the signature tooltip and go-to-definition, and a `NoExport` example explanation.
- **README:**
  - setup;
  - an expected-results table, generated by running the files through the checker with a fake resolver holding the USER_PROFILE definition (the same approach as earlier demo READMEs);
  - hovers;
  - the save / refresh-cache check.
- **Weak-spots doc:** the "A CI's own members are unchecked and untyped" item now says it applies only to unnamed CIs, and lists `GetCompIntfc(&name)` and `Find()` results as still untyped.

- [ ] **Step 1: Write the files**, run them through the checker, and fill in the table from the actual output.
- [ ] **Step 2: Commit.** `git commit -m "Add component interface demos and update weak-spots doc"`
