# GetDefinitionNames / template

## Binds
| # | CLR type | Meaning |
|--:|----------|---------|
| 1 | string | Upper-cased, LIKE-escaped (backslash) prefix followed by `%` |

## Meta-SQL
```sql
SELECT {Key}
  FROM %Table({Table})
 WHERE {Key} LIKE :1 ESCAPE '\'[ AND {Filter}]
 ORDER BY {Key}
```

`{Key}`, `{Table}` and `{Filter}` are filled from `IDataManager.DefinitionNameSource(keyword)`:

| Keyword(s) | Table | Key | Filter |
|---|---|---|---|
| Record | PSRECDEFN | RECNAME | |
| Field | PSDBFIELD | FIELDNAME | |
| Page, Panel | PSPNLDEFN | PNLNAME | |
| Component, PanelGroup | PSPNLGRPDEFN | PNLGRPNAME | |
| CompIntfc | PSBCDEFN | BCNAME | |
| HTML | PSCONTDEFN | CONTNAME | CONTTYPE = 4 |
| Image | PSCONTDEFN | CONTNAME | CONTTYPE = 1 |
| StyleSheet | PSCONTDEFN | CONTNAME | CONTTYPE = 9 |
| SQL | PSSQLDEFN | SQLID | SQLTYPE = '0' |
| MenuName | PSMENUDEFN | MENUNAME | |
| Message | PSMSGDEFN | MSGNAME | |
| FileLayout | PSFLDDEFN | FLDDEFNNAME | |
| Node | PSMSGNODEDEFN | MSGNODENAME | |
| Operation | PSOPERATION | IB_OPERATIONNAME | |

## maxRows
C#-side limit via maxRows (caller passes 501: 500 shown plus one to detect truncation); SQL itself unlimited

## Notes / residual risks
- One catalog template (`SqlProxyQueryCatalog.GetDefinitionNamesTemplate`) shared by every keyword; `string.Format` fills `{0}` = key, `{1}` = record, `{2}` = `""` or `" AND <filter>"`.
- Template values come only from the fixed keyword map, never from user input; the prefix is always bound as `:1`.
- No `%Upper`: definition names are stored upper case and the caller upper-cases the prefix, so the bare key column can use its index.
- Unsupported keywords return an empty list without querying.

## Checklist
- [x] %Table record names
- [x] positional binds only
- [ ] static (table/key/filter substituted from a fixed map)
