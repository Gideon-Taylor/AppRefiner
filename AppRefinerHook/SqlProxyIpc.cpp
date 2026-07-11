#include "SqlProxyIpc.h"
#include "SqlProxySam.h"
#include "Common.h"

#include <cstring>
#include <deque>
#include <memory>
#include <string>
#include <vector>

namespace ArSqlProxy {

namespace {

struct WorkItem {
    uint32_t requestId = 0;
    MsgType msgType = MsgType::Ping;
    ExecRequest execRequest;
    ExecResult execResult;
    Status status = Status::Ok;
    std::wstring errorMessage;
    HANDLE doneEvent = nullptr;
};

CRITICAL_SECTION g_cs;
bool g_csInit = false;
HANDLE g_requestRead = nullptr;
HANDLE g_responseWrite = nullptr;
HANDLE g_ioThread = nullptr;
DWORD g_uiThreadId = 0;
volatile LONG g_running = 0;
volatile LONG g_inPump = 0;
std::deque<std::shared_ptr<WorkItem>> g_queue;

void EnsureCs() {
    if (!g_csInit) {
        InitializeCriticalSection(&g_cs);
        g_csInit = true;
    }
}

bool WriteAll(HANDLE h, const void* data, DWORD size) {
    const uint8_t* p = static_cast<const uint8_t*>(data);
    DWORD remaining = size;
    while (remaining > 0) {
        DWORD written = 0;
        if (!WriteFile(h, p, remaining, &written, nullptr) || written == 0) {
            return false;
        }
        p += written;
        remaining -= written;
    }
    return true;
}

bool ReadAll(HANDLE h, void* data, DWORD size) {
    uint8_t* p = static_cast<uint8_t*>(data);
    DWORD remaining = size;
    while (remaining > 0) {
        DWORD read = 0;
        if (!ReadFile(h, p, remaining, &read, nullptr) || read == 0) {
            return false;
        }
        p += read;
        remaining -= read;
    }
    return true;
}

bool WriteFrame(HANDLE h, MsgType type, uint32_t requestId, const std::vector<uint8_t>& payload) {
    if (payload.size() > kMaxFrameBytes - sizeof(FrameHeader)) {
        return false;
    }
    FrameHeader hdr{};
    hdr.magic = kMagic;
    hdr.version = kVersion;
    hdr.msgType = static_cast<uint32_t>(type);
    hdr.requestId = requestId;
    hdr.flags = 0;
    hdr.payloadLen = static_cast<uint32_t>(payload.size());
    if (!WriteAll(h, &hdr, sizeof(hdr))) {
        return false;
    }
    if (!payload.empty() && !WriteAll(h, payload.data(), static_cast<DWORD>(payload.size()))) {
        return false;
    }
    return true;
}

bool ReadFrame(HANDLE h, FrameHeader& hdr, std::vector<uint8_t>& payload) {
    if (!ReadAll(h, &hdr, sizeof(hdr))) {
        return false;
    }
    if (hdr.magic != kMagic || hdr.version != kVersion) {
        OutputDebugStringA("SqlProxyIpc: bad frame magic/version\n");
        return false;
    }
    if (hdr.payloadLen > kMaxFrameBytes - sizeof(FrameHeader)) {
        OutputDebugStringA("SqlProxyIpc: frame too large\n");
        return false;
    }
    payload.resize(hdr.payloadLen);
    if (hdr.payloadLen > 0 && !ReadAll(h, payload.data(), hdr.payloadLen)) {
        return false;
    }
    return true;
}

class PayloadReader {
public:
    explicit PayloadReader(const std::vector<uint8_t>& data) : m_data(data) {}

    bool ReadU32(uint32_t& v) {
        if (m_pos + 4 > m_data.size()) return false;
        std::memcpy(&v, m_data.data() + m_pos, 4);
        m_pos += 4;
        return true;
    }

    bool ReadI32(int32_t& v) {
        uint32_t u = 0;
        if (!ReadU32(u)) return false;
        v = static_cast<int32_t>(u);
        return true;
    }

    bool ReadWString(std::wstring& s) {
        uint32_t cch = 0;
        if (!ReadU32(cch)) return false;
        if (cch > kMaxSqlChars) return false;
        const size_t bytes = static_cast<size_t>(cch) * sizeof(wchar_t);
        if (m_pos + bytes > m_data.size()) return false;
        s.assign(reinterpret_cast<const wchar_t*>(m_data.data() + m_pos), cch);
        m_pos += bytes;
        return true;
    }

private:
    const std::vector<uint8_t>& m_data;
    size_t m_pos = 0;
};

class PayloadWriter {
public:
    void WriteU32(uint32_t v) {
        const size_t o = m_data.size();
        m_data.resize(o + 4);
        std::memcpy(m_data.data() + o, &v, 4);
    }

