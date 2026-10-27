# GetComponentInterface / items

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Component interface name (BCNAME, uppercased) |

## Meta-SQL
```sql
SELECT i.BCTYPE, i.BCITEMPARENT, i.BCITEMNAME, i.SEQUENCE_NBR_6, i.BCACCESS,
       i.RECNAME, i.FIELDNAME, i.COMMENTSHORT, f.FIELDTYPE
  FROM %Table(PSBCITEM) i
  LEFT OUTER JOIN %Table(PSDBFIELD) f ON f.FIELDNAME = i.FIELDNAME
 WHERE i.BCNAME = :1
 ORDER BY i.SEQUENCE_NBR_6
```

## maxRows
10000 (`MultiChunkMaxRows`). A result of 10000 or more rows may be truncated, so the SQL proxy manager throws instead of building a partial definition; the lookup then reads as Unknown (unchecked), never as missing items. The Oracle and SQL Server managers are unbounded.

## Notes / residual risks
- BCTYPE: 1 GETKEYS, 2 CREATEKEYS, 3 collection, 4 property, 5 FINDKEYS. BCITEMPARENT 'PS_ROOT' is the CI root. BCACCESS: 1 read/write, 2 read-only.
- LEFT OUTER JOIN because collection rows carry no real field; a null FIELDTYPE maps to Unknown.
- C# trims BCITEMPARENT, BCITEMNAME, RECNAME and FIELDNAME, and trims trailing whitespace from COMMENTSHORT (CompIntfcDefinitionBuilder nulls any whitespace-only comment).

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
