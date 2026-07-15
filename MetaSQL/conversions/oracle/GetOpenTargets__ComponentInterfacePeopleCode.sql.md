# GetOpenTargets / ComponentInterfacePeopleCode

## Purpose
Open-target arm: Component Interface PeopleCode (OBJECTID1=74).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :id_search | string | Escaped ID search term (underscore escaped as \_); empty = no ID filter |
| 2 | :descr_search | string | Escaped description search term; empty = no description filter |

## Original Oracle SQL
```sql
SELECT 'Component Interface PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 AS ID,
       OBJECTVALUE1 || '.' || OBJECTVALUE2 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 74
  AND (LENGTH(:id_search) = 0 OR UPPER(OBJECTVALUE1) LIKE UPPER(:id_search || '%') ESCAPE '\')
  AND (LENGTH(:descr_search) = 0 OR UPPER(OBJECTVALUE1 || '.' || OBJECTVALUE2) LIKE UPPER('%' || :descr_search || '%') ESCAPE '\')
  AND (LENGTH(:id_search) > 0 OR LENGTH(:descr_search) > 0)
```

## Notes
- Split from NON_CLASS_PPC_QUERY (OpenTargetType.NonClassPeopleCode); originally UNION ALL of all PeopleCode flavors.
- Outer CTE/ROW_NUMBER ranking wrapper is **not** extracted (ranking moves to C#).
- Shared binds :id_search / :descr_search from GetOpenTargets.
- DisplayTargetType in C# is set to DEFN_TYPE string for NonClassPeopleCode results.
- maxRows: per-type limit applied by outer wrapper in Oracle; for Meta-SQL apply in C#
