# GetEventMapItems / component

## Purpose
List event-map service items for a Component event map context.

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :component | string | PORTAL_URI_SEG2 (component) |
| 2 | :segment | string | PORTAL_URI_SEG3 (market/segment) |
| 3 | :cmpevent | string | Component event name (ptcs_cmpevent) |

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
    AND evt.ptcs_cmpevent = :cmpevent
```

## Notes
- EventMapType.Component branch of GetEventMapItems.
- maxRows: unlimited
