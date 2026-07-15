# GetAppPackageItems / classes

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Root package id (PACKAGEROOT) |
| 2 | string | Qualify path (':' for root, or joined sub path) |

## Meta-SQL
```sql
SELECT APPCLASSID
  FROM %Table(PSAPPCLASSDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
 ORDER BY APPCLASSID
```

## maxRows
unlimited

## Notes / residual risks
- Always run for the resolved package path.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
