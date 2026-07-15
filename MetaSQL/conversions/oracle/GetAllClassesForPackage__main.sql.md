# GetAllClassesForPackage / main

## Purpose
List all application class ids under a package root + qualify path.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :packageRoot | string | PACKAGEROOT (first path segment) |
| 2 | :qualifyPath | string | QUALIFYPATH (rest joined by ':', or ':' if root only) |

## Original Oracle SQL
```sql
SELECT APPCLASSID FROM PSAPPCLASSDEFN WHERE PACKAGEROOT = :packageRoot AND QUALIFYPATH = :qualifyPath
```

## Notes
- packagePath split on ':' — first segment root, remainder qualify path.
- Parameter dictionary keys omit leading colon in source.
- maxRows: unlimited
