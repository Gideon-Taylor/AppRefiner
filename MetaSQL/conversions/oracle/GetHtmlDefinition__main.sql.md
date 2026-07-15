# GetHtmlDefinition / main

## Purpose
Retrieve HTML content binary chunks for a named content object from PSCONTENT.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :objectName | string | Content name (CONTNAME) |

## Original Oracle SQL
```sql
SELECT CONTDATA
FROM PSCONTENT
WHERE CONTNAME = :objectName
ORDER BY SEQNUM
```

## Notes
- C# decodes CONTDATA as Unicode and concatenates; also scans for max %Bind(:n).
- maxRows: unlimited (all SEQNUM chunks required)