    void WriteI32(int32_t v) {
        WriteU32(static_cast<uint32_t>(v));
    }

    void WriteWString(const std::wstring& s) {
        WriteU32(static_cast<uint32_t>(s.size()));
        const size_t o = m_data.size();
        const size_t bytes = s.size() * sizeof(wchar_t);
        m_data.resize(o + bytes);
        if (bytes) {
            std::memcpy(m_data.data() + o, s.data(), bytes);
        }
    }

    void WriteBytes(const std::vector<uint8_t>& b) {
        WriteU32(static_cast<uint32_t>(b.size()));
        if (!b.empty()) {
            const size_t o = m_data.size();
            m_data.resize(o + b.size());
            std::memcpy(m_data.data() + o, b.data(), b.size());
        }
    }

    const std::vector<uint8_t>& Data() const { return m_data; }

private:
    std::vector<uint8_t> m_data;
};

bool ParseExecSql(const std::vector<uint8_t>& payload, ExecRequest& req, std::wstring& err) {
    PayloadReader r(payload);
    uint32_t flags = 0;
    if (!r.ReadU32(flags)) {
        err = L"ExecSql: missing flags";
        return false;
    }
    if (!r.ReadWString(req.sql)) {
        err = L"ExecSql: bad sql";
        return false;
    }

    uint32_t bindCount = 0;
    if (!r.ReadU32(bindCount) || bindCount > kMaxBinds) {
        err = L"ExecSql: bad bindCount";
        return false;
    }
    req.binds.clear();
    req.binds.reserve(bindCount);
    for (uint32_t i = 0; i < bindCount; ++i) {
        uint32_t bt = 0;
        if (!r.ReadU32(bt)) {
            err = L"ExecSql: bad bind type";
            return false;
        }
        BindValue b;
        b.type = static_cast<ValueType>(bt);
        if (b.type == ValueType::Int32) {
            if (!r.ReadI32(b.intValue)) {
                err = L"ExecSql: bad bind int";
                return false;
            }
        } else {
            // String (and Null as empty string)
            if (!r.ReadWString(b.strValue)) {
                err = L"ExecSql: bad bind string";
                return false;
            }
            if (b.type != ValueType::String && b.type != ValueType::Null) {
                b.type = ValueType::String;
            }
        }
        req.binds.push_back(std::move(b));
    }

    // colCount == 0 → hook uses SamNumResultCols / SamDescribeCol (preferred ad-hoc path)
    uint32_t colCount = 0;
    if (!r.ReadU32(colCount) || colCount > kMaxColumns) {
        err = L"ExecSql: bad colCount";
        return false;
    }
    req.columns.clear();
    if (colCount > 0) {
        req.columns.reserve(colCount);
        for (uint32_t i = 0; i < colCount; ++i) {
            uint32_t ct = 0, maxSize = 0;
            if (!r.ReadU32(ct) || !r.ReadU32(maxSize)) {
                err = L"ExecSql: bad column spec";
                return false;
            }
            ColumnSpec spec;
            spec.type = static_cast<ValueType>(ct);
            spec.maxSize = maxSize;
            req.columns.push_back(spec);
        }
    }

    if (!r.ReadU32(req.maxRows)) {
        err = L"ExecSql: missing maxRows";
        return false;
    }
    // Optional trailing defaultStringCch (older clients may omit — treat as 512)
    uint32_t defCch = 512;
    if (r.ReadU32(defCch)) {
        req.defaultStringCch = defCch;
    } else {
        req.defaultStringCch = 512;
    }
    return true;
}

std::vector<uint8_t> BuildPong(Status status) {
    PayloadWriter w;
    w.WriteU32(static_cast<uint32_t>(status));
    w.WriteWString(L"");
    return w.Data();
}

std::vector<uint8_t> BuildExecResult(const ExecResult& r) {
    PayloadWriter w;
    w.WriteU32(static_cast<uint32_t>(r.status));
    w.WriteI32(r.samRc);
    w.WriteWString(r.errorMessage);
    w.WriteU32(r.columnCount);
    // Column metadata (from SamDescribeCol or override) before row data
    for (uint32_t i = 0; i < r.columnCount; ++i) {
        if (i < r.columns.size()) {
            w.WriteWString(r.columns[i].name);
            w.WriteU32(static_cast<uint32_t>(r.columns[i].type));
            w.WriteI32(r.columns[i].samTypeId);
        } else {
            w.WriteWString(L"");
            w.WriteU32(static_cast<uint32_t>(ValueType::String));
            w.WriteI32(0);
        }
    }
    w.WriteU32(r.rowCount);
    const size_t expected = static_cast<size_t>(r.columnCount) * static_cast<size_t>(r.rowCount);
    for (size_t i = 0; i < expected; ++i) {
        if (i >= r.cells.size()) {
            w.WriteU32(static_cast<uint32_t>(ValueType::Null));
            continue;
        }
        const CellValue& c = r.cells[i];
        w.WriteU32(static_cast<uint32_t>(c.type));
        switch (c.type) {
        case ValueType::String:
        case ValueType::LongText:
            // LongText also carries strValue when decodable
            w.WriteWString(c.strValue);
            if (c.type == ValueType::LongText) {
                w.WriteBytes(c.bytes);
            }
            break;
        case ValueType::Int32:
            w.WriteI32(c.intValue);
            break;
        case ValueType::LongRaw:
            w.WriteBytes(c.bytes);
            break;
        default:
            break; // Null: type only
        }
    }
    return w.Data();
}

std::vector<uint8_t> BuildError(Status status, const std::wstring& message) {
    PayloadWriter w;
    w.WriteU32(static_cast<uint32_t>(status));
    w.WriteWString(message);
    return w.Data();
}

void FailAllPending(Status status, const std::wstring& message) {
    EnsureCs();
    EnterCriticalSection(&g_cs);
    while (!g_queue.empty()) {
        auto item = g_queue.front();
        g_queue.pop_front();
        item->status = status;
        item->errorMessage = message;
        item->execResult.status = status;
        item->execResult.errorMessage = message;
        if (item->doneEvent) {
            SetEvent(item->doneEvent);
        }
    }
    LeaveCriticalSection(&g_cs);
}

void CloseHandles_NoLock() {
    if (g_requestRead) {
        CloseHandle(g_requestRead);
        g_requestRead = nullptr;
    }
    if (g_responseWrite) {
        CloseHandle(g_responseWrite);
        g_responseWrite = nullptr;
    }
}

DWORD WINAPI IoThreadProc(LPVOID) {
    OutputDebugStringA("SqlProxyIpc: IO thread started\n");

    while (InterlockedCompareExchange(&g_running, 1, 1) == 1) {
        FrameHeader hdr{};
        std::vector<uint8_t> payload;
        HANDLE reqHandle = nullptr;
        HANDLE respHandle = nullptr;

        EnterCriticalSection(&g_cs);
        reqHandle = g_requestRead;
        respHandle = g_responseWrite;
        LeaveCriticalSection(&g_cs);

        if (!reqHandle || !respHandle) {
            break;
        }

        if (!ReadFrame(reqHandle, hdr, payload)) {
            OutputDebugStringA("SqlProxyIpc: ReadFrame failed (disconnect or error)\n");
            break;
        }

        const MsgType type = static_cast<MsgType>(hdr.msgType);

        if (type == MsgType::Ping || type == MsgType::Hello) {
            if (!WriteFrame(respHandle, type == MsgType::Hello ? MsgType::Hello : MsgType::Pong,
                            hdr.requestId, BuildPong(Status::Ok))) {
                break;
            }
            continue;
        }

        if (type != MsgType::ExecSql) {
            WriteFrame(respHandle, MsgType::Error, hdr.requestId,
                       BuildError(Status::BadRequest, L"Unknown message type"));
            continue;
        }

        auto item = std::make_shared<WorkItem>();
        item->requestId = hdr.requestId;
        item->msgType = MsgType::ExecSql;
        item->doneEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!item->doneEvent) {
            WriteFrame(respHandle, MsgType::Error, hdr.requestId,
                       BuildError(Status::SamError, L"CreateEvent failed"));
            continue;
        }

        std::wstring parseErr;
        if (!ParseExecSql(payload, item->execRequest, parseErr)) {
            WriteFrame(respHandle, MsgType::Error, hdr.requestId,
                       BuildError(Status::BadRequest, parseErr));
            CloseHandle(item->doneEvent);
            continue;
        }

        EnterCriticalSection(&g_cs);
        g_queue.push_back(item);
        LeaveCriticalSection(&g_cs);

        if (g_uiThreadId != 0) {
            PostThreadMessageW(g_uiThreadId, WM_AR_SQLPROXY_PUMP, 0, 0);
        }

        WaitForSingleObject(item->doneEvent, INFINITE);

        if (item->status != Status::Ok && item->execResult.status == Status::Ok) {
            item->execResult.status = item->status;
            item->execResult.errorMessage = item->errorMessage;
        }
        auto resultPayload = BuildExecResult(item->execResult);

        CloseHandle(item->doneEvent);
        item->doneEvent = nullptr;

        if (!WriteFrame(respHandle, MsgType::ExecSqlResult, hdr.requestId, resultPayload)) {
            break;
        }
    }

