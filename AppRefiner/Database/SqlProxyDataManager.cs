using AppRefiner.Database.Models;
using AppRefiner.Database.SqlProxy;
using AppRefiner.Database.SqlProxyQueries;
using PeopleCodeTypeInfo.Types;
using System.Data;

namespace AppRefiner.Database
{
    /// <summary>
    /// IDataManager over App Designer SAM via static Meta-SQL and <see cref="SqlProxyDbConnection"/>.
    /// </summary>
    public sealed class SqlProxyDataManager : IDataManager
    {
        private const string PendingMessage = "SqlProxyDataManager: pending Task 7+";

        private readonly SqlProxyDbConnection connection;
        private Dictionary<string, RecordFieldCache> recordFieldCache = new();

        /// <summary>
        /// Creates a manager that owns <paramref name="connection"/>.
        /// </summary>
        public SqlProxyDataManager(SqlProxyDbConnection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Gets the underlying database connection
        /// </summary>
        public IDbConnection Connection => connection;

        /// <summary>
        /// Gets whether the manager is connected
        /// </summary>
        public bool IsConnected => connection.State == ConnectionState.Open;

        public string? LastConnectionError { get; private set; }

        /// <summary>
        /// Connect to the database (attach SAM proxy and Ping). No schema ALTER — session is App Designer's.
        /// </summary>
        public bool Connect()
        {
            try
            {
                LastConnectionError = null;
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    ClearRecordFieldCache();
                }
                return true;
            }
            catch (Exception ex)
            {
                LastConnectionError = ex.Message;
                Debug.LogError($"SqlProxyDataManager connect failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the database
        /// </summary>
        public void Disconnect()
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
            ClearRecordFieldCache();
        }

        public void Dispose()
        {
            Disconnect();
            connection.Dispose();
        }

        public void ClearRecordFieldCache() => recordFieldCache.Clear();

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
        }

        /// <summary>
        /// Execute static Meta-SQL with ordered positional binds (not Dictionary rewrite).
        /// </summary>
        private DataTable Query(string sql, IReadOnlyList<SqlProxyBind>? binds = null, int? maxRows = null)
        {
            EnsureConnected();
            return connection.ExecuteQuery(sql, binds ?? Array.Empty<SqlProxyBind>(), maxRows);
        }

        private static SqlProxyBind S(string? value) => SqlProxyBind.FromObject(value ?? string.Empty);
        private static SqlProxyBind I(int value) => SqlProxyBind.FromObject(value);

        public string GetToolsVersion()
        {
            DataTable result = Query(SqlProxyQueryCatalog.GetToolsVersion);
            if (result.Rows.Count == 0)
            {
                return string.Empty;
            }
            return result.Rows[0][0]?.ToString() ?? string.Empty;
        }

        // --- Remaining IDataManager members: stubs for Task 7+ ---

        public string GetSqlDefinition(string objectName)
            => throw new NotImplementedException(PendingMessage);

        public Dictionary<string, string> GetAllSqlDefinitions()
            => throw new NotImplementedException(PendingMessage);

        public HtmlDefinition GetHtmlDefinition(string objectName)
            => throw new NotImplementedException(PendingMessage);

        public Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions()
            => throw new NotImplementedException(PendingMessage);

        public List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName)
            => throw new NotImplementedException(PendingMessage);

        public List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName)
            => throw new NotImplementedException(PendingMessage);

        public bool LoadPeopleCodeItemContent(PeopleCodeItem item)
            => throw new NotImplementedException(PendingMessage);

        public bool CheckAppClassExists(string appClassPath)
            => throw new NotImplementedException(PendingMessage);

        public string? GetAppClassSourceByPath(string appClassPath)
            => throw new NotImplementedException(PendingMessage);

        public string? GetPeopleCodeProgram(OpenTarget openTarget)
            => throw new NotImplementedException(PendingMessage);

        public List<RecordFieldInfo>? GetRecordFields(string recordName)
            => throw new NotImplementedException(PendingMessage);

        public PackageItems GetAppPackageItems(string packagePath)
            => throw new NotImplementedException(PendingMessage);

        public List<EventMapItem> GetEventMapItems(EventMapInfo eventMapInfo)
            => throw new NotImplementedException(PendingMessage);

        public List<EventMapInfo> GetEventMapXrefs(string classPath)
            => throw new NotImplementedException(PendingMessage);

        public List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options)
            => throw new NotImplementedException(PendingMessage);

        public List<OpenTarget> GetFunctionDefiningPrograms()
            => throw new NotImplementedException(PendingMessage);

        public List<string> GetAllClassesForPackage(string packagePath)
            => throw new NotImplementedException(PendingMessage);

        public List<(PSCLASSID[] ObjectIds, string[] ObjectValues)> GetProgramObjectIds(string[] objectValues)
            => throw new NotImplementedException(PendingMessage);

        public PeopleCodeType GetFieldType(string fieldName)
            => throw new NotImplementedException(PendingMessage);

        public List<string> GetPackagesForClass(string className)
            => throw new NotImplementedException(PendingMessage);

        public List<MessageSetInfo> GetMessageSets()
            => throw new NotImplementedException(PendingMessage);

        public List<MessageCatalogEntry> GetMessagesForSet(int setNumber)
            => throw new NotImplementedException(PendingMessage);

        public MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber)
            => throw new NotImplementedException(PendingMessage);

        public List<MessageCatalogEntry> SearchMessageCatalog(string searchTerm, IReadOnlyCollection<int>? setNumbers, int limit)
            => throw new NotImplementedException(PendingMessage);
    }
}
