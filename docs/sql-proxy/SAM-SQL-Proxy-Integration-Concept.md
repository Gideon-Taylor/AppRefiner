# SAM SQL Proxy — Integration Concept

**Status:** Concept / design notes (not implemented)  
**Context:** PeopleTools 8.61 App Designer (`pside.exe`)  
**Related:** [`SAM-SQL-API-Reference.md`](./SAM-SQL-API-Reference.md)  
**Last updated:** 2026-07-11

---

## 1. Purpose

Define a workable approach for a **DLL injected into the App Designer process** that:

1. Receives commands from an external controller via **Windows messages**
2. Executes **SQL against the already-signed-on PeopleSoft session** using the in-process **SAM** (`pssam.dll`) APIs
3. Returns results through **shared memory** (or equivalent IPC buffer)

The goal is a **SQL proxy** that does **not** open its own Oracle/ODBC connection. It reuses the session established at login (`SamOpen`), and only creates short-lived cursors (`SamCreate` / `SamDestroy`) for each request.

---

## 2. Background (why this is feasible)

### 2.1 How App Designer uses the database

```
External tools / IDE UI
        │
        ▼
   pssam.dll  (SQL Access Manager)
        │  session: SAMPARMS (SamOpen / SamClose, refcounted)
        │  cursors: HSAMTRAN (SamCreate / SamDestroy)
        ▼
   PS_sql* backend
        ├─ ORACLE   → psora64.dll → OCI
        └─ MICROSFT → psodbc64.dll → ODBC
```

- **Session** (`SAMPARMS`): created/refcounted by `SamOpen`, torn down by `SamClose` (e.g. on exit with `SamClose(NULL)`).
- **Cursor** (`HSAMTRAN`): opaque handle from `SamCreate`; used for one statement lifecycle; freed with `SamDestroy`.

Live tracing showed that normal post-login work (signon queries, page load/update, component load, etc.) repeatedly:

```c
SamCreate(&h, flags, L"CursorName", /* pParms */ NULL, NULL);
SamSetSqlProc(h, sql, srcFile, srcLine);
SamBind*(...);
SamBuf*(...);
SamExecFetch(h, 1);
SamDestroy(h);
```

`pParms == NULL` means **use the current/default session** — the one established at login.

Therefore a hook DLL can act as another in-process “manager”: open its own cursors on that session without calling `SamOpen`/`SamClose`.

### 2.2 What the hook must not confuse

| API | Object | Hook usage |
|-----|--------|------------|
| `SamOpen` / `SamClose` | Session | **Do not** close the shared session; avoid Open unless taking a carefully paired ref |
| `SamCreate` / `SamDestroy` | Cursor | **Yes** — per request |
| `psqodb64.dll` | External ODBC **driver** for Excel/etc. | **Not** the internal SQL path |
| Own OCI/ODBC connect | Separate connection | Unnecessary if SAM session is live |

Full API detail: see `SAM-SQL-API-Reference.md`.

---

## 3. Goals and non-goals

### 3.1 Goals

- Execute arbitrary (or allowlisted) SQL **as the signed-on App Designer identity**
- Synchronous request/response suitable for tooling (inspect data, diagnostics, automation)
- Minimal surface: load DLL → send message → read shared memory
- Prefer **reusing** existing `pssam.dll` exports already loaded in the process

### 3.2 Non-goals (initial)

- Replacing App Designer’s definition managers
- Full Meta-SQL authoring environment
- Multi-statement transaction manager UI
- Supporting all SAM types (long raw, bulk tbl, etc.) in v1
- Working when **not** signed in (unless explicitly designed later for appserver-only modes)

### 3.3 Security / operational warning

The proxy runs **inside** a trusted client process with the user’s DB credentials (connect-id / operator context). Anyone who can send messages to the hook and write the shared section can run SQL **as that session**. Treat as:

- Developer/debug tooling, or  
- Hardened with allowlists, auth on the control channel, and local-only ACL on shared memory

---

## 4. Architecture

