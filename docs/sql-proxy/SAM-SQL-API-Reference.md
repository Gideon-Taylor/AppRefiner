# PeopleTools SAM SQL API Reference

**Target:** PeopleTools 8.61 client (`pside.exe` / App Designer)  
**Primary module:** `pssam.dll` (SQL Access Manager)  
**Method:** Static PE/string analysis + live x64dbg tracing (Windows x64)  
**Last updated:** 2026-07-11

This document captures reverse-engineered calling conventions and usage patterns for the SAM (`Sam*`) SQL surface used by App Designer and related tools. It is intended for integration / tooling work (hooks, interceptors, documentation), not as official Oracle documentation.

---

## 1. Architecture overview

```
pside.exe / pssys / psmgr / definition managers
        │
        │  SamCreate / SamSetSqlProc / SamBind* / SamBuf* / SamExecFetch / SamDestroy
        ▼
   pssam.dll  (SQL Access Manager)
        │  Meta-SQL expansion, SQLID catalog, cursor state (HSAMTRAN)
        │  LoadLibrary + GetProcAddress of network backend
        ▼
   PS_sql* interface  (shared function table)
        │
        ├─ DBType ORACLE   → psora64.dll → OCI.dll
        └─ DBType MICROSFT → psodbc64.dll → ODBC32.dll → SQL Server driver
```

### Layers

| Layer | Module(s) | Role |
|-------|-----------|------|
| App / managers | `pside`, `pssys`, `psmgr`, `psfld`, `psded`, `psprj`, `psbufapi`, … | Business objects; never talk OCI/ODBC directly |
| **SAM** | `pssam.dll` | Cursors, binds/buffers, Meta-SQL, errors, 2-tier vs appserver |
| Network SQL API | `PS_sql*` exports | Portable SQL machine ops |
| DB plug-in | `psora64.dll` / `psodbc64.dll` | Oracle OCI vs ODBC |

### Related (do not confuse)

| DLL | Role |
|-----|------|
| `pssqlora.dll` / `pssqlodb.dll` | Higher-level `C_SQL*` COBOL/AE wrappers statically linked to `PSORA64` / `PSODBC64` |
| `psqodb64.dll` | **PeopleSoft ODBC driver** for external apps (Excel, etc.) — not App Designer’s internal SQL path |
| `sqlapi.exe` | Harness that exercises raw `PS_sql*` / platform selection |

---

## 2. Calling convention

- **ABI:** Windows x64 (`RCX`, `RDX`, `R8`, `R9`, then stack at `[rsp+0x28]` …)
- **Wide strings:** `const wchar_t*` (UTF-16LE) unless noted
- **Handles:** `HSAMTRAN` is an **opaque integer ID**, not a pointer  
  Examples seen: `0x10000`, `0x150000`, `0x1E0000`, `0x80001`  
  **Do not dereference** — pass through to other `Sam*` APIs
- **Return codes (partial):**
  - `0` — success (common)
  - `3` — alternate row/status (callers often branch on this)
  - `8` — failure / special (e.g. `SamCreate` fail; `SamExecFetch` skips fetch if `SamExec` returns 8)

---

## 3. Core types

### 3.1 `HSAMTRAN`

Opaque cursor / transaction token produced by `SamCreate` (written to `*phTran`).

Internal lookup maps the ID to a real object (function pointer table / bind-buffer table). Assert strings refer to fields such as:

- `lpSamParms` → `SAMPARMS*`
- `cur` — active cursor id
- `lpBbt` / `nBbtCount` — bind/buffer table

### 3.2 `SAMPARMS`

Connection / session parameters (DB type, connect options, unicode/LOB flags, etc.).

- `SamGetSrcParms()` / `SamGetTgtParms()` — source vs target DB (copy/compare/upgrade)
- `ISamSession::GetSession(SAMPARMS*)`
- `NULL` to many APIs means “use default / current session parms”
- DB type enum strings include: `ORACLE`, `MICROSFT`, `DB2ODBC`, `DB2400`, `DB2UNIX`, `APPSRV`, …

### 3.3 `SamFldDesc` (for `SamBindTbl` / `SamBufTbl`)

```c
// 12 bytes per entry, little-endian
struct SamFldDesc {
    int type;    // SAM field type (1-based; see §6)
    int offset;  // byte offset from base object/row
    int size;    // capacity: for type 1 (string) = wchar count (internally ×2 → bytes)
};
```

