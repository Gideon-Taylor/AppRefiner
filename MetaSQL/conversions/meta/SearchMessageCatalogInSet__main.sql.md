# SearchMessageCatalogInSet / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Uppercased search term wrapped as %term% for LIKE (MESSAGE_TEXT) |
| 2 | string | Same value as :1 (DESCRLONG LIKE); bind twice for SAM positional clarity |
| 3 | int | MESSAGE_SET_NBR to scope the search to a single set |

## Meta-SQL
```sql
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE (%Upper(MESSAGE_TEXT) LIKE :1
        OR %Upper(%Substring(DESCRLONG, 1, 4000)) LIKE :2)
   AND MESSAGE_SET_NBR = :3
 ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
```

## maxRows
C#-side limit (remaining rows when multi-set loop merges results)

## Notes / residual risks
- Same LIKE semantics as SearchMessageCatalog, plus a static set-number bind.
- Used when `setNumbers` is non-empty: one query per set (no dynamic IN list).
- When `setNumbers` is null/empty, callers use SearchMessageCatalog (global) instead.
- DESCRLONG is CLOB; substring bounds match to first 4000 chars.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
