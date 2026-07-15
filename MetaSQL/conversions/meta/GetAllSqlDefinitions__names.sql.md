# GetAllSqlDefinitions / names

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| — | — | No binds |

## Meta-SQL
```sql
SELECT DISTINCT A.SQLID
  FROM %Table(PSSQLDEFN) A
 WHERE EXISTS (
       SELECT 1
         FROM %Table(PSSQLTEXTDEFN) B
        WHERE A.SQLID = B.SQLID
       )
```

## maxRows
unlimited

## Notes / residual risks
- C# then calls GetSqlDefinition for each name.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
