using AppRefiner.Database.Models;

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
    }
}
