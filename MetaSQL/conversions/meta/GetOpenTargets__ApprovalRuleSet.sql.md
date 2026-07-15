# GetOpenTargets / ApprovalRuleSet

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | 'Y' if id search empty (match all ids), else 'N' |
| 2 | string | ID LIKE pattern (prefix-style term%, underscore-escaped in C#) |
| 3 | string | 'Y' if descr search empty, else 'N' |
| 4 | string | DESCR LIKE pattern (contains-style %term%, underscore-escaped in C#) |

## Meta-SQL
```sql
SELECT 'Approval Rule Set' AS DEFN_TYPE,
       APPR_RULE_SET AS ID,
       DESCR60 AS DESCR,
       LASTUPDDTTM
  FROM %Table(APPR_RULE_HDR)
 WHERE (:1 = 'Y' OR %Upper(APPR_RULE_SET) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR60) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY APPR_RULE_SET
```

## maxRows
C#-side per-type limit (options.MaxRowsPerType); SQL itself unlimited

## Notes / residual risks
- Standalone per-type query; outer CTE/ROW_NUMBER ranking applied in C# (maxRows).
- Fixed binds: :1/:3 empty-match flags ('Y'/'N'); :2/:4 LIKE patterns (wildcards built in C#).
- ID pattern is prefix-style: C# binds :2 as term% (after underscore escaping).
- DESCR pattern is contains-style: C# binds :4 as %term%.
- Oracle LENGTH empty-search + at-least-one-nonempty filters replaced by :1/:3 flags; C# should skip query if both searches empty.
- Table PS_APPR_RULE_HDR → %Table(APPR_RULE_HDR).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static