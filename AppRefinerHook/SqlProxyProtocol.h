#pragma once

#include <stdint.h>

// Shared wire protocol between AppRefiner (C#) and AppRefinerHook (C++).
// Keep values in sync with AppRefiner/Database/SqlProxy/SqlProxyProtocol.cs

namespace ArSqlProxy {

constexpr uint32_t kMagic = 0x51535241u; // 'ARSQ' little-endian
constexpr uint32_t kVersion = 1;
constexpr uint32_t kMaxFrameBytes = 16u * 1024u * 1024u; // allow multi-row + long raw
constexpr uint32_t kMaxSqlChars = 256u * 1024u;
constexpr uint32_t kMaxBinds = 64;
constexpr uint32_t kMaxColumns = 256;
constexpr uint32_t kMaxColumnChars = 8192;
constexpr uint32_t kMaxRows = 10000;
constexpr uint32_t kMaxLongBytes = 4u * 1024u * 1024u; // per long cell

enum class MsgType : uint32_t {
    Hello = 1,
    Ping = 2,
    Pong = 3,
    ExecSql = 4,
    ExecSqlResult = 5,
    Error = 6,
};

enum class Status : uint32_t {
    Ok = 0,
    NotAttached = 1,
    NotSignedOn = 2,
    SamError = 3,
    Busy = 4,
    BadRequest = 5,
    Rejected = 6,
    Truncated = 7,
    VersionMismatch = 8,
    Disconnected = 9,
};

// Bind and column / cell type codes (catalog §5)
enum class ValueType : uint32_t {
    Null = 0,
    String = 1,
    Int32 = 2,
    LongRaw = 3,
    LongText = 4,
};

#pragma pack(push, 1)
struct FrameHeader {
    uint32_t magic;
    uint32_t version;
    uint32_t msgType;
    uint32_t requestId;
    uint32_t flags;
    uint32_t payloadLen;
};

// Written by AppRefiner into pside address space; delivered via WM_AR_SQLPROXY_ATTACH.
struct AttachInfo {
    uint32_t magic;
    uint32_t version;
    uint32_t structSize;
    uint32_t flags;
    uint64_t requestReadHandle;   // HANDLE valid in pside
    uint64_t responseWriteHandle; // HANDLE valid in pside
};
#pragma pack(pop)

static_assert(sizeof(FrameHeader) == 24, "FrameHeader size");
static_assert(sizeof(AttachInfo) == 32, "AttachInfo size");

} // namespace ArSqlProxy
