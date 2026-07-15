# GetSqlDefinition / main

## Purpose
Retrieve SQL definition text chunks for a named SQL object from PSSQLDEFN/PSSQLTEXTDEFN, ordered by sequence.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :objectName | string | SQL object id (SQLID) |

## Original Oracle SQL
```sql
SELECT B.SQLTEXT
FROM PSSQLDEFN A, PSSQLTEXTDEFN B
WHERE A.SQLID = B.SQLID
AND A.SQLID = :objectName
ORDER BY B.SEQNUM
```

## Notes
- C# concatenates all SQLTEXT rows into one definition string.
- maxRows: unlimited (all SEQNUM chunks required)
