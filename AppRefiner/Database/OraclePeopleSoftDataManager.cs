using AppRefiner.Database.Models;
using System.Data;
using System.Linq;
using System.Text;
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
        private readonly string? _namespace;
        // Cache for record field information
        private Dictionary<string, RecordFieldCache> _recordFieldCache = new();

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
        /// <param name="namespace">Optional PeopleSoft namespace to set as current schema</param>
        public OraclePeopleSoftDataManager(string connectionString, string? @namespace = null)
        {
            _connectionString = connectionString;
            _namespace = @namespace;
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
                    
                    // Set namespace if provided
                    if (!string.IsNullOrEmpty(_namespace))
                    {
                        string sql = $"ALTER SESSION SET CURRENT_SCHEMA={_namespace}";
                        _connection.ExecuteNonQuery(sql);
                    }
                    
                    // Clear the record field cache when connecting
                    ClearRecordFieldCache();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to connect to database: {ex.Message}");
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
            
            // Clear the record field cache when disconnecting
            ClearRecordFieldCache();
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

            Dictionary<string, object> parameters = new()
            {
                { ":objectName", objectName }
            };

            DataTable result = _connection.ExecuteQuery(sql, parameters);

            if (result.Rows.Count == 0)
            {
                return string.Empty;
            }

            // Concatenate all parts of the SQL definition
            System.Text.StringBuilder sqlDef = new();
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

            Dictionary<string, string> definitions = new();

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

            Dictionary<string, object> parameters = new()
            {
                { ":objectName", objectName }
            };

            DataTable result = _connection.ExecuteQuery(sql, parameters);

            if (result.Rows.Count == 0)
            {
                return new HtmlDefinition(string.Empty, 0);
            }

            // Concatenate all parts of the HTML definition
            StringBuilder htmlContent = new();
            foreach (DataRow row in result.Rows)
            {
                htmlContent.Append(Encoding.Unicode.GetString((byte[])row["CONTDATA"]));
            }

            string content = htmlContent.ToString();

            // Find the highest bind number in the form %Bind(:n)
            int maxBindNumber = 0;
            Regex bindRegex = new(@"%Bind\(:(\d+)\)", RegexOptions.IgnoreCase);
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

            Dictionary<string, HtmlDefinition> definitions = new();

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
        /// Gets metadata for PeopleCode items in a project without loading program text
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of PeopleCodeItem objects with metadata only</returns>
        public List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            List<PeopleCodeItem> results = new();

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

            Dictionary<string, object> parameters = new()
            {
                { ":projectName", projectName }
            };

            DataTable projectItems = _connection.ExecuteQuery(sql, parameters);

            foreach (DataRow row in projectItems.Rows)
            {
                int objectType = Convert.ToInt32(row["OBJECTTYPE"]);

                // Create ProjectItem instance
                ProjectItem projectItem = new(
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
                    List<Tuple<int, string>> programFields = projectItem.ToProgramFields();

                    // Create a PeopleCodeItem with empty program text
                    PeopleCodeItem peopleCodeItem = new(
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
                        Array.Empty<byte>(), // Empty program text
                        new List<NameReference>() // Empty name references
                    );

                    results.Add(peopleCodeItem);
                }
            }
            return results;
        }

        /// <summary>
        /// Loads program text and references for a specific PeopleCode item
        /// </summary>
        /// <param name="item">The PeopleCode item to load content for</param>
        /// <returns>True if loading was successful</returns>
        public bool LoadPeopleCodeItemContent(PeopleCodeItem item)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            // Convert object IDs and values to Tuple format needed for queries
            List<Tuple<int, string>> programFields = new();
            for (int i = 0; i < item.ObjectIDs.Length; i++)
            {
                programFields.Add(new Tuple<int, string>(item.ObjectIDs[i], item.ObjectValues[i]));
            }

            // Query to retrieve the actual PeopleCode from PSPCMPROG with all 7 fields
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

            Dictionary<string, object> progParameters = new();

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
            List<byte[]> progTextParts = new();
            foreach (DataRow progRow in programData.Rows)
            {
                byte[] blobData = (byte[])progRow["PROGTXT"];
                progTextParts.Add(blobData);
            }

            // Combine all byte arrays
            byte[] combinedProgramText = CombineBinaryData(progTextParts);
            if (combinedProgramText.Length == 0)
            {
                return false;
            }

            // Get name references
            List<NameReference> nameReferences = GetNameReferencesForProgram(programFields);

            // Update the item with the loaded content
            item.SetProgramText(combinedProgramText);
            item.SetNameReferences(nameReferences);

            return true;
        }

        /// <summary>
        /// Gets all PeopleCode definitions for a specified project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>List of tuples containing object type, path and content</returns>
        public List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName)
        {
            // Get metadata first
            List<PeopleCodeItem> results = GetPeopleCodeItemMetadataForProject(projectName);

            // Load content for each item
            foreach (var item in results)
            {
                LoadPeopleCodeItemContent(item);
            }

            return results;
        }

        /// <summary>
        /// Gets all subpackages and classes in the specified application package path
        /// </summary>
        /// <param name="packagePath">The package path (root package or path like ROOT:SubPackage:SubPackage2)</param>
        /// <returns>Dictionary containing lists of subpackages and classes in the current package path</returns>
        public PackageItems GetAppPackageItems(string packagePath)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            // Initialize result lists
            List<string> subpackages = new();
            List<string> classes = new();

            // Parse the package path
            string[] parts = packagePath.Split(':');
            string rootPackage = parts.Length > 0 ? parts[0] : string.Empty;
            
            // Determine package level (0=root, 1=subpackage, 2=subsubpackage)
            int packageLevel = parts.Length - 1;
            if (packageLevel < 0) packageLevel = 0;
            
            // Build qualify path for queries (e.g., ":" or "SUB:" or "SUB:SUBSUB:")
            string qualifyPath = ":";
            if (packageLevel > 0)
            {
                qualifyPath = string.Join(":", parts.Skip(1));
            }
            
            // Get subpackages based on current level
            string subpackageSql = string.Empty;
            Dictionary<string, object> subpackageParams = new();
            
            // Only root and first subpackage level can have subpackages in PeopleSoft
            // (max 3 levels: ROOT:SUB:SUBSUB)
            if (packageLevel < 2)
            {
                // If root package, get direct subpackages
                if (packageLevel == 0)
                {
                    subpackageSql = @"
                        SELECT PACKAGEID FROM PSPACKAGEDEFN 
                        WHERE PACKAGEROOT = :rootPackage 
                        AND QUALIFYPATH = ':' 
                        AND PACKAGELEVEL = 1
                        ORDER BY PACKAGEID";

                    subpackageParams = new Dictionary<string, object>
                    {
                        { ":rootPackage", rootPackage }
                    };

                }
                // If subpackage, get subsubpackages
                else if (packageLevel == 1)
                {
                    subpackageSql = @"
                        SELECT PACKAGEID FROM PSPACKAGEDEFN 
                        WHERE PACKAGEROOT = :rootPackage 
                        AND QUALIFYPATH = :qualifyPath 
                        AND PACKAGELEVEL = 2
                        ORDER BY PACKAGEID";

                    subpackageParams = new Dictionary<string, object>
                    {
                        { ":rootPackage", rootPackage },
                        { ":qualifyPath", qualifyPath }
                    };

                }


                // Execute query for subpackages if SQL is defined
                if (!string.IsNullOrEmpty(subpackageSql))
                {
                    DataTable subpackageResults = _connection.ExecuteQuery(subpackageSql, subpackageParams);
                    
                    foreach (DataRow row in subpackageResults.Rows)
                    {
                        string? subpackageName = row["PACKAGEID"].ToString();
                        if (!string.IsNullOrEmpty(subpackageName))
                        {
                            subpackages.Add(subpackageName);
                        }
                    }
                }
            }
            
            // Get classes in the current package path
            string classSql = @"
                SELECT APPCLASSID FROM PSAPPCLASSDEFN 
                WHERE PACKAGEROOT = :rootPackage 
                AND QUALIFYPATH = :qualifyPath
                ORDER BY APPCLASSID";
                
            Dictionary<string, object> classParams = new()
            {
                { ":rootPackage", rootPackage },
                { ":qualifyPath", qualifyPath }
            };
            
            DataTable classResults = _connection.ExecuteQuery(classSql, classParams);
            
            foreach (DataRow row in classResults.Rows)
            {
                string? className = row["APPCLASSID"].ToString();
                if (!string.IsNullOrEmpty(className))
                {
                    classes.Add(className);
                }
            }
            
            return new PackageItems(packagePath, subpackages, classes);
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
            List<NameReference> nameReferences = new();

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

            Dictionary<string, object> parameters = new();

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

        public bool CheckAppClassExists(string appClassPath)
        {
           if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            // Split the appClassPath by colons
            string[] parts = appClassPath.Split(':');
            
            // Need at least one package and a class name
            if (parts.Length < 2)
            {
                return false;
            }
            
            // Prepare the query parameters
            Dictionary<string, object> parameters = new();
            
            // Set all object IDs and values based on the parts of the appClassPath
            for (int i = 0; i < 7; i++)
            {
                int objId = 0;
                string objVal = " ";
                
                if (i < parts.Length)
                {
                    if (i == parts.Length - 1)
                    {
                        // Last part is the class (OBJECTID = 107)
                        objId = 107;
                        objVal = parts[i].ToUpper();
                    }
                    else
                    {
                        // Package parts start at 104 and increment
                        objId = 104 + i;
                        objVal = parts[i].ToUpper();
                    }
                }
                else if (i == parts.Length)
                {
                    // After the class comes OnExecute (OBJECTID = 12)
                    objId = 12;
                    objVal = "ONEXECUTE";
                }
                
                // Parameter indices are 1-based
                parameters[$":objId{i + 1}"] = objId;
                parameters[$":objVal{i + 1}"] = objVal;
            }

            // Construct the SQL query to check for existence
            string query = @"SELECT 'Y' FROM PSPCMPROG 
                WHERE OBJECTID1 = :objId1 AND UPPER(OBJECTVALUE1) = :objVal1
                AND OBJECTID2 = :objId2 AND UPPER(OBJECTVALUE2) = :objVal2
                AND OBJECTID3 = :objId3 AND UPPER(OBJECTVALUE3) = :objVal3
                AND OBJECTID4 = :objId4 AND UPPER(OBJECTVALUE4) = :objVal4
                AND OBJECTID5 = :objId5 AND UPPER(OBJECTVALUE5) = :objVal5
                AND OBJECTID6 = :objId6 AND UPPER(OBJECTVALUE6) = :objVal6
                AND OBJECTID7 = :objId7 AND UPPER(OBJECTVALUE7) = :objVal7";
            
            // Execute the query
            DataTable result = _connection.ExecuteQuery(query, parameters);
            
            // If any row exists, the app class exists
            if (result.Rows.Count > 0 && result.Rows[0][0] is string exists)
            {
                return exists == "Y";
            }
            
            return false;
        }

        /// <summary>
        /// Retrieves the source code for an Application Class by its path
        /// </summary>
        /// <param name="appClassPath">The fully qualified application class path</param>
        /// <returns>The source code of the application class if found, otherwise null</returns>
        public string? GetAppClassSourceByPath(string appClassPath)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }
            
            // First check if the class exists
            if (!CheckAppClassExists(appClassPath))
            {
                return null;
            }
            
            // Split the appClassPath by colons
            string[] parts = appClassPath.Split(':');
            
            // Create a PeopleCodeItem for the class
            List<Tuple<int, string>> programFields = new();
            
            // Map parts to object IDs and values
            for (int i = 0; i < parts.Length; i++)
            {
                int objId;
                string objVal = parts[i];
                
                if (i == parts.Length - 1)
                {
                    // Last part is the class (OBJECTID = 107)
                    objId = 107;
                }
                else
                {
                    // Package parts start at 104 and increment
                    objId = 104 + i;
                }
                
                programFields.Add(new Tuple<int, string>(objId, objVal));
            }
            
            // Add the ONEXECUTE part
            programFields.Add(new Tuple<int, string>(12, "OnExecute"));
            
            // Build the PeopleCodeItem with the proper object IDs and values
            int[] objectIDs = new int[7];
            string[] objectValues = new string[7];
            
            for (int i = 0; i < 7; i++)
            {
                if (i < programFields.Count)
                {
                    objectIDs[i] = programFields[i].Item1;
                    objectValues[i] = programFields[i].Item2;
                }
                else
                {
                    objectIDs[i] = 0;
                    objectValues[i] = " ";
                }
            }
            
            // Create a PeopleCodeItem with empty program text
            PeopleCodeItem item = new(
                objectIDs,
                objectValues,
                Array.Empty<byte>(), 
                new List<NameReference>()
            );
            
            // Load the program text
            if (LoadPeopleCodeItemContent(item))
            {
                // Convert the binary program text to a string
                return item.GetProgramTextAsString();
            }
            
            return null;
        }

        /// <summary>
        /// Retrieves field information for a specified PeopleSoft record.
        /// </summary>
        /// <param name="recordName">The name of the record (uppercase).</param>
        /// <returns>A list of RecordFieldInfo objects, or null if the record doesn't exist or an error occurs.</returns>
        public List<RecordFieldInfo>? GetRecordFields(string recordName)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            // Ensure record name is uppercase
            recordName = recordName.ToUpper();
            
            // Check if we have this record's version in the database
            int recordVersion = GetRecordVersion(recordName);
            
            // If record doesn't exist, return null
            if (recordVersion < 0)
            {
                return null;
            }
            
            // Check if we have a valid cached version
            if (_recordFieldCache.TryGetValue(recordName, out RecordFieldCache? cache))
            {
                // If version matches, return cached fields
                if (cache.Version == recordVersion)
                {
                    return cache.Fields;
                }
                // Otherwise, version is different, so remove the outdated cache entry
                _recordFieldCache.Remove(recordName);
            }
            
            // Cache miss or version mismatch, fetch the fields from the database
            List<RecordFieldInfo>? fields = FetchRecordFieldsFromDb(recordName);
            
            // If fields were retrieved successfully, cache them with the current version
            if (fields != null && fields.Count > 0)
            {
                _recordFieldCache[recordName] = new RecordFieldCache(recordName, recordVersion, fields);
            }
            
            return fields;
        }
        
        /// <summary>
        /// Gets the VERSION field value from PSRECDEFN for a specific record.
        /// </summary>
        /// <param name="recordName">Name of the record (uppercase)</param>
        /// <returns>Version number or -1 if record doesn't exist</returns>
        private int GetRecordVersion(string recordName)
        {
            string sql = @"
                SELECT VERSION 
                FROM PSRECDEFN 
                WHERE RECNAME = :recordName";

            Dictionary<string, object> parameters = new()
            {
                { ":recordName", recordName }
            };
            
            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);
                
                if (result.Rows.Count == 0)
                {
                    return -1; // Record doesn't exist
                }
                
                return Convert.ToInt32(result.Rows[0]["VERSION"]);
            }
            catch (Exception)
            {
                // Handle database errors
                return -1;
            }
        }
        
        /// <summary>
        /// Fetches record field information directly from the database.
        /// </summary>
        /// <param name="recordName">Name of the record (uppercase)</param>
        /// <returns>List of field information or null if an error occurs</returns>
        private List<RecordFieldInfo>? FetchRecordFieldsFromDb(string recordName)
        {
            string sql = @"
                SELECT r.FIELDNAME, r.FIELDNUM, f.FIELDTYPE, f.LENGTH, f.DECIMALPOS, r.USEEDIT
                FROM PSRECFIELDDB r
                JOIN PSDBFIELD f ON r.FIELDNAME = f.FIELDNAME
                WHERE r.RECNAME = :recordName
                ORDER BY r.FIELDNUM";

            Dictionary<string, object> parameters = new()
            {
                { ":recordName", recordName }
            };

            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);

                if (result.Rows.Count == 0)
                {
                    return null; // Record not found or has no fields
                }

                List<RecordFieldInfo> fields = new();
                foreach (DataRow row in result.Rows)
                {
                    fields.Add(new RecordFieldInfo(
                        row["FIELDNAME"].ToString() ?? string.Empty,
                        Convert.ToInt32(row["FIELDNUM"]),
                        Convert.ToInt32(row["FIELDTYPE"]),
                        Convert.ToInt32(row["LENGTH"]),
                        Convert.ToInt32(row["DECIMALPOS"]),
                        Convert.ToInt32(row["USEEDIT"])
                    ));
                }
                return fields;
            }
            catch (Exception) // Catch potential DB execution errors
            {
                // Log the exception (optional)
                return null;
            }
        }
        
        /// <summary>
        /// Clears the record field cache
        /// </summary>
        public void ClearRecordFieldCache()
        {
            _recordFieldCache.Clear();
        }
    }
}
