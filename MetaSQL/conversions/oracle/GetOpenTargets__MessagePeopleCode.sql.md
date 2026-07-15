# GetOpenTargets / MessagePeopleCode

## Purpose
Open-target arm: Message/Subscription PeopleCode (OBJECTID1=60 or 87).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :id_search | string | Escaped ID search term (underscore escaped as \_); empty = no ID filter |
| 2 | :descr_search | string | Escaped description search term; empty = no description filter |

## Original Oracle SQL
```sql
SELECT 'Message PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 || 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' || OBJECTVALUE2 ELSE '' END AS ID,
       OBJECTVALUE1 || 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' || OBJECTVALUE2 ELSE '' END ||
       CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' 
            THEN '.' || OBJECTVALUE3 ELSE '' END AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE (OBJECTID1 = 60 OR OBJECTID1 = 87)
  AND (LENGTH(:id_search) = 0 OR 
       UPPER(OBJECTVALUE1 || CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' || OBJECTVALUE2 ELSE '' END) LIKE UPPER(:id_search || '%') ESCAPE '\')
  AND (LENGTH(:descr_search) = 0 OR 
       UPPER(OBJECTVALUE1 || 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' || OBJECTVALUE2 ELSE '' END ||
       CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' 
            THEN '.' || OBJECTVALUE3 ELSE '' END) LIKE UPPER('%' || :descr_search || '%') ESCAPE '\')
  AND (LENGTH(:id_search) > 0 OR LENGTH(:descr_search) > 0)
```

## Notes
- Split from NON_CLASS_PPC_QUERY (OpenTargetType.NonClassPeopleCode); originally UNION ALL of all PeopleCode flavors.
- Outer CTE/ROW_NUMBER ranking wrapper is **not** extracted (ranking moves to C#).
- Shared binds :id_search / :descr_search from GetOpenTargets.
- DisplayTargetType in C# is set to DEFN_TYPE string for NonClassPeopleCode results.
- maxRows: per-type limit applied by outer wrapper in Oracle; for Meta-SQL apply in C#
