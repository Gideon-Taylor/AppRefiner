# GetMessageSets / main

## Purpose
List all message catalog set definitions.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| — | (none) | — | No binds |

## Original Oracle SQL
```sql
SELECT MESSAGE_SET_NBR, DESCR
FROM PSMSGSETDEFN
ORDER BY MESSAGE_SET_NBR
```

## Notes
- maxRows: unlimited
