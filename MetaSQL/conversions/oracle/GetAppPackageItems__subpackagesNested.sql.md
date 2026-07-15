# GetAppPackageItems / subpackagesNested

## Purpose
List nested subpackages under ROOT:SubPackage (PACKAGELEVEL = 2).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :rootPackage | string | Root package id (PACKAGEROOT) |
| 2 | :qualifyPath | string | Qualify path of parent subpackage (parts after root) |

## Original Oracle SQL
```sql
SELECT PACKAGEID FROM PSPACKAGEDEFN 
WHERE PACKAGEROOT = :rootPackage 
AND QUALIFYPATH = :qualifyPath 
AND PACKAGELEVEL = 2
ORDER BY PACKAGEID
```

## Notes
- Used when packageLevel == 1. PeopleSoft max depth is 3 (ROOT:SUB:SUBSUB); level 2 has no further subpackages.
- maxRows: unlimited
