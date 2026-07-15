# SqlProxyDataManager Design

**Date:** 2026-07-14  
**Status:** Draft for review  
**Related:** `docs/sql-proxy/*`, `MetaSQL/`, `OraclePeopleSoftDataManager`, `SqlProxyDbConnection`

## Summary

Add a third `IDataManager` implementation, **`SqlProxyDataManager`**, that talks to PeopleSoft only through **`SqlProxyDbConnection`** (App Designer SAM session). All queries are **static Meta-SQL** with **positional binds** (`:1`, `:2`, …) so the same SQL text can later be installed as PeopleSoft SQL definitions and executed via the platform.

Oracle and SQL Server managers remain for direct connection-string use. The App Designer auto-attach path switches from Oracle+proxy to `SqlProxyDataManager`.

## Goals

1. Full **`IDataManager` parity** with `OraclePeopleSoftDataManager` in one delivery.
2. **Static** SQL only — fixed Meta-SQL strings; no dynamic fragment assembly.
3. **Positional binds only** — installable Meta-SQL / `SQLExec`-style, not ADO named parameters.
4. Accept **only** `SqlProxyDbConnection`.
5. Convert Oracle SQL using **Meta-SQL** (`%Table`, `%Concat`, `%Upper`, etc.) and portable constructs.
6. Work via extract → convert → integrate so conversions are reviewable (subagent-friendly).

## Non-goals

- Installing definitions into `PSSQLDEFN` in this change (strings must be *ready* for that).
- Changing the public `IDataManager` surface.
- Shared abstract base across Oracle / SQL Server / SqlProxy managers.
- Write/DML support (proxy remains SELECT-only).
- Replacing direct Oracle/SQL Server connection-string managers.

## Decisions (locked)

| Topic | Decision |
|-------|----------|
| Type name | `SqlProxyDataManager` |
| Connection | Constructor takes `SqlProxyDbConnection` only |
| Proxy path on Oracle | **Remove** `OraclePeopleSoftDataManager(AppDesignerProcess, …)` and `UsesSqlProxy` |
| Attach wiring | `TryAttachSqlProxyDataManager` builds `SqlProxyDbConnection` → `SqlProxyDataManager` → `Connect` |
| Binds | Positional `:1`…`:n` only; execute via ordered `IReadOnlyList<SqlProxyBind>` |
| Named binds | **Not used** — not valid installable Meta-SQL |
| Dynamic SQL | **Not used** — filters/limits/type selection live in C# |
| Oracleisms | Best-effort Meta-SQL + portable SQL; row caps via proxy `maxRows` + C# |
| Delivery | Full parity; workflow: extract SQLs → convert → integrate |
| Approach | Clone control-flow from Oracle; replace SQL text and bind style |

---

## Architecture

```
App Designer (signed on)
        │ SAM
        ▼
  native SqlProxy hook
        │ IPC
        ▼
  SqlProxyDbConnection  ◄── only connection type accepted
        │
        ▼
  SqlProxyDataManager : IDataManager
        │
        ▼
  editors / plugins / type resolver (unchanged consumers)
```

### Constructor and lifecycle

```csharp
public sealed class SqlProxyDataManager : IDataManager
{
    public SqlProxyDataManager(SqlProxyDbConnection connection) { ... }

    public IDbConnection Connection { get; }
    public bool IsConnected { get; }
    public string? LastConnectionError { get; }

    public bool Connect();      // connection.Open(); clear caches
    public void Disconnect();   // connection.Close(); clear caches
    public void Dispose();      // Disconnect and dispose owned connection
}
```

**Connect rules**

- Call `_connection.Open()` (attach + Ping).
- **Never** `ExecuteNonQuery` / `ALTER SESSION` — schema is the App Designer signed-on session.
- Clear record-field (and any similar) caches on connect/disconnect, matching Oracle.

**Connection ownership**

- Manager **owns** the `SqlProxyDbConnection` passed to the constructor: `Disconnect` closes it; `Dispose` closes and disposes it.
- Callers must not share one connection instance across managers.

