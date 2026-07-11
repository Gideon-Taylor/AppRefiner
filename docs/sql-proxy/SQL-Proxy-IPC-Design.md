# SQL Proxy IPC Design

**Status:** Design (approved for implementation planning)  
**Branch:** `feature/sql-proxy-ipc`  
**Related:** [`SAM-SQL-Proxy-Integration-Concept.md`](./SAM-SQL-Proxy-Integration-Concept.md), [`SAM-SQL-API-Reference.md`](./SAM-SQL-API-Reference.md)  
**Last updated:** 2026-07-11

---

## 1. Purpose

Provide a **cross-process SQL proxy** so AppRefiner can eventually run PeopleSoft catalog queries through the **already-signed-on App Designer session** (via in-process SAM / `pssam.dll`), instead of opening its own Oracle/ODBC connection.

This document locks the **IPC foundation and protocol**. Implementation of the first slice should enable a hand-crafted `ExecSql` from a C# `SqlProxy` class; it does **not** replace `IDataManager` yet and does **not** add an automatic smoke-test call site.

---

## 2. Goals and non-goals

### 2.1 Goals

- Duplex IPC usable only by the AppRefiner process that creates and bootstraps the channel (no attach-by-name endpoint).
- Clear, versioned request/response framing suitable for growth (multi-row, richer types later).
- All `Sam*` calls on App Designer’s **UI thread**, with correct isolation so thread messages are not an authorization boundary.
- **SELECT-only** enforcement before any SAM call (matches `IDataManager` read use-cases).
- C# `SqlProxy` API that can be attached manually after AppRefiner has initialized an App Designer instance.
- Clean teardown on dispose, process exit, and existing hook detach (`WM_AR_DETACH`).

### 2.2 Non-goals (this foundation)

- Replacing `IDataManager` / removing AppRefiner’s own DB connection.
- Multi-row streaming, typed binds, long text/raw, Meta-SQL authoring UI.
- Automatic test wiring after App Designer init (callers add that themselves).
- Supporting SQL when App Designer is not signed on (return a clear status instead).
- Defending against a same-user attacker who can inject into `pside` or call `Sam*` directly.

---

## 3. Threat model and security rules

### 3.1 What we optimize for

| Threat | Mitigation |
|--------|------------|
| Random local app finds a window/map/pipe by name and runs SQL | **No named listen endpoint.** Anonymous pipes only. |
| Open-source repo documents message IDs / protocol | Acceptable. Secrecy of opcodes is not a control. Runtime exclusivity is **who holds the pipe handles**. |
| Attacker `PostThreadMessage`s a “run SQL” message to AD’s UI thread | **Thread messages never authorize SQL.** They only pump an in-process work queue filled exclusively by the pipe reader. |
| Accidental / malicious DML via the proxy | **SELECT-only** gate in the hook before any `Sam*`. |

### 3.2 Non-negotiable rules

1. **Pipe = only request ingress.** Only data read from the AppRefiner-owned anonymous pipe ends may create work items.
2. **In-process queue = only `Sam*` input.** Work items live in hook-private heap under a lock; not in `WPARAM`/`LPARAM`.
3. **UI message = scheduling only.** e.g. `PostThreadMessage(WM_AR_SQLPROXY_PUMP)` means “dequeue and run pending items,” never “here is SQL.”
4. **SELECT-only.** After trim of leading whitespace, SQL must start with `SELECT` (case-insensitive) or the command fails with no SAM call.
5. **No `SamOpen` / `SamClose`.** Cursor-only: `SamCreate` / `SamDestroy` with default session (`pParms == NULL`).

### 3.3 Same-user ceiling

A process with full control of `pside` (inject, `WriteProcessMemory`, etc.) can always call SAM without this proxy. That is out of scope. The proxy must not add an *ambient* SQL surface beyond that baseline.

---

## 4. Architecture

