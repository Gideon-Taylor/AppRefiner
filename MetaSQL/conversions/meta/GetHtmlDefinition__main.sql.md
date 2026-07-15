# GetHtmlDefinition / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Content name (CONTNAME) |

## Meta-SQL
```sql
SELECT CONTDATA
  FROM %Table(PSCONTENT)
 WHERE CONTNAME = :1
 ORDER BY SEQNUM
```

## maxRows
unlimited (all SEQNUM chunks required; C# concatenates CONTDATA)

## Notes / residual risks
- C# decodes CONTDATA as Unicode and concatenates; also scans for max %Bind(:n).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
