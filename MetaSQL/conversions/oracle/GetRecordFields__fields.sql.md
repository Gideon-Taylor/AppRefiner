# GetRecordFields / fields

## Purpose
Fetch field metadata for a record via PSRECFIELDDB joined to PSDBFIELD. Private helper FetchRecordFieldsFromDb.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :recordName | string | Record name (RECNAME, uppercased) |

## Original Oracle SQL
```sql
SELECT r.FIELDNAME, r.FIELDNUM, f.FIELDTYPE, f.LENGTH, f.DECIMALPOS, r.USEEDIT
FROM PSRECFIELDDB r
JOIN PSDBFIELD f ON r.FIELDNAME = f.FIELDNAME
WHERE r.RECNAME = :recordName
ORDER BY r.FIELDNUM
```

## Notes
- C# maps rows to RecordFieldInfo and caches by record version.
- maxRows: unlimited
