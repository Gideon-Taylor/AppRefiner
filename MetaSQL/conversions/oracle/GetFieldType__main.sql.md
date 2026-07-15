# GetFieldType / main

## Purpose
Look up PSDBFIELD.FIELDTYPE for a field name and map to PeopleCodeType.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :fieldName | string | FIELDNAME |

## Original Oracle SQL
```sql
SELECT FIELDTYPE FROM PSDBFIELD WHERE FIELDNAME = :fieldName
```

## Notes
- C# maps FIELDTYPE decimal codes to PeopleCodeType enum.
- Parameter dictionary key omits leading colon in source (fieldName).
- maxRows: 1 expected
