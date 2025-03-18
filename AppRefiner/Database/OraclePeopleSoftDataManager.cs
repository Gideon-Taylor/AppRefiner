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
        public List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            List<PeopleCodeItem> results = new List<PeopleCodeItem>();
            
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
                        string objVal = (i < programFields.Count) ? programFields[i].Item2 : " ";
                        
                        progParameters[$":objId{paramIndex}"] = objId;
                        progParameters[$":objVal{paramIndex}"] = objVal;
                    }
                    
                    DataTable programData = _connection.ExecuteQuery(query, progParameters);
                    
                    // Create a new PeopleCodeItem with the program text data
                    List<byte[]> progTextParts = new List<byte[]>();
                    foreach (DataRow progRow in programData.Rows)
                    {
                        byte[] blobData = (byte[])progRow["PROGTXT"];
                        progTextParts.Add(blobData);
                    }
                    
                    // Combine all byte arrays
                    byte[] combinedProgramText = CombineBinaryData(progTextParts);
                    if(combinedProgramText.Length == 0)
                    {
                        int i = 3;
                    }
                    // Create a PeopleCodeItem
                    PeopleCodeItem peopleCodeItem = new PeopleCodeItem(
                        new int[] {
                            programFields[0].Item1, programFields[1].Item1, programFields[2].Item1,
                            programFields[3].Item1, programFields[4].Item1, programFields[5].Item1,
                            programFields[6].Item1
                        },
                        new string[] {
                            programFields[0].Item2, programFields[1].Item2, programFields[2].Item2,
                            programFields[3].Item2, programFields[4].Item2, programFields[5].Item2,
                            programFields[6].Item2
                        },
                        combinedProgramText,
                        GetNameReferencesForProgram(programFields)
                    );
                    
                    results.Add(peopleCodeItem);
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
        
        /// <summary>
        /// Combines multiple byte arrays into a single byte array
        /// </summary>
        /// <param name="byteArrays">List of byte arrays to combine</param>
        /// <returns>Combined byte array</returns>
        private byte[] CombineBinaryData(List<byte[]> byteArrays)
        {
            if (byteArrays == null || byteArrays.Count == 0)
                return Array.Empty<byte>();
                
            // Calculate total length
            int totalLength = 0;
            foreach (byte[] array in byteArrays)
            {
                totalLength += array.Length;
            }
            
            // Create new array and copy data
            byte[] result = new byte[totalLength];
            int offset = 0;
            
            foreach (byte[] array in byteArrays)
            {
                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }
            
            return result;
        }
        
        /// <summary>
        /// Retrieves name references for a PeopleCode program
        /// </summary>
        /// <param name="programFields">Program object ID/value pairs</param>
        /// <returns>List of name references</returns>
        private List<NameReference> GetNameReferencesForProgram(List<Tuple<int, string>> programFields)
        {
            List<NameReference> nameReferences = new List<NameReference>();
            
            // Query to get name references from PSPCMNAME table
            string query = @"
                SELECT NAMENUM, RECNAME, REFNAME
                FROM PSPCMNAME
                WHERE OBJECTID1 = :objId1 AND OBJECTVALUE1 = :objVal1
                AND OBJECTID2 = :objId2 AND OBJECTVALUE2 = :objVal2
                AND OBJECTID3 = :objId3 AND OBJECTVALUE3 = :objVal3
                AND OBJECTID4 = :objId4 AND OBJECTVALUE4 = :objVal4
                AND OBJECTID5 = :objId5 AND OBJECTVALUE5 = :objVal5
                AND OBJECTID6 = :objId6 AND OBJECTVALUE6 = :objVal6
                AND OBJECTID7 = :objId7 AND OBJECTVALUE7 = :objVal7
                ORDER BY NAMENUM";
                
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            
            // Set parameters for all 7 object ID/value pairs
            for (int i = 0; i < 7; i++)
            {
                // Parameter index is 1-based in the query
                int paramIndex = i + 1;
                
                // Use values from programFields list or defaults for empty fields
                int objId = (i < programFields.Count) ? programFields[i].Item1 : 0;
                string objVal = (i < programFields.Count) ? programFields[i].Item2 : "";
                
                parameters[$":objId{paramIndex}"] = objId;
                parameters[$":objVal{paramIndex}"] = objVal;
            }
            
            DataTable nameData = _connection.ExecuteQuery(query, parameters);
            
            foreach (DataRow row in nameData.Rows)
            {
                int nameNum = Convert.ToInt32(row["NAMENUM"]);
                string recName = row["RECNAME"]?.ToString() ?? string.Empty;
                string refName = row["REFNAME"]?.ToString() ?? string.Empty;
                
                nameReferences.Add(new NameReference(nameNum, recName, refName));
            }
            
            return nameReferences;
        }
        
    }
}
