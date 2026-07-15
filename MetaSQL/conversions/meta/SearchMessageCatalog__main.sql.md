# SearchMessageCatalog / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Uppercased search term wrapped as %term% for LIKE (MESSAGE_TEXT) |
| 2 | string | Same value as :1 (DESCRLONG LIKE); bind twice for SAM positional clarity |

## Meta-SQL
```sql
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE (%Upper(MESSAGE_TEXT) LIKE :1
        OR %Upper(%Substring(DESCRLONG, 1, 4000)) LIKE :2)
 ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
```

## maxRows
C#-side limit via maxRows; SQL itself unlimited

## Notes / residual risks
- Oracle used a single named :searchTerm twice; Meta-SQL splits to :1 and :2 — C# must bind the same search term value for both.
- Oracle DBMS_LOB.SUBSTR(DESCRLONG, 4000, 1) converted to %Substring(DESCRLONG, 1, 4000) (start then length).
- Global search only (setNumbers null/empty). Set-scoped search uses SearchMessageCatalogInSet with MESSAGE_SET_NBR = :3.
- DESCRLONG is CLOB; substring bounds match to first 4000 chars.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
