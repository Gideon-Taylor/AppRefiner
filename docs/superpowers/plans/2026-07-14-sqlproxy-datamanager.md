# SqlProxyDataManager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `SqlProxyDataManager`, a full `IDataManager` implementation that runs only static Meta-SQL with positional `:1`… binds over `SqlProxyDbConnection`, and wire App Designer auto-attach to use it instead of Oracle+proxy.

**Architecture:** Extract every Oracle manager query into `MetaSQL/conversions/`, convert each to installable Meta-SQL (`%Table`, `%Concat`, `%Upper`, … + `:n` binds), then implement `SqlProxyDataManager` that owns a `SqlProxyDbConnection` and executes via ordered `SqlProxyBind` lists. Dynamic behavior (message-set filters, open-target type selection, row limits) lives in C#, not SQL text.

**Tech Stack:** C# / .NET 8, existing `AppRefiner.Database.SqlProxy` stack, PeopleSoft Meta-SQL expanded by SAM in App Designer. No AppRefinerHook changes required for this plan unless `maxRows` already fails in practice.

**Spec:** `docs/superpowers/specs/2026-07-14-sqlproxy-datamanager-design.md`  
**Meta-SQL reference:** `MetaSQL/INDEX.md`, `MetaSQL/functions/`  
**Oracle reference implementation:** `AppRefiner/Database/OraclePeopleSoftDataManager.cs`

## Global Constraints

- **Static SQL only:** no `$"…"` SQL assembly, no dynamic `IN (...)` lists, no dynamic `UNION` string building, no interpolating limits into SQL.
- **Positional binds only:** `:1`, `:2`, … in SQL; C# builds `List<SqlProxyBind>` in that order. Never named `:objectName` binds.
- **`%Table` record rule:** `PS_FOO` → `%Table(FOO)`; `PSSQLDEFN` (no underscore after PS) → `%Table(PSSQLDEFN)`.
- Execute production queries with  
  `SqlProxyDbConnection.ExecuteQuery(string sql, IReadOnlyList<SqlProxyBind> binds, int? maxRows = null)`  
  — not the `Dictionary<string, object>` overload.
- **SELECT-only.** No `ExecuteNonQuery`, no `ALTER SESSION`.
- Use AppRefiner’s `Debug.Log` / `Debug.LogError`, not `System.Diagnostics.Debug`.
- Prefer copying **C# control flow and row mapping** from `OraclePeopleSoftDataManager`; only SQL text, bind style, and former dynamic-SQL sites change.
- Build verify (per task unless noted):  
  `dotnet build AppRefiner/AppRefiner.csproj -c Debug`  
  Expected: **0 errors**. Do not require full `build.ps1` / hook rebuild for pure C# tasks.
- There is no dedicated unit-test project for DataManagers; verification is build + manual App Designer attach when available.
- Commit after each completed task with an imperative one-line message.

## File Structure

| File | Responsibility |
|------|----------------|
| `MetaSQL/conversions/README.md` | Conversion checklist and process |
| `MetaSQL/conversions/INDEX.md` | Method → query-id map |
| `MetaSQL/conversions/oracle/*.sql.md` | Extracted Oracle SQL + bind notes |
| `MetaSQL/conversions/meta/*.sql.md` | Converted static Meta-SQL + positional bind tables |
| `AppRefiner/Database/SqlProxyQueries/SqlProxyQueryCatalog.cs` | `public const string` Meta-SQL for every query (source of truth in code) |
| `AppRefiner/Database/SqlProxyQueries/SqlProxyOpenTargetQueries.cs` | Per–open-target-type static Meta-SQL constants (large; keep separate) |
| `AppRefiner/Database/SqlProxyDataManager.cs` | `IDataManager` implementation |
| `AppRefiner/MainForm.cs` | `TryAttachSqlProxyDataManager` wiring |
| `AppRefiner/Database/OraclePeopleSoftDataManager.cs` | Remove process/SqlProxy ctor and `UsesSqlProxy` |

Optional later split of `SqlProxyDataManager.cs` into partials if the file exceeds ~1500 lines; start as one file matching Oracle’s structure.

---

### Task 1: Conversion folder scaffolding

**Files:**
- Create: `MetaSQL/conversions/README.md`
- Create: `MetaSQL/conversions/INDEX.md`
- Create: `MetaSQL/conversions/oracle/.gitkeep` (or first extract files in Task 2)
- Create: `MetaSQL/conversions/meta/.gitkeep`

- [ ] **Step 1: Write `MetaSQL/conversions/README.md`**

