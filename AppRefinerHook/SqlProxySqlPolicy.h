#pragma once

#include <string>

namespace ArSqlProxy {

// Gate applied to every proxied query before any Sam* call. App Designer's session is
// signed on as the bootstrap user, so this is what stands between a request and the
// database. Conservative by design: it rejects anything it cannot reason about rather
// than trying to parse SQL fully. On rejection, `reason` says which rule failed.
//
// Rules, applied outside string literals and quoted identifiers:
//  - must start with SELECT (after leading whitespace)
//  - no ';' (no statement batches), no comments, no embedded NUL
//  - no Oracle q'...' alternative quoting or SQL Server [bracketed] identifiers;
//    literals and "quoted identifiers" must be closed
//  - no data-changing, DDL, transaction-control or pass-through keywords
//    (UPDATE also covers SELECT ... FOR UPDATE; INTO covers SQL Server SELECT INTO)
//  - Meta-SQL limited to an allow-list of read-only functions, since SAM expands it
//    after this check (%SQL, %InsertSelect and similar could smuggle in other SQL)
bool IsAllowedSelect(const std::wstring& sql, std::wstring* reason = nullptr);

} // namespace ArSqlProxy
