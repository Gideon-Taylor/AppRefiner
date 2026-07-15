# GetEventMapXrefs / main

## Purpose
Find event-map xrefs for an application class path (portal/component/record/field/page bindings).

## Original binds (Oracle named → will become positional)
| Order | Oracle name | CLR type | Meaning |
|------:|-------------|----------|---------|
| 1 | :root | string | Package root from class path |
| 2 | :path | string | Middle package path (':' if none) |
| 3 | :class | string | Class name (last path segment) |

## Original Oracle SQL
```sql
SELECT DISTINCT
    evt.portal_objname,
    portal.PORTAL_URI_SEG2 AS COMPONENT,
    portal.PORTAL_URI_SEG3 AS SEGMENT,
    evt.RECNAME as RECORD,
    evt.FIELDNAME as FIELD,
    evt.PNLNAME,
    evt.SEQNUM,
    evt.ptcs_procseq,
    evt.ptcs_cmpevent,
    evt.ptcs_cmprecevent
FROM
    psptcssrvconf evt
    INNER JOIN psprsmdefn    portal ON portal.portal_objname = evt.portal_objname
    INNER JOIN psptcssrvdefn svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
WHERE
    evt.portal_name = '_PTCS_PTEVMAP'
    AND svc.packageroot = :root
    AND svc.qualifypath = :path
    AND svc.appclassid = :class
```

## Notes
- DISTINCT covers CREF appearing in multiple portal registries.
- C# derives EventMapType from populated component/segment/record/field/page fields.
- maxRows: unlimited