```markdown
# Meta-SQL conversions for SqlProxyDataManager

## Process
1. Extract Oracle SQL from `OraclePeopleSoftDataManager` into `oracle/<Method>__<id>.sql.md`.
2. Convert into `meta/<Method>__<id>.sql.md` (static Meta-SQL, `:1`… binds).
3. Copy final Meta-SQL into `AppRefiner/Database/SqlProxyQueries/*` constants.
4. Implement method body in `SqlProxyDataManager` with ordered `SqlProxyBind` list.

## Checklist per query
- [ ] `%Table(recname)` with correct record name (strip `PS_` only when underscore present)
- [ ] No named binds; only `:1`, `:2`, …
- [ ] No dynamic SQL fragments
- [ ] SELECT only
- [ ] Column aliases match what C# reads (`row["SQLTEXT"]`, etc.)
- [ ] Bind table documents CLR type and meaning for each `:n`
- [ ] maxRows policy noted (default / limit / unlimited for blob chunks)

## Record-name rule
- `PS_APPR_RULE_HDR` → `%Table(APPR_RULE_HDR)`
- `PSSQLDEFN`, `PSPCMPROG`, `PSSTATUS`, … → `%Table(PSSQLDEFN)` etc.
```

- [ ] **Step 2: Write `MetaSQL/conversions/INDEX.md` skeleton**

Include every method/query id from the inventory below (mark status `pending`):

| Method | Query id | Oracle file | Meta file | Status |
|--------|----------|-------------|-----------|--------|
| GetSqlDefinition | main | … | … | pending |
| GetAllSqlDefinitions | names | … | … | pending |
| GetHtmlDefinition | main | … | … | pending |
| GetAllHtmlDefinitions | names | … | … | pending |
| GetPeopleCodeItemMetadataForProject | projectItems | … | … | pending |
| LoadPeopleCodeItemContent | progTxt | … | … | pending |
| GetNameReferencesForProgram | pcmName | … | … | pending |
| CheckAppClassExists | exists | … | … | pending |
| GetAppClassSourceByPath | (uses Check + Load paths) | — | — | n/a (C#) |
| GetAppPackageItems | subpackagesRoot | … | … | pending |
| GetAppPackageItems | subpackagesNested | … | … | pending |
| GetAppPackageItems | classes | … | … | pending |
| GetRecordFields | recExists | … | … | pending |
| GetRecordFields | fields | … | … | pending |
| GetMessageSets | main | … | … | pending |
| GetMessagesForSet | main | … | … | pending |
| GetMessageCatalogEntry | main | … | … | pending |
| SearchMessageCatalog | main | … | … | pending |
| GetEventMapXrefs | main | … | … | pending |
| GetEventMapItems | component | … | … | pending |
| GetEventMapItems | componentRecord | … | … | pending |
| GetEventMapItems | componentRecordField | … | … | pending |
| GetEventMapItems | page | … | … | pending |
| GetOpenTargets | per type (see Task 4) | … | … | pending |
| GetFunctionDefiningPrograms | main | … | … | pending |
| GetToolsVersion | main | … | … | pending |
| GetProgramObjectIds | main | … | … | pending |
| GetFieldType | main | … | … | pending |
| GetPackagesForClass | main | … | … | pending |
| GetAllClassesForPackage | main | … | … | pending |
| GetPeopleCodeProgram | (uses GetProgramObjectIds + prog load) | — | — | n/a |

- [ ] **Step 3: Commit**

```bash
git add MetaSQL/conversions
git commit -m "Add Meta-SQL conversion workspace for SqlProxyDataManager"
```

---

### Task 2: Extract all Oracle SQL into `conversions/oracle`

**Files:**
- Create: one `MetaSQL/conversions/oracle/<Method>__<id>.sql.md` per query in INDEX
- Modify: `MetaSQL/conversions/INDEX.md` (paths filled, status `extracted`)

**Source of truth:** `AppRefiner/Database/OraclePeopleSoftDataManager.cs` — every `_connection.ExecuteQuery` call.

- [ ] **Step 1: For each query, create a file in this exact shape**

Example for GetSqlDefinition:

```markdown
# GetSqlDefinition / main

## Purpose
Load concatenated SQL definition text for one SQLID.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :objectName | string | SQLID |

## Original Oracle SQL
```sql
SELECT B.SQLTEXT
FROM PSSQLDEFN A, PSSQLTEXTDEFN B
WHERE A.SQLID = B.SQLID
AND A.SQLID = :objectName
ORDER BY B.SEQNUM
```

## Notes
- Multi-row SEQNUM chunks; C# concatenates SQLTEXT.
- maxRows: high enough for long definitions (default OK unless truncated).
```

**Inventory (must all exist after this task):**

1. `GetSqlDefinition__main`
2. `GetAllSqlDefinitions__names`
3. `GetHtmlDefinition__main`
4. `GetAllHtmlDefinitions__names`
5. `GetPeopleCodeItemMetadataForProject__projectItems`
6. `LoadPeopleCodeItemContent__progTxt` (7 object id/value pairs)
7. `GetNameReferencesForProgram__pcmName` (same 7 pairs)
8. `CheckAppClassExists__exists`
9. `GetAppPackageItems__subpackagesRoot`
10. `GetAppPackageItems__subpackagesNested`
11. `GetAppPackageItems__classes`
12. `GetRecordFields__recExists` (`PSRECDEFN`)
13. `GetRecordFields__fields` (`PSRECFIELDDB` join field metadata as in Oracle)
14. `GetMessageSets__main`
15. `GetMessagesForSet__main`
16. `GetMessageCatalogEntry__main`
17. `SearchMessageCatalog__main` — capture **without** the dynamic set filter / FETCH FIRST (document those as C#-side in Notes)
18. `GetEventMapXrefs__main`
19. `GetEventMapItems__component`
20. `GetEventMapItems__componentRecord`
21. `GetEventMapItems__componentRecordField`
22. `GetEventMapItems__page`
23. `GetFunctionDefiningPrograms__main`
24. `GetToolsVersion__main`
25. `GetProgramObjectIds__main`
26. `GetFieldType__main`
27. `GetPackagesForClass__main`
28. `GetAllClassesForPackage__main`
29. **Open-target arms** — extract each `GetUnionClauseForType` arm and each PeopleCode open-target query from the private helpers near end of Oracle file as separate files:
    - `GetOpenTargets__Activity`
    - `GetOpenTargets__AnalyticModel`
    - `GetOpenTargets__AnalyticType`
    - `GetOpenTargets__AppEngineProgram`
    - `GetOpenTargets__ApplicationPackage`
    - `GetOpenTargets__ApplicationClass`
    - `GetOpenTargets__ApprovalRuleSet` (table `PS_APPR_RULE_HDR`)
    - `GetOpenTargets__BusinessInterlink`
    - `GetOpenTargets__BusinessProcess`
    - `GetOpenTargets__Component`
    - `GetOpenTargets__ComponentInterface`
    - `GetOpenTargets__Field`
    - `GetOpenTargets__FileLayout`
    - `GetOpenTargets__FileReference`
    - `GetOpenTargets__HTML`
    - `GetOpenTargets__Image`
    - `GetOpenTargets__Menu`
    - `GetOpenTargets__Message`
    - `GetOpenTargets__OptimizationModel`
    - `GetOpenTargets__Page`
    - `GetOpenTargets__PageFluid`
    - `GetOpenTargets__Project`
    - `GetOpenTargets__Record`
    - `GetOpenTargets__SQL`
    - `GetOpenTargets__StyleSheet`
    - Plus PeopleCode flavors currently separate:  
      `GetOpenTargets__PagePeopleCode`, `ComponentPeopleCode`, `ComponentRecordPeopleCode`, `ComponentRecFieldPeopleCode`, `RecordFieldPeopleCode`, `MenuPeopleCode`, `AppEnginePeopleCode`, `ComponentInterfacePeopleCode`, `MessagePeopleCode`
30. Do **not** extract the outer Oracle CTE/`ROW_NUMBER` wrapper as a required meta query — ranking moves to C# (spec).

- [ ] **Step 2: Update INDEX statuses to `extracted`**

- [ ] **Step 3: Commit**

```bash
git add MetaSQL/conversions
git commit -m "Extract Oracle DataManager SQL for Meta-SQL conversion"
```

---

### Task 3: Convert catalog / PeopleCode / package / record / message / event / misc SQL to Meta-SQL

**Files:**
- Create: matching `MetaSQL/conversions/meta/*.sql.md` for all non–open-target queries from Task 2
- Modify: INDEX statuses → `converted`

**Conversion rules (apply to every file):**

| From | To |
|------|-----|
| `FROM PSSQLDEFN` | `FROM %Table(PSSQLDEFN)` |
| `FROM PS_APPR_RULE_HDR` | `FROM %Table(APPR_RULE_HDR)` |
| `a \|\| b` | `a %Concat b` |
| `UPPER(x)` | `%Upper(x)` |
| `DBMS_LOB.SUBSTR(DESCRLONG, 4000, 1)` or `SUBSTR(...)` | `%Substring(DESCRLONG, 1, 4000)` (verify argument order vs Oracle SUBSTR; Meta-SQL is start then length) |
| `TO_CHAR(n)` numeric | `%NumToChar(n)` |
| Named `:foo` | `:1`, `:2`, … in left-to-right appearance order (same name reused = one bind position per appearance order for SAM; **if Oracle reused one named bind twice**, expand to two `:n` with the **same value bound twice** in C#, or restructure so each placeholder appears once — prefer **single appearance** when possible) |
| `FETCH FIRST` / dynamic IN | Remove; note C# filter + maxRows |

- [ ] **Step 1: Convert a reference example completely**

`meta/GetSqlDefinition__main.sql.md`:

```markdown
# GetSqlDefinition / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | SQLID |

## Meta-SQL
```sql
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM
```

## maxRows
Default (multi-chunk definition).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
```

`meta/GetToolsVersion__main.sql.md`:

```sql
SELECT TOOLSREL %Concat '.' %Concat PTPATCHREL FROM %Table(PSSTATUS)
```

(no binds)

`meta/SearchMessageCatalog__main.sql.md`:

```sql
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE (%Upper(MESSAGE_TEXT) LIKE :1
        OR %Upper(%Substring(DESCRLONG, 1, 4000)) LIKE :1)
 ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
```

Bind `:1` = `%TERM%` (uppercased in C#). Set filter + limit: **C# only**.

- [ ] **Step 2: Convert all remaining non–open-target oracle files** (subagent-friendly: one domain per agent)

Domains:
- SQL/HTML defs
- Project / PSPCMPROG / PSPCMNAME / app class exists
- Packages / classes
- Record fields
- Message catalog
- Event map (4 variants + xrefs)
- Function-defining programs, tools version, program object ids, field type

For **7-pair object id queries**, bind order must be:

`:1` objId1, `:2` objVal1, `:3` objId2, `:4` objVal2, … through `:13` objId7, `:14` objVal7  

(or document the exact order chosen — must match C# list). Prefer alternating id/value as above.

For `CheckAppClassExists`, keep `%Upper(OBJECTVALUEn) = :even` pattern with uppercased values prepared in C# **or** `%Upper` on both sides consistently with Oracle.

- [ ] **Step 3: Update INDEX to `converted` for those rows**

- [ ] **Step 4: Commit**

```bash
git add MetaSQL/conversions
git commit -m "Convert catalog and PeopleCode queries to static Meta-SQL"
```

---

### Task 4: Convert open-target queries to static Meta-SQL (per type)

**Files:**
- Create: `MetaSQL/conversions/meta/GetOpenTargets__*.sql.md` for every arm listed in Task 2
- Modify: INDEX

Each type query is **standalone** (no outer CTE). Pattern:

```sql
SELECT 'Activity' AS DEFN_TYPE,
       ACTIVITYNAME AS ID,
       DESCR60 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSACTIVITYDEFN)
 WHERE (:1 = 'Y' OR %Upper(ACTIVITYNAME) LIKE %Upper(:2))
   AND (:3 = 'Y' OR %Upper(DESCR60) LIKE %Upper(:4))
 ORDER BY ACTIVITYNAME
```

**Bind convention for all open-target type queries (fixed):**

| # | Meaning |
|--:|---------|
| 1 | `'Y'` if id search empty (match all ids), else `'N'` |
| 2 | id pattern e.g. `FOO%` (already uppercased in C# if using UPPER in SQL) |
| 3 | `'Y'` if descr search empty, else `'N'` |
| 4 | descr pattern e.g. `%BAR%` |

Do **not** use Oracle `LENGTH(:id_search) = 0` with empty string if that is fragile under SAM; empty-match flags `:1`/`:3` keep SQL static and portable.

For Application Class ID construction, replace `||` with `%Concat` and `CASE` as needed inside the **fixed** string.

Approval rule set:

```sql
FROM %Table(APPR_RULE_HDR)
```

(not `%Table(PS_APPR_RULE_HDR)`).

PeopleCode open-target queries: same four-bind convention; keep OBJECTID filters as **literals** inside static SQL (as Oracle does).

- [ ] **Step 1: Convert all type arms + PeopleCode arms**

- [ ] **Step 2: INDEX → `converted`**

- [ ] **Step 3: Commit**

```bash
git add MetaSQL/conversions
git commit -m "Convert open-target queries to static Meta-SQL per type"
```

---

### Task 5: Add `SqlProxyQueryCatalog` + open-target query constants

**Files:**
- Create: `AppRefiner/Database/SqlProxyQueries/SqlProxyQueryCatalog.cs`
- Create: `AppRefiner/Database/SqlProxyQueries/SqlProxyOpenTargetQueries.cs`

- [ ] **Step 1: Create catalog class with const strings copied from `meta/*.sql.md`**

```csharp
namespace AppRefiner.Database.SqlProxyQueries
{
    /// <summary>
    /// Static Meta-SQL strings for <see cref="SqlProxyDataManager"/>.
    /// Keep in sync with MetaSQL/conversions/meta/.
    /// </summary>
    public static class SqlProxyQueryCatalog
    {
        public const string GetSqlDefinition = @"
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM";

        public const string GetAllSqlDefinitionNames = @"
SELECT DISTINCT A.SQLID
  FROM %Table(PSSQLDEFN) A
 WHERE EXISTS (
       SELECT 1 FROM %Table(PSSQLTEXTDEFN) B
        WHERE A.SQLID = B.SQLID
 )";

        public const string GetToolsVersion = @"
SELECT TOOLSREL %Concat '.' %Concat PTPATCHREL FROM %Table(PSSTATUS)";

        // ... every non-open-target query from conversions/meta
    }
}
```

- [ ] **Step 2: Create `SqlProxyOpenTargetQueries` with one public const per type**

```csharp
namespace AppRefiner.Database.SqlProxyQueries
{
    public static class SqlProxyOpenTargetQueries
    {
        public const string Activity = @" ... ";
        // ...
        public static string? ForType(OpenTargetType type) => type switch
        {
            OpenTargetType.Activity => Activity,
            // ...
            _ => null
        };
    }
}
```

`OpenTargetType` lives in `AppRefiner.Database.Models` (or wherever Oracle already references it — use the same type).

- [ ] **Step 3: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj -c Debug
```

Expected: 0 errors (constants-only).

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/Database/SqlProxyQueries
git commit -m "Add static Meta-SQL query catalogs for SqlProxyDataManager"
```

---

### Task 6: Scaffold `SqlProxyDataManager` (lifecycle + helpers)

**Files:**
- Create: `AppRefiner/Database/SqlProxyDataManager.cs`

- [ ] **Step 1: Implement skeleton**

```csharp
using AppRefiner.Database.Models;
using AppRefiner.Database.SqlProxy;
using AppRefiner.Database.SqlProxyQueries;
using PeopleCodeTypeInfo.Types;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace AppRefiner.Database
{
    /// <summary>
    /// IDataManager over App Designer SAM via static Meta-SQL and SqlProxyDbConnection.
    /// </summary>
    public sealed class SqlProxyDataManager : IDataManager
    {
        private readonly SqlProxyDbConnection connection;
        private Dictionary<string, RecordFieldCache> recordFieldCache = new();

        public SqlProxyDataManager(SqlProxyDbConnection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public IDbConnection Connection => connection;
        public bool IsConnected => connection.State == ConnectionState.Open;
        public string? LastConnectionError { get; private set; }

        public bool Connect()
        {
            try
            {
                LastConnectionError = null;
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    ClearRecordFieldCache();
                }
                return true;
            }
            catch (Exception ex)
            {
                LastConnectionError = ex.Message;
                Debug.LogError($"SqlProxyDataManager connect failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
            ClearRecordFieldCache();
        }

        public void Dispose()
        {
            Disconnect();
            connection.Dispose();
        }

        public void ClearRecordFieldCache() => recordFieldCache.Clear();

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
        }

        private DataTable Query(string sql, IReadOnlyList<SqlProxyBind>? binds = null, int? maxRows = null)
        {
            EnsureConnected();
            return connection.ExecuteQuery(sql, binds ?? Array.Empty<SqlProxyBind>(), maxRows);
        }

        private static SqlProxyBind S(string? value) => SqlProxyBind.FromObject(value ?? string.Empty);
        private static SqlProxyBind I(int value) => SqlProxyBind.FromObject(value);

        // IDataManager methods: implement in subsequent tasks.
        // Until implemented, each method should still compile — port from Oracle in Tasks 7–10.

        public string GetSqlDefinition(string objectName) => throw new NotImplementedException();
        // ... temporarily throw for remaining members OR implement immediately in following tasks
    }
}
```

**Preferred:** Do not leave long-lived `NotImplementedException` on the branch — Task 6 only lands lifecycle + helpers + **empty method stubs that compile** if the next tasks run in the same PR stack; if committing Task 6 alone, either implement no-op safe returns for read methods that return empty collections, or combine Task 6+7 in one commit. **Plan requirement:** after Task 7, all methods used by attach path (`Connect`, `GetToolsVersion`) must work.

Minimum for Task 6 commit: class exists, `Connect`/`Disconnect`/`Dispose`/`IsConnected`/`Connection`/`LastConnectionError`/`ClearRecordFieldCache`/`Query` helpers compile; remaining `IDataManager` members can be stubbed with empty/safe defaults **only if** the next task lands in the same session. Otherwise implement GetToolsVersion in Task 6:

```csharp
public string GetToolsVersion()
{
    DataTable result = Query(SqlProxyQueryCatalog.GetToolsVersion);
    if (result.Rows.Count == 0) return string.Empty;
    return result.Rows[0][0]?.ToString() ?? string.Empty;
}
```

Copy private helpers from Oracle that are pure C# (`CombineBinaryData`, package path parsing helpers) as needed when methods land.

- [ ] **Step 2: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj -c Debug
```

- [ ] **Step 3: Commit**

```bash
git add AppRefiner/Database/SqlProxyDataManager.cs
git commit -m "Scaffold SqlProxyDataManager lifecycle over SqlProxyDbConnection"
```

---

### Task 7: Implement SQL/HTML/project/PeopleCode/app-class methods

**Files:**
- Modify: `AppRefiner/Database/SqlProxyDataManager.cs`

Port C# from Oracle for:

- `GetSqlDefinition` / `GetAllSqlDefinitions`
- `GetHtmlDefinition` / `GetAllHtmlDefinitions` (Unicode decode of CONTDATA bytes — same as Oracle)
- `GetPeopleCodeItemMetadataForProject`
- `LoadPeopleCodeItemContent` + private `GetNameReferencesForProgram`
- `GetPeopleCodeItemsForProject` (metadata then load)
- `CheckAppClassExists` / `GetAppClassSourceByPath` / `GetPeopleCodeProgram`
- `GetProgramObjectIds`
- `GetFunctionDefiningPrograms`

Pattern for GetSqlDefinition:

```csharp
public string GetSqlDefinition(string objectName)
{
    DataTable result = Query(
        SqlProxyQueryCatalog.GetSqlDefinition,
        new[] { S(objectName) });

    if (result.Rows.Count == 0) return string.Empty;

    var sqlDef = new StringBuilder();
    foreach (DataRow row in result.Rows)
    {
        sqlDef.Append(row["SQLTEXT"]);
    }
    return sqlDef.ToString();
}
```

For 14-bind program keys:

```csharp
var binds = new List<SqlProxyBind>(14);
for (int i = 0; i < 7; i++)
{
    int objId = i < programFields.Count ? programFields[i].Item1 : 0;
    string objVal = i < programFields.Count ? programFields[i].Item2 : "";
    binds.Add(I(objId));
    binds.Add(S(objVal));
}
DataTable programData = Query(SqlProxyQueryCatalog.LoadProgTxt, binds, maxRows: 10000);
```

- [ ] **Step 1: Implement methods**
- [ ] **Step 2: Build**
- [ ] **Step 3: Commit**

```bash
git commit -m "Implement SqlProxyDataManager PeopleCode and definition queries"
```

---

### Task 8: Implement record, package, field type, tools (if not done), message catalog

**Files:**
- Modify: `AppRefiner/Database/SqlProxyDataManager.cs`

Port:

- `GetRecordFields` + cache
- `GetAppPackageItems` (choose static SQL by package level — no string concat of SQL)
- `GetAllClassesForPackage` / `GetPackagesForClass`
- `GetFieldType`
- `GetMessageSets` / `GetMessagesForSet` / `GetMessageCatalogEntry`
- `SearchMessageCatalog` — **static SQL only**; filter sets and apply limit in C#:

```csharp
public List<MessageCatalogEntry> SearchMessageCatalog(
    string searchTerm, IReadOnlyCollection<int>? setNumbers, int limit)
{
    string pattern = $"%{searchTerm.ToUpperInvariant()}%";
    int fetchCap = limit;
    if (setNumbers != null && setNumbers.Count > 0)
    {
        // Over-fetch then filter; cap to avoid unbounded reads
        fetchCap = Math.Max(limit * 20, 500);
    }

    DataTable result = Query(
        SqlProxyQueryCatalog.SearchMessageCatalog,
        new[] { S(pattern) },
        maxRows: fetchCap);

    var setFilter = setNumbers != null && setNumbers.Count > 0
        ? new HashSet<int>(setNumbers)
        : null;

    var messages = new List<MessageCatalogEntry>();
    foreach (DataRow row in result.Rows)
    {
        int setNbr = Convert.ToInt32(row["MESSAGE_SET_NBR"]);
        if (setFilter != null && !setFilter.Contains(setNbr))
            continue;

        messages.Add(new MessageCatalogEntry(
            setNbr,
            Convert.ToInt32(row["MESSAGE_NBR"]),
            row["MSG_SEVERITY"].ToString() ?? string.Empty,
            row["MESSAGE_TEXT"].ToString() ?? string.Empty,
            row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty));

        if (messages.Count >= limit)
            break;
    }
    return messages;
}
```

- [ ] **Step 1: Implement**
- [ ] **Step 2: Build**
- [ ] **Step 3: Commit**

```bash
git commit -m "Implement SqlProxyDataManager record, package, and message catalog queries"
```

---

### Task 9: Implement event map + open targets

**Files:**
- Modify: `AppRefiner/Database/SqlProxyDataManager.cs`

**Event map:** Port `GetEventMapXrefs` and `GetEventMapItems` switch — assign SQL constant per type, build ordered binds, same row mapping as Oracle.

**Open targets:**

```csharp
public List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options)
{
    EnsureConnected();
    string idRaw = options.IdSearchTerm?.Trim() ?? string.Empty;
    string descrRaw = options.DescriptionSearchTerm?.Trim() ?? string.Empty;
    // Escape LIKE wildcards if Oracle did — port same escaping helper

    bool idEmpty = idRaw.Length == 0;
    bool descrEmpty = descrRaw.Length == 0;
    string idPattern = idEmpty ? "%" : EscapeLike(idRaw) + "%";      // match Oracle prefix semantics per type
    string descrPattern = descrEmpty ? "%" : "%" + EscapeLike(descrRaw) + "%";

    // Per Oracle GetOpenTargets: some types use prefix on ID, contains on descr — keep patterns consistent with converted SQL.

    var binds = new SqlProxyBind[]
    {
        S(idEmpty ? "Y" : "N"),
        S(idPattern),
        S(descrEmpty ? "Y" : "N"),
        S(descrPattern),
    };

    int maxPerType = options.MaxRowsPerType > 0 ? options.MaxRowsPerType : 50;
    var results = new List<OpenTarget>();

    foreach (OpenTargetType type in options.EnabledTypes /* or however options expose types */)
    {
        string? sql = SqlProxyOpenTargetQueries.ForType(type);
        if (sql == null) continue;

        DataTable data = Query(sql, binds, maxRows: maxPerType);
        foreach (DataRow row in data.Rows)
        {
            // Map DEFN_TYPE/ID/DESCR/LASTUPDDTTM → OpenTarget like Oracle
            results.Add(/* ... */);
        }
    }

    // Optional: C# relevance sort if Oracle ranked by INSTR — simple contains/prefix score
    return results;
}
```

Read `OpenTargetSearchOptions` and Oracle `GetOpenTargets` carefully for enabled-type enumeration and `OpenTarget` constructor arguments — copy mapping logic, do not invent new field semantics.

Also port PeopleCode-specific open target queries if they are separate from `OpenTargetType` enum entries (Oracle inlines them when “Non Class PeopleCode” options enabled).

- [ ] **Step 1: Implement event map + open targets**
- [ ] **Step 2: Build**
- [ ] **Step 3: Commit**

```bash
git commit -m "Implement SqlProxyDataManager event map and open-target queries"
```

---

### Task 10: Wire attach path; remove Oracle proxy ctor

**Files:**
- Modify: `AppRefiner/MainForm.cs` (`TryAttachSqlProxyDataManager` ~4185–4222)
- Modify: `AppRefiner/Database/OraclePeopleSoftDataManager.cs`

- [ ] **Step 1: Replace attach method body**

```csharp
private void TryAttachSqlProxyDataManager(AppDesignerProcess process)
{
    if (process == null || process.DataManager != null)
    {
        return;
    }

    try
    {
        var connection = new SqlProxyDbConnection(process);
        var manager = new SqlProxyDataManager(connection);
        if (!manager.Connect())
        {
            Debug.Log($"TryAttachSqlProxyDataManager: connect failed for pid {process.ProcessId}: {manager.LastConnectionError}");
            manager.Dispose();
            return;
        }

        string? toolsVersion = null;
        try
        {
            toolsVersion = manager.GetToolsVersion();
        }
        catch (Exception ex)
        {
            Debug.Log($"TryAttachSqlProxyDataManager: GetToolsVersion skipped: {ex.Message}");
        }

        ApplyDatabaseConnection(process, manager, "App Designer SAM proxy", toolsVersion);
        Debug.Log($"TryAttachSqlProxyDataManager: attached SqlProxyDataManager for pid {process.ProcessId}");
    }
    catch (Exception ex)
    {
        Debug.Log($"TryAttachSqlProxyDataManager: failed for pid {process.ProcessId}: {ex.Message}");
    }
}
```

Add `using AppRefiner.Database.SqlProxy;` if not already present.

- [ ] **Step 2: Remove from `OraclePeopleSoftDataManager`**

- Delete `UsesSqlProxy` property.
- Delete constructor:

```csharp
public OraclePeopleSoftDataManager(AppDesignerProcess process, SqlProxyDialect dialect = SqlProxyDialect.Oracle)
```

- Remove unused `using AppRefiner.Database.SqlProxy` if nothing else needs it.
- Keep connection-string constructor and all Oracle SQL as-is for `DBConnectDialog`.

- [ ] **Step 3: Grep for remaining references**

```powershell
# From repo root — use ripgrep or IDE search
# pattern: UsesSqlProxy|OraclePeopleSoftDataManager\(.*process|SqlProxyDialect
```

Expected: no remaining process-based Oracle manager construction.

- [ ] **Step 4: Build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj -c Debug
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/MainForm.cs AppRefiner/Database/OraclePeopleSoftDataManager.cs AppRefiner/Database/SqlProxyDataManager.cs
git commit -m "Wire SqlProxyDataManager for SAM attach; drop Oracle proxy path"
```

