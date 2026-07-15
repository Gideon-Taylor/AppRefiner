# GetMessagesForSet / main

## Purpose
List message catalog entries for a given message set number.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :setNumber | int | MESSAGE_SET_NBR |

## Original Oracle SQL
```sql
SELECT MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
FROM PSMSGCATDEFN
WHERE MESSAGE_SET_NBR = :setNumber
ORDER BY MESSAGE_NBR
```

## Notes
- maxRows: unlimited