---

## 4. Cursor lifecycle (canonical pattern)

```c
HSAMTRAN h = 0;

// 1. Create cursor
SamCreate(&h, /*flags*/ 1, L"MyCursor", /*pParms*/ NULL, /*ctx*/ NULL);

// 2. Attach SQL (literal or later resolved / Meta-SQL)
SamSetSqlProc(h,
    L"SELECT OWNERID FROM PS.PSDBOWNER WHERE DBNAME=:1",
    __FILEW__, __LINE__);   // file/line are debug-only

// 3. Bind inputs in :1, :2, ... order (no bind index argument)
SamBindStr(h, L"CR92U022");

// 4. Register outputs in column order (or use SamBufTbl)
wchar_t owner[0x81];
SamBufStr(h, owner, 0x81);

// 5. Execute + first fetch
int rc = SamExecFetch(h, 1);
// owner now populated on success

// 6. Optional: more rows
// while (SamFetch(h) == 0) { ... }

// 7. Destroy
SamDestroy(h);
```

**Live end-to-end example (signon / `stmget.cpp`):**

```text
SamSetSqlProc(h, L"SELECT OWNERID FROM PS.PSDBOWNER WHERE DBNAME=:1", file, 0xC07)
SamBindStr(h, L"CR92U022")
SamBufStr(h, ownerBuf, 0x81)
SamExecFetch(h, 1)
// → ownerBuf = L"SYSADM"
```

Later in same signon session (same handle reused):

```text
SELECT OWNERID, TOOLSREL, TO_CHAR(...), ... FROM SYSADM.PSSTATUS  → TOOLSREL = L"8.61"
SELECT ... FROM SYSADM.PSOPRDEFN WHERE OPRID = :1
  SamBindStr(h, L"VP1")
  SamBufStr / SamBufInt mix for VERSION, password fields, ENCRYPTED, ACCTLOCK, ...
```

---

## 5. API catalog

### 5.1 Session / cursor management

#### `SamCreate` — **live confirmed**

```c
// returns 0 success, 8 failure
int SamCreate(
    HSAMTRAN*       phTran,     // RCX  OUT handle
    int             flags,      // RDX  observed: 0, 1, 4
    const wchar_t*  name,       // R8   cursor label (NOT a source filename)
    SAMPARMS*       pParms,     // R9   NULL → default session
    void*           ctx         // [rsp+0x28] often NULL; stored on tran+0x90
);
```

**Live name examples:**  
`L"PS General Routines"`, `L"Security Table Manager (Get)"`, `L"SQL Access Manager"`, `L"MGR"`

**Live result:** `*phTran = 0x80001`, `eax = 0`.

#### `SamCreateSecondary`

Present; creates a secondary cursor. Signature not fully live-confirmed; similar spirit to `SamCreate` with extra linkage to a primary.

#### `SamDestroy` — **live confirmed**

```c
int SamDestroy(HSAMTRAN hTran);   // RCX only
```

Also observed mass calls with `h == 0` during cleanup — treat as invalid/no-op-ish.

#### `SamClose` — **live confirmed** (session close, not cursor destroy)

```c
// RCX = SAMPARMS* ; NULL → use process default/global SAMPARMS
void SamClose(SAMPARMS* pParms /* optional, may be NULL */);
```

**Not the same as `SamDestroy`.**  
- `SamDestroy(hTran)` — free one **cursor** (`HSAMTRAN`)  
- `SamClose(pParms)` — release a **session** (`SAMPARMS`), refcounted

**Live stop (App Designer exit):**

| Reg | Value | Role |
|-----|-------|------|
| RCX | `0` (NULL) | use default session |
| RDX/R8/R9 | garbage | unused |

**Call stack:**  
`CSignonDefProc::~CSignonDefProc` → … → `CPSToolsApp::ExitInstance` → process teardown after closing App Designer.

**Caller:**

```asm
xor  ecx, ecx          ; pParms = NULL
call SamClose
```

**Implementation (summary):**

```c
void SamClose(SAMPARMS* p) {
    SAMPARMS* s = p ? p : &g_DefaultSamParms;  // e.g. pssam!0x...35F90
    if (s->refCount /* +0x1360 */ != 1) {
        s->refCount--;
        return;                 // still other opens
    }
    // last close: tear down connections, clear globals, free session resources
    ...
}
```