```
AppRefiner                                      pside.exe + AppRefinerHook
──────────                                      ──────────────────────────
SqlProxy
  Create anonymous pipe pairs
  DuplicateHandle → pside
  PostThreadMessage(ATTACH, remote struct) ──►  GetMsgHook: take HANDLEs, start pipe thread
  WriteFile(request frames)  ───────────────►  Pipe thread: ReadFile, parse, enqueue WorkItem
                                               PostThreadMessage(PUMP)  // schedule only
                                               GetMsgHook(UI): dequeue → Sam* → fill result
  ReadFile(response frames)  ◄───────────────  Pipe thread: WriteFile result/error
  Dispose → close local ends                   broken pipe → cancel pending, stop thread
```

### 4.1 Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `SqlProxy` | AppRefiner (C#) | Create pipes, bootstrap attach, send/receive frames, public API |
| Bootstrap path | Existing inject + `GetMsgHook` | Deliver duplicated handle values once; not a SQL API |
| Pipe IO thread | Hook | Read requests, write responses, **only** producer of work items |
| Work queue | Hook | Thread-safe queue of `WorkItem`s |
| UI pump | `GetMsgHook` on AD main thread | Dequeue + run SAM; never trust message payload for SQL |
| SAM runner | Hook | Resolve `pssam.dll`, SELECT check, cursor lifecycle |

### 4.2 Why not a public HWND / named section

Earlier concept docs suggested a discoverable HWND + named shared memory. That is **rejected** for this design:

- Open source makes “undocumented HWND” meaningless as a boundary.
- Named objects are attach-by-name.
- Thread messages alone cannot carry authorization.

Anonymous pipes + private work queue address both discovery and false authorization.

---

## 5. Transport: anonymous pipes

### 5.1 Topology

Use **two unidirectional anonymous pipes** (simplest duplex on Windows):

| Direction | AppRefiner end | Hook end |
|-----------|----------------|----------|
| Requests | write | read |
| Responses | read | write |

(Alternatively one bidirectional pattern if preferred during implementation, as long as exclusivity and framing stay the same.)

### 5.2 Attach sequence

1. AppRefiner ensures hooks are installed for the target App Designer process (existing `EventHookInstaller` / process tracking).
2. `SqlProxy` creates the anonymous pipe pairs in the AppRefiner process.
3. `DuplicateHandle` into the App Designer process (`DUPLICATE_SAME_ACCESS`), obtaining handle values valid **in pside**.
4. AppRefiner closes its copies of the ends it duplicated away if those ends are only for the remote side (keep the ends AppRefiner will use).
5. Write a small **attach struct** into remote memory (existing remote-buffer patterns) or pass handle values in a tightly defined bootstrap buffer.
6. `PostThreadMessage(mainThreadId, WM_AR_SQLPROXY_ATTACH, …)` so `GetMsgHook` runs on the UI thread and:
   - Rejects if already attached (or replaces only after clean shutdown of prior session).
   - Stores the two hook-side `HANDLE`s.
   - Starts the pipe IO thread.
   - Optionally posts `Hello` / is ready for `Ping`.
7. AppRefiner uses only its local pipe ends thereafter; no further handle messages required for SQL.

Exact field layout of the attach struct is an implementation detail but must include at least:

- Protocol version the client expects
- Request-pipe read handle (hook)
- Response-pipe write handle (hook)
- Optional generation / cookie for debugging

### 5.3 Lifetime

| Event | Behavior |
|-------|----------|
| `SqlProxy.Dispose` / AppRefiner exit | Local ends closed → hook `ReadFile`/`WriteFile` fails → stop IO thread, fail pending work |
| `WM_AR_DETACH` / hook teardown | Stop proxy session before subclass teardown; close hook-side handles |
| AD process exit | Pipes break; AppRefiner sees failure on next IO |

---

## 6. UI thread marshaling

### 6.1 Mechanism

- Pipe thread **enqueues** a `WorkItem` (parsed request + result storage + completion signal).
- Pipe thread **`PostThreadMessage(uiThreadId, WM_AR_SQLPROXY_PUMP, 0, 0)`** (or equivalent) only to wake the UI thread.
- `GetMsgHook` on `PUMP`: under lock, dequeue all (or one) items; for each, run SAM runner; signal completion.
- Pipe thread waits on the item’s completion event (or equivalent), then writes the response frame.

### 6.2 What external `PostThreadMessage(PUMP)` can do

At worst: empty queue drain (no-op). It **must not** invent work items.

