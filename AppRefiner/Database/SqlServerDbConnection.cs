using System.Data.Odbc;
using System.Data;
using Microsoft.Win32;

namespace AppRefiner.Database
{
    /// <summary>
    /// SQL Server-specific implementation of IDbConnection using ODBC DSN
    /// </summary>
    public class SqlServerDbConnection : IDbConnection
    {
        private OdbcConnection _connection;
        
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

                return "SQL Server";
            }
        }

        /// <summary>
        /// Creates a new SQL Server ODBC connection
        /// </summary>
        /// <param name="connectionString">DSN-based connection string to use</param>
        public SqlServerDbConnection(string connectionString)
        {
            _connection = new OdbcConnection(connectionString);
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
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = sql;

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        OdbcParameter odbcParam = command.CreateParameter();
                        odbcParam.ParameterName = param.Key;
                        odbcParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(odbcParam);
                    }
                }

                DataTable dataTable = new();
                using (var adapter = new OdbcDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }

                return dataTable;
            }
        }

        /// <summary>
        /// Executes a non-query command and returns the number of rows affected
        /// </summary>
        public int ExecuteNonQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = sql;

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        OdbcParameter odbcParam = command.CreateParameter();
                        odbcParam.ParameterName = param.Key;
                        odbcParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(odbcParam);
                    }
                }

                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Disposes the connection
        /// </summary>
        public void Dispose()
        {
            _connection?.Dispose();
        }

        /// <summary>
        /// Gets all available DSNs (both System and User) for SQL Server drivers
        /// </summary>
        /// <returns>A list of DSN names</returns>
        public static List<string> GetAvailableDsns()
        {
            HashSet<string> allDsns = new(); // Use HashSet to avoid duplicates

            try
            {
                // Read System DSNs from registry
                var systemDsns = ReadDsnsFromRegistry(Registry.LocalMachine, "System");
                allDsns.UnionWith(systemDsns);
                
                // Read User DSNs from registry
                var userDsns = ReadDsnsFromRegistry(Registry.CurrentUser, "User");
                allDsns.UnionWith(userDsns);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error reading DSNs from registry: {ex.Message}");
            }

            return allDsns.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// Reads DSN entries from a specific registry hive
        /// </summary>
        /// <param name="registryHive">Registry hive to read from (LocalMachine or CurrentUser)</param>
        /// <param name="dsnType">Type description for logging (System or User)</param>
        /// <returns>List of DSN names for SQL Server drivers</returns>
        private static List<string> ReadDsnsFromRegistry(RegistryKey registryHive, string dsnType)
        {
            List<string> dsns = new();

            try
            {
                using (RegistryKey? odbcKey = registryHive.OpenSubKey(@"SOFTWARE\ODBC\ODBC.INI"))
                {
                    if (odbcKey != null)
                    {
                        // Get ODBC Data Sources key
                        using (RegistryKey? dataSourcesKey = odbcKey.OpenSubKey("ODBC Data Sources"))
                        {
                            if (dataSourcesKey != null)
                            {
                                // Iterate through all DSN entries
                                foreach (string dsnName in dataSourcesKey.GetValueNames())
                                {
                                    string? driver = dataSourcesKey.GetValue(dsnName)?.ToString();
                                    
                                    // Filter for SQL Server drivers
                                    if (IsSqlServerDriver(driver))
                                    {
                                        dsns.Add(dsnName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error reading {dsnType} DSNs from registry: {ex.Message}");
            }

            return dsns;
        }

        /// <summary>
        /// Determines if a driver string represents a SQL Server driver
        /// </summary>
        /// <param name="driver">The driver string from registry</param>
        /// <returns>True if it's a SQL Server driver</returns>
        private static bool IsSqlServerDriver(string? driver)
        {
            if (string.IsNullOrEmpty(driver))
                return false;

            string driverLower = driver.ToLowerInvariant();
            
            // Common SQL Server driver names
            return driverLower.Contains("sql server") ||
                   driverLower.Contains("sqlsrv") ||
                   driverLower.Contains("odbc driver") && driverLower.Contains("sql server");
        }
    }
}