#include "SqlProxySqlPolicy.h"

namespace ArSqlProxy {

namespace {

const wchar_t* const kDeniedKeywords[] = {
    // Data changes and DDL
    L"INSERT", L"UPDATE", L"DELETE", L"MERGE", L"UPSERT", L"INTO", L"TRUNCATE",
    L"CREATE", L"ALTER", L"DROP", L"RENAME", L"GRANT", L"REVOKE",
    // Procedural code and transaction control
    L"EXEC", L"EXECUTE", L"CALL", L"BEGIN", L"DECLARE", L"SET", L"USE",
    L"COMMIT", L"ROLLBACK", L"SAVEPOINT", L"LOCK",
    // SQL Server administration and pass-through
    L"DBCC", L"BACKUP", L"RESTORE", L"SHUTDOWN", L"KILL", L"WAITFOR", L"BULK",
    L"OPENROWSET", L"OPENQUERY", L"OPENDATASOURCE", L"OPENXML",
};

// Read-only Meta-SQL functions SAM may expand. Anything else after '%' is rejected.
const wchar_t* const kAllowedMetaSql[] = {
    L"Table", L"Upper", L"Concat", L"Substring", L"Coalesce", L"Round", L"Cast",
    L"TextIn", L"NumToChar",
    L"DateIn", L"DateOut", L"TimeIn", L"TimeOut", L"DateTimeIn", L"DateTimeOut",
    L"CurrentDateIn", L"CurrentDateOut", L"CurrentTimeIn", L"CurrentTimeOut",
    L"CurrentDateTimeIn", L"CurrentDateTimeOut",
    L"DatePart", L"TimePart", L"DateAdd", L"DateDiff",
};

bool IsWordChar(wchar_t ch) {
    return (ch >= L'A' && ch <= L'Z') || (ch >= L'a' && ch <= L'z') || (ch >= L'0' && ch <= L'9')
        || ch == L'_' || ch == L'$' || ch == L'#' || ch == L'@';
}

bool IsSpace(wchar_t ch) {
    return ch == L' ' || ch == L'\t' || ch == L'\r' || ch == L'\n';
}

wchar_t ToUpper(wchar_t ch) {
    return (ch >= L'a' && ch <= L'z') ? static_cast<wchar_t>(ch - L'a' + L'A') : ch;
}

bool EqualsIgnoreCase(const std::wstring& sql, size_t start, size_t len, const wchar_t* word) {
    size_t i = 0;
    for (; i < len && word[i] != L'\0'; ++i) {
        if (ToUpper(sql[start + i]) != ToUpper(word[i])) {
            return false;
        }
    }
    return i == len && word[i] == L'\0';
}

template <size_t N>
bool InList(const std::wstring& sql, size_t start, size_t len, const wchar_t* const (&list)[N]) {
    for (const wchar_t* word : list) {
        if (EqualsIgnoreCase(sql, start, len, word)) {
            return true;
        }
    }
    return false;
}

bool Reject(std::wstring* reason, const std::wstring& message) {
    if (reason) {
        *reason = message;
    }
    return false;
}

// Skips a quoted run that starts at sql[i] == open and ends at `close`, where a doubled
// close character is an escaped one. Returns false if it never closes.
bool SkipQuoted(const std::wstring& sql, size_t& i, wchar_t close) {
    ++i;
    while (i < sql.size()) {
        if (sql[i] == close) {
            if (i + 1 < sql.size() && sql[i + 1] == close) {
                i += 2;
                continue;
            }
            ++i;
            return true;
        }
        ++i;
    }
    return false;
}

} // namespace

bool IsAllowedSelect(const std::wstring& sql, std::wstring* reason) {
    if (sql.find(L'\0') != std::wstring::npos) {
        return Reject(reason, L"Only SELECT statements are allowed: SQL contains a NUL character");
    }

    size_t i = 0;
    while (i < sql.size() && IsSpace(sql[i])) {
        ++i;
    }
    size_t firstWordEnd = i;
    while (firstWordEnd < sql.size() && IsWordChar(sql[firstWordEnd])) {
        ++firstWordEnd;
    }
    if (!EqualsIgnoreCase(sql, i, firstWordEnd - i, L"SELECT")) {
        return Reject(reason, L"Only SELECT statements are allowed");
    }

    while (i < sql.size()) {
        const wchar_t ch = sql[i];

        if (ch == L'\'') {
            // Oracle alternative quoting (q'[...]', nq'{...}') ends on a delimiter this scan
            // does not track, so it could hide the rest of the statement.
            if (i > 0 && ToUpper(sql[i - 1]) == L'Q') {
                return Reject(reason, L"Only SELECT statements are allowed: q'...' quoting is not supported");
            }
            if (!SkipQuoted(sql, i, L'\'')) {
                return Reject(reason, L"Only SELECT statements are allowed: unterminated string literal");
            }
            continue;
        }
        if (ch == L'"') {
            if (!SkipQuoted(sql, i, L'"')) {
                return Reject(reason, L"Only SELECT statements are allowed: unterminated quoted identifier");
            }
            continue;
        }
        // SQL Server [identifiers] mean nothing to Oracle, so a bracket could be read
        // differently by the database than by this scan. PeopleSoft SQL never needs them.
        if (ch == L'[') {
            return Reject(reason, L"Only SELECT statements are allowed: [bracketed] identifiers are not supported");
        }
        if (ch == L';') {
            return Reject(reason, L"Only SELECT statements are allowed: ';' is not allowed");
        }
        if ((ch == L'-' && i + 1 < sql.size() && sql[i + 1] == L'-')
            || (ch == L'/' && i + 1 < sql.size() && sql[i + 1] == L'*')) {
            return Reject(reason, L"Only SELECT statements are allowed: comments are not allowed");
        }
        if (ch == L'%') {
            size_t end = i + 1;
            while (end < sql.size() && IsWordChar(sql[end])) {
                ++end;
            }
            // A bare '%' (SQL Server modulo) has no name after it.
            if (end > i + 1 && !InList(sql, i + 1, end - i - 1, kAllowedMetaSql)) {
                return Reject(reason, L"Only SELECT statements are allowed: Meta-SQL %"
                    + sql.substr(i + 1, end - i - 1) + L" is not allowed");
            }
            i = end;
            continue;
        }
        if (IsWordChar(ch)) {
            size_t end = i;
            while (end < sql.size() && IsWordChar(sql[end])) {
                ++end;
            }
            if (InList(sql, i, end - i, kDeniedKeywords)) {
                return Reject(reason, L"Only SELECT statements are allowed: "
                    + sql.substr(i, end - i) + L" is not allowed");
            }
            i = end;
            continue;
        }
        ++i;
    }

    return true;
}

} // namespace ArSqlProxy