    InterlockedExchange(&g_running, 0);
    FailAllPending(Status::Disconnected, L"SQL proxy IO thread exiting");

    EnterCriticalSection(&g_cs);
    CloseHandles_NoLock();
    LeaveCriticalSection(&g_cs);

    OutputDebugStringA("SqlProxyIpc: IO thread exiting\n");
    return 0;
}

} // namespace

BOOL HandleAttach(const AttachInfo* attach, uint32_t cb) {
    EnsureCs();

    if (!attach || cb < sizeof(AttachInfo)) {
        OutputDebugStringA("SqlProxyIpc: Attach bad pointer/size\n");
        return FALSE;
    }

    AttachInfo info{};
    std::memcpy(&info, attach, sizeof(info));

    if (info.magic != kMagic || info.version != kVersion ||
        info.structSize < sizeof(AttachInfo) ||
        info.requestReadHandle == 0 || info.responseWriteHandle == 0) {
        OutputDebugStringA("SqlProxyIpc: Attach validation failed\n");
        return FALSE;
    }

    EnterCriticalSection(&g_cs);
    if (InterlockedCompareExchange(&g_running, 0, 0) == 1) {
        LeaveCriticalSection(&g_cs);
        Shutdown();
        EnterCriticalSection(&g_cs);
    }

    g_requestRead = reinterpret_cast<HANDLE>(static_cast<uintptr_t>(info.requestReadHandle));
    g_responseWrite = reinterpret_cast<HANDLE>(static_cast<uintptr_t>(info.responseWriteHandle));
    g_uiThreadId = GetCurrentThreadId();

    InterlockedExchange(&g_running, 1);
    g_ioThread = CreateThread(nullptr, 0, IoThreadProc, nullptr, 0, nullptr);
    if (!g_ioThread) {
        CloseHandles_NoLock();
        InterlockedExchange(&g_running, 0);
        LeaveCriticalSection(&g_cs);
        return FALSE;
    }

    LeaveCriticalSection(&g_cs);
    OutputDebugStringA("SqlProxyIpc: attached successfully\n");
    return TRUE;
}

