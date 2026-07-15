# GetRecordFields / fields

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Record name (RECNAME, uppercased) |

## Meta-SQL
```sql
SELECT r.FIELDNAME, r.FIELDNUM, f.FIELDTYPE, f.LENGTH, f.DECIMALPOS, r.USEEDIT
  FROM %Table(PSRECFIELDDB) r
  JOIN %Table(PSDBFIELD) f ON r.FIELDNAME = f.FIELDNAME
 WHERE r.RECNAME = :1
 ORDER BY r.FIELDNUM
```

## maxRows
unlimited

## Notes / residual risks
- C# maps rows to RecordFieldInfo and caches by record version. Private helper FetchRecordFieldsFromDb.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
