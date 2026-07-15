using AppRefiner.Database.Models;

namespace AppRefiner.Database.SqlProxyQueries
{
    /// <summary>
    /// Static Meta-SQL open-target search queries for SqlProxyDataManager.
    /// Keep in sync with MetaSQL/conversions/meta/GetOpenTargets__*.sql.md.
    /// </summary>
    public static class SqlProxyOpenTargetQueries
    {
        public const string Activity = @"
SELECT 'Activity' AS DEFN_TYPE,
       ACTIVITYNAME AS ID,
       DESCR60 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSACTIVITYDEFN)
 WHERE (:1 = 'Y' OR %Upper(ACTIVITYNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR60) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY ACTIVITYNAME
";

        public const string AnalyticModel = @"
SELECT 'Analytic Model' AS DEFN_TYPE,
       ACEMODELID AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSACEMDLDEFN)
 WHERE (:1 = 'Y' OR %Upper(ACEMODELID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY ACEMODELID
";

        public const string AnalyticType = @"
SELECT 'Analytic Type' AS DEFN_TYPE,
       PROBTYPE AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSOPTPRBTYPE)
 WHERE (:1 = 'Y' OR %Upper(PROBTYPE) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PROBTYPE
";

        public const string AppEnginePeopleCode = @"
SELECT 'App Engine PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE6 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 %Concat '.' %Concat OBJECTVALUE5 %Concat '.' %Concat OBJECTVALUE6 %Concat '.' %Concat OBJECTVALUE7 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 66 AND OBJECTID2 = 77 AND OBJECTID3 = 39 AND OBJECTID4 = 20 AND OBJECTID5 = 21 AND OBJECTID6 = 78 AND OBJECTID7 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE6) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 %Concat '.' %Concat OBJECTVALUE5 %Concat '.' %Concat OBJECTVALUE6 %Concat '.' %Concat OBJECTVALUE7) LIKE %Upper(:4) ESCAPE '\')
";

        public const string AppEngineProgram = @"
SELECT 'App Engine Program' AS DEFN_TYPE,
       AE_APPLID AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSAEAPPLDEFN)
 WHERE (:1 = 'Y' OR %Upper(AE_APPLID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY AE_APPLID
";

        public const string ApplicationClass = @"
SELECT 'Application Class' AS DEFN_TYPE,
       PACKAGEROOT %Concat CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' %Concat QUALIFYPATH END %Concat ':' %Concat APPCLASSID AS ID,
       DESCR AS DESCR,
       %CurrentDateTimeIn AS LASTUPDDTTM
  FROM %Table(PSAPPCLASSDEFN)
 WHERE (:1 = 'Y' OR %Upper(PACKAGEROOT %Concat CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' %Concat QUALIFYPATH END %Concat ':' %Concat APPCLASSID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
";

        public const string ApplicationPackage = @"
SELECT 'Application Package' AS DEFN_TYPE,
       PACKAGEID AS ID,
       DESCRLONG AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPACKAGEDEFN)
 WHERE (:1 = 'Y' OR %Upper(PACKAGEID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCRLONG) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PACKAGEID
";

        public const string ApprovalRuleSet = @"
SELECT 'Approval Rule Set' AS DEFN_TYPE,
       APPR_RULE_SET AS ID,
       DESCR60 AS DESCR,
       LASTUPDDTTM
  FROM %Table(APPR_RULE_HDR)
 WHERE (:1 = 'Y' OR %Upper(APPR_RULE_SET) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR60) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY APPR_RULE_SET
";

        public const string BusinessInterlink = @"
SELECT 'Business Interlink' AS DEFN_TYPE,
       IONAME AS ID,
       IODESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSIODEFN)
 WHERE (:1 = 'Y' OR %Upper(IONAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(IODESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY IONAME
";

        public const string BusinessProcess = @"
SELECT 'Business Process' AS DEFN_TYPE,
       BUSPROCNAME AS ID,
       DESCR60 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSBUSPROCDEFN)
 WHERE (:1 = 'Y' OR %Upper(BUSPROCNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR60) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY BUSPROCNAME
";

        public const string Component = @"
SELECT 'Component' AS DEFN_TYPE,
       PNLGRPNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPNLGRPDEFN)
 WHERE (:1 = 'Y' OR %Upper(PNLGRPNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PNLGRPNAME
";

        public const string ComponentInterface = @"
SELECT 'Component Interface' AS DEFN_TYPE,
       BCNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSBCDEFN)
 WHERE (:1 = 'Y' OR %Upper(BCNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY BCNAME
";

        public const string ComponentInterfacePeopleCode = @"
SELECT 'Component Interface PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 74
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY OBJECTVALUE1
";

        public const string ComponentPeopleCode = @"
SELECT 'Component PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3) LIKE %Upper(:4) ESCAPE '\')
";

        public const string ComponentRecFieldPeopleCode = @"
SELECT 'Component Rec Field PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 %Concat '.' %Concat OBJECTVALUE5 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 1 AND OBJECTID4 = 2 AND OBJECTID5 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 %Concat '.' %Concat OBJECTVALUE5) LIKE %Upper(:4) ESCAPE '\')
";

        public const string ComponentRecordPeopleCode = @"
SELECT 'Component Record PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 1 AND OBJECTID4 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4) LIKE %Upper(:4) ESCAPE '\')
";

        public const string Field = @"
SELECT 'Field' AS DEFN_TYPE,
       FIELDNAME AS ID,
       DESCRLONG AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSDBFIELD)
 WHERE (:1 = 'Y' OR %Upper(FIELDNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCRLONG) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY FIELDNAME
";

        public const string FileLayout = @"
SELECT 'File Layout' AS DEFN_TYPE,
       FLDDEFNNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSFLDDEFN)
 WHERE (:1 = 'Y' OR %Upper(FLDDEFNNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY FLDDEFNNAME
";

        public const string FileReference = @"
SELECT 'File Reference' AS DEFN_TYPE,
       FILEREFNAME AS ID,
       DESCRLONG AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSFILEREDEFN)
 WHERE (:1 = 'Y' OR %Upper(FILEREFNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCRLONG) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY FILEREFNAME
";

        public const string HTML = @"
SELECT 'HTML' AS DEFN_TYPE,
       CONTNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSCONTDEFN)
 WHERE CONTTYPE = 4
   AND (:1 = 'Y' OR %Upper(CONTNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY CONTNAME
";

        public const string Image = @"
SELECT 'Image' AS DEFN_TYPE,
       CONTNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSCONTDEFN)
 WHERE CONTTYPE = 1
   AND (:1 = 'Y' OR %Upper(CONTNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY CONTNAME
";

        public const string Menu = @"
SELECT 'Menu' AS DEFN_TYPE,
       MENUNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSMENUDEFN)
 WHERE (:1 = 'Y' OR %Upper(MENUNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY MENUNAME
";

        public const string MenuPeopleCode = @"
SELECT 'Menu PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 3 AND OBJECTID2 = 4 AND OBJECTID3 = 5 AND OBJECTID4 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 %Concat '.' %Concat OBJECTVALUE4) LIKE %Upper(:4) ESCAPE '\')
";

        public const string Message = @"
SELECT 'Message' AS DEFN_TYPE,
       MSGNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSMSGDEFN)
 WHERE (:1 = 'Y' OR %Upper(MSGNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY MSGNAME
";

        public const string MessagePeopleCode = @"
SELECT 'Message PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END AS ID,
       OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END %Concat CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' THEN '.' %Concat OBJECTVALUE3 ELSE '' END AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE (OBJECTID1 = 60 OR OBJECTID1 = 87)
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' THEN '.' %Concat OBJECTVALUE2 ELSE '' END %Concat CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' THEN '.' %Concat OBJECTVALUE3 ELSE '' END) LIKE %Upper(:4) ESCAPE '\')
";

        public const string OptimizationModel = @"
SELECT 'Optimization Model' AS DEFN_TYPE,
       PAMS_MODEL_NAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSOPTMODEL)
 WHERE (:1 = 'Y' OR %Upper(PAMS_MODEL_NAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PAMS_MODEL_NAME
";

        public const string Page = @"
SELECT 'Page' AS DEFN_TYPE,
       PNLNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPNLDEFN)
 WHERE PNLTYPE <> 7
   AND (:1 = 'Y' OR %Upper(PNLNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PNLNAME
";

        public const string PageFluid = @"
SELECT 'Page (Fluid)' AS DEFN_TYPE,
       PNLNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPNLDEFN)
 WHERE PNLTYPE = 7
   AND (:1 = 'Y' OR %Upper(PNLNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PNLNAME
";

        public const string PagePeopleCode = @"
SELECT 'Page PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 AS ID,
       OBJECTVALUE1 %Concat '.Activate' AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 9 AND OBJECTID2 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.Activate') LIKE %Upper(:4) ESCAPE '\')
 ORDER BY OBJECTVALUE1
";

        public const string Project = @"
SELECT 'Project' AS DEFN_TYPE,
       PROJECTNAME AS ID,
       PROJECTDESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPROJECTDEFN)
 WHERE (:1 = 'Y' OR %Upper(PROJECTNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(PROJECTDESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY PROJECTNAME
";

        public const string Record = @"
SELECT 'Record' AS DEFN_TYPE,
       RECNAME AS ID,
       RECDESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSRECDEFN)
 WHERE (:1 = 'Y' OR %Upper(RECNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(RECDESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY RECNAME
";

        public const string RecordFieldPeopleCode = @"
SELECT 'Record Field PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 AS ID,
       OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3 AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSPCMPROG)
 WHERE OBJECTID1 = 1 AND OBJECTID2 = 2 AND OBJECTID3 = 12
   AND (:1 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(OBJECTVALUE1 %Concat '.' %Concat OBJECTVALUE2 %Concat '.' %Concat OBJECTVALUE3) LIKE %Upper(:4) ESCAPE '\')
";

        public const string SQL = @"
SELECT 'SQL' AS DEFN_TYPE,
       SQLID AS ID,
       ' ' AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSSQLDEFN)
 WHERE (:1 = 'Y' OR %Upper(SQLID) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(' ') LIKE %Upper(:4) ESCAPE '\')
 ORDER BY SQLID
";

        public const string StyleSheet = @"
SELECT 'Style Sheet' AS DEFN_TYPE,
       CONTNAME AS ID,
       DESCR AS DESCR,
       LASTUPDDTTM
  FROM %Table(PSCONTDEFN)
 WHERE CONTTYPE = 9
   AND (:1 = 'Y' OR %Upper(CONTNAME) LIKE %Upper(:2) ESCAPE '\')
   AND (:3 = 'Y' OR %Upper(DESCR) LIKE %Upper(:4) ESCAPE '\')
 ORDER BY CONTNAME
";

        /// <summary>
        /// Returns the Meta-SQL open-target query for <paramref name="type"/>, or null if unsupported.
        /// </summary>
        public static string? ForType(OpenTargetType type) => type switch
        {
            OpenTargetType.Activity => Activity,
            OpenTargetType.AnalyticModel => AnalyticModel,
            OpenTargetType.AnalyticType => AnalyticType,
            OpenTargetType.AppEnginePeopleCode => AppEnginePeopleCode,
            OpenTargetType.AppEngineProgram => AppEngineProgram,
            OpenTargetType.ApplicationClass => ApplicationClass,
            OpenTargetType.ApplicationPackage => ApplicationPackage,
            OpenTargetType.ApprovalRuleSet => ApprovalRuleSet,
            OpenTargetType.BusinessInterlink => BusinessInterlink,
            OpenTargetType.BusinessProcess => BusinessProcess,
            OpenTargetType.Component => Component,
            OpenTargetType.ComponentInterface => ComponentInterface,
            OpenTargetType.ComponentInterfacePeopleCode => ComponentInterfacePeopleCode,
            OpenTargetType.ComponentPeopleCode => ComponentPeopleCode,
            OpenTargetType.ComponentRecFieldPeopleCode => ComponentRecFieldPeopleCode,
            OpenTargetType.ComponentRecordPeopleCode => ComponentRecordPeopleCode,
            OpenTargetType.Field => Field,
            OpenTargetType.FileLayout => FileLayout,
            OpenTargetType.FileReference => FileReference,
            OpenTargetType.HTML => HTML,
            OpenTargetType.Image => Image,
            OpenTargetType.Menu => Menu,
            OpenTargetType.MenuPeopleCode => MenuPeopleCode,
            OpenTargetType.Message => Message,
            OpenTargetType.MessagePeopleCode => MessagePeopleCode,
            OpenTargetType.OptimizationModel => OptimizationModel,
            OpenTargetType.Page => Page,
            OpenTargetType.PageFluid => PageFluid,
            OpenTargetType.PagePeopleCode => PagePeopleCode,
            OpenTargetType.Project => Project,
            OpenTargetType.Record => Record,
            OpenTargetType.RecordFieldPeopleCode => RecordFieldPeopleCode,
            OpenTargetType.SQL => SQL,
            OpenTargetType.StyleSheet => StyleSheet,
            _ => null
        };
    }
}
