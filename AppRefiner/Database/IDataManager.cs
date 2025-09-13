using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted.Nodes;
using static SqlParser.Ast.DataType;

namespace AppRefiner.Database
{
    public enum EventMapType
    {
        Component, ComponentRecord, ComponentRecordField, Page
    }

    public enum EventMapSequence
    {
        Pre, Post, Replace
    }

    public class EventMapInfo
    {
        public EventMapType Type;
        public string? Component;
        public string? Segment;
        public string? Record;
        public string? Field;
        public string? Page;
        public string? ComponentEvent;
        public string? ComponentRecordEvent;


        /* These fields are used by xref lookups */
        public string? ContentReference;
        public int SequenceNumber;
        public EventMapSequence Sequence;

        public override string ToString()
        {
            /*
             * Format:
             * EventMapType:Component:Segment:Record:Field:Page:ComponentEvent:ComponentRecordEvent
             * Example:
             * Component:MY_COMPONENT:MY_SEGMENT:MY_RECORD:MY_FIELD:MY_PAGE:MY_EVENT
             */
            var xrefSuffix = "";
            if (!string.IsNullOrEmpty(ContentReference))
            {
                xrefSuffix = $"Process Order {Sequence} ({SequenceNumber}) ";
            }
            switch (Type)
            {
                case EventMapType.Component:
                    return $"Component: {Component}.{Segment}.{XlatToEvent(ComponentEvent!)} {xrefSuffix}";
                case EventMapType.ComponentRecord:
                    return $"ComponentRecord: {Component}.{Segment}.{Record}.{XlatToEvent(ComponentRecordEvent!)} {xrefSuffix}";
                case EventMapType.ComponentRecordField:
                    return $"ComponentRecordField: {Component}.{Segment}.{Record}.{Field}.{XlatToEvent(ComponentRecordEvent!)} {xrefSuffix}";
                case EventMapType.Page:
                    return $"Page: {Page}.{XlatToEvent(ComponentRecordEvent!)} {xrefSuffix}";
                default:
                    return $"Unknown EventMapType {xrefSuffix}";
            }
        }

        public static string EventToXlat(string evt)
        {
            switch (evt)
            {
                case "PostBuild":
                    return "POST";
                case "PreBuild":
                    return "PRE";
                case "SavePostChange":
                    return "SPOS";
                case "SavePreChange":
                    return "SPRE";
                case "Workflow":
                    return "WFLO";
                case "Activate":
                    return "PACT";
                case "RowDelete":
                    return "RDEL";
                case "FieldChange":
                    return "RFCH";
                case "FieldDefault":
                    return "RFDT";
                case "FieldEdit":
                    return "RFED";
                case "RowInit":
                    return "RINI";
                case "RowInsert":
                    return "RINS";
                case "RowSelect":
                    return "RSEL";
                case "SaveEdit":
                    return "SEDT";
                case "SearchInit":
                    return "SINT";
                case "SearchSave":
                    return "SSVE";

                default:
                    Debug.Log($"Unknown event: {evt}");
                    return "UNKN";
            }
        }

        public static string XlatToEvent(string xlat)
        {
            switch (xlat)
            {
                case "POST":
                    return "PostBuild";
                case "PRE":
                    return "PreBuild";
                case "SPOS":
                    return "SavePostChange";
                case "SPRE":
                    return "SavePreChange";
                case "WFLO":
                    return "Workflow";
                case "PACT":
                    return "Activate";
                case "RDEL":
                    return "RowDelete";
                case "RFCH":
                    return "FieldChange";
                case "RFDT":
                    return "FieldDefault";
                case "RFED":
                    return "FieldEdit";
                case "RINI":
                    return "RowInit";
                case "RINS":
                    return "RowInsert";
                case "RSEL":
                    return "RowSelect";
                case "SEDT":
                    return "SaveEdit";
                case "SINT":
                    return "SearchInit";
                case "SSVE":
                    return "SearchSave";
                default:
                    Debug.Log($"Unknown event: {xlat}");
                    return xlat;
            }
        }
    }

    public class EventMapItem
    {
        public EventMapSequence Sequence;
        public int SeqNumber;
        public string? ContentReference;
        public string? Component;
        public string? Segment;
        public string? PackageRoot;
        public string? PackagePath;
        public string? ClassName;
    }

    /// <summary>
    /// Interface for data management operations
    /// </summary>
    public interface IDataManager : IDisposable
    {
        /// <summary>
        /// Gets the underlying database connection
        /// </summary>
        IDbConnection Connection { get; }

        /// <summary>
        /// Gets whether the manager is connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connect to the database
        /// </summary>
        /// <returns>True if connection was successful</returns>
        bool Connect();

        /// <summary>
        /// Disconnect from the database
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Retrieves the SQL definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the SQL object</param>
        /// <returns>The SQL definition as a string</returns>
        string GetSqlDefinition(string objectName);