### 6.3 Reentrancy

- If the UI thread is already inside the SAM runner (`g_inSamProxy` / TLS), do not nest `Sam*`. Mark the item `Busy` or leave it queued until the outer pump finishes—prefer a simple **single-flight** model for v1: one active SAM execution; additional items wait on the queue until the current one completes, still only drained on the UI thread.
- Avoid calling SAM from the pipe thread under any circumstance.

---

## 7. Framing protocol

### 7.1 Frame header (both directions)

All multi-byte integers **little-endian**.

```c
struct ArSqlProxyFrameHeader {
    uint32_t magic;       // 'ARSQ' = 0x51535241  ('A','R','S','Q' little-endian)
    uint32_t version;     // 1
    uint32_t msgType;     // see §7.2
    uint32_t requestId;   // client-chosen; response echoes same id
    uint32_t flags;       // reserved, 0 in v1
    uint32_t payloadLen;  // bytes of payload immediately following header
};
// followed by payloadLen bytes
```

- **Max frame size** (header + payload): implementation cap (recommended **4 MiB**). Reject larger with `Error` / close session if hostile.
- Strings in payloads are **UTF-16LE** with explicit `uint32_t` character counts (not relying solely on NUL), plus optional trailing NUL for convenience—spec implementation should pick one style and use it consistently (**prefer length-prefixed without requiring interior NULs**).

### 7.2 Message types (v1)

| Value | Name | Direction | Purpose |
|------:|------|-----------|---------|
| 1 | `Hello` | either | Optional capability / version greeting |
| 2 | `Ping` | AppRefiner → hook | Liveness / session probe |
| 3 | `Pong` | hook → AppRefiner | Reply to `Ping` |
| 4 | `ExecSql` | AppRefiner → hook | Run one SELECT |
| 5 | `ExecSqlResult` | hook → AppRefiner | Rows / status |
| 6 | `Error` | hook → AppRefiner | Protocol or session error for a requestId |

### 7.3 Status codes

