# GetAllHtmlDefinitions / names

## Purpose
List all distinct HTML/content object names from PSCONTENT.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| — | (none) | — | No binds |

## Original Oracle SQL
```sql
SELECT DISTINCT CONTNAME
FROM PSCONTENT
```

## Notes
- C# then calls GetHtmlDefinition for each name.
- maxRows: unlimited
