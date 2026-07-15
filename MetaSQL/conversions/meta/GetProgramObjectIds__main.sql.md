# GetProgramObjectIds / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | OBJECTVALUE1 (padded with space if missing) |
| 2 | string | OBJECTVALUE2 |
| 3 | string | OBJECTVALUE3 |
| 4 | string | OBJECTVALUE4 |
| 5 | string | OBJECTVALUE5 |
| 6 | string | OBJECTVALUE6 |
| 7 | string | OBJECTVALUE7 |

## Meta-SQL
```sql
SELECT DISTINCT
       OBJECTID1, OBJECTID2, OBJECTID3, OBJECTID4, OBJECTID5, OBJECTID6, OBJECTID7,
       OBJECTVALUE1, OBJECTVALUE2, OBJECTVALUE3, OBJECTVALUE4, OBJECTVALUE5, OBJECTVALUE6, OBJECTVALUE7
  FROM %Table(PSPCMPROG)
 WHERE OBJECTVALUE1 = :1
   AND OBJECTVALUE2 = :2
   AND OBJECTVALUE3 = :3
   AND OBJECTVALUE4 = :4
   AND OBJECTVALUE5 = :5
   AND OBJECTVALUE6 = :6
   AND OBJECTVALUE7 = :7
```

## maxRows
unlimited (distinct program key matches)

## Notes / residual risks
- C# pads input objectValues to 7 elements with single-space strings.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
