# Field-Slot Compiler Results

Compiler-verified behavior (App Designer, 2026-10-23) for every non-reference `field`
parameter in the signature library (`PeopleCodeTypeInfo.Generator/Data`), from three probe
files. Argument forms tested:

| Form | Example |
|---|---|
| A — number buffer field | `IS_AT_COMMENTS.IS_AT_COMMENT_ID` |
| E — character buffer field | `PSOPRDEFN.OPRID` |
| B — definition reference | `Field.IS_AT_COMMENT_ID` |
| C — Field object | `&fld` (also rejected where tested: `&rec.FIELD`, `GetField(...)`, `Record.REC.FIELD`) |
| D — plain variable | `&anyVar` (declared `any`) |

Errors seen: "field name required" (not a field-name expression) and
"invalid function parameter type" (wrong data type / kind).

## Classification

| Kind | A | E | B | C | D | Signature |
|---|---|---|---|---|---|---|
| Field name, any data type | ✅ | (✅) | ✅ | ❌ | ❌ | field name of `any` |
| Field name, character data | ❌ type | ✅ | ✅ | ❌ | ❌ | field name of `string` |
| Plain string value | ❌ type | ✅ | ✅ | ❌ | ✅ | `string` |
| Field object | ❌ | ❌ | ❌ | ✅ | ✅ | `field` |
| Any value | ✅ | – | ✅ | ✅ | ✅ | `any` (or permissive union) |

Rules this implies for the checker:

- A **field-name** slot accepts a buffer field reference (bare `REC.FIELD`, or bare `FIELD`
  in record PeopleCode) or a `Field.X` reference — never a variable, Field object, or
  other expression. When the slot has a data type, a buffer reference's field data type
  must match it; `Field.X` is not type-checked (B passed into character slots with a
  number field).
- A parenthesized buffer reference `(REC.FIELD)` still counts as a field name; a string
  literal (e.g. builtin `GetField("OPRID")`) does not (confirmed 2026-10-25).
- `Field.X` passes into plain `string` slots because a reference converts to string.
- `&anyVar` passes into `field` slots because an `any` may hold a Field object.

## Per-slot results

### Field name, any data type (D → "field name required")

- GetNextNumberWithGaps, GetNextNumberWithGapsCommit — `record_field`
- SetDefault, SetDefaultAll, SetDefaultNext, SetDefaultPrior
- SetDefaultNextRel, SetDefaultPriorRel — both slots
- NextEffDt, PriorEffDt; NextRelEffDt, PriorRelEffDt, GetRelField — both slots
- AddKeyListItem, DeleteRecord, InsertImage; CropImage — both slots
- builtin GetField
- GetChart, GetGanttChart, GetTimeLine, GetLEDGauge, GetOrgChart, GetRatingBoxChart,
  GetRatingGauge, GetSparkChart, GetStatusMeterGauge
- GetCalendarDate — `outfieldname`, `company`, `paygroup` (all three are field names)
- GenerateScript{ContentRel,Content,PortalRel,Portal,Relative}URL — `field_name`
- SetLabel — scroll-path **target** and **level-1 record** slots (probe #2 cases 080/081;
  Hide/Gray/UnGray/UnHide take any value in the same positions)
- Field.GetRelated
- All Chart setters (SetDataColor … SetSvgGradientOffset4, SetDataXAxis/YAxis/ZAxis,
  SetFunnelData*, SetOLData*, SetSelectedData)
- All Gantt setters (SetActual*, SetPlanned*, SetTask*, SetWBSNumbering)
- All TimeLine setters (SetDataSeries, SetDataItemStart/Description/Title)

### Field name, character data (A → type error, E ✅, D → "field name required")

- SetDBFieldLength, SetDBFieldAuxFlag, SetDBFieldCharDefn, SetDBFieldFormat,
  SetDBFieldFormatLength, SetDBFieldLabel, SetDBFieldNotUsed
- BulkInsertField, RenameDBField — both slots
- SetRecFieldEditTable, SetRecFieldKey, SetPageFieldPageFieldName — `field_name`

(Only number vs. character tested; date/other field types untested.)

### Plain `string` (D ✅, E ✅)

- GetFieldDefn
- BulkDeleteField (inconsistent with BulkInsertField, which is a field-name slot)
- DateTimeToTimeZone — both timezone slots; FormatDateTime — `timezone`
- GetSetId — `set_ctrl_fieldvalue` (2nd slot)
- TreeDetailInNode — `detail_value`

### `field` object (C ✅, D ✅, A/B/E ❌)

- Field.CopyDisplayMask
- SetControlValue — `field`

### Any value (A B C D all ✅)

- PriorValue (both forms), FetchValue (all positions), FieldChanged (both forms),
  DeleteImage, GetStoredFormat, SetDisplayFormat, SetLabel `fieldname`
- Hide, UnHide, Gray, UnGray, SetCursorPos, UpdateValue — field and scroll-path slots
- InsertRow, ScrollFlush, ActiveRowCount, RecordChanged, RecordDeleted, RecordNew
- ClearSearchDefault, ClearSearchEdit, SetSearchDefault, SetSearchEdit
- GetNextNumber, GetSetId `set_ctrl_field` (1st slot)
- Prompt, CreateRowset `controlling_field`, SortScroll, Rowset.Sort
- All / AllOrNone / None / OnlyOne / OnlyOneOrNone
- Keylists: Transfer, TransferExact, TransferExactTop, TransferModeless, TransferTop,
  TransferNode, TransferPortal, DoModalComponent, DoModalXComponent,
  GenerateComponent*URL, GenerateScript*URL

## Other signature corrections found along the way

- `SetPageFieldPageFieldName` — `page_name` must be `@PAGE` (string rejected).
- `MARKET` is **not** a reference keyword to the compiler: `Market.GBL` parses as
  `REC.FIELD`. `@MARKET` slots (TransferNode, TransferPortal, GenerateComponent*URL)
  should be `string` (`"GBL"` / `%Market`). AppRefiner's
  `ReferenceTypeInfo.SpecialReferenceKeywords` also lists `MARKET` and should not.
- `TransferNode` and `TransferPortal` return nothing (`-> void`), contrary to PeopleBooks.
