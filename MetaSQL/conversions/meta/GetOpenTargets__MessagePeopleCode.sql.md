# GetOpenTargets / MessagePeopleCode

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | 'Y' if id search empty (match all ids), else 'N' |
| 2 | string | ID LIKE pattern (prefix-style term%, underscore-escaped in C#) |
| 3 | string | 'Y' if descr search empty, else 'N' |
| 4 | string | DESCR LIKE pattern (contains-style %term%, underscore-escaped in C#) |

## Meta-SQL
```sql
SELECT 'Message PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END AS ID,
       OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END %Concat CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' THEN '.' %Concat OBJECTVALUE3 ELSE '' END AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE (OBJECTID1 = 60 OR OBJECTID1 = 87)
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END) LIKE %Upper(:2))
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END %Concat CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' THEN '.' %Concat OBJECTVALUE3 ELSE '' END) LIKE %Upper(:4))
```

## maxRows
C#-side per-type limit (options.MaxRowsPerType); SQL itself unlimited

## Notes / residual risks
- Standalone per-type query; outer CTE/ROW_NUMBER ranking applied in C# (maxRows).
- Fixed binds: :1/:3 empty-match flags ('Y'/'N'); :2/:4 LIKE patterns (wildcards built in C#).
- ID pattern is prefix-style: C# binds :2 as term% (after underscore escaping).
- DESCR pattern is contains-style: C# binds :4 as %term%.
- Oracle LENGTH empty-search + at-least-one-nonempty filters replaced by :1/:3 flags; C# should skip query if both searches empty.
- OBJECTID filters kept as literals. DisplayTargetType in C# set from DEFN_TYPE for NonClassPeopleCode.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static