```
┌──────────────────────────┐         ┌─────────────────────────────────────┐
│  External controller     │         │  pside.exe                          │
│  (tool / script / IDE)   │         │                                     │
│                          │  WM_*   │  ┌─────────────────────────────┐  │
│  1. Write request → map  │────────►│  │ Hook DLL                      │  │
│  2. SendMessage(hwnd)    │         │  │  WndProc / message handler    │  │
│  3. Read response ← map  │◄────────│  │         │                     │  │
└──────────────────────────┘         │  │         ▼                     │  │
                                     │  │  SamCreate / SetSql / Bind /   │  │
         Shared memory section       │  │  Buf / ExecFetch / Destroy     │  │
         (named file mapping)        │  │         │                     │  │
                                     │  │         ▼                     │  │
                                     │  │  pssam.dll (existing session) │  │
                                     │  └─────────────────────────────┘  │
                                     └─────────────────────────────────────┘
```

### 4.1 Components

| Component | Responsibility |
|-----------|----------------|
| **Injector / loader** | Load hook DLL into `pside` (existing mechanism) |
| **Hook DLL** | Create HWND (or subclass), handle messages, call SAM, fill shared memory |
| **Shared memory** | Request SQL/binds; response rows/status/errors |
| **External controller** | User-facing API; writes request; `SendMessage`; reads response |
| **pssam.dll** | Unmodified; already loaded after Tools start |

### 4.2 Control channel: Windows messages

**Recommended:** `SendMessage` (synchronous) to a window owned by the hook.

| Approach | Pros | Cons |
|----------|------|------|
| **`SendMessage` to hook HWND** | Runs on window’s thread; simple request/response; easy lifetime | Blocks sender until SQL completes |
| `PostMessage` + completion event | Non-blocking | Async protocol, cancel, harder errors |
| Named pipe / RPC only | Clean IPC | Still need in-process SAM calls on a safe thread |

**Window creation:** Create the HWND on the **UI/main thread** of App Designer (e.g. during DLL init via `EnumThreadWindows` + `SendMessage` to bootstrap, or a hook installed on an existing PS window that only dispatches proxy work onto the UI thread).

**Message design (example):**

```c
#define WM_SAMPROXY_EXEC   (WM_APP + 0x401)  // wParam/lParam unused or map cookie
#define WM_SAMPROXY_PING   (WM_APP + 0x402)  // liveness / signed-on check
#define WM_SAMPROXY_CANCEL (WM_APP + 0x403)  // optional, best-effort
```

Payload lives in shared memory, not in `lParam` pointers from another process (those addresses are invalid in `pside`).

### 4.3 Data channel: shared memory

Named section, e.g. `Local\PsSamProxy_<pid>` (or include a nonce from the injector).

**Suggested layout:**

```c
// All strings UTF-16LE unless noted. Offsets relative to map base.

struct SamProxyHeader {
    uint32_t magic;          // 'SAMP' 
    uint32_t version;        // 1
    uint32_t cbHeader;
    uint32_t cbTotal;

    uint32_t command;        // EXEC_SQL, PING, ...
    uint32_t status;         // IDLE, PENDING, OK, ERROR, NOT_SIGNED_ON, BUSY
    int32_t  sam_rc;         // raw SamExecFetch / SamCreate return if applicable
    uint32_t flags;          // SELECT_ONLY, etc.

    uint32_t sql_offset;     // wchar_t SQL
    uint32_t sql_cch;        // length in wchars (excl. NUL)

    uint32_t bind_count;
    uint32_t binds_offset;   // array of SamProxyBind

    uint32_t col_count;      // request: max cols to bind as string outs; response: actual
    uint32_t row_count;      // response
    uint32_t data_offset;    // response row major or column buffers
    uint32_t data_bytes;

    uint32_t error_offset;   // wchar_t message
    uint32_t error_cch;

    uint32_t reserved[8];
};

struct SamProxyBind {
    uint32_t type;           // v1: STR=1 only
    uint32_t flags;
    uint32_t value_offset;   // wchar_t
    uint32_t value_cch;
};
```

**Synchronization:**

- Mutex: `Local\PsSamProxyMtx_<pid>`  
- Controller: lock → write request → unlock → `SendMessage` → lock → read response  
- Hook: on message, lock only for copy-in/copy-out if needed; avoid holding mutex across long DB I/O if multiple waiters matter (single-flight is simpler: hold busy flag in header)

