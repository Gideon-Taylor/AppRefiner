# Meta-SQL conversion inventory

Track extraction and conversion status for each SqlProxyDataManager query.

| Method | Query id | Oracle file | Meta file | Status |
|--------|----------|-------------|-----------|--------|
| GetSqlDefinition | main | oracle/GetSqlDefinition__main.sql.md | meta/GetSqlDefinition__main.sql.md | converted |
| GetAllSqlDefinitions | names | oracle/GetAllSqlDefinitions__names.sql.md | meta/GetAllSqlDefinitions__names.sql.md | converted |
| GetHtmlDefinition | main | oracle/GetHtmlDefinition__main.sql.md | meta/GetHtmlDefinition__main.sql.md | converted |
| GetAllHtmlDefinitions | names | oracle/GetAllHtmlDefinitions__names.sql.md | meta/GetAllHtmlDefinitions__names.sql.md | converted |
| GetPeopleCodeItemMetadataForProject | projectItems | oracle/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | meta/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | converted |
| LoadPeopleCodeItemContent | progTxt | oracle/LoadPeopleCodeItemContent__progTxt.sql.md | meta/LoadPeopleCodeItemContent__progTxt.sql.md | converted |
| GetNameReferencesForProgram | pcmName | oracle/GetNameReferencesForProgram__pcmName.sql.md | meta/GetNameReferencesForProgram__pcmName.sql.md | converted |
| CheckAppClassExists | exists | oracle/CheckAppClassExists__exists.sql.md | meta/CheckAppClassExists__exists.sql.md | converted |
| GetAppClassSourceByPath | (uses Check + Load paths) | — | — | n/a (C#) |
| GetAppPackageItems | subpackagesRoot | oracle/GetAppPackageItems__subpackagesRoot.sql.md | meta/GetAppPackageItems__subpackagesRoot.sql.md | converted |
| GetAppPackageItems | subpackagesNested | oracle/GetAppPackageItems__subpackagesNested.sql.md | meta/GetAppPackageItems__subpackagesNested.sql.md | converted |
| GetAppPackageItems | classes | oracle/GetAppPackageItems__classes.sql.md | meta/GetAppPackageItems__classes.sql.md | converted |
| GetRecordFields | recExists | oracle/GetRecordFields__recExists.sql.md | meta/GetRecordFields__recExists.sql.md | converted |
| GetRecordFields | fields | oracle/GetRecordFields__fields.sql.md | meta/GetRecordFields__fields.sql.md | converted |
| GetMessageSets | main | oracle/GetMessageSets__main.sql.md | meta/GetMessageSets__main.sql.md | converted |
| GetMessagesForSet | main | oracle/GetMessagesForSet__main.sql.md | meta/GetMessagesForSet__main.sql.md | converted |
| GetMessageCatalogEntry | main | oracle/GetMessageCatalogEntry__main.sql.md | meta/GetMessageCatalogEntry__main.sql.md | converted |
| SearchMessageCatalog | main | oracle/SearchMessageCatalog__main.sql.md | meta/SearchMessageCatalog__main.sql.md | converted |
| GetEventMapXrefs | main | oracle/GetEventMapXrefs__main.sql.md | meta/GetEventMapXrefs__main.sql.md | converted |
| GetEventMapItems | component | oracle/GetEventMapItems__component.sql.md | meta/GetEventMapItems__component.sql.md | converted |
| GetEventMapItems | componentRecord | oracle/GetEventMapItems__componentRecord.sql.md | meta/GetEventMapItems__componentRecord.sql.md | converted |
| GetEventMapItems | componentRecordField | oracle/GetEventMapItems__componentRecordField.sql.md | meta/GetEventMapItems__componentRecordField.sql.md | converted |
| GetEventMapItems | page | oracle/GetEventMapItems__page.sql.md | meta/GetEventMapItems__page.sql.md | converted |
| GetFunctionDefiningPrograms | main | oracle/GetFunctionDefiningPrograms__main.sql.md | meta/GetFunctionDefiningPrograms__main.sql.md | converted |
| GetToolsVersion | main | oracle/GetToolsVersion__main.sql.md | meta/GetToolsVersion__main.sql.md | converted |
| GetProgramObjectIds | main | oracle/GetProgramObjectIds__main.sql.md | meta/GetProgramObjectIds__main.sql.md | converted |
| GetFieldType | main | oracle/GetFieldType__main.sql.md | meta/GetFieldType__main.sql.md | converted |
| GetPackagesForClass | main | oracle/GetPackagesForClass__main.sql.md | meta/GetPackagesForClass__main.sql.md | converted |
| GetAllClassesForPackage | main | oracle/GetAllClassesForPackage__main.sql.md | meta/GetAllClassesForPackage__main.sql.md | converted |
| GetPeopleCodeProgram | (uses GetProgramObjectIds + prog load) | — | — | n/a |
| GetOpenTargets | Activity | oracle/GetOpenTargets__Activity.sql.md | meta/GetOpenTargets__Activity.sql.md | converted |
| GetOpenTargets | AnalyticModel | oracle/GetOpenTargets__AnalyticModel.sql.md | meta/GetOpenTargets__AnalyticModel.sql.md | converted |
| GetOpenTargets | AnalyticType | oracle/GetOpenTargets__AnalyticType.sql.md | meta/GetOpenTargets__AnalyticType.sql.md | converted |
| GetOpenTargets | AppEngineProgram | oracle/GetOpenTargets__AppEngineProgram.sql.md | meta/GetOpenTargets__AppEngineProgram.sql.md | converted |
| GetOpenTargets | ApplicationPackage | oracle/GetOpenTargets__ApplicationPackage.sql.md | meta/GetOpenTargets__ApplicationPackage.sql.md | converted |
| GetOpenTargets | ApplicationClass | oracle/GetOpenTargets__ApplicationClass.sql.md | meta/GetOpenTargets__ApplicationClass.sql.md | converted |
| GetOpenTargets | ApprovalRuleSet | oracle/GetOpenTargets__ApprovalRuleSet.sql.md | meta/GetOpenTargets__ApprovalRuleSet.sql.md | converted |
| GetOpenTargets | BusinessInterlink | oracle/GetOpenTargets__BusinessInterlink.sql.md | meta/GetOpenTargets__BusinessInterlink.sql.md | converted |
| GetOpenTargets | BusinessProcess | oracle/GetOpenTargets__BusinessProcess.sql.md | meta/GetOpenTargets__BusinessProcess.sql.md | converted |
| GetOpenTargets | Component | oracle/GetOpenTargets__Component.sql.md | meta/GetOpenTargets__Component.sql.md | converted |
| GetOpenTargets | ComponentInterface | oracle/GetOpenTargets__ComponentInterface.sql.md | meta/GetOpenTargets__ComponentInterface.sql.md | converted |
| GetOpenTargets | Field | oracle/GetOpenTargets__Field.sql.md | meta/GetOpenTargets__Field.sql.md | converted |
| GetOpenTargets | FileLayout | oracle/GetOpenTargets__FileLayout.sql.md | meta/GetOpenTargets__FileLayout.sql.md | converted |
| GetOpenTargets | FileReference | oracle/GetOpenTargets__FileReference.sql.md | meta/GetOpenTargets__FileReference.sql.md | converted |
| GetOpenTargets | HTML | oracle/GetOpenTargets__HTML.sql.md | meta/GetOpenTargets__HTML.sql.md | converted |
| GetOpenTargets | Image | oracle/GetOpenTargets__Image.sql.md | meta/GetOpenTargets__Image.sql.md | converted |
| GetOpenTargets | Menu | oracle/GetOpenTargets__Menu.sql.md | meta/GetOpenTargets__Menu.sql.md | converted |
| GetOpenTargets | Message | oracle/GetOpenTargets__Message.sql.md | meta/GetOpenTargets__Message.sql.md | converted |
| GetOpenTargets | OptimizationModel | oracle/GetOpenTargets__OptimizationModel.sql.md | meta/GetOpenTargets__OptimizationModel.sql.md | converted |
| GetOpenTargets | Page | oracle/GetOpenTargets__Page.sql.md | meta/GetOpenTargets__Page.sql.md | converted |
| GetOpenTargets | PageFluid | oracle/GetOpenTargets__PageFluid.sql.md | meta/GetOpenTargets__PageFluid.sql.md | converted |
| GetOpenTargets | Project | oracle/GetOpenTargets__Project.sql.md | meta/GetOpenTargets__Project.sql.md | converted |
| GetOpenTargets | Record | oracle/GetOpenTargets__Record.sql.md | meta/GetOpenTargets__Record.sql.md | converted |
| GetOpenTargets | SQL | oracle/GetOpenTargets__SQL.sql.md | meta/GetOpenTargets__SQL.sql.md | converted |
| GetOpenTargets | StyleSheet | oracle/GetOpenTargets__StyleSheet.sql.md | meta/GetOpenTargets__StyleSheet.sql.md | converted |
| GetOpenTargets | PagePeopleCode | oracle/GetOpenTargets__PagePeopleCode.sql.md | meta/GetOpenTargets__PagePeopleCode.sql.md | converted |
| GetOpenTargets | ComponentPeopleCode | oracle/GetOpenTargets__ComponentPeopleCode.sql.md | meta/GetOpenTargets__ComponentPeopleCode.sql.md | converted |
| GetOpenTargets | ComponentRecordPeopleCode | oracle/GetOpenTargets__ComponentRecordPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecordPeopleCode.sql.md | converted |
| GetOpenTargets | ComponentRecFieldPeopleCode | oracle/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | converted |
| GetOpenTargets | RecordFieldPeopleCode | oracle/GetOpenTargets__RecordFieldPeopleCode.sql.md | meta/GetOpenTargets__RecordFieldPeopleCode.sql.md | converted |
| GetOpenTargets | MenuPeopleCode | oracle/GetOpenTargets__MenuPeopleCode.sql.md | meta/GetOpenTargets__MenuPeopleCode.sql.md | converted |
| GetOpenTargets | AppEnginePeopleCode | oracle/GetOpenTargets__AppEnginePeopleCode.sql.md | meta/GetOpenTargets__AppEnginePeopleCode.sql.md | converted |
| GetOpenTargets | ComponentInterfacePeopleCode | oracle/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | meta/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | converted |
| GetOpenTargets | MessagePeopleCode | oracle/GetOpenTargets__MessagePeopleCode.sql.md | meta/GetOpenTargets__MessagePeopleCode.sql.md | converted |

## Notes

- Outer `GetOpenTargets` CTE/`ROW_NUMBER` ranking wrapper is **not** a Meta-SQL query; ranking/max-rows moves to C#.
- `SearchMessageCatalog` extract omits dynamic `MESSAGE_SET_NBR IN (...)` filter and `FETCH FIRST n ROWS ONLY` (documented in Notes as C#-side).
- Status values: `pending` → `extracted` → `converted` → `implemented`.

