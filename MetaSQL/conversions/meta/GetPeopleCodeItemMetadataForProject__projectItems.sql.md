# GetPeopleCodeItemMetadataForProject / projectItems

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Project name (PROJECTNAME) |

## Meta-SQL
```sql
SELECT OBJECTTYPE,
       OBJECTID1, OBJECTVALUE1,
       OBJECTID2, OBJECTVALUE2,
       OBJECTID3, OBJECTVALUE3,
       OBJECTID4, OBJECTVALUE4
  FROM %Table(PSPROJECTITEM)
 WHERE PROJECTNAME = :1
   AND OBJECTTYPE IN (8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66)
```

## maxRows
unlimited

## Notes / residual risks
- OBJECTTYPE list is static PeopleCode object types (hardcoded).
- C# maps each row via ProjectItem.ToProgramFields() into PeopleCodeItem metadata (no PROGTXT load).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
