# GetAppPackageItems / classes

## Purpose
List application classes in the current package path.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :rootPackage | string | Root package id (PACKAGEROOT) |
| 2 | :qualifyPath | string | Qualify path (':' for root, or joined sub path) |

## Original Oracle SQL
```sql
SELECT APPCLASSID FROM PSAPPCLASSDEFN 
WHERE PACKAGEROOT = :rootPackage 
AND QUALIFYPATH = :qualifyPath
ORDER BY APPCLASSID
```

## Notes
- Always run for the resolved package path.
- maxRows: unlimited
