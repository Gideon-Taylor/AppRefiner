# SQL Proxy — DataManager Type Catalog

**Source:** `OraclePeopleSoftDataManager` / `SqlServerPeopleSoftDataManager` + `OracleDbConnection` / `SqlServerDbConnection`  
**Purpose:** Map what AppRefiner actually binds and reads so the SAM proxy exposes the right `SamBind*` / `SamBuf*` surface.  
**Last updated:** 2026-07-11

---

## 1. Parameter (bind) types

`IDbConnection.ExecuteQuery(sql, Dictionary<string, object>? parameters)` passes CLR objects through without explicit DbType. Drivers infer types.

| CLR type used | Where | SAM bind API | Notes |
|---------------|--------|--------------|--------|
| **`string`** | Almost all filters (SQLID, names, package paths, search terms, object values, event map fields, `"Y"`/`"N"` flags) | `SamBindStr` | Already implemented |
| **`int`** | `OBJECTID*`, `MESSAGE_SET_NBR`, `MESSAGE_NBR`, `MaxRowsPerType`, package level ints | `SamBindInt` (**`int*`**, not by value) | **Required next** |
| **`null` / absent** | Rare; parameters usually always supplied | skip / bind empty string | Prefer empty string for PS CHAR fields |

No use of `DateTime`, `decimal`, `long`, `bool`, or byte[] **as binds** in current DataManagers.

### Parameter *style* (not type)

| Backend | Placeholder style | Order |
|---------|-------------------|--------|
| Oracle | Named `:name` (`:objectName`, `:objId1`, …) | `BindByName = true` |
| SQL Server ODBC | Positional `?` | Dictionary insertion order matches `?` order |

SAM is **positional** (`:1`, `:2`, … via successive `SamBind*`).  
`SqlProxyDbConnection` must rewrite named/`?` parameters into ordered SAM binds.

---

## 2. Result (buffer) types

| How DataManagers consume | Typical columns | SAM buffer API | Priority |
|--------------------------|-----------------|----------------|----------|
| **`row["X"].ToString()`** / `?.ToString()` | SQLID, names, DESCR, severity text, package paths, OBJECTVALUE*, etc. | `SamBufStr` | Done |
| **`Convert.ToInt32(row["X"])`** | OBJECTTYPE, OBJECTID*, NAMENUM, MESSAGE_*_NBR, FIELDNUM, FIELDTYPE, LENGTH, DECIMALPOS, USEEDIT, VERSION, SEQNUM | `SamBufInt` **or** `SamBufStr` + parse | Int preferred; string parse works |
| **`(byte[])row["PROGTXT"]`** | PeopleCode program text (`PSPCMPROG`) | `SamBufLongRaw` | **Required** (multi-row chunks) |
| **`(byte[])row["CONTDATA"]`** then `Encoding.Unicode.GetString` | HTML content (`PSCONTENT`) | `SamBufLongRaw` | **Required** |
| **`DESCRLONG` as string** (nullable) | Message catalog long description | `SamBufLongText` or large `SamBufStr` | Prefer `SamBufLongText` if length varies |

No DateTime/decimal result consumption in current managers.

### Multi-row

Almost every catalog query returns **multiple rows** (definitions lists, program segments, message sets, open-target search, package classes, etc.).  
Proxy must loop `SamExecFetch` + `SamFetch` up to `maxRows` (not first-row-only).

### Column names

DataManagers index by **name** (`row["SQLTEXT"]`), not ordinal.  
Drivers fill names from the result set. The proxy does not yet describe columns; `SqlProxyDbConnection` will **parse the SELECT list** for simple catalog SQL (good enough for existing queries).

---

## 3. Non-SELECT usage today

| Call | Purpose | Proxy policy |
|------|---------|--------------|
| `ExecuteNonQuery("ALTER SESSION SET CURRENT_SCHEMA=…")` | Oracle connect bootstrap only | **Not needed** with SAM session (schema already set). `SqlProxyDbConnection.ExecuteNonQuery` → throw or no-op with clear error. |
| All other access | `SELECT …` | SELECT-only gate stays |

---

## 4. SAM surface required for DataManager parity

| API | Role | Status |
|-----|------|--------|
| `SamBindStr` | string binds | Done |
| `SamBindInt` | int binds | Needed |
| `SamBufStr` | string columns | Done |
| `SamBufInt` | int columns (optional if string parse OK) | Needed for fidelity |
| `SamBufLongRaw` | PROGTXT, CONTDATA | Needed |
| `SamBufLongText` | DESCRLONG (safe) | Needed |
| `SamFetch` | multi-row | Needed |
| `SamExecFetch` | first row | Done |

Not required for current managers: `SamBindDate*`, `SamBufDouble`, `SamBindTbl` / `SamBufTbl`, commit/rollback.

---

## 5. Cell type codes (wire protocol)

Used in extended ExecSql request/response:

| Code | Name | Bind | Buffer |
|-----:|------|------|--------|
| 0 | Null | — | empty cell |
| 1 | String | `SamBindStr` | `SamBufStr` |
| 2 | Int32 | `SamBindInt` | `SamBufInt` |
| 3 | LongRaw | — (bind later if needed) | `SamBufLongRaw` |
| 4 | LongText | — | `SamBufLongText` |

---

## 6. SqlProxyDbConnection contracts

| Concern | Rule |
|---------|------|
| **Output column types / names** | From **SAM describe** on the hook: `SamNumResultCols` + `SamDescribeCol` (Query / `psopnqry` family). Not inferred from SQL text or forced by the caller. Optional wire override for tests only. |
| **Dialect** | `SqlProxyDialect.Oracle` (`:name` rewrite) or `SqlProxyDialect.SqlServer` (`?` rewrite). Does **not** drive result typing. |
| **Bind types** | From **CLR type** of each dictionary value via `SqlProxyBind.FromObject` (`int` → `SamBindInt`, `string` → `SamBindStr`). Or pass `IReadOnlyList<SqlProxyBind>`. |
| **Describe order** | `SamCreate` → `SamSetSqlProc` → binds → describe → `SamBuf*` → `SamExecFetch` / `SamFetch`. If `SamNumResultCols` fails, try `SamCompileNow` once then retry. |

## 7. Open risks

1. **Long raw lifetime** — copy bytes out of SAM-owned memory before `SamDestroy`.  
2. **Empty PROGTXT rows** — `ppData`/length NULL/0; represent as empty `byte[0]`.  
3. **Max rows / map size** — open-target search can be large; cap with `Truncated` status.  
4. **SamExecFetch rc 8** — currently treated as zero rows; may need split vs real SQL errors.  
5. **Oracle vs SQL Server SQL text** — DataManagers already dialect-specific; connection `Dialect` must match.

---

*End of catalog.*
