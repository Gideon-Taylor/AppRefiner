using AppRefiner.Database.Models;

namespace AppRefiner.Database
{
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
    }
}
