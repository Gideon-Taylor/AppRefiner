# GetEventMapXrefs / main

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Package root from class path (packageroot) |
| 2 | string | Middle package path (':' if none) (qualifypath) |
| 3 | string | Class name / last path segment (appclassid) |

## Meta-SQL
```sql
SELECT DISTINCT
       evt.portal_objname,
       portal.PORTAL_URI_SEG2 AS COMPONENT,
       portal.PORTAL_URI_SEG3 AS SEGMENT,
       evt.RECNAME AS RECORD,
       evt.FIELDNAME AS FIELD,
       evt.PNLNAME,
       evt.SEQNUM,
       evt.ptcs_procseq,
       evt.ptcs_cmpevent,
       evt.ptcs_cmprecevent
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND svc.packageroot = :1
   AND svc.qualifypath = :2
   AND svc.appclassid = :3
```

## maxRows
unlimited

## Notes / residual risks
- DISTINCT covers CREF appearing in multiple portal registries.
- C# derives EventMapType from populated component/segment/record/field/page fields.
- Record names PSPTCSSRVCONF / PSPRSMDEFN / PSPTCSSRVDEFN are PS-without-underscore tables.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
