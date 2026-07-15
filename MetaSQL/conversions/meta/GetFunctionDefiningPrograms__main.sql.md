# GetFunctionDefiningPrograms / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| — | — | No binds |

## Meta-SQL
```sql
SELECT DISTINCT
       OBJECTID1, OBJECTVALUE1,
       OBJECTID2, OBJECTVALUE2,
       OBJECTID3, OBJECTVALUE3,
       OBJECTID4, OBJECTVALUE4,
       OBJECTID5, OBJECTVALUE5,
       OBJECTID6, OBJECTVALUE6,
       OBJECTID7, OBJECTVALUE7
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 1
   AND OBJECTID2 = 2
   AND OBJECTID3 = 12 AND OBJECTVALUE3 = 'FieldFormula'
```

## maxRows
unlimited

## Notes / residual risks
- Filters to record/field FieldFormula programs (OBJECTID 1,2,12). Constants are static literals, not binds.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
