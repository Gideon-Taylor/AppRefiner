# GetProgramObjectIds / main

## Purpose
Resolve OBJECTIDn values for programs matching seven OBJECTVALUEn pads.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :val1 | string | OBJECTVALUE1 (padded with space if missing) |
| 2 | :val2 | string | OBJECTVALUE2 |
| 3 | :val3 | string | OBJECTVALUE3 |
| 4 | :val4 | string | OBJECTVALUE4 |
| 5 | :val5 | string | OBJECTVALUE5 |
| 6 | :val6 | string | OBJECTVALUE6 |
| 7 | :val7 | string | OBJECTVALUE7 |

## Original Oracle SQL
```sql
SELECT DISTINCT OBJECTID1, OBJECTID2, OBJECTID3, OBJECTID4, OBJECTID5, OBJECTID6, OBJECTID7,
       OBJECTVALUE1, OBJECTVALUE2, OBJECTVALUE3, OBJECTVALUE4, OBJECTVALUE5, OBJECTVALUE6, OBJECTVALUE7
FROM PSPCMPROG 
WHERE OBJECTVALUE1 = :val1 
  AND OBJECTVALUE2 = :val2 
  AND OBJECTVALUE3 = :val3 
  AND OBJECTVALUE4 = :val4 
  AND OBJECTVALUE5 = :val5 
  AND OBJECTVALUE6 = :val6 
  AND OBJECTVALUE7 = :val7
```

## Notes
- C# pads input objectValues to 7 elements with single-space strings.
- Parameter dictionary keys omit leading colon in source (val1..val7) but SQL uses :val1..:val7.
- maxRows: unlimited (distinct program key matches)
