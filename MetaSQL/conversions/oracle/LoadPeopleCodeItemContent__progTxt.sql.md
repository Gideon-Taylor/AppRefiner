# LoadPeopleCodeItemContent / progTxt

## Purpose
Load PeopleCode program text (PROGTXT BLOB chunks) for a program identified by 7 object id/value pairs.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :objId1 | int | OBJECTID1 |
| 2 | :objVal1 | string | OBJECTVALUE1 |
| 3 | :objId2 | int | OBJECTID2 |
| 4 | :objVal2 | string | OBJECTVALUE2 |
| 5 | :objId3 | int | OBJECTID3 |
| 6 | :objVal3 | string | OBJECTVALUE3 |
| 7 | :objId4 | int | OBJECTID4 |
| 8 | :objVal4 | string | OBJECTVALUE4 |
| 9 | :objId5 | int | OBJECTID5 |
| 10 | :objVal5 | string | OBJECTVALUE5 |
| 11 | :objId6 | int | OBJECTID6 |
| 12 | :objVal6 | string | OBJECTVALUE6 |
| 13 | :objId7 | int | OBJECTID7 |
| 14 | :objVal7 | string | OBJECTVALUE7 |

## Original Oracle SQL
```sql
SELECT PROGTXT FROM PSPCMPROG 
WHERE OBJECTID1 = :objId1 AND OBJECTVALUE1 = :objVal1
AND OBJECTID2 = :objId2 AND OBJECTVALUE2 = :objVal2
AND OBJECTID3 = :objId3 AND OBJECTVALUE3 = :objVal3
AND OBJECTID4 = :objId4 AND OBJECTVALUE4 = :objVal4
AND OBJECTID5 = :objId5 AND OBJECTVALUE5 = :objVal5
AND OBJECTID6 = :objId6 AND OBJECTVALUE6 = :objVal6
AND OBJECTID7 = :objId7 AND OBJECTVALUE7 = :objVal7
ORDER BY PROGSEQ
```

## Notes
- Missing program-field slots default to objId=0 and objVal="" "" (single space) in C#.
- C# concatenates BLOB chunks; then loads name references via GetNameReferencesForProgram.
- maxRows: unlimited (all PROGSEQ chunks required)
