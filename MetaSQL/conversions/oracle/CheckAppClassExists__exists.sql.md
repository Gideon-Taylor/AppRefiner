# CheckAppClassExists / exists

## Purpose
Check whether an Application Class path exists as a PeopleCode program (OnExecute) in PSPCMPROG.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :objId1 | int | OBJECTID1 (package/class mapping) |
| 2 | :objVal1 | string | OBJECTVALUE1 (uppercased path part) |
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
SELECT 'Y' FROM PSPCMPROG 
WHERE OBJECTID1 = :objId1 AND UPPER(OBJECTVALUE1) = :objVal1
AND OBJECTID2 = :objId2 AND UPPER(OBJECTVALUE2) = :objVal2
AND OBJECTID3 = :objId3 AND UPPER(OBJECTVALUE3) = :objVal3
AND OBJECTID4 = :objId4 AND UPPER(OBJECTVALUE4) = :objVal4
AND OBJECTID5 = :objId5 AND UPPER(OBJECTVALUE5) = :objVal5
AND OBJECTID6 = :objId6 AND UPPER(OBJECTVALUE6) = :objVal6
AND OBJECTID7 = :objId7 AND UPPER(OBJECTVALUE7) = :objVal7
```

## Notes
- C# builds 7 pairs from path: package parts OBJECTID 104+i, last part class OBJECTID 107, next slot OBJECTID 12 / ONEXECUTE; unused slots 0 / single space.
- Path values uppercased before bind; SQL also UPPER()s OBJECTVALUEn.
- Existence = any row with first column 'Y'.
- maxRows: existence only (1 sufficient)
