# GetFieldType / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | FIELDNAME |

## Meta-SQL
```sql
SELECT FIELDTYPE
  FROM %Table(PSDBFIELD)
 WHERE FIELDNAME = :1
```

## maxRows
1 expected

## Notes / residual risks
- C# maps FIELDTYPE decimal codes to PeopleCodeType enum.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
