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
        /// Helper method to convert project item object IDs to program object IDs
        /// </summary>
        /// <param name="objectType">The object type code</param>
        /// <param name="objectId1">First object ID</param>
        /// <param name="objectValue1">First object value</param>
        /// <param name="objectId2">Second object ID</param>
        /// <param name="objectValue2">Second object value</param>
        /// <param name="objectId3">Third object ID</param>
        /// <param name="objectValue3">Third object value</param>
        /// <param name="objectId4">Fourth object ID</param>
        /// <param name="objectValue4">Fourth object value</param>
        /// <returns>Dictionary mapping column names to values for PSPCMPROG query</returns>
        private Dictionary<string, object> ConvertProjectItemToProgram(
            int objectType,
            object objectId1, object objectValue1,
            object objectId2, object objectValue2,
            object objectId3, object objectValue3,
            object objectId4, object objectValue4)
        {
            // Placeholder for conversion logic - to be manually implemented
            // This converts from 4 pairs in PSPROJECTITEM to 7 pairs in PSPCMPROG
            
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            
            // Copy the existing values
            parameters["OBJECTID1"] = objectId1;
            parameters["OBJECTVALUE1"] = objectValue1;
            parameters["OBJECTID2"] = objectId2;
            parameters["OBJECTVALUE2"] = objectValue2;
            parameters["OBJECTID3"] = objectId3;
            parameters["OBJECTVALUE3"] = objectValue3;
            parameters["OBJECTID4"] = objectId4;
            parameters["OBJECTVALUE4"] = objectValue4;
            
            // Default values for remaining parameters
            // These will be overridden in the actual implementation
            parameters["OBJECTID5"] = DBNull.Value;
            parameters["OBJECTVALUE5"] = DBNull.Value;
            parameters["OBJECTID6"] = DBNull.Value;
            parameters["OBJECTVALUE6"] = DBNull.Value;
            parameters["OBJECTID7"] = DBNull.Value;
            parameters["OBJECTVALUE7"] = DBNull.Value;
            
            return parameters;
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
                // Build the path using non-empty OBJECTVALUE fields
                List<string> pathParts = new List<string>();
                int objectType = Convert.ToInt32(row["OBJECTTYPE"]);
                for (int i = 1; i <= 4; i++)
                {
                    string value = row[$"OBJECTVALUE{i}"]?.ToString();
                    if (string.IsNullOrEmpty(value))
                        break;
                    
                    pathParts.Add(value);
                }
                
                if (pathParts.Count > 0)
                {
                    string path = string.Join(":", pathParts);
                    string content = string.Empty;
                    
                    // Convert project item object IDs to program object IDs
                    Dictionary<string, object> programObjectIds = ConvertProjectItemToProgram(
                        objectType,
                        row["OBJECTID1"], row["OBJECTVALUE1"],
                        row["OBJECTID2"], row["OBJECTVALUE2"],
                        row["OBJECTID3"], row["OBJECTVALUE3"],
                        row["OBJECTID4"], row["OBJECTVALUE4"]);
                    
                    // Build query to retrieve the actual PeopleCode from PSPCMPROG
                    StringBuilder queryBuilder = new StringBuilder();
                    queryBuilder.Append("SELECT PROGTXT FROM PSPCMPROG WHERE ");
                    
                    Dictionary<string, object> progParameters = new Dictionary<string, object>();
                    List<string> conditions = new List<string>();
                    
                    // Add conditions for each object ID/value pair
                    for (int i = 1; i <= 7; i++)
                    {
                        if (programObjectIds.TryGetValue($"OBJECTID{i}", out object objId) && 
                            objId != DBNull.Value && objId != null)
                        {
                            conditions.Add($"OBJECTID{i} = :objId{i}");
                            progParameters[$":objId{i}"] = objId;
                        }
                        
                        if (programObjectIds.TryGetValue($"OBJECTVALUE{i}", out object objVal) && 
                            objVal != DBNull.Value && objVal != null)
                        {
                            conditions.Add($"OBJECTVALUE{i} = :objVal{i}");
                            progParameters[$":objVal{i}"] = objVal;
                        }
                    }
                    
                    queryBuilder.Append(string.Join(" AND ", conditions));
                    queryBuilder.Append(" ORDER BY PROGSEQ");
                    
                    DataTable programData = _connection.ExecuteQuery(queryBuilder.ToString(), progParameters);
                    
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
