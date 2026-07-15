# GetRecordFields / recExists

## Purpose
Get PSRECDEFN.VERSION for a record (existence + cache version check). Private helper GetRecordVersion.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :recordName | string | Record name (RECNAME, uppercased) |

## Original Oracle SQL
```sql
SELECT VERSION 
FROM PSRECDEFN 
WHERE RECNAME = :recordName
```

## Notes
- Returns -1 in C# when no row (record does not exist).
- maxRows: 1 expected
