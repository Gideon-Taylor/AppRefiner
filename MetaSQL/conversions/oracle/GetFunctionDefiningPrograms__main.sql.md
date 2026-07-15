# GetFunctionDefiningPrograms / main

## Purpose
List PeopleCode programs that may define functions (Record.Field.FieldFormula).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| — | (none) | — | No binds |

## Original Oracle SQL
```sql
SELECT DISTINCT 
    OBJECTID1, OBJECTVALUE1,
    OBJECTID2, OBJECTVALUE2, 
    OBJECTID3, OBJECTVALUE3,
    OBJECTID4, OBJECTVALUE4,
    OBJECTID5, OBJECTVALUE5,
    OBJECTID6, OBJECTVALUE6,
    OBJECTID7, OBJECTVALUE7
FROM PSPCMPROG
WHERE OBJECTID1 = 1
AND OBJECTID2 = 2
AND OBJECTID3 = 12 AND OBJECTVALUE3 = 'FieldFormula'
```

## Notes
- Filters to record/field FieldFormula programs (OBJECTID 1,2,12).
- maxRows: unlimited