**Size:** start with 1–4 MB fixed map (enough for metadata + modest result sets). Stream large results later.

---

## 5. Session reuse model (core design question)

### 5.1 Answer

**Yes — the hook can leverage the existing SAM session created at login.**

Mechanism:

```c
HSAMTRAN h = 0;
// pParms NULL → default/current session (post-SamOpen)
SamCreate(&h, /*flags*/ 1, L"SqlProxy", /*pParms*/ NULL, /*ctx*/ NULL);
```

Optional explicit session pointer:

```c
SAMPARMS* p = SamGetSrcParms();  // if export resolved
SamCreate(&h, 1, L"SqlProxy", p, NULL);
```

### 5.2 Session vs cursor ownership

```
Login:     SamOpen(NULL / parms)     → session refcount++
App work:  SamCreate/Destroy × N     → cursors only
Hook:      SamCreate/Destroy × N     → cursors only  (same session)
Logout:    SamClose(NULL)            → session refcount--; last close tears down DB
```

| Hook should | Hook must not |
|-------------|----------------|
| `GetProcAddress` SAM exports | Assume `HSAMTRAN` is a pointer |
| `SamCreate` / `SamDestroy` per request | `SamClose` on the shared session |
| Gate on “signed on” | Open a second full app connection “for safety” without understanding refs |
| Prefer UI thread for SAM calls | Call SAM concurrently from random worker threads |

### 5.3 When session reuse is unavailable

- Before signon / after signoff  
- Failed or incomplete login  
- Unusual configurations (if any path uses only appserver without local SAM SQL — validate for target deploy)

Return status `NOT_SIGNED_ON` rather than crashing.

---

## 6. Per-request SQL execution sequence

### 6.1 v1 — minimal viable proxy

**Request:** one SQL string + optional string binds `:1`…`:n`  
**Response:** `sam_rc` + first row + string columns only (fixed max width)

```c
// All on UI thread; pssam already loaded
typedef int  (*PFN_SamCreate)(HSAMTRAN*, int, const wchar_t*, void* /*SAMPARMS*/, void*);
typedef int  (*PFN_SamSetSqlProc)(HSAMTRAN, const wchar_t*, const wchar_t*, int);
typedef void (*PFN_SamBindStr)(HSAMTRAN, const wchar_t*);
typedef void (*PFN_SamBufStr)(HSAMTRAN, wchar_t*, int /*cch*/);
typedef int  (*PFN_SamExecFetch)(HSAMTRAN, int);
typedef int  (*PFN_SamDestroy)(HSAMTRAN);
// optional: SamGetLastErrorText, SamGetSql, SamGetSrcParms

HSAMTRAN h = 0;
int rc = SamCreate(&h, 1, L"SqlProxy", NULL, NULL);
if (rc != 0 || h == 0) { status = ERROR; goto done; }

SamSetSqlProc(h, sqlW, L"SqlProxyHook", 0);

for each bind string:
    SamBindStr(h, bindW[i]);   // positional :1, :2, ...

wchar_t cols[MAX_COLS][MAX_CCH];
for (c = 0; c < req_cols; c++)
    SamBufStr(h, cols[c], MAX_CCH);

rc = SamExecFetch(h, 1);
// Map rc: 0 success with row; 8 failure; 3 may mean alternate row status — log raw

// Copy cols + rc into shared memory
SamDestroy(h);
h = 0;
```

### 6.2 v2 — multi-row

```c
rc = SamExecFetch(h, 1);
while (/* success with row */) {
    append_row_to_map();
    rc = SamFetch(h);   // public: HSAMTRAN only
}
```

Cap `row_count` and `data_bytes` to map size; set `STATUS_TRUNCATED` if needed.

### 6.3 v3 — richer types (optional)

| Need | SAM API |
|------|---------|
| Integers | `SamBindInt` / `SamBufInt` (`int*`, not by value) |
| Long text | `SamBufLongText(h, void** pp, int* pLen)` / `SamBindLongText(h, void** pp, …)` |
| Bulk struct | `SamBufTbl` / `SamBindTbl` — usually unnecessary for generic SQL proxy |

v1 should avoid longs/tbl; document as future.