        /// <summary>
        /// Retrieves all available SQL definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their SQL definitions</returns>
        Dictionary<string, string> GetAllSqlDefinitions();

        /// <summary>
        /// Retrieves the HTML definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the HTML object</param>
        /// <returns>The HTML definition</returns>
        HtmlDefinition GetHtmlDefinition(string objectName);

        /// <summary>
        /// Retrieves all available HTML definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their HTML definitions</returns>
        Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions();

        /// <summary>
        /// Gets all PeopleCode definitions for a specified project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of tuples containing path and content (initially empty)</returns>
        List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName);

        /// <summary>
        /// Gets metadata for PeopleCode items in a project without loading program text
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of PeopleCodeItem objects with metadata only</returns>
        List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName);

        /// <summary>
        /// Loads program text and references for a specific PeopleCode item
        /// </summary>
        /// <param name="item">The PeopleCode item to load content for</param>
        /// <returns>True if loading was successful</returns>
        bool LoadPeopleCodeItemContent(PeopleCodeItem item);

        /// <summary>
        /// Checks if an Application Class exists in the database
        /// </summary>
        /// <param name="appClassPath">The application class path to check</param>
        /// <returns>True if the application class exists, false otherwise</returns>
        bool CheckAppClassExists(string appClassPath);

        /// <summary>
        /// Retrieves the source code for an Application Class by its path
        /// </summary>
        /// <param name="appClassPath">The fully qualified application class path (e.g., "Package:Subpackage:ClassName")</param>
        /// <returns>The source code of the application class if found, otherwise null</returns>
        string? GetAppClassSourceByPath(string appClassPath);

        string? GetPeopleCodeProgram(OpenTarget openTarget);

        /// <summary>
        /// Retrieves field information for a specified PeopleSoft record.
        /// </summary>
        /// <param name="recordName">The name of the record (uppercase).</param>
        /// <returns>A list of RecordFieldInfo objects, or null if the record doesn't exist or an error occurs.</returns>
        List<RecordFieldInfo>? GetRecordFields(string recordName);

        /// <summary>
        /// Gets all subpackages and classes in the specified application package path
        /// </summary>
        /// <param name="packagePath">The package path (root package or path like ROOT:SubPackage:SubPackage2)</param>
        /// <returns>Dictionary containing lists of subpackages and classes in the current package path</returns>
        PackageItems GetAppPackageItems(string packagePath);

        List<EventMapItem> GetEventMapItems(EventMapInfo eventMapInfo);

        List<EventMapInfo> GetEventMapXrefs(string classPath);

        /// <summary>
        /// Gets targets that can be opened based on search options including separate ID and description search terms
        /// </summary>
        /// <param name="options">Search options including enabled types, limits, and search terms for ID and description</param>
        /// <returns>List of OpenTarget objects matching the search criteria</returns>
        List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options);

        /// <summary>
        /// Gets programs from PSPCMPROG that may contain function definitions
        /// </summary>
        /// <returns>List of OpenTarget objects representing programs that may contain function definitions</returns>
        List<OpenTarget> GetFunctionDefiningPrograms();

        string GetToolsVersion();

        /// <summary>
        /// Gets program object IDs from PSPCMPROG based on object values
        /// </summary>
        /// <param name="objectValues">Array of object values (padded to 7 with spaces)</param>
        /// <returns>List of tuples containing object IDs and values for matching programs</returns>
        List<(PSCLASSID[] ObjectIds, string[] ObjectValues)> GetProgramObjectIds(string[] objectValues);


        public static bool TryMapStringToTargetType(string typeName, out OpenTargetType targetType)
        {
            targetType = typeName switch
            {
                "Activity" => OpenTargetType.Activity,
                "Analytic Model" => OpenTargetType.AnalyticModel,
                "Analytic Type" => OpenTargetType.AnalyticType,
                "App Engine Program" => OpenTargetType.AppEngineProgram,
                "Application Package" => OpenTargetType.ApplicationPackage,
                "Application Class" => OpenTargetType.ApplicationClass,
                "Approval Rule Set" => OpenTargetType.ApprovalRuleSet,
                "Business Interlink" => OpenTargetType.BusinessInterlink,
                "Business Process" => OpenTargetType.BusinessProcess,
                "Component" => OpenTargetType.Component,
                "Component Interface" => OpenTargetType.ComponentInterface,
                "Field" => OpenTargetType.Field,
                "File Layout" => OpenTargetType.FileLayout,
                "File Reference" => OpenTargetType.FileReference,
                "HTML" => OpenTargetType.HTML,
                "Image" => OpenTargetType.Image,
                "Menu" => OpenTargetType.Menu,
                "Message" => OpenTargetType.Message,
                "Optimization Model" => OpenTargetType.OptimizationModel,
                "Page" => OpenTargetType.Page,
                "Page (Fluid)" => OpenTargetType.PageFluid,

                // Non class PeopleCode Types
                "Non Class PeopleCode" => OpenTargetType.NonClassPeopleCode, // used by the open config dialog */

                /* These are used during the reverse lookup from the query results */
                "Page PeopleCode" => OpenTargetType.PagePeopleCode,
                "Component PeopleCode" => OpenTargetType.ComponentPeopleCode,
                "Component Record PeopleCode" => OpenTargetType.ComponentRecordPeopleCode,
                "Component Rec Field PeopleCode" => OpenTargetType.ComponentRecFieldPeopleCode,
                "Record Field PeopleCode" => OpenTargetType.RecordFieldPeopleCode,
                "Menu PeopleCode" => OpenTargetType.MenuPeopleCode,
                "App Engine PeopleCode" => OpenTargetType.AppEnginePeopleCode,
                "Component Interface PeopleCode" => OpenTargetType.ComponentInterfacePeopleCode,
                "Message PeopleCode" => OpenTargetType.MessagePeopleCode,
                

                "Project" => OpenTargetType.Project,
                "Record" => OpenTargetType.Record,
                "SQL" => OpenTargetType.SQL,
                "Style Sheet" => OpenTargetType.StyleSheet,
                _ => OpenTargetType.Project
            };

            return !typeName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        }

        public static List<(PSCLASSID, string)> CreateObjectPairs(OpenTargetType targetType, string id, string descr)
        {
            var pairs = new List<(PSCLASSID, string)>();

            switch (targetType)
            {
                case OpenTargetType.Project:
                    pairs.Add((PSCLASSID.PROJECT, id));
                    break;
                case OpenTargetType.Page:
                case OpenTargetType.PageFluid:
                    pairs.Add((PSCLASSID.PAGE, id));
                    break;
                case OpenTargetType.Activity:
                    pairs.Add((PSCLASSID.ACTIVITYNAME, id));
                    break;
                case OpenTargetType.AnalyticModel:
                    pairs.Add((PSCLASSID.ANALYTIC_MODEL_ID, id));
                    break;
                case OpenTargetType.AnalyticType:
                    pairs.Add((PSCLASSID.NONE, id)); // No specific PSCLASSID for analytic types
                    break;
                case OpenTargetType.AppEngineProgram:
                    pairs.Add((PSCLASSID.AEAPPLICATIONID, id));
                    break;
                case OpenTargetType.ApplicationPackage:
                    // Application packages use a hierarchical structure
                    pairs.Add((PSCLASSID.APPLICATION_PACKAGE, id));
                    break;
                case OpenTargetType.ApplicationClass:
                    // Application classes use package root, qualify path, and class name
                    string[] classParts = id.Split(':');
                    var currentClassID = PSCLASSID.APPLICATION_PACKAGE;
                    for (var x = 0; x < classParts.Length; x++)
                    {
                        pairs.Add(((PSCLASSID)currentClassID++, classParts[x]));
                    }

                    /* Last one is always APPLICATION_CLASS */
                    pairs[pairs.Count - 1] = (PSCLASSID.APPLICATION_CLASS, pairs[pairs.Count - 1].Item2);
                    pairs.Add((PSCLASSID.METHOD, "OnExecute"));
                    break;
                case OpenTargetType.ApprovalRuleSet:
                    pairs.Add((PSCLASSID.APPRRULESET, id));
                    break;
                case OpenTargetType.BusinessInterlink:
                    pairs.Add((PSCLASSID.NONE, id)); // No specific PSCLASSID for business interlinks
                    break;
                case OpenTargetType.BusinessProcess:
                    pairs.Add((PSCLASSID.BUSINESSPROCESS, id));
                    break;
                case OpenTargetType.Component:
                    pairs.Add((PSCLASSID.COMPONENT, id));
                    break;
                case OpenTargetType.ComponentInterface:
                    pairs.Add((PSCLASSID.COMPONENTINTERFACE, id));
                    break;
                case OpenTargetType.Field:
                    pairs.Add((PSCLASSID.FIELD, id));
                    break;
                case OpenTargetType.FileLayout:
                    pairs.Add((PSCLASSID.FILELAYOUT, id));
                    break;
                case OpenTargetType.FileReference:
                    pairs.Add((PSCLASSID.FILEREFERENCE, id));
                    break;
                case OpenTargetType.HTML:
                    pairs.Add((PSCLASSID.HTML, id));
                    break;
                case OpenTargetType.Image:
                    pairs.Add((PSCLASSID.IMAGE, id));
                    break;
                case OpenTargetType.Menu:
                    pairs.Add((PSCLASSID.MENU, id));
                    break;
                case OpenTargetType.Message:
                    pairs.Add((PSCLASSID.MESSAGE, id));
                    break;
                case OpenTargetType.OptimizationModel:
                    pairs.Add((PSCLASSID.OPTMODEL, id));
                    break;
                case OpenTargetType.Record:
                    pairs.Add((PSCLASSID.RECORD, id));
                    break;
                case OpenTargetType.SQL:
                    pairs.Add((PSCLASSID.SQL, id));
                    break;
                case OpenTargetType.StyleSheet:
                    pairs.Add((PSCLASSID.STYLESHEET, id));
                    break;
                case OpenTargetType.PagePeopleCode:
                    pairs.Add((PSCLASSID.PAGE, id));  // id is just the page name
                    pairs.Add((PSCLASSID.METHOD, "Activate"));
                    break;

                case OpenTargetType.ComponentPeopleCode:
                    // id format: Component.Market.Method
                    string[] parts = descr.Split('.');
                    pairs.Add((PSCLASSID.COMPONENT, parts[0]));
                    pairs.Add((PSCLASSID.MARKET, parts[1]));
                    pairs.Add((PSCLASSID.METHOD, parts[2]));
                    break;

                case OpenTargetType.ComponentRecordPeopleCode:
                    // id format: Component.Market.Record, descr adds .Method
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.COMPONENT, parts[0]));
                    pairs.Add((PSCLASSID.MARKET, parts[1]));
                    pairs.Add((PSCLASSID.RECORD, parts[2]));
                    pairs.Add((PSCLASSID.METHOD, parts[3]));
                    break;

                case OpenTargetType.ComponentRecFieldPeopleCode:
                    // id format: Component.Market.Record.Field, descr adds .Method
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.COMPONENT, parts[0]));
                    pairs.Add((PSCLASSID.MARKET, parts[1]));
                    pairs.Add((PSCLASSID.RECORD, parts[2]));
                    pairs.Add((PSCLASSID.FIELD, parts[3]));
                    pairs.Add((PSCLASSID.METHOD, parts[4]));
                    break;

                case OpenTargetType.RecordFieldPeopleCode:
                    // id format: Record.Field, descr adds .Method
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.RECORD, parts[0]));
                    pairs.Add((PSCLASSID.FIELD, parts[1]));
                    pairs.Add((PSCLASSID.METHOD, parts[2]));
                    break;

                case OpenTargetType.MenuPeopleCode:
                    // id format: Menu.BarName.ItemName, descr adds .ItemSelected
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.MENU, parts[0]));
                    pairs.Add((PSCLASSID.MENUBAR, parts[1]));
                    pairs.Add((PSCLASSID.MENUITEM, parts[2]));
                    pairs.Add((PSCLASSID.METHOD, parts[3]));
                    break;

                case OpenTargetType.AppEnginePeopleCode:
                    // id format: Program.Section.Step, descr has all 7 parts
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.AEAPPLICATIONID, parts[0]));
                    pairs.Add((PSCLASSID.AESECTION, parts[1]));
                    pairs.Add((PSCLASSID.MARKET, parts[2]));
                    pairs.Add((PSCLASSID.DBTYPE, parts[3]));
                    pairs.Add((PSCLASSID.EFFDT, parts[4]));
                    pairs.Add((PSCLASSID.AESTEP, parts[5]));
                    pairs.Add((PSCLASSID.METHOD, parts[6]));
                    break;

                case OpenTargetType.ComponentInterfacePeopleCode:
                    // id format: CI name, descr adds .Method
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.COMPINTFCINTERFACE, parts[0]));
                    pairs.Add((PSCLASSID.METHOD, parts[1]));
                    break;

                case OpenTargetType.MessagePeopleCode:
                    parts = descr.Split('.');
                    pairs.Add((PSCLASSID.MESSAGE, parts[0]));
                    if (parts.Length == 2 && parts[1] == "OnRequest")
                    {
                        pairs.Add((PSCLASSID.METHOD, parts[1]));
                    }
                    else if (parts.Length == 3 && parts[2] == "Subscription")
                    {
                        pairs.Add((PSCLASSID.SUBSCRIPTION, parts[1]));
                        pairs.Add((PSCLASSID.METHOD, parts[2]));
                    }
                    break;
                default:
                    // For any remaining types without specific PSCLASSID mapping
                    pairs.Add((PSCLASSID.NONE, id));
                    break;
            }

            return pairs;
        }

        private static bool IsValidFieldMethod(string method)
        {
            var fieldMethods = new HashSet<string>
            {
                "FieldChange", "FieldDefault", "FieldEdit",
                "FieldFormula", "PrePopup", "RowDelete",
                "RowInit", "RowInsert", "RowSelect",
                "SaveEdit", "SavePostChange", "SavePreChange",
                "SearchInit", "SearchSave", "Workflow"
            };
            return fieldMethods.Contains(method);
        }
    }
}
