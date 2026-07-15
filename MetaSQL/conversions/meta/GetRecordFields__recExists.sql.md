# GetRecordFields / recExists

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Record name (RECNAME, uppercased) |

## Meta-SQL
```sql
SELECT VERSION
  FROM %Table(PSRECDEFN)
 WHERE RECNAME = :1
```

## maxRows
1 expected

## Notes / residual risks
- Returns -1 in C# when no row (record does not exist). Private helper GetRecordVersion.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