### 6.4 DML and commits

- Default policy: **SELECT only** (parse or allowlist; reject `UPDATE`/`DELETE`/`INSERT`/`MERGE`/`ALTER`/…).
- If DML is ever enabled: define explicit `SamCommit` / `SamRollback` commands and document that work shares the **same** session as App Designer’s uncommitted changes.

---

## 7. Threading and reentrancy

### 7.1 Problem

SAM and the network stack appear to assume **serialized, UI-centric** use. Concurrent `Sam*` from a background thread while App Designer runs a page load can corrupt cursor state or crash.

### 7.2 Rules

1. **All `Sam*` calls for the proxy run on one designated thread** — ideally the App Designer UI thread.  
2. **Bootstrap:** inject → create hook window on UI thread (or subclass existing) → store `HWND` for controller.  
3. **External controller uses `SendMessage(hwnd, WM_SAMPROXY_*, …)`** so the handler runs on that thread.  
4. **Busy flag:** if a proxy call is already in progress, return `BUSY` (no nested SAM from proxy).  
5. **Reentrancy:** if AD pumps messages during a long query, a second `SendMessage` could nest — reject nested proxy entry with TLS `in_proxy` guard.

### 7.3 Anti-patterns

- `CreateThread` in the hook that calls `SamExecFetch`  
- `PostMessage` without a clear completion protocol  
- Calling SAM from the injector process (wrong address space)

---

## 8. Lifecycle and process events

```
pside start
  → hook load
  → create HWND / register class
  → open shared memory + mutex (named by pid)

user signs on
  → SamOpen (by AD) — session live
  → proxy PING returns SIGNED_ON

proxy EXEC requests
  → SamCreate…Destroy only

user closes AD
  → SamClose(NULL) on exit path
  → hook unload / process exit
  → controller sees SendMessage fail / window gone
```

**Controller behavior:**

- Discover `pid` of `pside`  
- Open `Local\PsSamProxy_<pid>` (created by hook)  
- Find HWND: `FindWindow` with unique class name, or pass HWND via a small handshake file/map field written at hook init  

---

## 9. Error handling

| Condition | `status` | Notes |
|-----------|----------|--------|
| Not signed on | `NOT_SIGNED_ON` | `SamCreate` fails or pre-check fails |
| SAM returns 8 | `SAM_ERROR` | Fill `sam_rc`; try `SamGetLastErrorText` if available |
| Timeout | Controller-side | `SendMessage` timeout APIs or async redesign |
| Map too small | `TRUNCATED` | Partial rows |
| Nested/busy | `BUSY` | Retry |
| Invalid UTF-16 / empty SQL | `BAD_REQUEST` | |

Always `SamDestroy` in a cleanup path if `SamCreate` succeeded (RAII / `__try/__finally`).

---

## 10. Security considerations

1. **Same trust as App Designer user** — full data access of that connect-id.  
2. **Shared memory ACL** — default DACL to current user only; random map suffix.  
3. **Optional allowlist** — table prefixes, read-only, max rows, max time.  
4. **No silent DML** in default build.  
5. **Audit log** (optional file): timestamp, sql hash, rc, rowcount.  
6. **Message HWND** — custom window class; ignore messages from unexpected sources if an auth token in the map does not match.

---

## 11. Implementation phases

### Phase 0 — Spike (half day)

- [ ] Inject empty DLL; prove `GetModuleHandle(L"pssam.dll")`  
- [ ] Resolve `SamCreate`, `SamSetSqlProc`, `SamBufStr`, `SamExecFetch`, `SamDestroy`  
- [ ] From UI thread only: hard-coded `SELECT 1 FROM PSSTATUS` (or DB-appropriate) after manual signon  
- [ ] Log `sam_rc` and one column  

### Phase 1 — Proxy v1