So nested `SamOpen`/`SamClose` pairs are refcounted on the `SAMPARMS` object; only the last close fully disconnects.

#### `SamOpen` — disasm (pair of `SamClose`)

```c
// RCX = SAMPARMS* (NULL → default); RDX = additional context (saved as rbp)
int SamOpen(SAMPARMS* pParms, void* ctx /* or related */);
```

Prologue mirrors Close: `cmovnz` selects non-null parms vs global default; increments refcount at `+0x1360` if already open, else full session init. Live confirm of `RDX` meaning still partial.

---

### 5.2 SQL attachment

#### `SamSetSqlProc` — **live confirmed**

```c
int SamSetSqlProc(
    HSAMTRAN        hTran,      // RCX  opaque handle
    const wchar_t*  sql,        // RDX  full SQL; may contain :1, :2, ...
    const wchar_t*  srcFile,    // R8   debug: e.g. "...\\stmget.cpp"
    int             srcLine     // R9   debug: e.g. 0xC07
);
```

**Notes:**

- `srcFile` / `srcLine` are instrumentation (PSAffirm-style), not required for SQL semantics.
- SAM may expand **Meta-SQL** (`%SQL`, `%Bind`, `%Join`, `DATEIN`/`DATEOUT`, …) using `eDBType`.
- Named SQL can be resolved from `PSSQLTEXTDEFN` by SQLID + DBTYPE.
- **Different meaning of R8 than `SamCreate`:** create = cursor **name**; set-sql = source **file**.

---

### 5.3 Bind APIs (inputs)

Binds are **positional**: first `SamBind*` → `:1`, second → `:2`, etc.  
No bind-index argument on the simple APIs.

Helper path uses internal mode **`edx = 2`** (bind).

#### Simple binds — `(HSAMTRAN h, const T* value)` — **RCX, RDX only**

| API | Value type | Internal type notes |
|-----|------------|---------------------|
| `SamBindStr` | `const wchar_t*` | tag `0x02` |
| `SamBindChar` | char-related | |
| `SamBindInt` | `const int*` (**pointer**, not by value) | tag `0x06`, size 4 |
| `SamBindLong` | `const long*` | |
| `SamBindShort` | short* | |
| `SamBindDouble` | double* | |
| `SamBindDate` | date struct* | |
| `SamBindDateTime` | datetime struct* | tag `0x19` |
| `SamBindTime` | time struct* | |
| `SamBindNumStr` | numeric-as-string | |
| `SamBindAtom` | atom* | |

**Live:** `SamBindStr(0x10000, L"CR92U022")`, `SamBindStr(0x10000, L"VP1")`.

#### `SamBindPswd` — **3 args**

```c
void SamBindPswd(HSAMTRAN h, const wchar_t* pswd, int cch);
// R8 = capacity in wchars; stored byte length = cch*2; tag 0x02; type id 0x0C
```

#### `SamBindLongText` — **live confirmed**

```c
void SamBindLongText(
    HSAMTRAN     hTran,     // RCX
    void**       ppData,    // RDX  address of pointer to long payload / long object
    void*        pLength,   // R8   optional length (often NULL)
    void*        pExtra     // R9   optional (often NULL); stored at desc+0x28
);
```

**Live stop (Page update — `CPdm::UpdateImp` / `PdmUpdate`, same handle family as BindTbl):**

| Reg | Value | Role |
|-----|-------|------|
| RCX | `0x10C0000` | handle |
| RDX | `0x24a9615c220` | `ppData` = `&obj->field` at **object+0x288** |
| R8 | `0` | length not passed |
| R9 | `0` | extra not passed |

`*ppData` → `0x24a97a7c878` (heap long object / payload; structured header + text-like content).

Caller only binds when the pointer is non-null:

```asm
cmp  qword ptr [r14+0x288], 0
jz   skip
xor  r9d, r9d
xor  r8d, r8d
lea  rdx, [r14+0x288]    ; &pointer_field  (void**)
mov  rcx, hTran
call SamBindLongText
```

Internal: type id **`0x08`**, tag **`0x16`**, stores `ppData` at `desc+0x10`, R8 at `+0x20`, R9 at `+0x28`. Mode bind (`edx=2`).

When R8/R9 are NULL, length is implied by the long object / SAM (not a separate byte count on this path).

