# GetMessageCatalogEntry / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | int | MESSAGE_SET_NBR |
| 2 | int | MESSAGE_NBR |

## Meta-SQL
```sql
SELECT MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE MESSAGE_SET_NBR = :1
   AND MESSAGE_NBR = :2
```

## maxRows
1 expected

## Notes / residual risks
- None.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
