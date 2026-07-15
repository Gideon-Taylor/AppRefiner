# GetPeopleCodeItemMetadataForProject / projectItems

## Purpose
Fetch PeopleCode project items (metadata only) for a project from PSPROJECTITEM.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :projectName | string | Project name (PROJECTNAME) |

## Original Oracle SQL
```sql
SELECT OBJECTTYPE, 
       OBJECTID1, OBJECTVALUE1, 
       OBJECTID2, OBJECTVALUE2, 
       OBJECTID3, OBJECTVALUE3, 
       OBJECTID4, OBJECTVALUE4
FROM PSPROJECTITEM
WHERE PROJECTNAME = :projectName
AND OBJECTTYPE IN ( 8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66 )
```

## Notes
- OBJECTTYPE filter is PeopleCode object types (hardcoded in Oracle manager).
- C# maps each row via ProjectItem.ToProgramFields() into PeopleCodeItem metadata (no PROGTXT load).
- maxRows: unlimited
