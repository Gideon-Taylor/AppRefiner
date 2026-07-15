# GetMessageCatalogEntry / main

## Purpose
Fetch a single message catalog entry by set and message number.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :setNumber | int | MESSAGE_SET_NBR |
| 2 | :messageNumber | int | MESSAGE_NBR |

## Original Oracle SQL
```sql
SELECT MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
FROM PSMSGCATDEFN
WHERE MESSAGE_SET_NBR = :setNumber
AND MESSAGE_NBR = :messageNumber
```

## Notes
- maxRows: 1 expected