void HandlePump() {
    if (InterlockedCompareExchange(&g_inPump, 1, 0) != 0) {
        return;
    }

    EnsureCs();

    for (;;) {
        std::shared_ptr<WorkItem> item;
        EnterCriticalSection(&g_cs);
        if (!g_queue.empty()) {
            item = g_queue.front();
            g_queue.pop_front();
        }
        LeaveCriticalSection(&g_cs);

        if (!item) {
            break;
        }

        if (item->msgType == MsgType::ExecSql) {
            item->execResult = ExecuteSelect(item->execRequest);
            item->status = item->execResult.status;
        } else {
            item->status = Status::Ok;
        }

        if (item->doneEvent) {
            SetEvent(item->doneEvent);
        }
    }

    InterlockedExchange(&g_inPump, 0);

    EnterCriticalSection(&g_cs);
    const bool more = !g_queue.empty();
    const DWORD tid = g_uiThreadId;
    LeaveCriticalSection(&g_cs);
    if (more && tid != 0) {
        PostThreadMessageW(tid, WM_AR_SQLPROXY_PUMP, 0, 0);
    }
}

void Shutdown() {
    EnsureCs();
    InterlockedExchange(&g_running, 0);

    EnterCriticalSection(&g_cs);
    if (g_requestRead) {
        CancelIoEx(g_requestRead, nullptr);
        CloseHandle(g_requestRead);
        g_requestRead = nullptr;
    }
    if (g_responseWrite) {
        CancelIoEx(g_responseWrite, nullptr);
        CloseHandle(g_responseWrite);
        g_responseWrite = nullptr;
    }
    HANDLE thread = g_ioThread;
    g_ioThread = nullptr;
    LeaveCriticalSection(&g_cs);

    FailAllPending(Status::Disconnected, L"SQL proxy shut down");

    if (thread) {
        WaitForSingleObject(thread, 5000);
        CloseHandle(thread);
    }

    OutputDebugStringA("SqlProxyIpc: shutdown complete\n");
}

bool IsAttached() {
    return InterlockedCompareExchange(&g_running, 0, 0) == 1;
}

} // namespace ArSqlProxy
