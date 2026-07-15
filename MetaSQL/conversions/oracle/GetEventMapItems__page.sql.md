# GetEventMapItems / page

## Purpose
List event-map service items for a Page event map context.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :page | string | PNLNAME |
| 2 | :cmprecevent | string | Event name (ptcs_cmprecevent) |

## Original Oracle SQL
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
FROM
    psptcssrvconf evt
    INNER JOIN psprsmdefn portal ON portal.portal_objname = evt.portal_objname
    INNER JOIN psptcssrvdefn svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
WHERE
    evt.portal_name = '_PTCS_PTEVMAP'
    AND evt.PNLNAME = :page
    AND evt.ptcs_cmprecevent = :cmprecevent
```

## Notes
- EventMapType.Page branch; also selects PORTAL_URI_SEG2/3 for component/segment on the item.
- maxRows: unlimited
