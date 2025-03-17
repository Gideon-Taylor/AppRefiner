using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using AppRefiner.Database.Models;

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
                WHERE EXISTS (
                    SELECT 1 FROM PSSQLTEXTDEFN B
                    WHERE A.SQLID = B.SQLID
                )";
                
            DataTable namesResult = _connection.ExecuteQuery(sqlNames);
            
            foreach (DataRow row in namesResult.Rows)
            {
                string? objectName = row["SQLID"].ToString();
                if (objectName != null)
                {
                    string definition = GetSqlDefinition(objectName);
                    definitions[objectName] = definition;
                }
            }
            
            return definitions;
        }
        
        /// <summary>
        /// Retrieves the HTML definition for a given object name
        /// </summary>
        /// <param name="objectName">Name of the HTML object</param>
        /// <returns>The HTML definition</returns>
        public HtmlDefinition GetHtmlDefinition(string objectName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            // PeopleSoft stores HTML definitions in PSCONTENT table
            string sql = @"
                SELECT CONTDATA
                FROM PSCONTENT
                WHERE CONTNAME = :objectName
                ORDER BY SEQNUM";
                
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { ":objectName", objectName }
            };
            
            DataTable result = _connection.ExecuteQuery(sql, parameters);
            
            if (result.Rows.Count == 0)
            {
                return new HtmlDefinition(string.Empty, 0);
            }
            
            // Concatenate all parts of the HTML definition
            StringBuilder htmlContent = new StringBuilder();
            foreach (DataRow row in result.Rows)
            {
                htmlContent.Append(Encoding.Unicode.GetString((byte[])row["CONTDATA"]));
            }
            
            string content = htmlContent.ToString();
            
            // Find the highest bind number in the form %Bind(:n)
            int maxBindNumber = 0;
            Regex bindRegex = new Regex(@"%Bind\(:(\d+)\)", RegexOptions.IgnoreCase);
            MatchCollection matches = bindRegex.Matches(content);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int bindNumber))
                {
                    maxBindNumber = Math.Max(maxBindNumber, bindNumber);
                }
            }
            
            return new HtmlDefinition(content, maxBindNumber);
        }

        /// <summary>
        /// Retrieves all available HTML definitions
        /// </summary>
        /// <returns>Dictionary mapping object names to their HTML definitions</returns>
        public Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            Dictionary<string, HtmlDefinition> definitions = new Dictionary<string, HtmlDefinition>();
            
            // Query to get all HTML object names
            string sqlNames = @"
                SELECT DISTINCT CONTNAME
                FROM PSCONTENT";
                
            DataTable namesResult = _connection.ExecuteQuery(sqlNames);
            
            foreach (DataRow row in namesResult.Rows)
            {
                string? objectName = row["CONTNAME"].ToString();
                if (objectName != null)
                {
                    HtmlDefinition definition = GetHtmlDefinition(objectName);
                    definitions[objectName] = definition;
                }
            }
            
            return definitions;
        }
        

        /// <summary>
        /// Gets all PeopleCode definitions for a specified project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of tuples containing object type, path and content</returns>
        public List<Tuple<int, string, string>> GetPeopleCodeForProject(string projectName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            List<Tuple<int, string, string>> results = new List<Tuple<int, string, string>>();
            
            // PeopleSoft stores project items in PSPROJECTITEM table
            // We're looking for PeopleCode object types
            string sql = @"
                SELECT OBJECTTYPE, 
                       OBJECTID1, OBJECTVALUE1, 
                       OBJECTID2, OBJECTVALUE2, 
                       OBJECTID3, OBJECTVALUE3, 
                       OBJECTID4, OBJECTVALUE4
                FROM PSPROJECTITEM
                WHERE PROJECTNAME = :projectName
                AND OBJECTTYPE IN ( 8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66 )"; // PeopleCode object types
                
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { ":projectName", projectName }
            };
            
            DataTable projectItems = _connection.ExecuteQuery(sql, parameters);
            
            foreach (DataRow row in projectItems.Rows)
            {
                int objectType = Convert.ToInt32(row["OBJECTTYPE"]);
                
                // Create ProjectItem instance
                ProjectItem projectItem = new ProjectItem(
                    objectType,
                    Convert.ToInt32(row["OBJECTID1"]), row["OBJECTVALUE1"]?.ToString() ?? " ",
                    Convert.ToInt32(row["OBJECTID2"]), row["OBJECTVALUE2"]?.ToString() ?? " ",
                    Convert.ToInt32(row["OBJECTID3"]), row["OBJECTVALUE3"]?.ToString() ?? " ",
                    Convert.ToInt32(row["OBJECTID4"]), row["OBJECTVALUE4"]?.ToString() ?? " "
                );
                
                string path = projectItem.BuildPath();
                
                if (!string.IsNullOrEmpty(path))
                {
                    string content = string.Empty;
                    
                    // Convert project item object IDs to program object IDs
                    List<Tuple<int,string>> programFields = projectItem.ToProgramFields();
                    
                    // Fixed query to retrieve the actual PeopleCode from PSPCMPROG with all 7 fields
                    string query = @"
                        SELECT PROGTXT FROM PSPCMPROG 
                        WHERE OBJECTID1 = :objId1 AND OBJECTVALUE1 = :objVal1
                        AND OBJECTID2 = :objId2 AND OBJECTVALUE2 = :objVal2
                        AND OBJECTID3 = :objId3 AND OBJECTVALUE3 = :objVal3
                        AND OBJECTID4 = :objId4 AND OBJECTVALUE4 = :objVal4
                        AND OBJECTID5 = :objId5 AND OBJECTVALUE5 = :objVal5
                        AND OBJECTID6 = :objId6 AND OBJECTVALUE6 = :objVal6
                        AND OBJECTID7 = :objId7 AND OBJECTVALUE7 = :objVal7
                        ORDER BY PROGSEQ";
                    
                    Dictionary<string, object> progParameters = new Dictionary<string, object>();
                    
                    // Set parameters for all 7 object ID/value pairs
                    for (int i = 0; i < 7; i++)
                    {
                        // Parameter index is 1-based in the query
                        int paramIndex = i + 1;
                        
                        // Use values from programFields list or defaults for empty fields
                        int objId = (i < programFields.Count) ? programFields[i].Item1 : 0;
                        string objVal = (i < programFields.Count) ? programFields[i].Item2 : "";
                        
                        progParameters[$":objId{paramIndex}"] = objId;
                        progParameters[$":objVal{paramIndex}"] = objVal;
                    }
                    
                    DataTable programData = _connection.ExecuteQuery(query, progParameters);
                    
                    // Combine the program text from all rows
                    StringBuilder programText = new StringBuilder();
                    foreach (DataRow progRow in programData.Rows)
                    {
                        byte[] blobData = (byte[])progRow["PROGTXT"];
                        // Convert blob to string - the actual conversion depends on how PeopleSoft stores the data
                        // This is a placeholder and may need custom implementation
                        string textPart = Encoding.UTF8.GetString(blobData);
                        programText.Append(textPart);
                    }
                    
                    content = programText.ToString();
                    
                    results.Add(new Tuple<int, string, string>(objectType, path, content));
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Disposes of the connection
        /// </summary>
        public void Dispose()
        {
            _connection?.Dispose();
        }
        
    }
}
