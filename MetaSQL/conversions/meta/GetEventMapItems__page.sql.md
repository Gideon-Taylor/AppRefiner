# GetEventMapItems / page

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | PNLNAME |
| 2 | string | Event name (ptcs_cmprecevent) |

## Meta-SQL
```sql
SELECT DISTINCT
       evt.portal_objname,
       portal.PORTAL_URI_SEG2,
       portal.PORTAL_URI_SEG3,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND evt.PNLNAME = :1
   AND evt.ptcs_cmprecevent = :2
```

## maxRows
unlimited

## Notes / residual risks
- EventMapType.Page branch; also selects PORTAL_URI_SEG2/3 for component/segment on the item.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [x] static