**Dialect flag**

- `SqlProxyDialect` on the connection is irrelevant for this manager when SQL is already positional and binds are passed as an ordered list. Attach may still construct `SqlProxyDbConnection(process)` with a default; Meta-SQL expansion is SAM’s job.

### Execution pattern

```csharp
// Prefer explicit ordered binds — no Dictionary named rewrite for production queries
var binds = new List<SqlProxyBind>
{
    SqlProxyBind.FromObject(objectName),  // :1
};
DataTable result = _connection.ExecuteQuery(SqlProxyQueries.GetSqlDefinition, binds, maxRows: null);
```

Do **not** use `ExecuteQuery(sql, Dictionary<string, object>)` for manager queries; that path rewrites named/`?` placeholders and fights the Meta-SQL installability goal.

---

## Static Meta-SQL policy

### Allowed

- Fixed SQL string constants (or catalog of constants).
- Selecting among **predefined** static statements in C# (`switch` on event-map type, package level, open-target type).
- Positional binds `:1`, `:2`, …
- Meta-SQL constructs: `%Table`, `%Concat`, `%Upper`, `%Substring`, `%NumToChar`, `%FirstRows` (hint only), etc.
- Plain portable SQL fragments that are not Meta-SQL (e.g. `CASE`, `UNION ALL` *inside a fixed string*, `ORDER BY`) when no Meta-SQL equivalent is needed.

### Forbidden

- Interpolating identifiers, lists, or SQL fragments into the string (`$"… IN ({ids})"`, dynamic `UNION ALL` assembly, `FETCH FIRST {limit}`).
- Named binds (`:objectName`).
- Table names as physical `PS_…` in `%Table` — use **record** names only.

### Record / `%Table` rules

| Physical table | Record name for `%Table` |
|----------------|---------------------------|
| Starts with `PS_` (underscore) | Strip `PS_` → e.g. `PS_APPR_RULE_HDR` → `%Table(APPR_RULE_HDR)` |
| Starts with `PS` **without** underscore | Record name = table name → e.g. `%Table(PSSQLDEFN)`, `%Table(PSPCMPROG)` |

Always: `FROM %Table(RECNAME)` / joins via `%Table(...)`. Never quote record names. Never pass `PS_…` into `%Table`.

### Bind style

PeopleSoft / SAM positional form:

```sql
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM
```

C# bind list index `0` → `:1`, index `1` → `:2`, etc. Types: `string` → `SamBindStr`, `int` → `SamBindInt` via existing `SqlProxyBind.FromObject`.

---

## Replacing dynamic SQL with C#

### Message catalog search

**Oracle today:** splices `MESSAGE_SET_NBR IN (...)` and `FETCH FIRST {limit}`.

**SqlProxyDataManager:**

One static search statement (search term only), e.g.:

```sql
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE (%Upper(MESSAGE_TEXT) LIKE :1
        OR %Upper(%Substring(DESCRLONG, 1, 4000)) LIKE :1)
 ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
```

