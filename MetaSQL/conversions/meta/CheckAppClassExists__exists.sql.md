# CheckAppClassExists / exists

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | int | OBJECTID1 (package/class mapping) |
| 2 | string | OBJECTVALUE1 (uppercased path part) |
| 3 | int | OBJECTID2 |
| 4 | string | OBJECTVALUE2 |
| 5 | int | OBJECTID3 |
| 6 | string | OBJECTVALUE3 |
| 7 | int | OBJECTID4 |
| 8 | string | OBJECTVALUE4 |
| 9 | int | OBJECTID5 |
| 10 | string | OBJECTVALUE5 |
| 11 | int | OBJECTID6 |
| 12 | string | OBJECTVALUE6 |
| 13 | int | OBJECTID7 |
| 14 | string | OBJECTVALUE7 |

## Meta-SQL
```sql
SELECT 'Y'
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = :1 AND %Upper(OBJECTVALUE1) = :2
   AND OBJECTID2 = :3 AND %Upper(OBJECTVALUE2) = :4
   AND OBJECTID3 = :5 AND %Upper(OBJECTVALUE3) = :6
   AND OBJECTID4 = :7 AND %Upper(OBJECTVALUE4) = :8
   AND OBJECTID5 = :9 AND %Upper(OBJECTVALUE5) = :10
   AND OBJECTID6 = :11 AND %Upper(OBJECTVALUE6) = :12
   AND OBJECTID7 = :13 AND %Upper(OBJECTVALUE7) = :14
```

## maxRows
1 sufficient (existence check)

## Notes / residual risks
- C# builds 7 pairs from path: package parts OBJECTID 104+i, last part class OBJECTID 107, next slot OBJECTID 12 / ONEXECUTE; unused slots 0 / single space.
- Path values uppercased before bind; SQL also %Upper()s OBJECTVALUEn.
- Existence = any row with first column 'Y'.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
