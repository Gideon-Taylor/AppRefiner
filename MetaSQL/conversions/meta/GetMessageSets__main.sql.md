# GetMessageSets / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| — | — | No binds |

## Meta-SQL
```sql
SELECT MESSAGE_SET_NBR, DESCR
  FROM %Table(PSMSGSETDEFN)
 ORDER BY MESSAGE_SET_NBR
```

## maxRows
unlimited

## Notes / residual risks
- None.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
