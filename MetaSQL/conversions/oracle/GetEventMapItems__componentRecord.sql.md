# GetEventMapItems / componentRecord

## Purpose
List event-map service items for a Component Record event map context.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :component | string | PORTAL_URI_SEG2 |
| 2 | :segment | string | PORTAL_URI_SEG3 |
| 3 | :record | string | RECNAME |
| 4 | :cmprecevent | string | Component-record event (ptcs_cmprecevent) |

## Original Oracle SQL
```sql
SELECT DISTINCT
    evt.portal_objname,
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
    AND portal.PORTAL_URI_SEG2 = :component
    AND portal.PORTAL_URI_SEG3 = :segment
    AND evt.RECNAME = :record
    AND evt.ptcs_cmprecevent = :cmprecevent
```

## Notes
- EventMapType.ComponentRecord branch of GetEventMapItems.
- maxRows: unlimited
