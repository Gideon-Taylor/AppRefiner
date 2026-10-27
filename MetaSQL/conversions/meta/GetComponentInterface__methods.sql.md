# GetComponentInterface / methods

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | int | OBJECTID1 = 74 (PSCLASSID.COMPONENTINTERFACE) |
| 2 | string | OBJECTVALUE1 = component interface name (uppercased) |
| 3 | int | OBJECTID2 = 12 (PSCLASSID.METHOD) |
| 4 | string | OBJECTVALUE2 = 'Methods' |
| 5 | int | OBJECTID3 = 0 |
| 6 | string | OBJECTVALUE3 = ' ' |
| 7 | int | OBJECTID4 = 0 |
| 8 | string | OBJECTVALUE4 = ' ' |
| 9 | int | OBJECTID5 = 0 |
| 10 | string | OBJECTVALUE5 = ' ' |
| 11 | int | OBJECTID6 = 0 |
| 12 | string | OBJECTVALUE6 = ' ' |
| 13 | int | OBJECTID7 = 0 |
| 14 | string | OBJECTVALUE7 = ' ' |

## Meta-SQL
Reuses `SqlProxyQueryCatalog.LoadPeopleCodeItemContent` (see LoadPeopleCodeItemContent__progTxt.sql.md).
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
- Loaded through LoadPeopleCodeItemContent directly (not GetPeopleCodeProgram, which swallows errors), so a query failure throws instead of silently dropping the custom methods.
- No program means no custom methods (methodsSource null). The source isn't kept after CompIntfcDefinitionBuilder parses it.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