Shared enum (C# + C++ keep values identical):

| Value | Name | Meaning |
|------:|------|---------|
| 0 | `Ok` | Success |
| 1 | `NotAttached` | Proxy session not active |
| 2 | `NotSignedOn` | SAM session unavailable (`SamCreate` fail / pre-check) |
| 3 | `SamError` | SAM returned failure (see `sam_rc` / error text) |
| 4 | `Busy` | Could not run without nested SAM (v1 if needed) |
| 5 | `BadRequest` | Malformed frame / empty SQL / invalid counts |
| 6 | `Rejected` | **SELECT-only** (or other policy) rejection |
| 7 | `Truncated` | Result hit max rows/bytes/columns |
| 8 | `VersionMismatch` | Unsupported protocol version |
| 9 | `Disconnected` | Pipe closed mid-operation |

### 7.4 Payload sketches

Payloads use sequential length-prefixed fields. Exact packing is fixed at implementation time in a shared comment block or header mirrored on both sides; conceptual contents:

**`Ping`:** empty or client tick (`uint64`).

**`Pong`:**

- `status` (`uint32`)
- optional `dbType` hint length + UTF-16 (if cheap via SAM later; empty in first spike OK)

**`ExecSql`:**

- `flags` (`uint32`, reserved 0)
- `sqlCch` + `sql` (UTF-16)
- `bindCount`
- for each bind: `cch` + UTF-16 string (positional `:1`…`:n`)
- `maxColumns` (`uint32`)
- `maxColumnChars` (`uint32`) — capacity per string column for `SamBufStr`
- `maxRows` (`uint32`) — v1 may force/clamp to 1

**`ExecSqlResult`:**

- `status`
- `sam_rc` (`int32`)
- `errorCch` + error text (may be empty)
- `columnCount`, `rowCount`
- for each row × column: `cch` + UTF-16 cell (empty string if null/unavailable)

**`Error`:**

- `status`
- `messageCch` + UTF-16

### 7.5 Correlation

- Client assigns monotonic `requestId` per outstanding request.
- v1 may be **strictly synchronous** on the C# side (one in-flight request per `SqlProxy` instance) even though the frame supports ids for later pipelining.

---

## 8. SELECT-only policy

### 8.1 Rule

Before any `SamCreate`:

1. Take the SQL string from the request.
2. Trim leading whitespace (space, tab, CR, LF).
3. If the remaining text does **not** start with `SELECT` when compared **case-insensitively** as a whole-word prefix of the first token (`SELECT` followed by end or whitespace), set status **`Rejected`** and return.
4. Do not call any SAM API for that request.

Recommended check: after left-trim, require `SELECT` case-insensitive match on the first six characters **and** (length == 6 or character 7 is whitespace). This rejects `SELECTFOO` as a single token abuse while allowing `SELECT * …`.

### 8.2 Rationale

Current `IDataManager` / `OraclePeopleSoftDataManager` data paths are read queries (`SELECT …`). The only non-query today is `ALTER SESSION` on AppRefiner’s own connection for schema—which is unnecessary when reusing the App Designer session.

### 8.3 Enforcement location

- **Hook (authoritative).**
- Optional duplicate check in C# `SqlProxy.ExecSql` for fast failure and clearer client exceptions.

### 8.4 Explicitly out of scope for v1

- Full SQL parsing, comment-stripping (`/* */`, `--`), or `WITH … SELECT` CTE support. If a future catalog query needs CTEs, extend the policy deliberately.

---

## 9. SAM execution (v1)

### 9.1 Resolution

On first use (or attach), `GetModuleHandleW(L"pssam.dll")` + `GetProcAddress` for at least:

| Export | Required |
|--------|----------|
| `SamCreate` | Yes |
| `SamSetSqlProc` | Yes |
| `SamBindStr` | Yes |
| `SamBufStr` | Yes |
| `SamExecFetch` | Yes |
| `SamDestroy` | Yes |
| `SamFetch` | Optional (multi-row later) |
| `SamGetLastErrorText` / related | Optional |

### 9.2 Per-request sequence (UI thread)

```text
if !SelectOnlyAllowed(sql) → Rejected; return

HSAMTRAN h = 0
rc = SamCreate(&h, 1, L"AppRefinerSqlProxy", NULL, NULL)
if rc != 0 or h == 0 → NotSignedOn or SamError; return

try:
  SamSetSqlProc(h, sql, L"AppRefinerSqlProxy", 0)
  for each bind: SamBindStr(h, bind)
  allocate col buffers [maxColumns][maxColumnChars]
  for c in 0..maxColumns-1: SamBufStr(h, cols[c], maxColumnChars)
  rc = SamExecFetch(h, 1)
  map rc + buffers into ExecSqlResult (rowCount 0 or 1 in v1)
finally:
  SamDestroy(h)
```

### 9.3 Pitfalls (from SAM reference)

- `HSAMTRAN` is opaque—never dereference.
- `SamBindInt` takes `int*` (not used in v1 string-only binds).
- String capacities for `SamBufStr` are in **wchars**.
- Do not call `SamClose` on the shared session.

---

## 10. C# API surface

```csharp
namespace AppRefiner.Database.SqlProxy; // or AppRefiner.SqlProxy

public sealed class SqlProxy : IDisposable
{
    /// <summary>
    /// Creates anonymous pipes, duplicates ends into the target process,
    /// and bootstraps the hook-side session. Returns null on failure.
    /// </summary>
    public static SqlProxy? TryAttach(AppDesignerProcess process);

    public bool IsAttached { get; }

    public SqlProxyPong Ping(CancellationToken cancellationToken = default);

    public SqlProxyResult ExecSql(
        string sql,
        IReadOnlyList<string>? binds = null,
        int maxColumns = 32,
        int maxColumnChars = 256,
        int maxRows = 1,
        CancellationToken cancellationToken = default);

    public void Dispose();
}

public sealed class SqlProxyPong
{
    public SqlProxyStatus Status { get; init; }
    // optional fields later
}

public sealed class SqlProxyResult
{
    public SqlProxyStatus Status { get; init; }
    public int SamReturnCode { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string>? ColumnValues { get; init; } // v1: first row, flat list
    // multi-row shape can replace this later without changing transport magic
}
```

### 10.1 Integration expectations

- Callers obtain an `AppDesignerProcess` that already has hooks installed.
- **No** built-in call after init in this work item; manual test code may call `TryAttach` + `ExecSql` elsewhere.
- Threading: public methods may block on pipe IO; do not call from a context that cannot tolerate waiting on AD UI (same as today’s cross-process `SendMessage` patterns). Document that `ExecSql` runs SAM on the AD UI thread and can stall that thread for the duration of the query.

---

## 11. Hook integration points

| Item | Notes |
|------|--------|
| New messages | `WM_AR_SQLPROXY_ATTACH`, `WM_AR_SQLPROXY_PUMP` in `Common.h` (values chosen not to collide with existing `WM_USER+1002…1012` and SCN range) |
| `GetMsgHook` | Handle ATTACH + PUMP; PUMP only dequeues |
| `TearDownAndUnload` | Shut down SqlProxy session first |
| New files (suggested) | `SqlProxyProtocol.h`, `SqlProxyIpc.cpp/.h`, `SqlProxySam.cpp/.h` |
| Logging | `OutputDebugString` for attach/detach/errors (consistent with hook style) |

C# side should define matching constants next to other hook messages (`EventHookInstaller` / shared constants).

---

## 12. Implementation phases (for planning)

### Phase A — Transport + Ping (no SAM)

- Anonymous pipes + attach + frame read/write
- `Ping` / `Pong`
- Teardown
- C# `SqlProxy.TryAttach` + `Ping`

### Phase B — ExecSql SELECT spike

- Work queue + UI pump
- SELECT-only gate
- SAM resolve + first-row string Exec
- C# `ExecSql`

### Phase C — Later (out of this foundation’s required ship)

- Multi-row, truncation details, error text from SAM
- `IDataManager` implementation over `SqlProxy`
- Hardening (timeouts, cancel, allowlists beyond SELECT)

---

## 13. Success criteria

1. With App Designer signed on and hooks installed, `SqlProxy.TryAttach` succeeds and `Ping` returns `Ok` (or a dedicated not-signed-on path is clean when logged out).
2. `ExecSql` with a simple `SELECT …` returns status `Ok` and at least one string cell without AppRefiner opening its own DB connection.
3. `ExecSql` with `DELETE …` / `UPDATE …` returns `Rejected` and performs no SAM execute.
4. `PostThreadMessage(PUMP)` without prior pipe-enqueued work never runs SQL.
5. `Dispose` / detach does not crash AD; subsequent attach can be defined as supported or explicitly unsupported in v1 (prefer: clean re-attach after dispose).

---

## 14. Open implementation choices (non-blocking)

These may be decided during coding without changing the architecture:

1. One vs two anonymous pipe pairs for duplex.
2. Whether `Hello` is mandatory on attach or `Ping` alone is enough for the spike.
3. Exact remote attach struct layout and whether ACK is a pipe `Hello` vs attach message return value.
4. Clamp policy when client sends `maxRows > 1` in v1 (force 1 vs return `BadRequest`).
5. Whether sign-on detection is “SamCreate failed” only or a lighter pre-check.

---

## 15. Summary of decisions

| Topic | Decision |
|-------|----------|
| Transport | Anonymous pipes + `DuplicateHandle` bootstrap |
| Named pipe / HWND / named map as public SQL API | **No** |
| Who may enqueue SQL work | Pipe reader thread only |
| UI marshaling | `PostThreadMessage(PUMP)` + `GetMsgHook` dequeue |
| Message params as SQL | **Never** |
| SAM session | Reuse login session; cursor per request |
| DML | **Rejected**; must start with `SELECT` |
| First client | C# `SqlProxy`; manual test by developer |
| `IDataManager` replacement | Future phase |

---

## 16. References

- [`SAM-SQL-Proxy-Integration-Concept.md`](./SAM-SQL-Proxy-Integration-Concept.md) — earlier concept (HWND/shared memory superseded for exclusivity)
- [`SAM-SQL-API-Reference.md`](./SAM-SQL-API-Reference.md) — SAM signatures and pitfalls
- Existing hook IPC: `AppRefinerHook/HookManager.cpp` (`GetMsgHook`), `AppRefiner/Events/EventHookInstaller.cs`

---

*End of design document.*
