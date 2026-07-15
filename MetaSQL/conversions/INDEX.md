# Meta-SQL conversion inventory

Track extraction and conversion status for each SqlProxyDataManager query.

| Method | Query id | Oracle file | Meta file | Status |
|--------|----------|-------------|-----------|--------|
| GetSqlDefinition | main | oracle/GetSqlDefinition__main.sql.md | meta/GetSqlDefinition__main.sql.md | pending |
| GetAllSqlDefinitions | names | oracle/GetAllSqlDefinitions__names.sql.md | meta/GetAllSqlDefinitions__names.sql.md | pending |
| GetHtmlDefinition | main | oracle/GetHtmlDefinition__main.sql.md | meta/GetHtmlDefinition__main.sql.md | pending |
| GetAllHtmlDefinitions | names | oracle/GetAllHtmlDefinitions__names.sql.md | meta/GetAllHtmlDefinitions__names.sql.md | pending |
| GetPeopleCodeItemMetadataForProject | projectItems | oracle/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | meta/GetPeopleCodeItemMetadataForProject__projectItems.sql.md | pending |
| LoadPeopleCodeItemContent | progTxt | oracle/LoadPeopleCodeItemContent__progTxt.sql.md | meta/LoadPeopleCodeItemContent__progTxt.sql.md | pending |
| GetNameReferencesForProgram | pcmName | oracle/GetNameReferencesForProgram__pcmName.sql.md | meta/GetNameReferencesForProgram__pcmName.sql.md | pending |
| CheckAppClassExists | exists | oracle/CheckAppClassExists__exists.sql.md | meta/CheckAppClassExists__exists.sql.md | pending |
| GetAppClassSourceByPath | (uses Check + Load paths) | — | — | n/a (C#) |
| GetAppPackageItems | subpackagesRoot | oracle/GetAppPackageItems__subpackagesRoot.sql.md | meta/GetAppPackageItems__subpackagesRoot.sql.md | pending |
| GetAppPackageItems | subpackagesNested | oracle/GetAppPackageItems__subpackagesNested.sql.md | meta/GetAppPackageItems__subpackagesNested.sql.md | pending |
| GetAppPackageItems | classes | oracle/GetAppPackageItems__classes.sql.md | meta/GetAppPackageItems__classes.sql.md | pending |
| GetRecordFields | recExists | oracle/GetRecordFields__recExists.sql.md | meta/GetRecordFields__recExists.sql.md | pending |
| GetRecordFields | fields | oracle/GetRecordFields__fields.sql.md | meta/GetRecordFields__fields.sql.md | pending |
| GetMessageSets | main | oracle/GetMessageSets__main.sql.md | meta/GetMessageSets__main.sql.md | pending |
| GetMessagesForSet | main | oracle/GetMessagesForSet__main.sql.md | meta/GetMessagesForSet__main.sql.md | pending |
| GetMessageCatalogEntry | main | oracle/GetMessageCatalogEntry__main.sql.md | meta/GetMessageCatalogEntry__main.sql.md | pending |
| SearchMessageCatalog | main | oracle/SearchMessageCatalog__main.sql.md | meta/SearchMessageCatalog__main.sql.md | pending |
| GetEventMapXrefs | main | oracle/GetEventMapXrefs__main.sql.md | meta/GetEventMapXrefs__main.sql.md | pending |
| GetEventMapItems | component | oracle/GetEventMapItems__component.sql.md | meta/GetEventMapItems__component.sql.md | pending |
| GetEventMapItems | componentRecord | oracle/GetEventMapItems__componentRecord.sql.md | meta/GetEventMapItems__componentRecord.sql.md | pending |
| GetEventMapItems | componentRecordField | oracle/GetEventMapItems__componentRecordField.sql.md | meta/GetEventMapItems__componentRecordField.sql.md | pending |
| GetEventMapItems | page | oracle/GetEventMapItems__page.sql.md | meta/GetEventMapItems__page.sql.md | pending |
| GetOpenTargets | per type (see Task 4) | oracle/GetOpenTargets__perType.sql.md | meta/GetOpenTargets__perType.sql.md | pending |
| GetFunctionDefiningPrograms | main | oracle/GetFunctionDefiningPrograms__main.sql.md | meta/GetFunctionDefiningPrograms__main.sql.md | pending |
| GetToolsVersion | main | oracle/GetToolsVersion__main.sql.md | meta/GetToolsVersion__main.sql.md | pending |
| GetProgramObjectIds | main | oracle/GetProgramObjectIds__main.sql.md | meta/GetProgramObjectIds__main.sql.md | pending |
| GetFieldType | main | oracle/GetFieldType__main.sql.md | meta/GetFieldType__main.sql.md | pending |
| GetPackagesForClass | main | oracle/GetPackagesForClass__main.sql.md | meta/GetPackagesForClass__main.sql.md | pending |
| GetAllClassesForPackage | main | oracle/GetAllClassesForPackage__main.sql.md | meta/GetAllClassesForPackage__main.sql.md | pending |
| GetPeopleCodeProgram | (uses GetProgramObjectIds + prog load) | — | — | n/a |
