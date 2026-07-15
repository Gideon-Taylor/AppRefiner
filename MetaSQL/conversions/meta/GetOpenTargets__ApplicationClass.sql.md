# GetOpenTargets / ApplicationClass

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | 'Y' if id search empty (match all ids), else 'N' |
| 2 | string | ID LIKE pattern (contains-style %term%, underscore-escaped in C#) |
| 3 | string | 'Y' if descr search empty, else 'N' |
| 4 | string | DESCR LIKE pattern (contains-style %term%, underscore-escaped in C#) |

## Meta-SQL
```sql
SELECT 'Application Class' AS DEFN_TYPE,
       PACKAGEROOT %Concat CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' %Concat QUALIFYPATH END %Concat ':' %Concat APPCLASSID AS ID,
       DESCR AS DESCR,
       %CurrentDateTimeIn AS LASTUPDDTTM
  FROM %Table(PSAPPCLASSDEFN)
 WHERE (:1 = 'Y' OR %Upper(PACKAGEROOT %Concat CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' %Concat QUALIFYPATH END %Concat ':' %Concat APPCLASSID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
```

## maxRows
C#-side per-type limit (options.MaxRowsPerType); SQL itself unlimited

## Notes / residual risks
- Standalone per-type query; outer CTE/ROW_NUMBER ranking applied in C# (maxRows).
- Fixed binds: :1/:3 empty-match flags ('Y'/'N'); :2/:4 LIKE patterns (wildcards built in C#).
- ID pattern is contains-style: C# must bind :2 as %term% (not prefix FOO%).
- DESCR pattern is contains-style: C# binds :4 as %term%.
- Oracle LENGTH empty-search + at-least-one-nonempty filters replaced by :1/:3 flags; C# should skip query if both searches empty.
- Oracle SYSDATE dummy LASTUPDDTTM → %CurrentDateTimeIn (no real last-update column).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static