#### `SamBindLongRaw` — disasm twin of LongText

```c
void SamBindLongRaw(HSAMTRAN h, void** ppData, void* pLength /*opt*/, void* pExtra /*opt*/);
// type id 0x09, tag 0x17; same four-arg / store layout as SamBindLongText
```

#### `SamBindTbl` — **live confirmed** (bulk bind)

```c
// Same four-arg shape as SamBufTbl; internal mode = 2 (bind), not 1 (buffer)
int SamBindTbl(
    HSAMTRAN           hTran,   // RCX  e.g. 0x10C0000
    void*              base,    // RDX  object/row with **input** values already filled
    const SamFldDesc*  descs,   // R8   often static .rdata in pssys (same layout as BufTbl)
    int                count    // R9  e.g. 0x53 = 83 fields
);
```

**Live stop (Page delete/update — `CPdm::DeleteImp` / `UpdateImp` / `PdmUpdate`):**

| Reg | Value | Role |
|-----|-------|------|
| RCX | `0x10C0000` | handle |
| RDX | `0x70f4ffd1b0` | base (stack-backed struct; values **pre-populated**) |
| R8 | `0x7ff8e10c0cf0` | descriptor table in `pssys` |
| R9 | `0x53` (83) | field count (loaded from a global in caller) |

Caller sketch:

```asm
; SamSetSqlProc ...
; SamBindStr(h, obj+0x140)   ; extra individual bind
; SamBufLong? / other setup ...
mov  r9d, [pssys!g_nBindCount]   ; 0x53
lea  r8,  [pssys!bindDescs]
lea  rdx, [rbp-0x20]             ; base (or stack image of fields)
mov  rcx, rdi                    ; hTran
call SamBindTbl
```

**vs `SamBufTbl`:**

| | `SamBindTbl` | `SamBufTbl` |
|--|--------------|-------------|
| Direction | **input** (read base → SQL binds) | **output** (SQL → write base) |
| Internal mode | `2` | `1` |
| Base before call | filled with values to send | often zeroed, filled after fetch |
| `SamFldDesc` | same 12-byte `{type, offset, size}` | same |

Live field types in first entries: short, int, long, char, atom — with non-zero values (e.g. ints `586`, long `1`), confirming bind-from-struct behavior.

**Semantics:** equivalent to looping `SamBind*` over `base + desc[i].offset` in descriptor order (bind positions `:1`, `:2`, …).

---

### 5.4 Buffer APIs (outputs)

Registered **before** exec/fetch. SAM writes results into caller storage (or pointer slots for longs).

Helper path uses internal mode **`edx = 1`** (buffer).

#### Fixed / simple buffers

| API | Signature | Notes |
|-----|-----------|--------|
| `SamBufStr` | `(h, wchar_t* buf, int cchMax)` | **Live.** tag `0x02`; byte size = `cch*2`. Capacities seen: `0x81`, `0x15`, `0x21`, `0x5C`, `0x41` |
| `SamBufPswd` | `(h, wchar_t* buf, int cchMax)` | same string machinery; type id `0x0C` |
| `SamBufInt` | `(h, int* out)` | **Live.** tag `0x06`, size 4 |
| `SamBufLong` | `(h, long* out)` | type id `0x05`, size 4 |
| `SamBufShort` | `(h, short* out)` | type id `0x0E`, size 2 |
| `SamBufDouble` | `(h, double* out)` | type id `0x06`/`tag 0x0A`, size 8 |
| `SamBufDate` | `(h, void* out)` | size `0x16` |
| `SamBufDateTime` | `(h, void* out)` | **Live in call chains.** size `0x36`, tag `0x19` |
| `SamBufTime` | `(h, void* out)` | size `0x20` |
| `SamBufChar` | `(h, char* out)` | tag `0x01` |
| `SamBufAtom` | `(h, void* out)` | type id `0x0F`, size 8 |
| `SamBufDecimal` | `(h, void* out, int r8, int r9)` | type id `0x07`; precision/scale-ish — not fully live-confirmed |

**Live results after fetch:** `L"SYSADM"`, `L"8.61"` written into `SamBufStr` buffers.

#### `SamBufLongText` — **live confirmed**

```c
void SamBufLongText(
    HSAMTRAN  hTran,      // RCX
    void**    ppData,     // RDX  OUT: pointer to long payload (or NULL)
    long*     pLength     // R8   OUT: length (0 if null/empty)
);
```

