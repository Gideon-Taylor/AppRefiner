using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;

namespace AppRefiner.Database
{
    /// <summary>
    /// PeopleSoft-specific implementation of the data manager
    /// </summary>
    public class OraclePeopleSoftDataManager : IDataManager
    {
        private IDbConnection _connection;
        private readonly string _connectionString;
        
        /// <summary>
        /// Gets the underlying database connection
        /// </summary>
        public IDbConnection Connection => _connection;
        
        /// <summary>
        /// Gets whether the manager is connected
        /// </summary>
        public bool IsConnected => _connection?.State == ConnectionState.Open;
        
        /// <summary>
        /// Creates a new PeopleSoft data manager with the specified connection string
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public OraclePeopleSoftDataManager(string connectionString)
        {
            _connectionString = connectionString;
            _connection = new OracleDbConnection(connectionString);
        }
        
        /// <summary>
        /// Connect to the database
        /// </summary>
        /// <returns>True if connection was successful</returns>
        public bool Connect()
        {
            try
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }
                return true;
            }
            catch (Exception)
            {
                // Connection failed
                return false;
            }
        }
        
        /// <summary>
        /// Disconnect from the database
        /// </summary>
        public void Disconnect()
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }
        }
        
        /// <summary>
        /// Retrieves the SQL definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the SQL object</param>
        /// <returns>The SQL definition as a string</returns>
        public string GetSqlDefinition(string objectName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            // PeopleSoft stores SQL definitions in PSSQLDEFN/PSSQLTEXTDEFN tables
            string sql = @"
                SELECT B.SQLTEXT
                FROM PSSQLDEFN A, PSSQLTEXTDEFN B
                WHERE A.SQLID = B.SQLID
                AND A.SQLTYPE = 'SQL'
                AND A.SQLID = :objectName
                ORDER BY B.SEQNUM";
                
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { ":objectName", objectName }
            };
            
            DataTable result = _connection.ExecuteQuery(sql, parameters);
            
            if (result.Rows.Count == 0)
            {
                return string.Empty;
            }
            
            // Concatenate all parts of the SQL definition
            System.Text.StringBuilder sqlDef = new System.Text.StringBuilder();
            foreach (DataRow row in result.Rows)
            {
                sqlDef.Append(row["SQLTEXT"]);
            }
            
            return sqlDef.ToString();
        }
        
        /// <summary>
        /// Retrieves all available SQL definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their SQL definitions</returns>
        public Dictionary<string, string> GetAllSqlDefinitions()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            Dictionary<string, string> definitions = new Dictionary<string, string>();
            
            // Query to get all SQL object names
            string sqlNames = @"
                SELECT DISTINCT A.SQLID
                FROM PSSQLDEFN A
                WHERE A.SQLTYPE = 'SQL'
                AND EXISTS (
                    SELECT 1 FROM PSSQLTEXTDEFN B
                    WHERE A.SQLID = B.SQLID
                )";
                
            DataTable namesResult = _connection.ExecuteQuery(sqlNames);
            
            foreach (DataRow row in namesResult.Rows)
            {
                string objectName = row["SQLID"].ToString();
                string definition = GetSqlDefinition(objectName);
                definitions[objectName] = definition;
            }
            
            return definitions;
        }
        
        /// <summary>
        /// Disposes of the connection
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
            string tnsNamesPath = GetTnsNamesPath();
            
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
        private static string GetTnsNamesPath()
        {
            // Check TNS_ADMIN environment variable first
            string tnsAdmin = Environment.GetEnvironmentVariable("TNS_ADMIN");
            if (!string.IsNullOrEmpty(tnsAdmin))
            {
                string path = Path.Combine(tnsAdmin, "tnsnames.ora");
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            // Check ORACLE_HOME environment variable
            string oracleHome = Environment.GetEnvironmentVariable("ORACLE_HOME");
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