- `:1` = search pattern (preserve current uppercased `%term%` behavior in C#).
- If `setNumbers` is non-null/non-empty: filter rows in C#.
- Apply `limit` in C# after filter; set proxy `maxRows` high enough when filtering client-side, or `maxRows = limit` when no set filter.

Optional second static statement for single-set search (`AND MESSAGE_SET_NBR = :2`) may be added later; still fully static. No `IN`-list construction.

### Open targets

**Oracle today:** builds a CTE with `UNION ALL` of enabled type arms + window ranking.

**SqlProxyDataManager:**

- One **static** Meta-SQL constant per open-target type (and per non-class PeopleCode flavor currently inlined).
- C# loops enabled types from `OpenTargetSearchOptions`, runs each constant with binds for id/descr search patterns.
- Per-type `maxRows ≈ MaxRowsPerType`.
- Ranking / truncation that used `ROW_NUMBER() OVER (PARTITION BY DEFN_TYPE … INSTR …)` moves to C# (group by type, score prefix/contains, take top N). Per-type SQL may still `ORDER BY` identity columns for stable results.

### Acceptable static selection (not dynamic SQL)

- `GetEventMapItems`: `switch (type)` → different constant strings.
- Package browser: choose static “root subpackages” vs “nested subpackages” SQL by level.
- Any method that picks among fixed queries without concatenating SQL.

---

## Meta-SQL / portability conversion map

| Current (Oracle-oriented) | Prefer |
|---------------------------|--------|
| `FROM PSSQLDEFN` / `FROM PS_APPR_RULE_HDR` | `FROM %Table(PSSQLDEFN)` / `FROM %Table(APPR_RULE_HDR)` |
| `a \|\| b` | `a %Concat b` |
| `UPPER(x)` | `%Upper(x)` when appropriate |
| `SUBSTR` / `DBMS_LOB.SUBSTR` | `%Substring(...)` when suitable |
| `TO_CHAR(number)` | `%NumToChar(...)` |
| `TO_CHAR(long/clob)` | Prefer raw column + C# conversion when SAM returns long text |
| `FETCH FIRST n` / `ROWNUM` | Proxy `maxRows` + C# take; optional `%FirstRows(n)` as optimizer hint only, not the sole cap |
| `ROW_NUMBER() …` ranking | C# after per-type queries |
| `LENGTH` / `INSTR` / empty-search guards | Prefer C# preparing binds (e.g. empty id search → broad pattern) **or** keep simple portable predicates inside a **fixed** SQL string if still static |

Each converted query documents residual platform risk if any non-Meta-SQL construct remains.

---

## Row limits

| Scenario | Policy |
|----------|--------|
| Typical catalog reads | Connection `DefaultMaxRows` or explicit per-method cap |
| `SearchMessageCatalog` | C# `limit` + proxy `maxRows` (higher when set-filtering in C#) |
| `GetOpenTargets` | Per-type proxy `maxRows ≈ MaxRowsPerType` |
| Program / HTML multi-row blobs (`PROGTXT`, `CONTDATA`) | High ceiling so chunks for one object are not truncated |

Optional future IPC frame fields for harder fetch control are **out of scope** for v1 if existing `maxRows` is sufficient.

---

## File layout

### Conversion working area (extract / convert)

```
MetaSQL/
  README.md
  INDEX.md
  functions/                          # existing reference
  conversions/                        # NEW
    README.md                         # checklist + process
    INDEX.md                          # method → query id
    oracle/                           # extracted from OraclePeopleSoftDataManager
      <Method>__<queryId>.sql.md
    meta/                             # converted Meta-SQL
      <Method>__<queryId>.sql.md
```

**Per-query conversion file**

```markdown
# Method / Query id
## Purpose
## Binds (positional)
| # | CLR type | Meaning |
## Original Oracle SQL
## Meta-SQL (static, :n only)
## maxRows policy
## Notes / residual risks
```

### Application code

```
AppRefiner/Database/
  SqlProxyDataManager.cs              # IDataManager; flow + row mapping
  SqlProxyQueries/                    # static Meta-SQL string constants
    SqlProxyQueryCatalog.cs           # and/or partials by domain
  SqlProxy/                           # existing IPC connection stack
    SqlProxyDbConnection.cs
    ...
```

SQL constants in code should match `MetaSQL/conversions/meta/*` so installable definitions and runtime strings stay in sync.

### Call-site changes

| Location | Change |
|----------|--------|
| `MainForm.TryAttachSqlProxyDataManager` | `new SqlProxyDbConnection(process)` → `new SqlProxyDataManager(conn)` → Connect → ApplyDatabaseConnection |
| `OraclePeopleSoftDataManager` | Remove process/SqlProxy ctor and `UsesSqlProxy` |
| `DBConnectDialog` | Unchanged (Oracle/SQL Server connection strings only) |

---

## Implementation workflow

1. **Extract** — Inventory every Oracle `ExecuteQuery` (and multi-query methods). Write `MetaSQL/conversions/oracle/*` with method, purpose, original SQL, bind semantics.
2. **Convert** — Parallel conversion to static Meta-SQL + `:n` binds into `MetaSQL/conversions/meta/*`. Checklist: `%Table` record rules, no named binds, no dynamic fragments, column aliases preserved for C#.
3. **Scaffold** — Add `SqlProxyDataManager` + query catalog; Connect/Disconnect/Dispose; implement methods against converted SQL.
4. **C# filters** — Message catalog set/limit; open-target type loop, merge, rank, truncate.
5. **Wire** — Switch `TryAttachSqlProxyDataManager`; remove Oracle proxy path.
6. **Verify** — `dotnet build` AppRefiner; manual attach smoke when App Designer + proxy available; spot-check critical methods (tools version, SQL def, record fields, app class source, open targets).

## Success criteria

- [ ] `SqlProxyDataManager` implements full `IDataManager`.
- [ ] Constructor accepts only `SqlProxyDbConnection`.
- [ ] All production SQL is static Meta-SQL with `:1`… binds only.
- [ ] No dynamic `IN` lists, no dynamic `UNION` string building, no SQL interpolation of limits.
- [ ] `TryAttachSqlProxyDataManager` uses the new manager.
- [ ] Oracle manager no longer constructs or special-cases SqlProxy.
- [ ] Conversion folder documents every query for future SQL definition install.
- [ ] Project builds.

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Meta-SQL expansion differences across PT versions | Stick to common constructs (`%Table`, `%Concat`, `%Upper`); document edge cases |
| `%Substring` on long columns vs `DBMS_LOB.SUBSTR` | Validate on real SAM; fall back to C#-side truncate if needed |
| Open-target quality without SQL `INSTR` ranking | C# scoring on returned rows; enough `maxRows` per type |
| Client-side set filter for message search over-fetches | Higher `maxRows` when filtering; optional later static single-set SQL |
| Positional bind order bugs | Conversion files + unit-style review of bind tables; careful port of Oracle parameter order |
| Large open-target fan-out (N queries) | Acceptable for correctness/static purity; profile later if slow |

## Future (explicitly later)

- Client feature to install these SQL definitions into PeopleSoft and execute by SQLID.
- `%SQL(SQLID, …)` wrappers once definitions live in the catalog.
- IPC enhancements for fetch limits if `maxRows` is insufficient.
- Optional shared base for three managers only if duplication becomes painful after this lands.

---

## Appendix A: Oracle methods to port

Full surface from `OraclePeopleSoftDataManager` (parity target):

- Connect / Disconnect / Dispose / IsConnected / Connection  
- GetSqlDefinition / GetAllSqlDefinitions  
- GetHtmlDefinition / GetAllHtmlDefinitions  
- GetPeopleCodeItemsForProject / GetPeopleCodeItemMetadataForProject / LoadPeopleCodeItemContent  
- CheckAppClassExists / GetAppClassSourceByPath / GetPeopleCodeProgram  
- GetRecordFields / ClearRecordFieldCache  
- GetAppPackageItems / GetAllClassesForPackage / GetPackagesForClass  
- GetEventMapItems / GetEventMapXrefs  
- GetOpenTargets / GetFunctionDefiningPrograms  
- GetToolsVersion / GetProgramObjectIds / GetFieldType  
- GetMessageSets / GetMessagesForSet / GetMessageCatalogEntry / SearchMessageCatalog  

## Appendix B: Example conversion sketch

**Before (Oracle):**

```sql
SELECT B.SQLTEXT
FROM PSSQLDEFN A, PSSQLTEXTDEFN B
WHERE A.SQLID = B.SQLID
AND A.SQLID = :objectName
ORDER BY B.SEQNUM
```

**After (Meta-SQL, static, positional):**

```sql
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM
```

**Before (Oracle concat / tools version):**

```sql
SELECT TOOLSREL || '.' || PTPATCHREL FROM PSSTATUS
```

**After:**

```sql
SELECT TOOLSREL %Concat '.' %Concat PTPATCHREL FROM %Table(PSSTATUS)
```
