# GetAllClassesForPackage / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | PACKAGEROOT (first path segment) |
| 2 | string | QUALIFYPATH (rest joined by ':', or ':' if root only) |

## Meta-SQL
```sql
SELECT APPCLASSID
  FROM %Table(PSAPPCLASSDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
```

## maxRows
unlimited

## Notes / residual risks
- packagePath split on ':' — first segment root, remainder qualify path.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
