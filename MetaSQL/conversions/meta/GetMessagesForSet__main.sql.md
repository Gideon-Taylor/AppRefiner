# GetMessagesForSet / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | int | MESSAGE_SET_NBR |

## Meta-SQL
```sql
SELECT MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE MESSAGE_SET_NBR = :1
 ORDER BY MESSAGE_NBR
```

## maxRows
unlimited

## Notes / residual risks
- None.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
