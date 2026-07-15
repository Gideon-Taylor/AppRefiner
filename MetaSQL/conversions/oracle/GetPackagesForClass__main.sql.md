# GetPackagesForClass / main

## Purpose
Find all package paths containing a given application class name, ordered by package last-update then root.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :className | string | APPCLASSID |

## Original Oracle SQL
```sql
SELECT
    A.PACKAGEROOT,
    A.QUALIFYPATH
FROM
    PSAPPCLASSDEFN A
    LEFT OUTER JOIN PSPACKAGEDEFN B ON B.PACKAGEROOT = A.PACKAGEROOT
                                       AND B.QUALIFYPATH = CASE
                                                           WHEN A.QUALIFYPATH = ':' THEN '.'
                                                           ELSE A.QUALIFYPATH
                                                           END
WHERE
    A.APPCLASSID = :className
ORDER BY
    TRUNC(B.LASTUPDDTTM) DESC NULLS LAST,
    A.PACKAGEROOT ASC
```

## Notes
- C# builds full path PACKAGEROOT:CLASSNAME when QUALIFYPATH=':', else PACKAGEROOT:QUALIFYPATH:CLASSNAME.
- Parameter dictionary key omits leading colon in source (className).
- maxRows: unlimited
