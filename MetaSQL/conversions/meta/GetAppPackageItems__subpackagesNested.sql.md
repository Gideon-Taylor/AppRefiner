# GetAppPackageItems / subpackagesNested

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Root package id (PACKAGEROOT) |
| 2 | string | Qualify path of parent subpackage (parts after root) |

## Meta-SQL
```sql
SELECT PACKAGEID
  FROM %Table(PSPACKAGEDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
   AND PACKAGELEVEL = 2
 ORDER BY PACKAGEID
```

## maxRows
unlimited

## Notes / residual risks
- Used when packageLevel == 1. PeopleSoft max depth is 3 (ROOT:SUB:SUBSUB); level 2 has no further subpackages.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