**Critical difference from `SamBufStr`:**  
`RDX` is a **pointer-sized OUT slot** (`void**`), not a large caller buffer.  
Caller pattern:

```asm
lea  r8,  [rsp+0x40]    ; pLength
lea  rdx, [rsp+0x48]    ; ppData (8-byte slot)
mov  rcx, h
call SamBufLongText
call SamExecFetch
```

Empty long (live): `*pLength == 0`, `*ppData == NULL`, `SamExecFetch` returned `0`.

Internal: type id `0x08`, `desc+0x10 = ppData`, `desc+0x20 = pLength`, fixed capacity `0`.

#### `SamBufLongRaw` — **live confirmed**

```c
void SamBufLongRaw(
    HSAMTRAN  hTran,      // RCX  e.g. 0xA10002
    void**    ppData,     // RDX  OUT pointer slot for binary long
    int*      pLength     // R8   OUT length (caller used 4-byte slot; packed next to ppData)
);
```

**Live stop (Component load — `CCrm::LoadDefn`):**

| Reg | Value | Role |
|-----|-------|------|
| RCX | `0xA10002` | handle |
| RDX | `0x70f4ff4ad0` | `ppData` (`[rbp-0x70]`) — pre-fetch junk/`0xC` in slot |
| R8 | `0x70f4ff4acc` | `pLength` (`[rbp-0x74]`) — **4 bytes below** `ppData`, value `0` |

Caller:

```asm
lea  r8,  [rbp-0x74]    ; pLength (int)
lea  rdx, [rbp-0x70]    ; ppData
mov  rcx, rsi           ; hTran
call SamBufLongRaw
```

Internal: type id **`0x09`**, `desc+0x10 = ppData`, `desc+0x20 = pLength`, fixed capacity `0` — same template as `SamBufLongText` (type id `0x08`).

**Note:** Length slot was tightly packed as **4-byte `int`** here; LongText sample used an 8-byte stack slot. Treat `pLength` as integer length OUT either way.

#### `SamBufTbl` — **live confirmed**

```c
int SamBufTbl(
    HSAMTRAN           hTran,   // RCX  e.g. 0x1E0000
    void*              base,    // RDX  object/row base (often still zeroed pre-fetch)
    const SamFldDesc*  descs,   // R8   often static .rdata in pssys
    int                count    // R9  e.g. 0x17 = 23 fields
);
```

**Semantics:** equivalent to a loop of individual `SamBuf*` on `base + desc[i].offset`.

**Live context:** `CPdm::LoadDefn` / `WriteSwap` — Page Definition Manager loading a page.

**Live descriptor sample (23 fields)** — types seen: `1` (str×31 wchar), `4` (int), `5` (long), `14` (short), `15` (atom×8).  
Max field end ≈ `0x3A8` into base.

**Caller mix:** bulk `SamBufTbl` plus extra individual `SamBindStr` / `SamBufLong` / `SamBufStr` / `SamBufDateTime` around it.

---

### 5.5 Execute / fetch

#### `SamExecFetch` — **live + disasm**

```c
int SamExecFetch(HSAMTRAN h, int flags);  // flags almost always 1; once seen 0
```

Implementation:

```c
int SamExecFetch(HSAMTRAN h, int flags) {
    int rc = SamExec(h, /*a2*/ 0, flags);
    if (rc == 8)
        return rc;
    return SamFetch(h);   // tail call
}
```

#### `SamExecMustFetch`

Same idea as `SamExecFetch`, but treats unsuccessful/empty fetch as error `8`.

#### `SamExec`

```c
int SamExec(HSAMTRAN h, int a2, int flags);
// When called from ExecFetch: a2=0, flags=incoming flags
```

#### `SamFetch`

```c
int SamFetch(HSAMTRAN h);   // public: RCX only
```

Internal calls from `SamExecFetch` may pass extra pointers in `RDX`/`R8` — not part of the public surface.

#### Other exec variants (partial)

| API | Notes |
|-----|--------|
| `SamExecAllowDup` | Insert path allowing duplicate-key semantics; takes flags in `EDX` |
| `SamExecForceBinds` | Re-exec with forced binds |
| `SamExecLongQuery` | Long-query path |
| `SamCompileNow` / `SamForceCompileNow` | Force prepare |

