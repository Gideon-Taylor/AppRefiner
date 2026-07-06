using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace AppRefiner.Database
{
    /// <summary>
    /// Oracle-specific implementation of IDbConnection
    /// </summary>
    public class OracleDbConnection : IDbConnection
    {
        private OracleConnection _connection;
        /// <summary>
        /// Gets or sets the connection string
        /// </summary>
        public string ConnectionString
        {
            get => _connection.ConnectionString;
            set => _connection.ConnectionString = value;
        }

        /// <summary>
        /// Gets the current state of the connection
        /// </summary>
        public ConnectionState State => _connection.State;

        /// <summary>
        /// Gets the name of the database server
        /// </summary>
        public string ServerName
        {
            get
            {
                try
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        return _connection.ServerVersion;
                    }
                }
                catch (Exception)
                {
                    // Ignore errors when getting server version
                }

                return "Oracle";
            }
        }

        /// <summary>
        /// Creates a new Oracle connection
        /// </summary>
        /// <param name="connectionString">Connection string to use</param>
        public OracleDbConnection(string connectionString)
        {
            _connection = new OracleConnection(connectionString);
            /* If TNS_ADMIN setting is populated, set it here */
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TNS_ADMIN))
            {
                string TNS_ADMIN = Properties.Settings.Default.TNS_ADMIN;

                // OracleConfiguration.TnsAdmin is a PROCESS-GLOBAL setting that becomes
                // immutable once ANY Oracle connection has been opened in the process —
                // re-assigning it after that throws ORA-50099 ("cannot be set after a
                // connection has been opened"). It only needs to be set once and its value
                // persists for the process lifetime, so skip the assignment when it is already
                // the desired value (the case on every reconnect after the first connect).
                // This is what actually points the driver at tnsnames.ora for plain-alias
                // resolution, so the persisted value keeps working.
                try
                {
                    if (!string.Equals(OracleConfiguration.TnsAdmin, TNS_ADMIN, StringComparison.Ordinal))
                    {
                        OracleConfiguration.TnsAdmin = TNS_ADMIN;
                        Debug.Log($"OracleConfiguration.TnsAdmin set to: {TNS_ADMIN}");
                    }
                }
                catch (Exception ex)
                {
                    // Already locked by a prior open connection — the existing global value
                    // (set on the first connect) remains in effect, so resolution still works.
                    Debug.Log($"OracleConfiguration.TnsAdmin already locked, keeping existing value: {ex.Message}");
                }

                // The per-connection TnsAdmin has the same immutability (pool-level) — best-effort.
                try
                {
                    _connection.TnsAdmin = TNS_ADMIN;
                    Debug.Log($"Per-connection TnsAdmin set to: {_connection.TnsAdmin}");
                }
                catch (Exception ex)
                {
                    Debug.Log($"Per-connection TnsAdmin not set (using global OracleConfiguration.TnsAdmin instead): {ex.Message}");
                }
            }

        }

        /// <summary>
        /// Opens the database connection
        /// </summary>
        public void Open()
        {
            _connection.Open();
        }

        /// <summary>
        /// Closes the database connection
        /// </summary>
        public void Close()
        {
            _connection.Close();
        }

        /// <summary>
        /// Creates a command associated with this connection
        /// </summary>
        public IDbCommand CreateCommand()
        {
            return _connection.CreateCommand();
        }

        /// <summary>
        /// Executes a query and returns the results as a DataTable
        /// </summary>
        public DataTable ExecuteQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.BindByName = true;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    OracleParameter oracleParam = command.CreateParameter();
                    oracleParam.ParameterName = param.Key;
                    oracleParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(oracleParam);
                }
            }

            DataTable dataTable = new();
            using (var adapter = new OracleDataAdapter(command))
            {
                adapter.Fill(dataTable);
            }

            return dataTable;
        }

        /// <summary>
        /// Executes a non-query command and returns the number of rows affected
        /// </summary>
        public int ExecuteNonQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.BindByName = true;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    OracleParameter oracleParam = command.CreateParameter();
                    oracleParam.ParameterName = param.Key;
                    oracleParam.Value = param.Value ?? DBNull.Value;
                    command.Parameters.Add(oracleParam);
                }
            }

            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Disposes the connection
        /// </summary>
        public void Dispose()
        {
            _connection?.Dispose();
        }

        /// <summary>
        /// Gets all TNS entries from the tnsnames.ora file, following IFILE includes.
        /// The managed ODP.NET driver does not resolve IFILE itself, so include
        /// failures and warnings are logged here for diagnosis.
        /// </summary>
        /// <returns>The parse result (empty if no tnsnames.ora could be located)</returns>
        public static TnsParseResult GetTnsEntries()
        {
            string? tnsNamesPath = GetTnsNamesPath();
            if (string.IsNullOrEmpty(tnsNamesPath))
            {
                return new TnsParseResult();
            }

            TnsParseResult result = TnsNamesParser.Parse(tnsNamesPath);

            foreach (string failure in result.FailedIncludes)
            {
                Debug.Log($"tnsnames.ora include could not be read: {failure}");
            }
            foreach (string warning in result.Warnings)
            {
                Debug.Log($"tnsnames.ora parse warning: {warning}");
            }

            return result;
        }

        /// <summary>
        /// Gets all TNS names from the tnsnames.ora file (including IFILE includes)
        /// </summary>
        /// <returns>A list of TNS names</returns>
        public static List<string> GetAllTnsNames()
        {
            return GetTnsEntries().Entries.Select(e => e.Alias).ToList();
        }

        /// <summary>
        /// Gets the path to the tnsnames.ora file
        /// </summary>
        /// <returns>The full path to tnsnames.ora, or null if not found</returns>
        private static string? GetTnsNamesPath()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.TNS_ADMIN))
            {
                return Path.Combine(Properties.Settings.Default.TNS_ADMIN, "tnsnames.ora");
            }

            // Check TNS_ADMIN environment variable first
            string? tnsAdmin = Environment.GetEnvironmentVariable("TNS_ADMIN");
            if (!string.IsNullOrEmpty(tnsAdmin))
            {
                string path = Path.Combine(tnsAdmin, "tnsnames.ora");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Check ORACLE_HOME environment variable
            string? oracleHome = Environment.GetEnvironmentVariable("ORACLE_HOME");
            if (!string.IsNullOrEmpty(oracleHome))
            {
                string path = Path.Combine(oracleHome, "network", "admin", "tnsnames.ora");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