- [ ] Shared memory + mutex  
- [ ] Hook HWND + `WM_SAMPROXY_EXEC` / `PING`  
- [ ] Controller library (C#/C++/Python ctypes)  
- [ ] String binds + multi string columns, first row  
- [ ] SELECT-only guard  
- [ ] Document failure modes  

### Phase 2 — Hardening

- [ ] Multi-row + truncation  
- [ ] Error text via SAM  
- [ ] Busy/reentrancy guards  
- [ ] Signon detection  
- [ ] Allowlist / max rows  

### Phase 3 — Optional

- [ ] Typed binds (`SamBindInt`, …)  
- [ ] Explicit commit command (if ever required)  
- [ ] Cancel  
- [ ] Appserver-only validation  

---

## 12. API resolution checklist

From `pssam.dll` (names confirmed in 8.61 client):

| Export | v1 | Notes |
|--------|----|--------|
| `SamCreate` | Required | `(&h, flags, name, NULL, NULL)` |
| `SamSetSqlProc` | Required | `(h, sql, file, line)` |
| `SamBindStr` | Required | positional |
| `SamBufStr` | Required | `(h, buf, cchMax)` wchars |
| `SamExecFetch` | Required | `(h, 1)` typical |
| `SamFetch` | v2 | more rows |
| `SamDestroy` | Required | always pair with Create |
| `SamGetSrcParms` | Optional | explicit session |
| `SamGetLastErrorText` / `SamGetLastErrorSql` | Optional | better errors |
| `SamGetszDBType` / `SamGetDatabaseType` | Optional | dialect hints |
| `SamOpen` / `SamClose` | Avoid | session ownership |
| `SamCommit` / `SamRollback` | Later | DML only |

See `SAM-SQL-API-Reference.md` for full signatures and pitfalls (`HSAMTRAN` opaque, `SamBindInt` takes `int*`, long text uses `void**`, etc.).

---

## 13. Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Crash AD from off-thread SAM | UI-thread-only rule; no worker Sam* |
| Corrupt in-flight IDE query | Single-flight busy flag; short queries |
| Accidental production DML | SELECT-only default; allowlist |
| Session closed mid-call | Check signon; handle Create rc 8 |
| Shared memory hijack | Per-user ACL; random name; token |
| Oracle vs SQL Server SQL | Document; optional DBType in PING |
| Meta-SQL surprises | Prefer plain SQL in v1 |
| Handle leaks | Strict Destroy on all paths |

---

## 14. Success criteria

1. With App Designer signed on to Oracle or SQL Server, controller sends `SELECT …` and receives a correct first row via shared memory.  
2. No extra DB login appears in DB audit beyond the existing App Designer session.  
3. Closing App Designer does not require the hook to call `SamClose`.  
4. Under normal IDE use (open pages, save), proxy either succeeds or returns `BUSY` without crashing.  
5. When not signed on, proxy returns `NOT_SIGNED_ON` cleanly.

---

## 15. Open questions

1. Exact semantics of `SamCreate` **flags** (`0`, `1`, `4` observed) — use `1` until proven otherwise.  
2. Full meaning of `SamExecFetch` return `3` vs `0` for multi-row loops.  
3. Whether any Tools path uses a non-default `SAMPARMS` that `NULL` would miss (secondary signon / compare targets — may need `SamGetTgtParms` for target DB).  
4. Appserver-only client sessions: does local SAM still execute SQL or only route remotely? Validate if that deploy mode matters.  
5. Long-running queries and UI freeze under `SendMessage` — acceptable for v1 tooling?  

---

## 16. Summary

| Question | Decision |
|----------|----------|
| Reuse login `SamOpen` session? | **Yes** |
| How? | `SamCreate(..., pParms=NULL)` / `SamGetSrcParms()` |
| Own DB connection? | **No** |
| IPC? | Windows **message** + **shared memory** |
| Session close from hook? | **No** |
| Hardest engineering problem? | **Thread affinity / reentrancy**, not API discovery |

This concept is aligned with observed App Designer behavior: managers never own the connection layer directly; they borrow the session and allocate cursors. The SQL proxy is the same pattern behind a message-driven façade.

---

## 17. References

- [`SAM-SQL-API-Reference.md`](./SAM-SQL-API-Reference.md) — reverse-engineered SAM signatures and live trace notes  
- Modules: `pssam.dll`, `psora64.dll`, `psodbc64.dll`, `pside.exe`, `pssys.dll`, `pstls.dll`  
- PeopleTools 8.61 client tree analyzed under App Designer bin folder  

---

*End of concept document.*
