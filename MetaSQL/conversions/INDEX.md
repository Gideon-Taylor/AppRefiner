# Meta-SQL conversion inventory

Track extraction and conversion status for each SqlProxyDataManager query.

| Method | Query id | Oracle file | Meta file | Status |
|--------|----------|-------------|-----------|--------|
| GetSqlDefinition | main | oracle/GetSqlDefinition__main.sql.md | meta/GetSqlDefinition__main.sql.md | integrated |
| GetAllSqlDefinitions | names | oracle/GetAllSqlDefinitions__names.sql.md | meta/GetAllSqlDefinitions__names.sql.md | integrated |
| GetHtmlDefinition | main | oracle/GetHtmlDefinition__main.sql.md | meta/GetHtmlDefinition__main.sql.md | integrated |
| GetAllHtmlDefinitions | names | oracle/GetAllHtmlDefinitions__names.sql.md | meta/GetAllHtmlDefinitions__names.sql.md | integrated |
| GetPeopleCodeItemMetadataForProject | projectItems | oracle/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | meta/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | integrated |
| LoadPeopleCodeItemContent | progTxt | oracle/LoadPeopleCodeItemContent__progTxt.sql.md | meta/LoadPeopleCodeItemContent__progTxt.sql.md | integrated |
| GetNameReferencesForProgram | pcmName | oracle/GetNameReferencesForProgram__pcmName.sql.md | meta/GetNameReferencesForProgram__pcmName.sql.md | integrated |
| CheckAppClassExists | exists | oracle/CheckAppClassExists__exists.sql.md | meta/CheckAppClassExists__exists.sql.md | integrated |
| GetAppClassSourceByPath | (uses Check + Load paths) | — | — | n/a (C#) |
| GetAppPackageItems | subpackagesRoot | oracle/GetAppPackageItems__subpackagesRoot.sql.md | meta/GetAppPackageItems__subpackagesRoot.sql.md | integrated |
| GetAppPackageItems | subpackagesNested | oracle/GetAppPackageItems__subpackagesNested.sql.md | meta/GetAppPackageItems__subpackagesNested.sql.md | integrated |
| GetAppPackageItems | classes | oracle/GetAppPackageItems__classes.sql.md | meta/GetAppPackageItems__classes.sql.md | integrated |
| GetRecordFields | recExists | oracle/GetRecordFields__recExists.sql.md | meta/GetRecordFields__recExists.sql.md | integrated |
| GetRecordFields | fields | oracle/GetRecordFields__fields.sql.md | meta/GetRecordFields__fields.sql.md | integrated |
| GetMessageSets | main | oracle/GetMessageSets__main.sql.md | meta/GetMessageSets__main.sql.md | integrated |
| GetMessagesForSet | main | oracle/GetMessagesForSet__main.sql.md | meta/GetMessagesForSet__main.sql.md | integrated |
| GetMessageCatalogEntry | main | oracle/GetMessageCatalogEntry__main.sql.md | meta/GetMessageCatalogEntry__main.sql.md | integrated |
| SearchMessageCatalog | main | oracle/SearchMessageCatalog__main.sql.md | meta/SearchMessageCatalog__main.sql.md | integrated |
| SearchMessageCatalog | inSet | — | meta/SearchMessageCatalogInSet__main.sql.md | integrated |
| GetDefinitionNames | template | — | meta/GetDefinitionNames.sql.md | integrated |
| GetEventMapXrefs | main | oracle/GetEventMapXrefs__main.sql.md | meta/GetEventMapXrefs__main.sql.md | integrated |
| GetEventMapItems | component | oracle/GetEventMapItems__component.sql.md | meta/GetEventMapItems__component.sql.md | integrated |
| GetEventMapItems | componentRecord | oracle/GetEventMapItems__componentRecord.sql.md | meta/GetEventMapItems__componentRecord.sql.md | integrated |
| GetEventMapItems | componentRecordField | oracle/GetEventMapItems__componentRecordField.sql.md | meta/GetEventMapItems__componentRecordField.sql.md | integrated |
| GetEventMapItems | page | oracle/GetEventMapItems__page.sql.md | meta/GetEventMapItems__page.sql.md | integrated |
| GetFunctionDefiningPrograms | main | oracle/GetFunctionDefiningPrograms__main.sql.md | meta/GetFunctionDefiningPrograms__main.sql.md | integrated |
| GetToolsVersion | main | oracle/GetToolsVersion__main.sql.md | meta/GetToolsVersion__main.sql.md | integrated |
| GetProgramObjectIds | main | oracle/GetProgramObjectIds__main.sql.md | meta/GetProgramObjectIds__main.sql.md | integrated |
| GetFieldType | main | oracle/GetFieldType__main.sql.md | meta/GetFieldType__main.sql.md | integrated |
| GetPackagesForClass | main | oracle/GetPackagesForClass__main.sql.md | meta/GetPackagesForClass__main.sql.md | integrated |
| GetAllClassesForPackage | main | oracle/GetAllClassesForPackage__main.sql.md | meta/GetAllClassesForPackage__main.sql.md | integrated |
| GetPeopleCodeProgram | (uses GetProgramObjectIds + prog load) | — | — | n/a |
| GetOpenTargets | Activity | oracle/GetOpenTargets__Activity.sql.md | meta/GetOpenTargets__Activity.sql.md | integrated |
| GetOpenTargets | AnalyticModel | oracle/GetOpenTargets__AnalyticModel.sql.md | meta/GetOpenTargets__AnalyticModel.sql.md | integrated |
| GetOpenTargets | AnalyticType | oracle/GetOpenTargets__AnalyticType.sql.md | meta/GetOpenTargets__AnalyticType.sql.md | integrated |
| GetOpenTargets | AppEngineProgram | oracle/GetOpenTargets__AppEngineProgram.sql.md | meta/GetOpenTargets__AppEngineProgram.sql.md | integrated |
| GetOpenTargets | ApplicationPackage | oracle/GetOpenTargets__ApplicationPackage.sql.md | meta/GetOpenTargets__ApplicationPackage.sql.md | integrated |
| GetOpenTargets | ApplicationClass | oracle/GetOpenTargets__ApplicationClass.sql.md | meta/GetOpenTargets__ApplicationClass.sql.md | integrated |
| GetOpenTargets | ApprovalRuleSet | oracle/GetOpenTargets__ApprovalRuleSet.sql.md | meta/GetOpenTargets__ApprovalRuleSet.sql.md | integrated |
| GetOpenTargets | BusinessInterlink | oracle/GetOpenTargets__BusinessInterlink.sql.md | meta/GetOpenTargets__BusinessInterlink.sql.md | integrated |
| GetOpenTargets | BusinessProcess | oracle/GetOpenTargets__BusinessProcess.sql.md | meta/GetOpenTargets__BusinessProcess.sql.md | integrated |
| GetOpenTargets | Component | oracle/GetOpenTargets__Component.sql.md | meta/GetOpenTargets__Component.sql.md | integrated |
| GetOpenTargets | ComponentInterface | oracle/GetOpenTargets__ComponentInterface.sql.md | meta/GetOpenTargets__ComponentInterface.sql.md | integrated |
| GetOpenTargets | Field | oracle/GetOpenTargets__Field.sql.md | meta/GetOpenTargets__Field.sql.md | integrated |
| GetOpenTargets | FileLayout | oracle/GetOpenTargets__FileLayout.sql.md | meta/GetOpenTargets__FileLayout.sql.md | integrated |
| GetOpenTargets | FileReference | oracle/GetOpenTargets__FileReference.sql.md | meta/GetOpenTargets__FileReference.sql.md | integrated |
| GetOpenTargets | HTML | oracle/GetOpenTargets__HTML.sql.md | meta/GetOpenTargets__HTML.sql.md | integrated |
| GetOpenTargets | Image | oracle/GetOpenTargets__Image.sql.md | meta/GetOpenTargets__Image.sql.md | integrated |
| GetOpenTargets | Menu | oracle/GetOpenTargets__Menu.sql.md | meta/GetOpenTargets__Menu.sql.md | integrated |
| GetOpenTargets | Message | oracle/GetOpenTargets__Message.sql.md | meta/GetOpenTargets__Message.sql.md | integrated |
| GetOpenTargets | OptimizationModel | oracle/GetOpenTargets__OptimizationModel.sql.md | meta/GetOpenTargets__OptimizationModel.sql.md | integrated |
| GetOpenTargets | Page | oracle/GetOpenTargets__Page.sql.md | meta/GetOpenTargets__Page.sql.md | integrated |
| GetOpenTargets | PageFluid | oracle/GetOpenTargets__PageFluid.sql.md | meta/GetOpenTargets__PageFluid.sql.md | integrated |
| GetOpenTargets | Project | oracle/GetOpenTargets__Project.sql.md | meta/GetOpenTargets__Project.sql.md | integrated |
| GetOpenTargets | Record | oracle/GetOpenTargets__Record.sql.md | meta/GetOpenTargets__Record.sql.md | integrated |
| GetOpenTargets | SQL | oracle/GetOpenTargets__SQL.sql.md | meta/GetOpenTargets__SQL.sql.md | integrated |
| GetOpenTargets | StyleSheet | oracle/GetOpenTargets__StyleSheet.sql.md | meta/GetOpenTargets__StyleSheet.sql.md | integrated |
| GetOpenTargets | PagePeopleCode | oracle/GetOpenTargets__PagePeopleCode.sql.md | meta/GetOpenTargets__PagePeopleCode.sql.md | integrated |
| GetOpenTargets | ComponentPeopleCode | oracle/GetOpenTargets__ComponentPeopleCode.sql.md | meta/GetOpenTargets__ComponentPeopleCode.sql.md | integrated |
| GetOpenTargets | ComponentRecordPeopleCode | oracle/GetOpenTargets__ComponentRecordPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecordPeopleCode.sql.md | integrated |
| GetOpenTargets | ComponentRecFieldPeopleCode | oracle/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | meta/GetOpenTargets__ComponentRecFieldPeopleCode.sql.md | integrated |
| GetOpenTargets | RecordFieldPeopleCode | oracle/GetOpenTargets__RecordFieldPeopleCode.sql.md | meta/GetOpenTargets__RecordFieldPeopleCode.sql.md | integrated |
| GetOpenTargets | MenuPeopleCode | oracle/GetOpenTargets__MenuPeopleCode.sql.md | meta/GetOpenTargets__MenuPeopleCode.sql.md | integrated |
| GetOpenTargets | AppEnginePeopleCode | oracle/GetOpenTargets__AppEnginePeopleCode.sql.md | meta/GetOpenTargets__AppEnginePeopleCode.sql.md | integrated |
| GetOpenTargets | ComponentInterfacePeopleCode | oracle/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | meta/GetOpenTargets__ComponentInterfacePeopleCode.sql.md | integrated |
| GetOpenTargets | MessagePeopleCode | oracle/GetOpenTargets__MessagePeopleCode.sql.md | meta/GetOpenTargets__MessagePeopleCode.sql.md | integrated |

## Notes

- Outer `GetOpenTargets` CTE/`ROW_NUMBER` ranking wrapper is **not** a Meta-SQL query; ranking/max-rows moves to C#.
- `SearchMessageCatalog` extract omits dynamic `MESSAGE_SET_NBR IN (...)` filter and `FETCH FIRST n ROWS ONLY` (documented in Notes as C#-side).
- Status values: `pending` → `extracted` → `converted` → `integrated`.

