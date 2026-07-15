# GetPackagesForClass / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | APPCLASSID (class name) |

## Meta-SQL
```sql
SELECT A.PACKAGEROOT,
       A.QUALIFYPATH
  FROM %Table(PSAPPCLASSDEFN) A
  LEFT OUTER JOIN %Table(PSPACKAGEDEFN) B
    ON B.PACKAGEROOT = A.PACKAGEROOT
   AND B.QUALIFYPATH = CASE
                         WHEN A.QUALIFYPATH = ':' THEN '.'
                         ELSE A.QUALIFYPATH
                       END
 WHERE A.APPCLASSID = :1
 ORDER BY B.LASTUPDDTTM DESC,
          A.PACKAGEROOT ASC
```

## maxRows
unlimited

## Notes / residual risks
- C# builds full path PACKAGEROOT:CLASSNAME when QUALIFYPATH=':', else PACKAGEROOT:QUALIFYPATH:CLASSNAME.
- **Residual Oracleisms removed/simplified:**
  - Original `ORDER BY TRUNC(B.LASTUPDDTTM) DESC NULLS LAST` — Meta-SQL has no portable date-trunc or NULLS LAST; ordered by `B.LASTUPDDTTM DESC` instead. Null package rows (no join match) may sort differently across platforms.
  - CASE join on QUALIFYPATH ':' → '.' is retained (ANSI SQL).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
