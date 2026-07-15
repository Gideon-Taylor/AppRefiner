# Meta-SQL conversions for SqlProxyDataManager

## Process
1. Extract Oracle SQL from `OraclePeopleSoftDataManager` into `oracle/<Method>__<id>.sql.md`.
2. Convert into `meta/<Method>__<id>.sql.md` (static Meta-SQL, `:1`… binds).
3. Copy final Meta-SQL into `AppRefiner/Database/SqlProxyQueries/*` constants.
4. Implement method body in `SqlProxyDataManager` with ordered `SqlProxyBind` list.

## Checklist per query
- [ ] `%Table(recname)` with correct record name (strip `PS_` only when underscore present)
- [ ] No named binds; only `:1`, `:2`, …
- [ ] No dynamic SQL fragments
- [ ] SELECT only
- [ ] Column aliases match what C# reads (`row["SQLTEXT"]`, etc.)
- [ ] Bind table documents CLR type and meaning for each `:n`
- [ ] maxRows policy noted (default / limit / unlimited for blob chunks)

## Record-name rule
- `PS_APPR_RULE_HDR` → `%Table(APPR_RULE_HDR)`
- `PSSQLDEFN`, `PSPCMPROG`, `PSSTATUS`, … → `%Table(PSSQLDEFN)` etc.
