# GetOpenTargets / ApprovalRuleSet

## Purpose
Open-target search arm for Approval Rule Set definitions (PS_APPR_RULE_HDR).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :id_search | string | Escaped ID search term (underscore escaped as \_); empty = no ID filter |
| 2 | :descr_search | string | Escaped description search term; empty = no description filter |

## Original Oracle SQL
```sql
SELECT 'Approval Rule Set' AS DEFN_TYPE, APPR_RULE_SET AS ID, DESCR60 AS DESCR, LASTUPDDTTM
FROM PS_APPR_RULE_HDR
WHERE  (LENGTH(:id_search) = 0 OR UPPER(APPR_RULE_SET) LIKE UPPER(:id_search || '%') ESCAPE '\')
   AND (LENGTH(:descr_search) = 0 OR UPPER(DESCR60) LIKE UPPER('%' || :descr_search || '%') ESCAPE '\')
   AND (LENGTH(:id_search) > 0 OR LENGTH(:descr_search) > 0)
```

## Notes
- Table PS_APPR_RULE_HDR (underscore form → %Table(APPR_RULE_HDR) later).
- Arm from GetUnionClauseForType; outer CTE/ROW_NUMBER ranking wrapper is **not** extracted (ranking moves to C#).
- Shared binds :id_search / :descr_search (and wrapper-only :max_rows_per_type, :sort_by_date) supplied by GetOpenTargets.
- Filter requires at least one of id_search/descr_search non-empty.
- maxRows: per-type limit applied by outer wrapper in Oracle; for Meta-SQL apply in C#
