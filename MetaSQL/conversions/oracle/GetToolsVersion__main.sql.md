# GetToolsVersion / main

## Purpose
Read PeopleTools release and patch level from PSSTATUS.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| — | (none) | — | No binds |

## Original Oracle SQL
```sql
SELECT TOOLSREL || '.' || PTPATCHREL FROM PSSTATUS
```

## Notes
- C# falls back to ""99.99.99"" on error or empty result.
- maxRows: 1 expected
