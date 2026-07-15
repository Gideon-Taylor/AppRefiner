# GetSqlDefinition / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | SQL object id (SQLID) |

## Meta-SQL
```sql
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM
```

## maxRows
unlimited (all SEQNUM chunks required; C# concatenates SQLTEXT)

## Notes / residual risks
- C# concatenates all SQLTEXT rows into one definition string.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
