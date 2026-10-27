# GetComponentInterface / defn

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Component interface name (BCNAME, uppercased) |

## Meta-SQL
```sql
SELECT VERSION, BCSTDMETHODS FROM %Table(PSBCDEFN) WHERE BCNAME = :1
```

## maxRows
1 expected

## Notes / residual risks
- No row means the CI doesn't exist; C# returns null. Query failures propagate (never "doesn't exist").
- VERSION keys the per-name definition cache: every GetComponentInterface call runs this query and re-fetches items and Methods only when the version changed.
- BCSTDMETHODS bits: Cancel = 1, Create = 2, Find = 4, Get = 8, Save = 16.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
