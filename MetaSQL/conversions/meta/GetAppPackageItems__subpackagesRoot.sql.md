# GetAppPackageItems / subpackagesRoot

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Root package id (PACKAGEROOT) |

## Meta-SQL
```sql
SELECT PACKAGEID
  FROM %Table(PSPACKAGEDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = ':'
   AND PACKAGELEVEL = 1
 ORDER BY PACKAGEID
```

## maxRows
unlimited

## Notes / residual risks
- Used when packageLevel == 0 (path is just root).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