---

### 5.6 Transaction / utility

| API | Signature (best known) | Notes |
|-----|------------------------|--------|
| `SamCommit` | `(HSAMTRAN h)` | Commit UOW |
| `SamRollback` | `(HSAMTRAN h)` | Rollback |
| `SamFail` | effectively marks failure | little/no useful public args |
| `SamGetSql` | retrieve effective SQL text | error/debug |
| `SamGetLastError` / `SamGetLastErrorText` / `SamGetLastErrorSql` | error info | |
| `SamGetRow` | `(HSAMTRAN h)` → row count-ish | |
| `SamNumResultCols` | column count | |
| `SamDoesTableExist` | `(h, const wchar_t* tableName)` | convenience |
| `SamGetDatabaseType` / `SamGetszDBType` | DB type | |
| `SamGetSrcParms` / `SamGetTgtParms` | session parms | |
| `SamStartBulkInsert` / `SamStopBulkInsert` | array insert mode | |
| `SamBufferType` | buffer type helper | not fully documented |

---

## 6. Field type IDs (descriptor / desc+0x04)

Used by `SamBufTbl` / `SamBindTbl` and mirrored in individual `SamBuf*` / `SamBind*`:

| type | Role | Typical size field |
|-----:|------|--------------------|
| 1 | Wide string (`SamBufStr`) | wchar **count** (×2 → bytes internally) |
| 2 | Char | small |
| 3 | Date | `0x16` bytes |
| 4 | Int | 4 |
| 5 | Long (4-byte) | 4 |
| 6 | Double (related) | 8 |
| 7 | Decimal | precision/scale via extra args on `SamBufDecimal` |
| 8 | Long text | dynamic via `ppData`/`pLength` |
| 9 | Long raw | dynamic via `ppData`/`pLength` |
| 10 | Time | `0x20` |
| 11 | DateTime | `0x36` |
| 12 | Password string | like string + type id `0x0C` |
| 14 | Short | 2 |
| 15 | Atom | 8 |

Internal **tag** at descriptor `+0x08` (examples):  
`0x02` string/pswd, `0x06` int, `0x08`/`0x09` long text/raw type ids, `0x12` short, `0x16`/`0x17` bind long text/raw, `0x19` datetime, `0x1A` date, `0x1B` time.

---

## 7. Network backend (`PS_sql*`) — below SAM

SAM resolves and calls a shared table loaded from `PSORA` or `PSODBC` (+ `64` suffix on this build).

| `PS_sql*` | Rough role |
|-----------|------------|
| `PS_sqlini` | Init library |
| `PS_sqlcnc` / `PS_sqlcch` | Connect / connection handle |
| `PS_sqlcom` / `PS_sqlcex` | Compile / set statement |
| `PS_sqlbnn` / `PS_sqlssb` / … | Bind |
| `PS_sqlexe` | Execute |
| `PS_sqlfet` / `PS_sqlget` / `PS_sqlrow` | Fetch / get column |
| `PS_sqldes` / `PS_sqlnsi` | Describe / num columns |
| `PS_sqlcmt` / `PS_sqlrbk` | Commit / rollback |
| `PS_sqldis` / `PS_sqldon` / `PS_sqldch` | Disconnect / close |
| `PS_sqlerr` / `PS_sqlrcd` | Error |

**Oracle path:** `psora64.dll` → `OCI.dll`  
**SQL Server path:** `psodbc64.dll` → `ODBC32.dll` → MS ODBC driver  
Connect-style strings inside `psodbc64` include `DSN=...;UID=...;PWD=...;APP=PeopleSoft`.

Backend selection: `g_SamDbTypeMatrix` / `SamDbTypeInfo` keyed by profile **`DBType`** (`ORACLE` vs `MICROSFT`, etc.).

---

## 8. Who imports SAM (client tree)

Static dependents of `pssam.dll` include:

`pside.exe`, `pssys.dll`, `psmgr.dll`, `psbufapi.dll`, `psprj.dll`, `psbld.dll`, `psappeng.dll`, `psae.exe`, `psaed.dll`, `psded.dll`, `psfld.dll`, `psred.dll`, `psppr.dll`, `psqryapi.dll`, `psopnqry.dll`, …

Largest consumers of the SQL surface: **`pssys`**, **`psmgr`**.

---

