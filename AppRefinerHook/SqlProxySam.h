#pragma once

#include "SqlProxyProtocol.h"
#include <cstdint>
#include <string>
#include <vector>

namespace ArSqlProxy {

struct BindValue {
    ValueType type = ValueType::String;
    std::wstring strValue;
    int32_t intValue = 0;
};

// Optional override; empty = describe via SAM.
struct ColumnSpec {
    ValueType type = ValueType::String;
    uint32_t maxSize = 256;
    std::wstring name; // optional display name when overriding
};

struct CellValue {
    ValueType type = ValueType::Null;
    std::wstring strValue;
    int32_t intValue = 0;
    std::vector<uint8_t> bytes;
};

struct ColumnMeta {
    std::wstring name;
    ValueType type = ValueType::String;
    int samTypeId = 0; // raw SAM field type from describe
    int precision = 0;
};

struct ExecRequest {
    std::wstring sql;
    std::vector<BindValue> binds;
    std::vector<ColumnSpec> columns; // empty → SamDescribeCol path
    uint32_t maxRows = 100;
    uint32_t defaultStringCch = 512; // when describe gives weak precision
};

struct ExecResult {
    Status status = Status::Ok;
    int32_t samRc = 0;
    std::wstring errorMessage;
    std::vector<ColumnMeta> columns;
    uint32_t columnCount = 0;
    uint32_t rowCount = 0;
    std::vector<CellValue> cells; // row-major
};

// Must run on App Designer UI thread only.
ExecResult ExecuteSelect(const ExecRequest& request);

} // namespace ArSqlProxy
