# GetEventMapItems / component

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | PORTAL_URI_SEG2 (component) |
| 2 | string | PORTAL_URI_SEG3 (market/segment) |
| 3 | string | Component event name (ptcs_cmpevent) |

## Meta-SQL
```sql
SELECT DISTINCT
       evt.portal_objname,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND portal.PORTAL_URI_SEG2 = :1
   AND portal.PORTAL_URI_SEG3 = :2
   AND evt.ptcs_cmpevent = :3
```

## maxRows
unlimited

## Notes / residual risks
- EventMapType.Component branch of GetEventMapItems.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