## 9. App server vs 2-tier

Managers still call the same `Sam*` APIs. For Application Server signon, SAM conversation classes (`CSamConversation`, `CSamReqRepSvc`, `ReqRepService`) can ship logical operations over the net. Direct 2-tier uses the loaded `psora64`/`psodbc64` in-process.

---

## 10. Integration notes / pitfalls

1. **`HSAMTRAN` is not a pointer** — never `*(struct*)h`.
2. **`SamCreate` R8 ≠ `SamSetSqlProc` R8`** — name vs source file.
3. **Binds and buffers are ordered**, not named by index on simple APIs.
4. **Register all `SamBuf*` / `SamBufTbl` before `SamExecFetch`.**
5. **Long text/raw use `void**` + length pointer**, not `SamBufStr`-style fixed buffers.
6. **String capacity for `SamBufStr` is in wchars**, not bytes.
7. **`SamBindInt` takes `int*`**, not a raw integer in `RDX`.
8. Prefer **`SamExecFetch(h, 1)`** over raw `SamExec`/`SamFetch` unless you need the split.
9. Return code **`8`** is special across create/exec paths.
10. SQL may be **Oracle-flavored or Meta-SQL** even when the logical API is DB-agnostic; expansion is SAM’s job.

---

## 11. Confidence legend

| Level | Meaning |
|-------|---------|
| **Live confirmed** | Observed registers/stack/memory on real `pside` calls |
| **Disasm confirmed** | Clear from function body + consistent with live siblings |
| **Partial** | Shape known; some args or return enums incomplete |

| API / topic | Confidence |
|-------------|------------|
| Architecture, Oracle vs ODBC split | Live + static |
| `SamCreate`, `SamDestroy`, `SamSetSqlProc` | Live confirmed |
| `SamBindStr`, `SamBufStr`, `SamBufInt` | Live confirmed |
| `SamExecFetch` / `SamExec` / `SamFetch` chain | Live + disasm |
| `SamBufLongText` | Live confirmed |
| `SamBufLongRaw` | Live confirmed (same shape as LongText; type id `0x09`) |
| `SamBufTbl` + `SamFldDesc` | Live confirmed |
| Other simple `SamBind*`/`SamBuf*` | Disasm confirmed (same templates) |
| `SamBindLongText` | Live confirmed (`void**` + optional NULL length/extra) |
| `SamBindLongRaw` | Disasm twin of BindLongText; confirm live |
| `SamBindTbl` | Live confirmed (83-field page update/delete bind) |
| `SamClose` | Live confirmed (NULL = default session; refcount at SAMPARMS+0x1360) |
| `SamOpen` | Disasm pair of Close; RDX role partial |
| `SamBufDecimal`, AllowDup, ForceBinds | Partial |
| Exact `SAMPARMS` layout | Not fully mapped |
| Full return-code enum | Partial |

---

## 12. Suggested breakpoints (x64dbg)

Useful labels in `pssam.dll` (addresses ASLR — resolve via export):

- Lifecycle: `SamCreate`, `SamDestroy`, `SamSetSqlProc`
- Hot path: `SamExecFetch`, `SamBindStr`, `SamBufStr`
- Bulk: `SamBufTbl`, `SamBindTbl`
- Longs: `SamBufLongText`, `SamBufLongRaw`, `SamBindLongText`, `SamBindLongRaw`

---

## 13. Revision history

| Date | Notes |
|------|--------|
| 2026-07-11 | Initial write-up from PE analysis + x64dbg session on PT 8.61 App Designer: multi-DB architecture, full cursor lifecycle, Bind/Buf matrix, BufTbl descriptors, LongText pointer-slot semantics. |
| 2026-07-11 | Added live `SamBufLongRaw` confirmation (`CCrm::LoadDefn`); length slot observed as 4-byte int packed against `ppData`. |
| 2026-07-11 | Added live `SamBindTbl` confirmation (`CPdm::DeleteImp`/`UpdateImp`, 83 fields); same `SamFldDesc` as BufTbl, mode=2. |
| 2026-07-11 | Added live `SamBindLongText` (`CPdm::UpdateImp`): `void**` at obj+0x288, R8=R9=NULL when non-null long present. |
| 2026-07-11 | Added live `SamClose` on App Designer exit: `SamClose(NULL)` via signon dtor; session refcount teardown. |

---

*End of document.*
