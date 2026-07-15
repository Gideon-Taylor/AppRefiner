# SearchMessageCatalog / main

## Purpose
Search message catalog by text match on MESSAGE_TEXT or DESCRLONG (first 4000 chars via DBMS_LOB.SUBSTR).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :searchTerm | string | Uppercased search term wrapped as %term% for LIKE |

## Original Oracle SQL
```sql
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
FROM PSMSGCATDEFN
WHERE (UPPER(MESSAGE_TEXT) LIKE :searchTerm
       OR UPPER(DBMS_LOB.SUBSTR(DESCRLONG, 4000, 1)) LIKE :searchTerm)
ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
```

## Notes
- **C#-side dynamic pieces omitted from SQL extract:**
  - Optional set filter via BuildMessageSetFilter(setNumbers): AND (MESSAGE_SET_NBR IN (...)) OR-chunked by 900 ints (interpolated, not bound).
  - FETCH FIRST {limit} ROWS ONLY appended with limit from method argument.
- BindByName = true so one :searchTerm serves both LIKE placeholders.
- DESCRLONG is CLOB; DBMS_LOB.SUBSTR bounds match to first 4000 chars.
- maxRows: applied in C# via FETCH FIRST {limit}; ranking/filter remains C#-side for Meta-SQL
