# GetAppPackageItems / subpackagesRoot

## Purpose
List direct subpackages under a root application package (PACKAGELEVEL = 1).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :rootPackage | string | Root package id (PACKAGEROOT) |

## Original Oracle SQL
```sql
SELECT PACKAGEID FROM PSPACKAGEDEFN 
WHERE PACKAGEROOT = :rootPackage 
AND QUALIFYPATH = ':' 
AND PACKAGELEVEL = 1
ORDER BY PACKAGEID
```

## Notes
- Used when packageLevel == 0 (path is just root).
- maxRows: unlimited
