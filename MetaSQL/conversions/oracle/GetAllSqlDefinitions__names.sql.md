# GetAllSqlDefinitions / names

## Purpose
List all SQL object names that have at least one text chunk in PSSQLTEXTDEFN.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| — | (none) | — | No binds |

## Original Oracle SQL
```sql
SELECT DISTINCT A.SQLID
FROM PSSQLDEFN A
WHERE EXISTS (
    SELECT 1 FROM PSSQLTEXTDEFN B
    WHERE A.SQLID = B.SQLID
)
```

## Notes
- C# then calls GetSqlDefinition for each name.
- maxRows: unlimited
