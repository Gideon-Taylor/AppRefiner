#include "SqlProxySam.h"
#include <windows.h>
#include <cstring>
#include <vector>

namespace ArSqlProxy {

namespace {

using HSAMTRAN = intptr_t;

using PFN_SamCreate = int (*)(HSAMTRAN* phTran, int flags, const wchar_t* name, void* pParms, void* ctx);
using PFN_SamSetSqlProc = int (*)(HSAMTRAN hTran, const wchar_t* sql, const wchar_t* srcFile, int srcLine);
using PFN_SamBindStr = void (*)(HSAMTRAN hTran, const wchar_t* value);
using PFN_SamBindInt = void (*)(HSAMTRAN hTran, const int* value);
using PFN_SamBufStr = void (*)(HSAMTRAN hTran, wchar_t* buf, int cchMax);
using PFN_SamBufInt = void (*)(HSAMTRAN hTran, int* out);
using PFN_SamBufShort = void (*)(HSAMTRAN hTran, short* out);
using PFN_SamBufLongText = void (*)(HSAMTRAN hTran, void** ppData, long* pLength);
using PFN_SamBufLongRaw = void (*)(HSAMTRAN hTran, void** ppData, int* pLength);
using PFN_SamExec = int (*)(HSAMTRAN hTran, int a2, int flags);
using PFN_SamExecFetch = int (*)(HSAMTRAN hTran, int flags);
using PFN_SamFetch = int (*)(HSAMTRAN hTran);
using PFN_SamDestroy = int (*)(HSAMTRAN hTran);
using PFN_SamNumResultCols = int (*)(HSAMTRAN hTran, int* pNumCols);
using PFN_SamDescribeCol = int (*)(
    HSAMTRAN hTran,
    unsigned short colIndex,
    wchar_t* nameBuf,
    unsigned short nameCch,
    unsigned short* pNameLen,
    int* pSamType,
    int* pPrecision,
    unsigned short* pScale,
    unsigned short* pNullable);
using PFN_SamCompileNow = int (*)(HSAMTRAN hTran);
using PFN_SamForceCompileNow = int (*)(HSAMTRAN hTran);

// SAM field type ids (SamFldDesc / SamDescribeCol) — see SAM-SQL-API-Reference §6
constexpr int kSamTypeString = 1;
constexpr int kSamTypeChar = 2;
constexpr int kSamTypeDate = 3;
constexpr int kSamTypeInt = 4;
constexpr int kSamTypeLong = 5;
constexpr int kSamTypeDouble = 6;
constexpr int kSamTypeDecimal = 7;
constexpr int kSamTypeLongText = 8;
constexpr int kSamTypeLongRaw = 9;
constexpr int kSamTypeTime = 10;
constexpr int kSamTypeDateTime = 11;
constexpr int kSamTypePassword = 12;
constexpr int kSamTypeShort = 14;
constexpr int kSamTypeAtom = 15;

struct SamApi {
    HMODULE module = nullptr;
    PFN_SamCreate SamCreate = nullptr;
    PFN_SamSetSqlProc SamSetSqlProc = nullptr;
    PFN_SamBindStr SamBindStr = nullptr;
    PFN_SamBindInt SamBindInt = nullptr;
    PFN_SamBufStr SamBufStr = nullptr;
    PFN_SamBufInt SamBufInt = nullptr;
    PFN_SamBufShort SamBufShort = nullptr;
    PFN_SamBufLongText SamBufLongText = nullptr;
    PFN_SamBufLongRaw SamBufLongRaw = nullptr;
    PFN_SamExec SamExec = nullptr;
    PFN_SamExecFetch SamExecFetch = nullptr;
    PFN_SamFetch SamFetch = nullptr;
    PFN_SamDestroy SamDestroy = nullptr;
    PFN_SamNumResultCols SamNumResultCols = nullptr;
    PFN_SamDescribeCol SamDescribeCol = nullptr;
    PFN_SamCompileNow SamCompileNow = nullptr;
    PFN_SamForceCompileNow SamForceCompileNow = nullptr;
    bool resolved = false;
    bool resolveAttempted = false;
};

SamApi g_sam;

template <typename T>
T GetExport(HMODULE mod, const char* name) {
    return reinterpret_cast<T>(GetProcAddress(mod, name));
}

bool ResolveSam() {
    if (g_sam.resolveAttempted) {
        return g_sam.resolved;
    }
    g_sam.resolveAttempted = true;

    g_sam.module = GetModuleHandleW(L"pssam.dll");
    if (!g_sam.module) {
        OutputDebugStringA("SqlProxySam: pssam.dll not loaded\n");
        return false;
    }

    g_sam.SamCreate = GetExport<PFN_SamCreate>(g_sam.module, "SamCreate");
    g_sam.SamSetSqlProc = GetExport<PFN_SamSetSqlProc>(g_sam.module, "SamSetSqlProc");
    g_sam.SamBindStr = GetExport<PFN_SamBindStr>(g_sam.module, "SamBindStr");
    g_sam.SamBindInt = GetExport<PFN_SamBindInt>(g_sam.module, "SamBindInt");
    g_sam.SamBufStr = GetExport<PFN_SamBufStr>(g_sam.module, "SamBufStr");
    g_sam.SamBufInt = GetExport<PFN_SamBufInt>(g_sam.module, "SamBufInt");
    g_sam.SamBufShort = GetExport<PFN_SamBufShort>(g_sam.module, "SamBufShort");
    g_sam.SamBufLongText = GetExport<PFN_SamBufLongText>(g_sam.module, "SamBufLongText");
    g_sam.SamBufLongRaw = GetExport<PFN_SamBufLongRaw>(g_sam.module, "SamBufLongRaw");
    g_sam.SamExec = GetExport<PFN_SamExec>(g_sam.module, "SamExec");
    g_sam.SamExecFetch = GetExport<PFN_SamExecFetch>(g_sam.module, "SamExecFetch");
    g_sam.SamFetch = GetExport<PFN_SamFetch>(g_sam.module, "SamFetch");
    g_sam.SamDestroy = GetExport<PFN_SamDestroy>(g_sam.module, "SamDestroy");
    g_sam.SamNumResultCols = GetExport<PFN_SamNumResultCols>(g_sam.module, "SamNumResultCols");
    g_sam.SamDescribeCol = GetExport<PFN_SamDescribeCol>(g_sam.module, "SamDescribeCol");
    g_sam.SamCompileNow = GetExport<PFN_SamCompileNow>(g_sam.module, "SamCompileNow");
    g_sam.SamForceCompileNow = GetExport<PFN_SamForceCompileNow>(g_sam.module, "SamForceCompileNow");

    g_sam.resolved =
        g_sam.SamCreate && g_sam.SamSetSqlProc && g_sam.SamBindStr &&
        g_sam.SamBufStr && g_sam.SamExecFetch && g_sam.SamDestroy;

    if (!g_sam.resolved) {
        OutputDebugStringA("SqlProxySam: failed to resolve core Sam* exports\n");
    }
    if (!g_sam.SamNumResultCols || !g_sam.SamDescribeCol) {
        OutputDebugStringA("SqlProxySam: describe exports missing — auto column layout unavailable\n");
    }
    return g_sam.resolved;
}

// Map SAM type id → wire ValueType + buffer strategy.
ValueType MapSamType(int samType) {
    switch (samType) {
    case kSamTypeInt:
    case kSamTypeLong:
    case kSamTypeShort:
        return ValueType::Int32;
    case kSamTypeLongText:
        return ValueType::LongText;
    case kSamTypeLongRaw:
        return ValueType::LongRaw;
    case kSamTypeString:
    case kSamTypeChar:
    case kSamTypePassword:
    case kSamTypeDate:
    case kSamTypeTime:
    case kSamTypeDateTime:
    case kSamTypeDouble:
    case kSamTypeDecimal:
    case kSamTypeAtom:
    default:
        // Dates/decimals/atoms → string buffers for DataTable friendliness in v1
        return ValueType::String;
    }
}

// Data character capacity from SamDescribeCol precision (or override/default).
// Caller allocates dataCch + 1 wchars so SamBufStr can null-terminate within the buffer.
uint32_t StringCapacityFromPrecision(int precision, uint32_t defaultCch) {
    if (precision <= 0) {
        return defaultCch;
    }
    // Describe precision is the column data width in characters (e.g. CHAR(18) → 18),
    // not the SamBufStr cchMax (which is buffer size including NUL).
    uint32_t cch = static_cast<uint32_t>(precision);
    if (cch < 8) {
        cch = 8;
    }
    if (cch > kMaxColumnChars) {
        cch = kMaxColumnChars;
    }
    return cch;
}

struct ColumnScratch {
    ValueType type = ValueType::String;
    int samTypeId = 0;
    std::wstring name;
    std::vector<wchar_t> strBuf;
    int intBuf = 0;
    short shortBuf = 0;
    bool useShort = false;
    void* longPtr = nullptr;
    long longLenLong = 0;
    int longLenInt = 0;
};

CellValue ReadCell(const ColumnScratch& scratch) {
    CellValue cell;
    cell.type = scratch.type;

    switch (scratch.type) {
    case ValueType::String:
        cell.strValue = scratch.strBuf.data();
        break;
    case ValueType::Int32:
        cell.intValue = scratch.useShort ? static_cast<int>(scratch.shortBuf) : scratch.intBuf;
        break;
    case ValueType::LongRaw: {
        if (scratch.longPtr && scratch.longLenInt > 0) {
            const size_t n = static_cast<size_t>(scratch.longLenInt);
            if (n <= kMaxLongBytes) {
                const uint8_t* p = static_cast<const uint8_t*>(scratch.longPtr);
                cell.bytes.assign(p, p + n);
            }
        }
        break;
    }
    case ValueType::LongText: {
        if (scratch.longPtr && scratch.longLenLong > 0) {
            const size_t n = static_cast<size_t>(scratch.longLenLong);
            if (n <= kMaxLongBytes) {
                const uint8_t* p = static_cast<const uint8_t*>(scratch.longPtr);
                cell.bytes.assign(p, p + n);
                if ((n % sizeof(wchar_t)) == 0) {
                    cell.strValue.assign(reinterpret_cast<const wchar_t*>(p), n / sizeof(wchar_t));
                    const size_t z = cell.strValue.find(L'\0');
                    if (z != std::wstring::npos) {
                        cell.strValue.resize(z);
                    }
                }
            }
        }
        break;
    }
    default:
        cell.type = ValueType::Null;
        break;
    }
    return cell;
}

bool RegisterBuffers(HSAMTRAN h, std::vector<ColumnScratch>& cols, std::wstring& err) {
    for (auto& c : cols) {
        switch (c.type) {
        case ValueType::String:
            // cchMax is the full buffer capacity in wchars (including room for the
            // terminating null), matching live pside call sites:
            //   wchar_t owner[0x81]; SamBufStr(h, owner, 0x81);
            // Passing size-1 truncated CHAR(N) values to N-1 (e.g. 18-char field names → 17).
            g_sam.SamBufStr(h, c.strBuf.data(), static_cast<int>(c.strBuf.size()));
            break;
        case ValueType::Int32:
            if (c.useShort) {
                if (!g_sam.SamBufShort) {
                    // fall back to int if short not exported
                    if (!g_sam.SamBufInt) {
                        err = L"SamBufInt/SamBufShort not available";
                        return false;
                    }
                    c.useShort = false;
                    g_sam.SamBufInt(h, &c.intBuf);
                } else {
                    g_sam.SamBufShort(h, &c.shortBuf);
                }
            } else {
                if (!g_sam.SamBufInt) {
                    err = L"SamBufInt not available";
                    return false;
                }
                g_sam.SamBufInt(h, &c.intBuf);
            }
            break;
        case ValueType::LongRaw:
            if (!g_sam.SamBufLongRaw) {
                err = L"SamBufLongRaw not available";
                return false;
            }
            g_sam.SamBufLongRaw(h, &c.longPtr, &c.longLenInt);
            break;
        case ValueType::LongText:
            if (!g_sam.SamBufLongText) {
                err = L"SamBufLongText not available";
                return false;
            }
            g_sam.SamBufLongText(h, &c.longPtr, &c.longLenLong);
            break;
        default:
            err = L"Unsupported column type";
            return false;
        }
    }
    return true;
}

// Returns true if numCols > 0 and each column describes successfully.
bool TryDescribeColumns(HSAMTRAN h, uint32_t defaultStringCch, std::vector<ColumnScratch>& cols, std::wstring& err) {
    cols.clear();
    if (!g_sam.SamNumResultCols || !g_sam.SamDescribeCol) {
        err = L"SamNumResultCols/SamDescribeCol not available in pssam.dll";
        return false;
    }

    int numCols = 0;
    int rc = g_sam.SamNumResultCols(h, &numCols);
    char dbg[160];
    sprintf_s(dbg, "SqlProxySam: SamNumResultCols rc=%d n=%d\n", rc, numCols);
    OutputDebugStringA(dbg);

    if (rc != 0 || numCols <= 0) {
        err = L"SamNumResultCols failed rc=" + std::to_wstring(rc) + L" n=" + std::to_wstring(numCols);
        return false;
    }

    if (static_cast<uint32_t>(numCols) > kMaxColumns) {
        err = L"Too many result columns";
        return false;
    }

    cols.reserve(static_cast<size_t>(numCols));

    for (int i = 1; i <= numCols; ++i) {
        wchar_t nameBuf[256] = {};
        unsigned short nameLen = 0;
        int samType = 0;
        int precision = 0;
        unsigned short scale = 0;
        unsigned short nullable = 0;

        rc = g_sam.SamDescribeCol(
            h,
            static_cast<unsigned short>(i),
            nameBuf,
            static_cast<unsigned short>(255),
            &nameLen,
            &samType,
            &precision,
            &scale,
            &nullable);

        if (rc != 0) {
            err = L"SamDescribeCol failed col=" + std::to_wstring(i) + L" rc=" + std::to_wstring(rc);
            cols.clear();
            return false;
        }

        ColumnScratch col;
        col.samTypeId = samType;
        col.type = MapSamType(samType);
        col.name = nameBuf;
        if (col.name.empty()) {
            col.name = L"Column" + std::to_wstring(i);
        }

        if (col.type == ValueType::String) {
            const uint32_t cch = StringCapacityFromPrecision(precision, defaultStringCch);
            col.strBuf.assign(static_cast<size_t>(cch) + 1, L'\0');
        } else if (col.type == ValueType::Int32 && samType == kSamTypeShort) {
            col.useShort = true;
        }

        cols.push_back(std::move(col));
    }

    sprintf_s(dbg, "SqlProxySam: described %d columns OK\n", numCols);
    OutputDebugStringA(dbg);
    return true;
}

// Prepare / open result metadata so describe works. Sets *alreadyExecuted if SamExec ran
// (caller must SamFetch, not SamExecFetch).
bool EnsureDescribeReady(HSAMTRAN h, uint32_t defaultStringCch, std::vector<ColumnScratch>& cols,
                         bool* alreadyExecuted, std::wstring& err) {
    *alreadyExecuted = false;

    // 1) Always try compile (prepare) first — cheap if already done
    if (g_sam.SamCompileNow) {
        int crc = g_sam.SamCompileNow(h);
        char dbg[96];
        sprintf_s(dbg, "SqlProxySam: SamCompileNow rc=%d\n", crc);
        OutputDebugStringA(dbg);
    }
    if (g_sam.SamForceCompileNow) {
        int crc = g_sam.SamForceCompileNow(h);
        char dbg[96];
        sprintf_s(dbg, "SqlProxySam: SamForceCompileNow rc=%d\n", crc);
        OutputDebugStringA(dbg);
    }

    std::wstring lastErr;
    if (TryDescribeColumns(h, defaultStringCch, cols, lastErr)) {
        return true;
    }

    // 2) Metadata often only exists after the network open/exec (PS_sqlnsi path).
    //    SamExec without fetch should populate describe info; then we register buffers and Fetch.
    if (g_sam.SamExec) {
        int erc = g_sam.SamExec(h, /*a2*/ 0, /*flags*/ 1);
        char dbg[96];
        sprintf_s(dbg, "SqlProxySam: SamExec (pre-describe) rc=%d\n", erc);
        OutputDebugStringA(dbg);

        if (erc == 8) {
            // Exec failed hard — still try describe once (some paths set cols on error)
            if (TryDescribeColumns(h, defaultStringCch, cols, lastErr)) {
                *alreadyExecuted = true;
                return true;
            }
            err = L"SamExec failed rc=8 and describe still unavailable: " + lastErr;
            return false;
        }

        *alreadyExecuted = true;
        if (TryDescribeColumns(h, defaultStringCch, cols, lastErr)) {
            return true;
        }
        err = L"Describe still unavailable after SamExec: " + lastErr;
        return false;
    }

    err = L"Describe unavailable after compile (no SamExec): " + lastErr;
    return false;
}

bool BuildColumnsFromSpecs(const std::vector<ColumnSpec>& specs, uint32_t defaultStringCch, std::vector<ColumnScratch>& cols, std::wstring& err) {
    if (specs.empty() || specs.size() > kMaxColumns) {
        err = L"Invalid column override count";
        return false;
    }
    cols.clear();
    cols.reserve(specs.size());
    for (size_t i = 0; i < specs.size(); ++i) {
        ColumnScratch col;
        col.type = specs[i].type;
        col.name = specs[i].name.empty() ? (L"Column" + std::to_wstring(i + 1)) : specs[i].name;
        if (col.type == ValueType::String) {
            uint32_t cch = specs[i].maxSize ? specs[i].maxSize : defaultStringCch;
            if (cch > kMaxColumnChars) cch = kMaxColumnChars;
            col.strBuf.assign(static_cast<size_t>(cch) + 1, L'\0');
        }
        cols.push_back(std::move(col));
    }
    return true;
}

} // namespace

bool IsSelectOnly(const std::wstring& sql) {
    size_t i = 0;
    while (i < sql.size()) {
        const wchar_t ch = sql[i];
        if (ch != L' ' && ch != L'\t' && ch != L'\r' && ch != L'\n') {
            break;
        }
        ++i;
    }

    static const wchar_t kSelect[] = L"SELECT";
    constexpr size_t kLen = 6;
    if (sql.size() - i < kLen) {
        return false;
    }

    for (size_t c = 0; c < kLen; ++c) {
        const wchar_t a = sql[i + c];
        const wchar_t b = kSelect[c];
        const wchar_t al = (a >= L'A' && a <= L'Z') ? static_cast<wchar_t>(a - L'A' + L'a') : a;
        const wchar_t bl = (b >= L'A' && b <= L'Z') ? static_cast<wchar_t>(b - L'A' + L'a') : b;
        if (al != bl) {
            return false;
        }
    }

    if (sql.size() - i == kLen) {
        return true;
    }

    const wchar_t next = sql[i + kLen];
    return next == L' ' || next == L'\t' || next == L'\r' || next == L'\n' || next == L'(';
}

ExecResult ExecuteSelect(const ExecRequest& request) {
    ExecResult result;

    if (!IsSelectOnly(request.sql)) {
        result.status = Status::Rejected;
        result.errorMessage = L"Only SELECT statements are allowed";
        return result;
    }

    if (request.sql.empty()) {
        result.status = Status::BadRequest;
        result.errorMessage = L"Empty SQL";
        return result;
    }

    if (request.binds.size() > kMaxBinds) {
        result.status = Status::BadRequest;
        result.errorMessage = L"Too many binds";
        return result;
    }

    if (!ResolveSam()) {
        result.status = Status::NotSignedOn;
        result.errorMessage = L"pssam.dll exports not available";
        return result;
    }

    uint32_t maxRows = request.maxRows;
    if (maxRows == 0) maxRows = 1;
    if (maxRows > kMaxRows) maxRows = kMaxRows;

    uint32_t defaultStringCch = request.defaultStringCch;
    if (defaultStringCch == 0) defaultStringCch = 512;
    if (defaultStringCch > kMaxColumnChars) defaultStringCch = kMaxColumnChars;

    std::vector<int> intBindStorage;
    intBindStorage.reserve(request.binds.size());

    HSAMTRAN h = 0;
    int rc = g_sam.SamCreate(&h, 1, L"AppRefinerSqlProxy", nullptr, nullptr);
    if (rc != 0 || h == 0) {
        result.status = Status::NotSignedOn;
        result.samRc = rc;
        result.errorMessage = L"SamCreate failed (not signed on or session unavailable)";
        return result;
    }

    try {
        g_sam.SamSetSqlProc(h, request.sql.c_str(), L"AppRefinerSqlProxy", 0);

        for (const auto& bind : request.binds) {
            if (bind.type == ValueType::Int32) {
                if (!g_sam.SamBindInt) {
                    result.status = Status::SamError;
                    result.errorMessage = L"SamBindInt not available";
                    g_sam.SamDestroy(h);
                    return result;
                }
                intBindStorage.push_back(bind.intValue);
                g_sam.SamBindInt(h, &intBindStorage.back());
            } else {
                g_sam.SamBindStr(h, bind.strValue.c_str());
            }
        }

        std::vector<ColumnScratch> cols;
        std::wstring layoutErr;
        bool alreadyExecuted = false;

        if (request.columns.empty()) {
            // Ad-hoc path: compile / possibly SamExec so PS_sqlnsi fills metadata, then describe
            if (!EnsureDescribeReady(h, defaultStringCch, cols, &alreadyExecuted, layoutErr)) {
                result.status = Status::SamError;
                result.errorMessage = layoutErr;
                g_sam.SamDestroy(h);
                return result;
            }
        } else {
            if (!BuildColumnsFromSpecs(request.columns, defaultStringCch, cols, layoutErr)) {
                result.status = Status::BadRequest;
                result.errorMessage = layoutErr;
                g_sam.SamDestroy(h);
                return result;
            }
        }

        std::wstring bufErr;
        if (!RegisterBuffers(h, cols, bufErr)) {
            result.status = Status::SamError;
            result.errorMessage = bufErr;
            g_sam.SamDestroy(h);
            return result;
        }

        // Publish column metadata to caller (names + types from describe or override)
        result.columns.reserve(cols.size());
        for (const auto& c : cols) {
            ColumnMeta meta;
            meta.name = c.name;
            meta.type = c.type;
            meta.samTypeId = c.samTypeId;
            meta.precision = c.type == ValueType::String && !c.strBuf.empty()
                ? static_cast<int>(c.strBuf.size() - 1)
                : 0;
            result.columns.push_back(std::move(meta));
        }
        result.columnCount = static_cast<uint32_t>(cols.size());

        result.cells.reserve(static_cast<size_t>(maxRows) * cols.size());

        auto appendRow = [&]() {
            for (const auto& c : cols) {
                result.cells.push_back(ReadCell(c));
            }
            result.rowCount++;
        };

        // If we already SamExec'd to populate describe info, only Fetch.
        // Otherwise ExecFetch (exec + first fetch).
        if (alreadyExecuted) {
            if (!g_sam.SamFetch) {
                result.status = Status::SamError;
                result.errorMessage = L"SamFetch not available after pre-exec describe";
                g_sam.SamDestroy(h);
                return result;
            }
            rc = g_sam.SamFetch(h);
            result.samRc = rc;
            if (rc == 8) {
                result.status = Status::Ok;
                result.rowCount = 0;
                g_sam.SamDestroy(h);
                return result;
            }
            if (rc != 0) {
                // No first row
                result.status = Status::Ok;
                result.rowCount = 0;
                g_sam.SamDestroy(h);
                return result;
            }
            appendRow();
        } else {
            rc = g_sam.SamExecFetch(h, 1);
            result.samRc = rc;
            if (rc == 8) {
                result.status = Status::Ok;
                result.rowCount = 0;
                g_sam.SamDestroy(h);
                return result;
            }
            appendRow();
        }

        while (result.rowCount < maxRows && g_sam.SamFetch) {
            int frc = g_sam.SamFetch(h);
            if (frc != 0) {
                break;
            }
            appendRow();
        }

        if (result.rowCount >= maxRows && g_sam.SamFetch) {
            result.status = (g_sam.SamFetch(h) == 0) ? Status::Truncated : Status::Ok;
        } else {
            result.status = Status::Ok;
        }
    } catch (...) {
        result.status = Status::SamError;
        result.errorMessage = L"Exception during SAM execution";
    }

    g_sam.SamDestroy(h);
    return result;
}

} // namespace ArSqlProxy
