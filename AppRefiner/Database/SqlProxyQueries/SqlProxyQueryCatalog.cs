namespace AppRefiner.Database.SqlProxyQueries
{
    /// <summary>
    /// Static Meta-SQL strings for SqlProxyDataManager.
    /// Keep in sync with MetaSQL/conversions/meta/.
    /// </summary>
    public static class SqlProxyQueryCatalog
    {
        public const string CheckAppClassExists = @"
SELECT 'Y'
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = :1 AND %Upper(OBJECTVALUE1) = :2
   AND OBJECTID2 = :3 AND %Upper(OBJECTVALUE2) = :4
   AND OBJECTID3 = :5 AND %Upper(OBJECTVALUE3) = :6
   AND OBJECTID4 = :7 AND %Upper(OBJECTVALUE4) = :8
   AND OBJECTID5 = :9 AND %Upper(OBJECTVALUE5) = :10
   AND OBJECTID6 = :11 AND %Upper(OBJECTVALUE6) = :12
   AND OBJECTID7 = :13 AND %Upper(OBJECTVALUE7) = :14
";

        public const string GetAllClassesForPackage = @"
SELECT APPCLASSID
  FROM %Table(PSAPPCLASSDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
";

        public const string GetAllHtmlDefinitionNames = @"
SELECT DISTINCT CONTNAME
  FROM %Table(PSCONTENT)
";

        public const string GetAllSqlDefinitionNames = @"
SELECT DISTINCT A.SQLID
  FROM %Table(PSSQLDEFN) A
 WHERE EXISTS (
       SELECT 1
         FROM %Table(PSSQLTEXTDEFN) B
        WHERE A.SQLID = B.SQLID
       )
";

        public const string GetAppPackageItemsClasses = @"
SELECT APPCLASSID
  FROM %Table(PSAPPCLASSDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
 ORDER BY APPCLASSID
";

        public const string GetAppPackageItemsSubpackagesNested = @"
SELECT PACKAGEID
  FROM %Table(PSPACKAGEDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = :2
   AND PACKAGELEVEL = 2
 ORDER BY PACKAGEID
";

        public const string GetAppPackageItemsSubpackagesRoot = @"
SELECT PACKAGEID
  FROM %Table(PSPACKAGEDEFN)
 WHERE PACKAGEROOT = :1
   AND QUALIFYPATH = ':'
   AND PACKAGELEVEL = 1
 ORDER BY PACKAGEID
";

        public const string GetEventMapItemsComponent = @"
SELECT DISTINCT
       evt.portal_objname,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND portal.PORTAL_URI_SEG2 = :1
   AND portal.PORTAL_URI_SEG3 = :2
   AND evt.ptcs_cmpevent = :3
";

        public const string GetEventMapItemsComponentRecord = @"
SELECT DISTINCT
       evt.portal_objname,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND portal.PORTAL_URI_SEG2 = :1
   AND portal.PORTAL_URI_SEG3 = :2
   AND evt.RECNAME = :3
   AND evt.ptcs_cmprecevent = :4
";

        public const string GetEventMapItemsComponentRecordField = @"
SELECT DISTINCT
       evt.portal_objname,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND portal.PORTAL_URI_SEG2 = :1
   AND portal.PORTAL_URI_SEG3 = :2
   AND evt.RECNAME = :3
   AND evt.FIELDNAME = :4
   AND evt.ptcs_cmprecevent = :5
";

        public const string GetEventMapItemsPage = @"
SELECT DISTINCT
       evt.portal_objname,
       portal.PORTAL_URI_SEG2,
       portal.PORTAL_URI_SEG3,
       evt.ptcs_procseq,
       evt.SEQNUM,
       svc.packageroot,
       svc.qualifypath,
       svc.appclassid
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND evt.PNLNAME = :1
   AND evt.ptcs_cmprecevent = :2
";

        public const string GetEventMapXrefs = @"
SELECT DISTINCT
       evt.portal_objname,
       portal.PORTAL_URI_SEG2 AS COMPONENT,
       portal.PORTAL_URI_SEG3 AS SEGMENT,
       evt.RECNAME AS RECORD,
       evt.FIELDNAME AS FIELD,
       evt.PNLNAME,
       evt.SEQNUM,
       evt.ptcs_procseq,
       evt.ptcs_cmpevent,
       evt.ptcs_cmprecevent
  FROM %Table(PSPTCSSRVCONF) evt
 INNER JOIN %Table(PSPRSMDEFN) portal ON portal.portal_objname = evt.portal_objname
 INNER JOIN %Table(PSPTCSSRVDEFN) svc ON svc.ptcs_serviceid = evt.ptcs_serviceid
 WHERE evt.portal_name = '_PTCS_PTEVMAP'
   AND svc.packageroot = :1
   AND svc.qualifypath = :2
   AND svc.appclassid = :3
";

        public const string GetFieldType = @"
SELECT FIELDTYPE
  FROM %Table(PSDBFIELD)
 WHERE FIELDNAME = :1
";

        public const string GetFunctionDefiningPrograms = @"
SELECT DISTINCT
       OBJECTID1, OBJECTVALUE1,
       OBJECTID2, OBJECTVALUE2,
       OBJECTID3, OBJECTVALUE3,
       OBJECTID4, OBJECTVALUE4,
       OBJECTID5, OBJECTVALUE5,
       OBJECTID6, OBJECTVALUE6,
       OBJECTID7, OBJECTVALUE7
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 1
   AND OBJECTID2 = 2
   AND OBJECTID3 = 12 AND OBJECTVALUE3 = 'FieldFormula'
";

        public const string GetHtmlDefinition = @"
SELECT CONTDATA
  FROM %Table(PSCONTENT)
 WHERE CONTNAME = :1
 ORDER BY SEQNUM
";

        public const string GetMessageCatalogEntry = @"
SELECT MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE MESSAGE_SET_NBR = :1
   AND MESSAGE_NBR = :2
";

        public const string GetMessageSets = @"
SELECT MESSAGE_SET_NBR, DESCR
  FROM %Table(PSMSGSETDEFN)
 ORDER BY MESSAGE_SET_NBR
";

        public const string GetMessagesForSet = @"
SELECT MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE MESSAGE_SET_NBR = :1
 ORDER BY MESSAGE_NBR
";

        public const string GetNameReferencesForProgram = @"
SELECT NAMENUM, RECNAME, REFNAME
  FROM %Table(PSPCMNAME)
 WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2
   AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4
   AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6
   AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8
   AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10
   AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12
   AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14
 ORDER BY NAMENUM
";

        public const string GetPackagesForClass = @"
SELECT A.PACKAGEROOT,
       A.QUALIFYPATH
  FROM %Table(PSAPPCLASSDEFN) A
  LEFT OUTER JOIN %Table(PSPACKAGEDEFN) B
    ON B.PACKAGEROOT = A.PACKAGEROOT
   AND B.QUALIFYPATH = CASE
                         WHEN A.QUALIFYPATH = ':' THEN '.'
                         ELSE A.QUALIFYPATH
                       END
 WHERE A.APPCLASSID = :1
 ORDER BY B.LASTUPDDTTM DESC,
          A.PACKAGEROOT ASC
";

        public const string GetPeopleCodeItemMetadataForProject = @"
SELECT OBJECTTYPE,
       OBJECTID1, OBJECTVALUE1,
       OBJECTID2, OBJECTVALUE2,
       OBJECTID3, OBJECTVALUE3,
       OBJECTID4, OBJECTVALUE4
  FROM %Table(PSPROJECTITEM)
 WHERE PROJECTNAME = :1
   AND OBJECTTYPE IN (8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66)
";

        public const string GetProgramObjectIds = @"
SELECT DISTINCT
       OBJECTID1, OBJECTID2, OBJECTID3, OBJECTID4, OBJECTID5, OBJECTID6, OBJECTID7,
       OBJECTVALUE1, OBJECTVALUE2, OBJECTVALUE3, OBJECTVALUE4, OBJECTVALUE5, OBJECTVALUE6, OBJECTVALUE7
  FROM %Table(PSPCMPROG)
 WHERE OBJECTVALUE1 = :1
   AND OBJECTVALUE2 = :2
   AND OBJECTVALUE3 = :3
   AND OBJECTVALUE4 = :4
   AND OBJECTVALUE5 = :5
   AND OBJECTVALUE6 = :6
   AND OBJECTVALUE7 = :7
";

        public const string GetRecordFields = @"
SELECT r.FIELDNAME, r.FIELDNUM, f.FIELDTYPE, f.LENGTH, f.DECIMALPOS, r.USEEDIT
  FROM %Table(PSRECFIELDDB) r
  JOIN %Table(PSDBFIELD) f ON r.FIELDNAME = f.FIELDNAME
 WHERE r.RECNAME = :1
 ORDER BY r.FIELDNUM
";

        public const string GetRecordFieldsRecExists = @"
SELECT VERSION
  FROM %Table(PSRECDEFN)
 WHERE RECNAME = :1
";

        public const string GetSqlDefinition = @"
SELECT B.SQLTEXT
  FROM %Table(PSSQLDEFN) A, %Table(PSSQLTEXTDEFN) B
 WHERE A.SQLID = B.SQLID
   AND A.SQLID = :1
 ORDER BY B.SEQNUM
";

        public const string GetToolsVersion = @"
SELECT TOOLSREL %Concat '.' %Concat PTPATCHREL
  FROM %Table(PSSTATUS)
";

        public const string LoadPeopleCodeItemContent = @"
SELECT PROGTXT
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = :1 AND OBJECTVALUE1 = :2
   AND OBJECTID2 = :3 AND OBJECTVALUE2 = :4
   AND OBJECTID3 = :5 AND OBJECTVALUE3 = :6
   AND OBJECTID4 = :7 AND OBJECTVALUE4 = :8
   AND OBJECTID5 = :9 AND OBJECTVALUE5 = :10
   AND OBJECTID6 = :11 AND OBJECTVALUE6 = :12
   AND OBJECTID7 = :13 AND OBJECTVALUE7 = :14
 ORDER BY PROGSEQ
";

        public const string SearchMessageCatalog = @"
SELECT MESSAGE_SET_NBR, MESSAGE_NBR, MSG_SEVERITY, MESSAGE_TEXT, DESCRLONG
  FROM %Table(PSMSGCATDEFN)
 WHERE (%Upper(MESSAGE_TEXT) LIKE :1
        OR %Upper(%Substring(DESCRLONG, 1, 4000)) LIKE :2)
 ORDER BY MESSAGE_SET_NBR, MESSAGE_NBR
";
    }
}
