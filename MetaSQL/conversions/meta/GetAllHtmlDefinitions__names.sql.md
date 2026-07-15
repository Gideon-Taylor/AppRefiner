# GetAllHtmlDefinitions / names

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| — | — | No binds |

## Meta-SQL
```sql
SELECT DISTINCT CONTNAME
  FROM %Table(PSCONTENT)
```

## maxRows
unlimited

## Notes / residual risks
- C# then calls GetHtmlDefinition for each name.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
