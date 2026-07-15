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
| GetOpenTargets | Activity | oracle/GetOpenTargets__Activity.sql.md | meta/GetOpenTargets__Activity.sql.md | extracted |
| GetOpenTargets | AnalyticModel | oracle/GetOpenTargets__AnalyticModel.sql.md | meta/GetOpenTargets__AnalyticModel.sql.md | extracted |
| GetOpenTargets | AnalyticType | oracle/GetOpenTargets__AnalyticType.sql.md | meta/GetOpenTargets__AnalyticType.sql.md | extracted |
| GetOpenTargets | AppEngineProgram | oracle/GetOpenTargets__AppEngineProgram.sql.md | meta/GetOpenTargets__AppEngineProgram.sql.md | extracted |
| GetOpenTargets | ApplicationPackage | oracle/GetOpenTargets__ApplicationPackage.sql.md | meta/GetOpenTargets__ApplicationPackage.sql.md | extracted |
| GetOpenTargets | ApplicationClass | oracle/GetOpenTargets__ApplicationClass.sql.md | meta/GetOpenTargets__ApplicationClass.sql.md | extracted |
| GetOpenTargets | ApprovalRuleSet | oracle/GetOpenTargets__ApprovalRuleSet.sql.md | meta/GetOpenTargets__ApprovalRuleSet.sql.md | extracted |
| GetOpenTargets | BusinessInterlink | oracle/GetOpenTargets__BusinessInterlink.sql.md | meta/GetOpenTargets__BusinessInterlink.sql.md | extracted |
| GetOpenTargets | BusinessProcess | oracle/GetOpenTargets__BusinessProcess.sql.md | meta/GetOpenTargets__BusinessProcess.sql.md | extracted |
| GetOpenTargets | Component | oracle/GetOpenTargets__Component.sql.md | meta/GetOpenTargets__Component.sql.md | extracted |
| GetOpenTargets | ComponentInterface | oracle/GetOpenTargets__ComponentInterface.sql.md | meta/GetOpenTargets__ComponentInterface.sql.md | extracted |
| GetOpenTargets | Field | oracle/GetOpenTargets__Field.sql.md | meta/GetOpenTargets__Field.sql.md | extracted |
| GetOpenTargets | FileLayout | oracle/GetOpenTargets__FileLayout.sql.md | meta/GetOpenTargets__FileLayout.sql.md | extracted |
| GetOpenTargets | FileReference | oracle/GetOpenTargets__FileReference.sql.md | meta/GetOpenTargets__FileReference.sql.md | extracted |
| GetOpenTargets | HTML | oracle/GetOpenTargets__HTML.sql.md | meta/GetOpenTargets__HTML.sql.md | extracted |
| GetOpenTargets | Image | oracle/GetOpenTargets__Image.sql.md | meta/GetOpenTargets__Image.sql.md | extracted |
| GetOpenTargets | Menu | oracle/GetOpenTargets__Menu.sql.md | meta/GetOpenTargets__Menu.sql.md | extracted |
| GetOpenTargets | Message | oracle/GetOpenTargets__Message.sql.md | meta/GetOpenTargets__Message.sql.md | extracted |
| GetOpenTargets | OptimizationModel | oracle/GetOpenTargets__OptimizationModel.sql.md | meta/GetOpenTargets__OptimizationModel.sql.md | extracted |
| GetOpenTargets | Page | oracle/GetOpenTargets__Page.sql.md | meta/GetOpenTargets__Page.sql.md | extracted |
| GetOpenTargets | PageFluid | oracle/GetOpenTargets__PageFluid.sql.md | meta/GetOpenTargets__PageFluid.sql.md | extracted |
| GetOpenTargets | Project | oracle/GetOpenTargets__Project.sql.md | meta/GetOpenTargets__Project.sql.md | extracted |
| GetOpenTargets | Record | oracle/GetOpenTargets__Record.sql.md | meta/GetOpenTargets__Record.sql.md | extracted |
| GetOpenTargets | SQL | oracle/GetOpenTargets__SQL.sql.md | meta/GetOpenTargets__SQL.sql.md | extracted |
| GetOpenTargets | StyleSheet | oracle/GetOpenTargets__StyleSheet.sql.md | meta/GetOpenTargets__StyleSheet.sql.md | extracted |
| GetOpenTargets | PagePeopleCode | oracle/GetOpenTargets__PagePeopleCode.sql.md | meta/GetOpenTargets__PagePeopleCode.sql.md | extracted |
| GetOpenTargets | ComponentPeopleCode | oracle/GetOpenTargets__ComponentPeopleCode.sql.md | meta/GetOpenTargets__ComponentPeopleCode.sql.md | extracted |
| GetOpenTargets | ComponentRecordPeopleCode | oracle/GetOpenTargets__ComponentRecordPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecordPeopleCode.sql.md | extracted |
| GetOpenTargets | ComponentRecFieldPeopleCode | oracle/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | extracted |
| GetOpenTargets | RecordFieldPeopleCode | oracle/GetOpenTargets__RecordFieldPeopleCode.sql.md | meta/GetOpenTargets__RecordFieldPeopleCode.sql.md | extracted |
| GetOpenTargets | MenuPeopleCode | oracle/GetOpenTargets__MenuPeopleCode.sql.md | meta/GetOpenTargets__MenuPeopleCode.sql.md | extracted |
| GetOpenTargets | AppEnginePeopleCode | oracle/GetOpenTargets__AppEnginePeopleCode.sql.md | meta/GetOpenTargets__AppEnginePeopleCode.sql.md | extracted |
| GetOpenTargets | ComponentInterfacePeopleCode | oracle/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | meta/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | extracted |
| GetOpenTargets | MessagePeopleCode | oracle/GetOpenTargets__MessagePeopleCode.sql.md | meta/GetOpenTargets__MessagePeopleCode.sql.md | extracted |

## Notes

- Outer `GetOpenTargets` CTE/`ROW_NUMBER` ranking wrapper is **not** a Meta-SQL query; ranking/max-rows moves to C#.
- `SearchMessageCatalog` extract omits dynamic `MESSAGE_SET_NBR IN (...)` filter and `FETCH FIRST n ROWS ONLY` (documented in Notes as C#-side).
- Status values: `pending` → `extracted` → `converted` → `implemented`.