---

### Task 11: Final verification and conversion INDEX completion

**Files:**
- Modify: `MetaSQL/conversions/INDEX.md` (all statuses `integrated`)
- Modify: `MetaSQL/conversions/README.md` if any process lessons learned

- [ ] **Step 1: Confirm every INDEX row is `integrated` and every meta SQL string matches catalog constants**

Spot-check at least:

| Query | Spot-check |
|-------|------------|
| GetToolsVersion | `%Concat`, `%Table(PSSTATUS)` |
| GetSqlDefinition | `:1`, dual `%Table` |
| SearchMessageCatalog | no IN list, no FETCH FIRST |
| GetOpenTargets ApprovalRuleSet | `%Table(APPR_RULE_HDR)` |
| LoadProgTxt | 14 positional binds |

- [ ] **Step 2: Full AppRefiner build**

```powershell
dotnet build AppRefiner/AppRefiner.csproj -c Debug
```

- [ ] **Step 3: Manual smoke (when App Designer available)**

1. Start App Designer, sign on, ensure hooks load.
2. Confirm instance shows DB connection via SAM proxy / tools version populated.
3. Exercise: open-by-name search, record field tooltip/autocomplete path, message catalog browse if connected through proxy, open one app class definition path that hits DB.

- [ ] **Step 4: Commit**

