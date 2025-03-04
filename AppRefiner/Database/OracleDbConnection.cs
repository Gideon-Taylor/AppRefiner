using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

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
                        OracleParameter oracleParam = command.CreateParameter() as OracleParameter;
                        oracleParam.ParameterName = param.Key;
                        oracleParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(oracleParam);
                    }
                }
                
                DataTable dataTable = new DataTable();
                using (var adapter = new OracleDataAdapter(command as OracleCommand))
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
                        OracleParameter oracleParam = command.CreateParameter() as OracleParameter;
                        oracleParam.ParameterName = param.Key;
                        oracleParam.Value = param.Value ?? DBNull.Value;
                        command.Parameters.Add(oracleParam);
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
        /// Gets all TNS names from the tnsnames.ora file
        /// </summary>
        /// <returns>A list of TNS names</returns>
        public static List<string> GetAllTnsNames()
        {
            List<string> tnsNames = new List<string>();
            string? tnsNamesPath = GetTnsNamesPath();

            if (string.IsNullOrEmpty(tnsNamesPath) || !File.Exists(tnsNamesPath))
            {
                return tnsNames;
            }

            try
            {
                string content = File.ReadAllText(tnsNamesPath);

                // Regular expression to find TNS entries
                Regex regex = new Regex(@"^\s*([a-zA-Z0-9_\-]+)\s*=", RegexOptions.Multiline);
                MatchCollection matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        tnsNames.Add(match.Groups[1].Value.Trim());
                    }
                }
            }
            catch (Exception)
            {
                // Ignore any errors reading the file
            }

            return tnsNames;
        }

        /// <summary>
        /// Gets the path to the tnsnames.ora file
        /// </summary>
        /// <returns>The full path to tnsnames.ora, or null if not found</returns>
        private static string? GetTnsNamesPath()
        {
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
