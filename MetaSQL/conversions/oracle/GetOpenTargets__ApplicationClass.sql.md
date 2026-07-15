# GetOpenTargets / ApplicationClass

## Purpose
Open-target search arm for Application Class definitions (PSAPPCLASSDEFN); ID is full path.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :id_search | string | Escaped ID search term (underscore escaped as \_); empty = no ID filter |
| 2 | :descr_search | string | Escaped description search term; empty = no description filter |

## Original Oracle SQL
```sql
SELECT 'Application Class' AS DEFN_TYPE, 
       PACKAGEROOT || CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' || QUALIFYPATH END || ':' || APPCLASSID AS ID, 
       DESCR AS DESCR, SYSDATE AS LASTUPDDTTM
FROM PSAPPCLASSDEFN
WHERE  (LENGTH(:id_search) = 0 OR UPPER(PACKAGEROOT || CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' || QUALIFYPATH END || ':' || APPCLASSID) LIKE UPPER('%' || :id_search || '%') ESCAPE '\')
   AND (LENGTH(:descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' || :descr_search || '%') ESCAPE '\')
   AND (LENGTH(:id_search) > 0 OR LENGTH(:descr_search) > 0)
```

## Notes
- ID search uses contains-style LIKE ('%' || :id_search || '%') rather than prefix-only.
- LASTUPDDTTM is SYSDATE (no real last-update column used).
- Arm from GetUnionClauseForType; outer CTE/ROW_NUMBER ranking wrapper is **not** extracted (ranking moves to C#).
- Shared binds :id_search / :descr_search (and wrapper-only :max_rows_per_type, :sort_by_date) supplied by GetOpenTargets.
- Filter requires at least one of id_search/descr_search non-empty.
- maxRows: per-type limit applied by outer wrapper in Oracle; for Meta-SQL apply in C#