```bash
git add MetaSQL/conversions
git commit -m "Mark Meta-SQL conversions integrated for SqlProxyDataManager"
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| SqlProxyDataManager type | 6–9 |
| SqlProxyDbConnection only | 6, 10 |
| Static Meta-SQL | 3–5, 7–9 |
| Positional `:n` binds | 3–5, 7–9 |
| No dynamic IN / UNION SQL | 3, 8, 9 |
| `%Table` record rules | 3–4 |
| C# message set filter + limit | 8 |
| Per-type open targets + C# merge | 4, 5, 9 |
| maxRows for limits | 8–9 |
| Extract → convert → integrate | 1–5, 7–9, 11 |
| TryAttach wiring | 10 |
| Remove Oracle process ctor | 10 |
| Full IDataManager parity | 7–9 |
| Conversion folder for future SQL install | 1–4, 11 |

## Out of scope (do not do in this plan)

- Hook/IPC protocol changes
- Installing SQL into PeopleSoft `PSSQLDEFN`
- Refactoring Oracle/SQL Server managers onto a shared base
- Unit test project for DataManagers
- `build.ps1` / native hook rebuild (unless C# alone cannot run)

---

## Execution notes for subagents

- **Task 2–4** are documentation-heavy; parallelize by domain freely.
- **Task 5** depends on Task 3–4 content (may start partial catalog after Task 3, add open targets after Task 4).
- **Tasks 7–9** may run as sequential commits on one agent with Oracle file open for mapping logic.
- Never reintroduce named binds or `Dictionary` parameter execute for new manager queries.
- If `%Substring` / `%Upper` fail under SAM on a given site, document in the meta file Notes and fall back to plain `SUBSTRING`/`UPPER` **only if still static and portable** — do not reintroduce Oracle-only `DBMS_LOB` without a meta note and user approval.
