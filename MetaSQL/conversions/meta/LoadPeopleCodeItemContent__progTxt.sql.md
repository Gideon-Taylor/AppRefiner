# LoadPeopleCodeItemContent / progTxt

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | int | OBJECTID1 |
| 2 | string | OBJECTVALUE1 |
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
SELECT PROGTXT
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2
   AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4
   AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6
   AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8
   AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10
   AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12
   AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14
 ORDER BY PROGSEQ
```

## maxRows
unlimited (all PROGSEQ chunks required)

## Notes / residual risks
- Missing program-field slots default to objId=0 and objVal=" " (single space) in C#.
- C# concatenates BLOB chunks; then loads name references via GetNameReferencesForProgram.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
