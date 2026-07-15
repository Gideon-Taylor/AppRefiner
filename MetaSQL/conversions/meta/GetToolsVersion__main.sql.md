# GetToolsVersion / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| — | — | No binds |

## Meta-SQL
```sql
SELECT TOOLSREL %Concat '.' %Concat PTPATCHREL
  FROM %Table(PSSTATUS)
```

## maxRows
1 expected

## Notes / residual risks
- C# falls back to "99.99.99" on error or empty result.
- Oracle `||` converted to `%Concat`.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
