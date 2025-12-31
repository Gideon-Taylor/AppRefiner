using AppRefiner.Database.Models;
using PeopleCodeTypeInfo.Types;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace AppRefiner.Database
{
    /// <summary>
    /// SQL Server PeopleSoft-specific implementation of the data manager using ODBC
    /// </summary>
    public class SqlServerPeopleSoftDataManager : IDataManager
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
        /// Creates a new SQL Server PeopleSoft data manager with the specified connection string
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="namespace">Optional PeopleSoft namespace to set as current database</param>
        public SqlServerPeopleSoftDataManager(string connectionString, string? @namespace = null)
        {
            _connectionString = connectionString;
            _namespace = @namespace;
            _connection = new SqlServerDbConnection(connectionString);
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

                    // Set database if namespace provided (SQL Server equivalent of Oracle's schema setting)
                    if (!string.IsNullOrEmpty(_namespace))
                    {
                        string sql = $"USE [{_namespace}]";
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
                AND A.SQLID = ?
                ORDER BY B.SEQNUM";

            Dictionary<string, object> parameters = new()
            {
                { "objectName", objectName }
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
                WHERE CONTNAME = ?
                ORDER BY SEQNUM";

            Dictionary<string, object> parameters = new()
            {
                { "objectName", objectName }
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
                WHERE PROJECTNAME = ?
                AND OBJECTTYPE IN ( 8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66 )"; // PeopleCode object types

            Dictionary<string, object> parameters = new()
            {
                { "projectName", projectName }
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
                WHERE OBJECTID1 = ? AND OBJECTVALUE1 = ?
                AND OBJECTID2 = ? AND OBJECTVALUE2 = ?
                AND OBJECTID3 = ? AND OBJECTVALUE3 = ?
                AND OBJECTID4 = ? AND OBJECTVALUE4 = ?
                AND OBJECTID5 = ? AND OBJECTVALUE5 = ?
                AND OBJECTID6 = ? AND OBJECTVALUE6 = ?
                AND OBJECTID7 = ? AND OBJECTVALUE7 = ?
                ORDER BY PROGSEQ";

            Dictionary<string, object> progParameters = new();

            // Set parameters for all 7 object ID/value pairs in exact order they appear in SQL
            // Order must be: objId1, objVal1, objId2, objVal2, objId3, objVal3, etc.
            for (int i = 0; i < 7; i++)
            {
                // Use values from programFields list or defaults for empty fields
                int objId = (i < programFields.Count) ? programFields[i].Item1 : 0;
                string objVal = (i < programFields.Count) ? programFields[i].Item2 : " ";

                // Add parameters in exact SQL order
                progParameters[$"param{(i * 2) + 1}"] = objId;   // Odd positions: 1, 3, 5, 7, 9, 11, 13
                progParameters[$"param{(i * 2) + 2}"] = objVal;  // Even positions: 2, 4, 6, 8, 10, 12, 14
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
                        WHERE PACKAGEROOT = ? 
                        AND QUALIFYPATH = ':' 
                        AND PACKAGELEVEL = 1
                        ORDER BY PACKAGEID";

                    subpackageParams = new Dictionary<string, object>
                    {
                        { "param1", rootPackage }
                    };

                }
                // If subpackage, get subsubpackages
                else if (packageLevel == 1)
                {
                    subpackageSql = @"
                        SELECT PACKAGEID FROM PSPACKAGEDEFN 
                        WHERE PACKAGEROOT = ? 
                        AND QUALIFYPATH = ? 
                        AND PACKAGELEVEL = 2
                        ORDER BY PACKAGEID";

                    subpackageParams = new Dictionary<string, object>
                    {
                        { "param1", rootPackage },
                        { "param2", qualifyPath }
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
                WHERE PACKAGEROOT = ? 
                AND QUALIFYPATH = ?
                ORDER BY APPCLASSID";

            Dictionary<string, object> classParams = new()
            {
                { "param1", rootPackage },
                { "param2", qualifyPath }
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
                WHERE OBJECTID1 = ? AND OBJECTVALUE1 = ?
                AND OBJECTID2 = ? AND OBJECTVALUE2 = ?
                AND OBJECTID3 = ? AND OBJECTVALUE3 = ?
                AND OBJECTID4 = ? AND OBJECTVALUE4 = ?
                AND OBJECTID5 = ? AND OBJECTVALUE5 = ?
                AND OBJECTID6 = ? AND OBJECTVALUE6 = ?
                AND OBJECTID7 = ? AND OBJECTVALUE7 = ?
                ORDER BY NAMENUM";

            Dictionary<string, object> parameters = new();

            // Set parameters for all 7 object ID/value pairs in exact SQL order
            // Order must be: objId1, objVal1, objId2, objVal2, objId3, objVal3, etc.
            for (int i = 0; i < 7; i++)
            {
                // Use values from programFields list or defaults for empty fields
                int objId = (i < programFields.Count) ? programFields[i].Item1 : 0;
                string objVal = (i < programFields.Count) ? programFields[i].Item2 : "";

                // Add parameters in exact SQL order
                parameters[$"param{(i * 2) + 1}"] = objId;   // Odd positions: 1, 3, 5, 7, 9, 11, 13
                parameters[$"param{(i * 2) + 2}"] = objVal;  // Even positions: 2, 4, 6, 8, 10, 12, 14
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

                // Add parameters in exact SQL order (alternating objId, objVal)
                parameters[$"param{(i * 2) + 1}"] = objId;   // Odd positions: 1, 3, 5, 7, 9, 11, 13
                parameters[$"param{(i * 2) + 2}"] = objVal;  // Even positions: 2, 4, 6, 8, 10, 12, 14
            }

            // Construct the SQL query to check for existence
            string query = @"SELECT 'Y' FROM PSPCMPROG 
                WHERE OBJECTID1 = ? AND UPPER(OBJECTVALUE1) = ?
                AND OBJECTID2 = ? AND UPPER(OBJECTVALUE2) = ?
                AND OBJECTID3 = ? AND UPPER(OBJECTVALUE3) = ?
                AND OBJECTID4 = ? AND UPPER(OBJECTVALUE4) = ?
                AND OBJECTID5 = ? AND UPPER(OBJECTVALUE5) = ?
                AND OBJECTID6 = ? AND UPPER(OBJECTVALUE6) = ?
                AND OBJECTID7 = ? AND UPPER(OBJECTVALUE7) = ?";

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
                WHERE RECNAME = ?";

            Dictionary<string, object> parameters = new()
            {
                { "recordName", recordName }
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
                WHERE r.RECNAME = ?
                ORDER BY r.FIELDNUM";

            Dictionary<string, object> parameters = new()
            {
                { "recordName", recordName }
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

        public List<EventMapInfo> GetEventMapXrefs(string classPath)
        {
            var parts = classPath.Split(':');
            var root = parts[0];
            var className = parts[parts.Length - 1];
            var path = string.Join(":", parts.Skip(1).SkipLast(1));
            if (path == "")
            {
                path = ":";
            }

            // distinct here covers the fact the CREF may exist in multiple portal registries and so shows up as duplicates.
            string sql = @"SELECT DISTINCT
                            EVT.PORTAL_OBJNAME,
                            PORTAL.PORTAL_URI_SEG2 AS COMPONENT,
                            PORTAL.PORTAL_URI_SEG3 AS SEGMENT,
                            EVT.RECNAME AS RECORD,
                            EVT.FIELDNAME AS FIELD,
                            EVT.PNLNAME,
                            EVT.SEQNUM,
                            EVT.PTCS_PROCSEQ,
                            EVT.PTCS_CMPEVENT,
                            EVT.PTCS_CMPRECEVENT
                        FROM
                            PSPTCSSRVCONF EVT
                            INNER JOIN PSPRSMDEFN PORTAL ON PORTAL.PORTAL_OBJNAME = EVT.PORTAL_OBJNAME
                            INNER JOIN PSPTCSSRVDEFN SVC ON SVC.PTCS_SERVICEID = EVT.PTCS_SERVICEID
                        WHERE
                            EVT.PORTAL_NAME = '_PTCS_PTEVMAP'
                            AND SVC.PACKAGEROOT = ?
                            AND SVC.QUALIFYPATH = ?
                            AND SVC.APPCLASSID = ?";
            Dictionary<string, object> parameters = new();
            parameters.Add("param1", root);
            parameters.Add("param2", path);
            parameters.Add("param3", className);

            DataTable result = _connection.ExecuteQuery(sql, parameters);
            List<EventMapInfo> infos = new();
            foreach (DataRow row in result.Rows)
            {
                var info = new EventMapInfo();
                info.ContentReference = row["PORTAL_OBJNAME"].ToString()!.Trim() ?? string.Empty;
                info.Component = row["COMPONENT"].ToString()!.Trim() ?? string.Empty;
                info.Segment = row["SEGMENT"].ToString()!.Trim() ?? string.Empty;
                info.Record = row["RECORD"].ToString()!.Trim() ?? string.Empty;
                info.Field = row["FIELD"].ToString()!.Trim() ?? string.Empty;
                info.Page = row["PNLNAME"].ToString()!.Trim() ?? string.Empty;
                info.ComponentEvent = row["PTCS_CMPEVENT"].ToString()!.Trim() ?? string.Empty;
                info.ComponentRecordEvent = row["PTCS_CMPRECEVENT"].ToString()!.Trim() ?? string.Empty;
                info.SequenceNumber = Convert.ToInt32(row["SEQNUM"]);

                var eventSeq = row["ptcs_procseq"].ToString() ?? string.Empty;
                switch (eventSeq)
                {
                    case "PRE":
                        info.Sequence = EventMapSequence.Pre;
                        break;
                    case "OVER":
                        info.Sequence = EventMapSequence.Replace;
                        break;
                    case "POST":
                        info.Sequence = EventMapSequence.Post;
                        break;
                    default:
                        break;
                }

                /* Determine the "type" */
                if (info.Component != string.Empty && info.Segment != string.Empty)
                {
                    info.Type = EventMapType.Component;
                }
                if (info.Component != string.Empty && info.Segment != string.Empty && info.Record != string.Empty)
                {
                    info.Type = EventMapType.ComponentRecord;
                }
                if (info.Component != string.Empty && info.Segment != string.Empty && info.Record != string.Empty && info.Field != string.Empty)
                {
                    info.Type = EventMapType.ComponentRecordField;
                }
                if (info.Page != string.Empty)
                {
                    info.Type = EventMapType.Page;
                }

                infos.Add(info);
            }

            return infos;
        }

        public List<EventMapItem> GetEventMapItems(EventMapInfo eventMapInfo)
        {
            string sql;
            Dictionary<string, object> parameters = new();
            List<EventMapItem> results = new();

            switch (eventMapInfo.Type)
            {
                case EventMapType.Component:
                    sql = @"
                        SELECT DISTINCT
                            EVT.PORTAL_OBJNAME,
                            EVT.PTCS_PROCSEQ,
                            EVT.SEQNUM,
                            SVC.PACKAGEROOT,
                            SVC.QUALIFYPATH,
                            SVC.APPCLASSID
                        FROM
                            PSPTCSSRVCONF EVT
                            INNER JOIN PSPRSMDEFN PORTAL ON PORTAL.PORTAL_OBJNAME = EVT.PORTAL_OBJNAME
                            INNER JOIN PSPTCSSRVDEFN SVC ON SVC.PTCS_SERVICEID = EVT.PTCS_SERVICEID
                        WHERE
                            EVT.PORTAL_NAME = '_PTCS_PTEVMAP'
                            AND PORTAL.PORTAL_URI_SEG2 = ?
                            AND PORTAL.PORTAL_URI_SEG3 = ?
                            AND EVT.PTCS_CMPEVENT = ?";

                    parameters.Add("param1", eventMapInfo.Component ?? string.Empty);
                    parameters.Add("param2", eventMapInfo.Segment ?? string.Empty);
                    parameters.Add("param3", eventMapInfo.ComponentEvent ?? string.Empty);
                    break;

                case EventMapType.ComponentRecord:
                    sql = @"
                        SELECT DISTINCT
                            EVT.PORTAL_OBJNAME,
                            EVT.PTCS_PROCSEQ,
                            EVT.SEQNUM,
                            SVC.PACKAGEROOT,
                            SVC.QUALIFYPATH,
                            SVC.APPCLASSID
                        FROM
                            PSPTCSSRVCONF EVT
                            INNER JOIN PSPRSMDEFN PORTAL ON PORTAL.PORTAL_OBJNAME = EVT.PORTAL_OBJNAME
                            INNER JOIN PSPTCSSRVDEFN SVC ON SVC.PTCS_SERVICEID = EVT.PTCS_SERVICEID
                        WHERE
                            EVT.PORTAL_NAME = '_PTCS_PTEVMAP'
                            AND PORTAL.PORTAL_URI_SEG2 = ?
                            AND PORTAL.PORTAL_URI_SEG3 = ?
                            AND EVT.RECNAME = ?
                            AND EVT.PTCS_CMPRECEVENT = ?";

                    parameters.Add("param1", eventMapInfo.Component ?? string.Empty);
                    parameters.Add("param2", eventMapInfo.Segment ?? string.Empty);
                    parameters.Add("param3", eventMapInfo.Record ?? string.Empty);
                    parameters.Add("param4", eventMapInfo.ComponentRecordEvent ?? string.Empty);
                    break;

                case EventMapType.ComponentRecordField:
                    sql = @"
                        SELECT DISTINCT
                            EVT.PORTAL_OBJNAME,
                            EVT.PTCS_PROCSEQ,
                            EVT.SEQNUM,
                            SVC.PACKAGEROOT,
                            SVC.QUALIFYPATH,
                            SVC.APPCLASSID
                        FROM
                            PSPTCSSRVCONF EVT
                            INNER JOIN PSPRSMDEFN PORTAL ON PORTAL.PORTAL_OBJNAME = EVT.PORTAL_OBJNAME
                            INNER JOIN PSPTCSSRVDEFN SVC ON SVC.PTCS_SERVICEID = EVT.PTCS_SERVICEID
                        WHERE
                            EVT.PORTAL_NAME = '_PTCS_PTEVMAP'
                            AND PORTAL.PORTAL_URI_SEG2 = ?
                            AND PORTAL.PORTAL_URI_SEG3 = ?
                            AND EVT.RECNAME = ?
                            AND EVT.FIELDNAME = ?
                            AND EVT.PTCS_CMPRECEVENT = ?";

                    parameters.Add("param1", eventMapInfo.Component ?? string.Empty);
                    parameters.Add("param2", eventMapInfo.Segment ?? string.Empty);
                    parameters.Add("param3", eventMapInfo.Record ?? string.Empty);
                    parameters.Add("param4", eventMapInfo.Field ?? string.Empty);
                    parameters.Add("param5", eventMapInfo.ComponentRecordEvent ?? string.Empty);
                    break;

                case EventMapType.Page:
                    sql = @"
                        SELECT DISTINCT
                            EVT.PORTAL_OBJNAME,
                            PORTAL.PORTAL_URI_SEG2,
                            PORTAL.PORTAL_URI_SEG3,
                            EVT.PTCS_PROCSEQ,
                            EVT.SEQNUM,
                            SVC.PACKAGEROOT,
                            SVC.QUALIFYPATH,
                            SVC.APPCLASSID
                        FROM
                            PSPTCSSRVCONF EVT
                            INNER JOIN PSPRSMDEFN PORTAL ON PORTAL.PORTAL_OBJNAME = EVT.PORTAL_OBJNAME
                            INNER JOIN PSPTCSSRVDEFN SVC ON SVC.PTCS_SERVICEID = EVT.PTCS_SERVICEID
                        WHERE
                            EVT.PORTAL_NAME = '_PTCS_PTEVMAP'
                            AND EVT.PNLNAME = ?
                            AND EVT.PTCS_CMPRECEVENT = ?";

                    parameters.Add("param1", eventMapInfo.Page ?? string.Empty);
                    parameters.Add("param2", eventMapInfo.ComponentRecordEvent ?? string.Empty);
                    break;

                default:
                    return results;
            }

            try
            {
                DataTable result = _connection.ExecuteQuery(sql, parameters);

                foreach (DataRow row in result.Rows)
                {
                    EventMapSequence sequence;
                    string procSeq = row["PTCS_PROCSEQ"].ToString() ?? string.Empty;

                    if (procSeq.Equals("PRE", StringComparison.OrdinalIgnoreCase))
                        sequence = EventMapSequence.Pre;
                    else if (procSeq.Equals("OVER", StringComparison.OrdinalIgnoreCase))
                        sequence = EventMapSequence.Replace;
                    else // "POST" or any other value
                        sequence = EventMapSequence.Post;

                    string cref = row["PORTAL_OBJNAME"].ToString() ?? string.Empty;

                    string component = string.Empty;
                    string segment = string.Empty;

                    if (eventMapInfo.Type == EventMapType.Page)
                    {
                        component = row["PORTAL_URI_SEG2"].ToString() ?? string.Empty;
                        segment = row["PORTAL_URI_SEG3"].ToString() ?? string.Empty;
                    }
                    else
                    {
                        component = eventMapInfo.Component ?? string.Empty;
                        segment = eventMapInfo.Segment ?? string.Empty;
                    }

                    var packagePath = row["QUALIFYPATH"].ToString() ?? string.Empty;
                    if (packagePath == ":")
                    {
                        packagePath = string.Empty;
                    }

                    results.Add(new EventMapItem
                    {
                        ContentReference = cref,
                        Sequence = sequence,
                        SeqNumber = Convert.ToInt32(row["SEQNUM"]),
                        Component = component,
                        Segment = segment,
                        PackageRoot = row["PACKAGEROOT"].ToString() ?? string.Empty,
                        PackagePath = row["QUALIFYPATH"].ToString() ?? string.Empty,
                        ClassName = row["APPCLASSID"].ToString() ?? string.Empty
                    });
                }
            }
            catch (Exception)
            {
                // Consider logging the exception here
                // Logger.LogError($"Error retrieving event map items: {ex.Message}");
            }

            return results;
        }

        public List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            var results = new List<OpenTarget>();

            try
            {
                // Build the unified UNION ALL query based on enabled types
                string unifiedQuery = BuildUnifiedQuery(options.EnabledTypes);

                if (string.IsNullOrEmpty(unifiedQuery))
                {
                    return results; // No enabled types
                }

                // Escape wildcards in search terms for LIKE queries
                string escapedIdTerm = options.IDSearchTerm.Replace("_", "\\_").Replace("%", "\\%");
                string escapedDescriptionTerm = options.DescriptionSearchTerm.Replace("_", "\\_").Replace("%", "\\%");

                var parameters = new Dictionary<string, object>
                {
                    ["id_search"] = escapedIdTerm,
                    ["descr_search"] = escapedDescriptionTerm,
                    ["max_rows_per_type"] = options.MaxRowsPerType,
                    ["sort_by_date"] = options.SortByDate ? "Y" : "N"
                };

                DataTable data = _connection.ExecuteQuery(unifiedQuery, parameters);

                foreach (DataRow row in data.Rows)
                {
                    string defnType = row["DEFN_TYPE"]?.ToString() ?? string.Empty;
                    string id = row["ID"]?.ToString() ?? string.Empty;
                    string description = row["DESCR"]?.ToString() ?? string.Empty;

                    // Map definition type string to OpenTargetType enum
                    if (IDataManager.TryMapStringToTargetType(defnType, out OpenTargetType targetType))
                    {
                        // Map to appropriate PSCLASSID and create object pairs
                        var objectPairs = IDataManager.CreateObjectPairs(targetType, id, description);

                        results.Add(new OpenTarget(
                            targetType,
                            id,
                            description,
                            objectPairs));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error retrieving open targets: {ex.Message}");
            }

            return results;
        }

        private string BuildUnifiedQuery(HashSet<OpenTargetType> enabledTypes)
        {
            var unionClauses = new List<string>();

            foreach (var type in enabledTypes)
            {
                string clause = GetUnionClauseForType(type);
                if (!string.IsNullOrEmpty(clause))
                {
                    unionClauses.Add(clause);
                }
            }

            if (unionClauses.Count == 0)
            {
                return string.Empty;
            }

            // Build the complete query with CTE and ranking
            var query = $@"
WITH U AS (
{string.Join("\n\nUNION ALL\n\n", unionClauses)}
)
SELECT DEFN_TYPE, ID, DESCR, LASTUPDDTTM
FROM (
  SELECT U.*,
         ROW_NUMBER() OVER (
           PARTITION BY DEFN_TYPE
           ORDER BY CASE
                      WHEN (LEN(@id_search) > 0 AND CHARINDEX(UPPER(@id_search), UPPER(ID)) > 0) AND 
                           (LEN(@descr_search) > 0 AND CHARINDEX(UPPER(@descr_search), UPPER(DESCR)) > 0)
                      THEN CASE WHEN CHARINDEX(UPPER(@id_search), UPPER(ID)) <= CHARINDEX(UPPER(@descr_search), UPPER(DESCR)) 
                               THEN CHARINDEX(UPPER(@id_search), UPPER(ID)) 
                               ELSE CHARINDEX(UPPER(@descr_search), UPPER(DESCR)) END
                      WHEN LEN(@id_search) > 0 AND CHARINDEX(UPPER(@id_search), UPPER(ID)) > 0 
                      THEN CHARINDEX(UPPER(@id_search), UPPER(ID))
                      WHEN LEN(@descr_search) > 0 AND CHARINDEX(UPPER(@descr_search), UPPER(DESCR)) > 0 
                      THEN CHARINDEX(UPPER(@descr_search), UPPER(DESCR))
                      ELSE 999999
                    END,
                    ID
         ) AS RN
  FROM U
)
WHERE RN <= @max_rows_per_type
ORDER BY DEFN_TYPE ASC, ID ASC, CASE WHEN @sort_by_date = 'Y' THEN LASTUPDDTTM END DESC";

            return query;
        }

        private string GetUnionClauseForType(OpenTargetType type)
        {
            return type switch
            {
                OpenTargetType.Activity => @"
  SELECT 'Activity' AS DEFN_TYPE, ACTIVITYNAME AS ID, DESCR60 AS DESCR, LASTUPDDTTM
  FROM   PSACTIVITYDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(ACTIVITYNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR60) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.AppEngineProgram => @"
  SELECT 'App Engine Program' AS DEFN_TYPE, AE_APPLID AS ID, CAST(DESCR AS NVARCHAR(254)) AS DESCR, LASTUPDDTTM
  FROM PSAEAPPLDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(AE_APPLID) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.ApplicationPackage => @"
  SELECT 'Application Package' AS DEFN_TYPE, 
         PACKAGEID AS ID, 
         CAST(DESCRLONG AS NVARCHAR(254)) AS DESCR, LASTUPDDTTM
  FROM   PSPACKAGEDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(PACKAGEID) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(CAST(DESCRLONG AS NVARCHAR(254))) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.ApplicationClass => @"
  SELECT 'Application Class' AS DEFN_TYPE, 
         PACKAGEROOT + CASE WHEN QUALIFYPATH = ':' THEN '' ELSE ':' + QUALIFYPATH END + ':' + APPCLASSID AS ID, 
         DESCR AS DESCR, LASTUPDDTTM
  FROM PSAPPCLASSDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(APPCLASSID) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.ApprovalRuleSet => @"
  SELECT 'Approval Rule Set' AS DEFN_TYPE, APPR_RULE_SET AS ID, DESCR60 AS DESCR, LASTUPDDTTM
  FROM PS_APPR_RULE_HDR
  WHERE  (LEN(@id_search) = 0 OR UPPER(APPR_RULE_SET) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR60) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.BusinessInterlink => @"
  SELECT 'Business Interlink' AS DEFN_TYPE, IONAME AS ID, IODESCR AS DESCR, LASTUPDDTTM
  FROM PSIODEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(IONAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(IODESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.BusinessProcess => @"
  SELECT 'Business Process' AS DEFN_TYPE, BUSPROCNAME AS ID, DESCR60 AS DESCR, LASTUPDDTTM
  FROM   PSBUSPROCDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(BUSPROCNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR60) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Component => @"
  SELECT 'Component' AS DEFN_TYPE, PNLGRPNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSPNLGRPDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(PNLGRPNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.ComponentInterface => @"
  SELECT 'Component Interface' AS DEFN_TYPE, BCNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSBCDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(BCNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Field => @"
  SELECT 'Field' AS DEFN_TYPE, FIELDNAME AS ID, CAST(DESCRLONG AS NVARCHAR(254)) AS DESCR, LASTUPDDTTM
  FROM   PSDBFIELD
  WHERE  (LEN(@id_search) = 0 OR UPPER(FIELDNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCRLONG) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.FileLayout => @"
  SELECT 'File Layout' AS DEFN_TYPE, FLDDEFNNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSFLDDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(FLDDEFNNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.FileReference => @"
  SELECT 'File Reference' AS DEFN_TYPE, FILEREFNAME AS ID, CAST(DESCRLONG AS NVARCHAR(254)) AS DESCR, LASTUPDDTTM
  FROM PSFILEREDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(FILEREFNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(CAST(DESCRLONG AS NVARCHAR(254))) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.HTML => @"
  SELECT 'HTML' AS DEFN_TYPE, CONTNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSCONTDEFN
  WHERE  CONTTYPE = 4
     AND (LEN(@id_search) = 0 OR UPPER(CONTNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Image => @"
  SELECT 'Image' AS DEFN_TYPE, CONTNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSCONTDEFN
  WHERE  CONTTYPE = 1
     AND (LEN(@id_search) = 0 OR UPPER(CONTNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Menu => @"
  SELECT 'Menu' AS DEFN_TYPE, MENUNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSMENUDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(MENUNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Message => @"
  SELECT 'Message' AS DEFN_TYPE, MSGNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSMSGDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(MSGNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Page => @"
  SELECT 'Page' AS DEFN_TYPE, PNLNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSPNLDEFN
  WHERE  PNLTYPE <> 7
     AND (LEN(@id_search) = 0 OR UPPER(PNLNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.PageFluid => @"
  SELECT 'Page (Fluid)' AS DEFN_TYPE, PNLNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSPNLDEFN
  WHERE  PNLTYPE = 7
     AND (LEN(@id_search) = 0 OR UPPER(PNLNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Project => @"
  SELECT 'Project' AS DEFN_TYPE, PROJECTNAME AS ID, PROJECTDESCR AS DESCR, LASTUPDDTTM
  FROM   PSPROJECTDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(PROJECTNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(PROJECTDESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.Record => @"
  SELECT 'Record' AS DEFN_TYPE, RECNAME AS ID, RECDESCR AS DESCR, LASTUPDDTTM
  FROM   PSRECDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(RECNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(RECDESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.SQL => @"
  SELECT 'SQL' AS DEFN_TYPE, SQLID AS ID, ' ' AS DESCR, LASTUPDDTTM
  FROM   PSSQLDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(SQLID) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR ' ' LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.StyleSheet => @"
  SELECT 'Style Sheet' AS DEFN_TYPE, CONTNAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSCONTDEFN
  WHERE  CONTTYPE = 9
     AND (LEN(@id_search) = 0 OR UPPER(CONTNAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.AnalyticModel => @"
  SELECT 'Analytic Model' AS DEFN_TYPE, ACEMODELID AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSACEMDLDEFN
  WHERE  (LEN(@id_search) = 0 OR UPPER(ACEMODELID) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.AnalyticType => @"
  SELECT 'Analytic Type' AS DEFN_TYPE, PROBTYPE AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSOPTPRBTYPE
  WHERE  (LEN(@id_search) = 0 OR UPPER(PROBTYPE) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.OptimizationModel => @"
  SELECT 'Optimization Model' AS DEFN_TYPE, PAMS_MODEL_NAME AS ID, DESCR AS DESCR, LASTUPDDTTM
  FROM   PSOPTMODEL
  WHERE  (LEN(@id_search) = 0 OR UPPER(PAMS_MODEL_NAME) LIKE UPPER(@id_search + '%') ESCAPE '\')
     AND (LEN(@descr_search) = 0 OR UPPER(DESCR) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
     AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)",

                OpenTargetType.NonClassPeopleCode => NON_CLASS_PPC_QUERY,
                _ => string.Empty
            };
        }

        /// <summary>
        /// Gets programs from PSPCMPROG that may contain function definitions
        /// </summary>
        /// <param name="queryFilter">Optional query filter to limit which programs are returned</param>
        /// <returns>List of OpenTarget objects representing programs that may contain function definitions</returns>
        public List<OpenTarget> GetFunctionDefiningPrograms()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            var results = new List<OpenTarget>();

            try
            {
                // Base query to find programs in PSPCMPROG
                // TODO: Add specific filtering logic based on queryFilter parameter
                string sql = @"
                    SELECT DISTINCT 
                        OBJECTID1, OBJECTVALUE1,
                        OBJECTID2, OBJECTVALUE2, 
                        OBJECTID3, OBJECTVALUE3,
                        OBJECTID4, OBJECTVALUE4,
                        OBJECTID5, OBJECTVALUE5,
                        OBJECTID6, OBJECTVALUE6,
                        OBJECTID7, OBJECTVALUE7
                    FROM  PSPCMPROG
                    WHERE OBJECTID1 = 1
                    AND OBJECTID2 = 2
                    AND OBJECTID3 = 12 AND OBJECTVALUE3 = 'FieldFormula'";

                using var command = _connection.CreateCommand();
                command.CommandText = sql;

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var objectPairs = new List<(PSCLASSID ObjectID, string ObjectValue)>();

                    // Read all 7 object ID/value pairs
                    for (int i = 0; i < 7; i++)
                    {
                        int objIdIndex = i * 2;
                        int objValueIndex = objIdIndex + 1;

                        if (!reader.IsDBNull(objIdIndex) && !reader.IsDBNull(objValueIndex))
                        {
                            var objectId = (PSCLASSID)reader.GetInt32(objIdIndex);
                            var objectValue = reader.GetString(objValueIndex);

                            if (objectId != PSCLASSID.NONE && !string.IsNullOrEmpty(objectValue))
                            {
                                objectPairs.Add((objectId, objectValue));
                            }
                        }
                    }

                    if (objectPairs.Count > 0)
                    {
                        // Create OpenTarget - for programs we'll use a generic name and description
                        var name = BuildProgramName(objectPairs);
                        results.Add(new OpenTarget(
                            OpenTargetType.UNKNOWN, // Programs don't have a specific OpenTargetType
                            name,
                            "PeopleCode Program",
                            objectPairs
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting function defining programs: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Builds a descriptive name for a program based on its object pairs
        /// </summary>
        /// <param name="objectPairs">The object ID/value pairs</param>
        /// <returns>A descriptive name for the program</returns>
        private static string BuildProgramName(List<(PSCLASSID ObjectID, string ObjectValue)> objectPairs)
        {
            // TODO: Enhance this method to create meaningful program names based on object types
            var values = objectPairs.Where(o => o.ObjectID != PSCLASSID.NONE).Select(p => p.ObjectValue).Where(v => !string.IsNullOrEmpty(v));
            return string.Join(".", values);
        }

        /// <summary>
        /// Gets the PeopleCode program text for a given OpenTarget
        /// </summary>
        /// <param name="openTarget">The OpenTarget representing the program</param>
        /// <returns>The program text as a string, or null if not found</returns>
        public string? GetPeopleCodeProgram(OpenTarget openTarget)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Database connection is not open");
            }

            try
            {
                // Create PeopleCodeItem from OpenTarget
                var item = new PeopleCodeItem(
                    [.. openTarget.ObjectIDs.Select(a => (int)a)],
                    [.. openTarget.ObjectValues.Select(a => a ?? " ")],
                    Array.Empty<byte>(),
                    new List<NameReference>()
                );

                // Load the program content
                if (LoadPeopleCodeItemContent(item))
                {
                    return item.GetProgramTextAsString();
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting PeopleCode program: {ex.Message}");
            }

            return null;
        }

        public string GetToolsVersion()
        {
            string sql = @"SELECT TOOLSREL || '.' || PTPATCHREL FROM PSSTATUS";

            try
            {
                DataTable result = _connection.ExecuteQuery(sql);

                if (result.Rows.Count == 0)
                {
                    return "99.99.99";
                }

                return result.Rows[0][0].ToString()!;
            }
            catch (Exception)
            {
                // Handle database errors
                return "99.99.99";
            }
        }

        private const string NON_CLASS_PPC_QUERY = @"-- Page PeopleCode
SELECT 'Page PeopleCode' AS DEFN_TYPE, 
       OBJECTVALUE1 AS ID,
       OBJECTVALUE1 + '.Activate' AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 9 AND OBJECTID2 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.Activate') LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Component PeopleCode
SELECT 'Component PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Component Record PeopleCode
SELECT 'Component Record PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 1 AND OBJECTID4 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Component Record Field PeopleCode
SELECT 'Component Rec Field PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4 + '.' + OBJECTVALUE5 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 10 AND OBJECTID2 = 39 AND OBJECTID3 = 1 AND OBJECTID4 = 2 AND OBJECTID5 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4 + '.' + OBJECTVALUE5) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Record Field PeopleCode
SELECT 'Record Field PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 1 AND OBJECTID2 = 2 AND OBJECTID3 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Menu PeopleCode
SELECT 'Menu PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 3 AND OBJECTID2 = 4 AND OBJECTID3 = 5 AND OBJECTID4 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + OBJECTVALUE4) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- App Engine PeopleCode
SELECT 'App Engine PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE6 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + 
       OBJECTVALUE4 + '.' + OBJECTVALUE5 + '.' + OBJECTVALUE6 + '.' + OBJECTVALUE7 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 66 AND OBJECTID2 = 77 AND OBJECTID3 = 39 AND OBJECTID4 = 20 
      AND OBJECTID5 = 21 AND OBJECTID6 = 78 AND OBJECTID7 = 12
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE6) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2 + '.' + OBJECTVALUE3 + '.' + 
      OBJECTVALUE4 + '.' + OBJECTVALUE5 + '.' + OBJECTVALUE6 + '.' + OBJECTVALUE7) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Component Interface PeopleCode
SELECT 'Component Interface PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 AS ID,
       OBJECTVALUE1 + '.' + OBJECTVALUE2 AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE OBJECTID1 = 74
  AND (LEN(@id_search) = 0 OR UPPER(OBJECTVALUE1) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR UPPER(OBJECTVALUE1 + '.' + OBJECTVALUE2) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)

UNION ALL

-- Message/Subscription PeopleCode
SELECT 'Message PeopleCode' AS DEFN_TYPE,
       OBJECTVALUE1 + 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' + OBJECTVALUE2 ELSE '' END AS ID,
       OBJECTVALUE1 + 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' + OBJECTVALUE2 ELSE '' END +
       CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' 
            THEN '.' + OBJECTVALUE3 ELSE '' END AS DESCR,
       LASTUPDDTTM
FROM PSPCMPROG 
WHERE (OBJECTID1 = 60 OR OBJECTID1 = 87)
  AND (LEN(@id_search) = 0 OR 
       UPPER(OBJECTVALUE1 + CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' + OBJECTVALUE2 ELSE '' END) LIKE UPPER(@id_search + '%') ESCAPE '\')
  AND (LEN(@descr_search) = 0 OR 
       UPPER(OBJECTVALUE1 + 
       CASE WHEN OBJECTVALUE2 IS NOT NULL AND OBJECTVALUE2 != ' ' 
            THEN '.' + OBJECTVALUE2 ELSE '' END +
       CASE WHEN OBJECTVALUE3 IS NOT NULL AND OBJECTVALUE3 != ' ' 
            THEN '.' + OBJECTVALUE3 ELSE '' END) LIKE UPPER('%' + @descr_search + '%') ESCAPE '\')
  AND (LEN(@id_search) > 0 OR LEN(@descr_search) > 0)";

        public List<(PSCLASSID[] ObjectIds, string[] ObjectValues)> GetProgramObjectIds(string[] objectValues)
        {
            if (!IsConnected)
            {
                return new List<(PSCLASSID[] ObjectIds, string[] ObjectValues)>();
            }

            // Pad array to 7 elements with spaces if needed
            string[] paddedValues = new string[7];
            for (int i = 0; i < 7; i++)
            {
                paddedValues[i] = i < objectValues.Length && !string.IsNullOrEmpty(objectValues[i])
                    ? objectValues[i]
                    : " ";
            }

            string sql = @"
                SELECT OBJECTID1, OBJECTID2, OBJECTID3, OBJECTID4, OBJECTID5, OBJECTID6, OBJECTID7,
                       OBJECTVALUE1, OBJECTVALUE2, OBJECTVALUE3, OBJECTVALUE4, OBJECTVALUE5, OBJECTVALUE6, OBJECTVALUE7
                FROM PSPCMPROG 
                WHERE OBJECTVALUE1 = @val1 
                  AND OBJECTVALUE2 = @val2 
                  AND OBJECTVALUE3 = @val3 
                  AND OBJECTVALUE4 = @val4 
                  AND OBJECTVALUE5 = @val5 
                  AND OBJECTVALUE6 = @val6 
                  AND OBJECTVALUE7 = @val7";

            var results = new List<(PSCLASSID[] ObjectIds, string[] ObjectValues)>();

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    ["val1"] = paddedValues[0],
                    ["val2"] = paddedValues[1],
                    ["val3"] = paddedValues[2],
                    ["val4"] = paddedValues[3],
                    ["val5"] = paddedValues[4],
                    ["val6"] = paddedValues[5],
                    ["val7"] = paddedValues[6]
                };

                DataTable result = _connection.ExecuteQuery(sql, parameters);

                foreach (DataRow row in result.Rows)
                {
                    var objectIds = new PSCLASSID[7];
                    var objectVals = new string[7];

                    for (int i = 0; i < 7; i++)
                    {
                        objectIds[i] = (PSCLASSID)Convert.ToInt32(row[$"OBJECTID{i + 1}"]);
                        objectVals[i] = row[$"OBJECTVALUE{i + 1}"]?.ToString() ?? " ";
                    }

                    results.Add((objectIds, objectVals));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error querying PSPCMPROG for object IDs: {ex.Message}");
            }

            return results;
        }

        public PeopleCodeType GetFieldType(string fieldName)
        {
            string sql = @"
                SELECT FIELDTYPE FROM PSDBFIELD WHERE FIELDNAME = @fieldName";

            var results = new List<(PSCLASSID[] ObjectIds, string[] ObjectValues)>();

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    ["fieldName"] = fieldName
                };

                DataTable result = _connection.ExecuteQuery(sql, parameters);
                if (result.Rows.Count == 0)
                {
                    return PeopleCodeType.Unknown;
                }

                var fieldType = (int)result.Rows[0][0];

                return fieldType switch
                {
                    0 => PeopleCodeType.String,
                    1 => PeopleCodeType.String,
                    2 => PeopleCodeType.Number,
                    3 => PeopleCodeType.Number,
                    4 => PeopleCodeType.Date,
                    5 => PeopleCodeType.Time,
                    6 => PeopleCodeType.DateTime,
                    8 => PeopleCodeType.Any,
                    9 => PeopleCodeType.Any,
                    _ => PeopleCodeType.Unknown,
                };

            }
            catch (Exception ex)
            {
                Debug.Log($"Error querying PSDBFIELD for field type: {ex.Message}");
            }

            return PeopleCodeType.Unknown;
        }

        /// <summary>
        /// Gets all package paths for a given application class name using SQL Server
        /// </summary>
        public List<string> GetPackagesForClass(string className)
        {
            var results = new List<string>();

            if (!IsConnected)
            {
                Debug.Log("Cannot query packages - database not connected");
                return results;
            }

            try
            {
                // Query returns PACKAGEROOT and QUALIFYPATH
                // Need to construct full path as PACKAGEROOT:QUALIFYPATH:CLASSNAME
                // Special case: when QUALIFYPATH = ':', path is just PACKAGEROOT:CLASSNAME

                string sql = @"
                    SELECT
                        A.PACKAGEROOT,
                        A.QUALIFYPATH
                    FROM
                        PSAPPCLASSDEFN A
                        LEFT OUTER JOIN PSPACKAGEDEFN B ON B.PACKAGEROOT = A.PACKAGEROOT
                                                           AND B.QUALIFYPATH = CASE
                                                                               WHEN A.QUALIFYPATH = ':' THEN '.'
                                                                               ELSE A.QUALIFYPATH
                                                                               END
                    WHERE
                        A.APPCLASSID = @className
                    ORDER BY
                        CASE WHEN B.LASTUPDDTTM IS NULL THEN 1 ELSE 0 END,
                        CAST(B.LASTUPDDTTM AS DATE) DESC,
                        A.PACKAGEROOT ASC
                ";

                var parameters = new Dictionary<string, object>
                {
                    ["className"] = className
                };

                DataTable result = _connection.ExecuteQuery(sql, parameters);

                foreach (DataRow row in result.Rows)
                {
                    string packageRoot = row[0].ToString() ?? string.Empty;
                    string qualifyPath = row[1].ToString() ?? string.Empty;

                    if (string.IsNullOrEmpty(packageRoot))
                        continue;

                    // Build full package path
                    string fullPath;
                    if (qualifyPath == ":")
                    {
                        // No subpackage - just PACKAGEROOT:CLASSNAME
                        fullPath = $"{packageRoot}:{className}";
                    }
                    else
                    {
                        // Has subpackage - PACKAGEROOT:QUALIFYPATH:CLASSNAME
                        fullPath = $"{packageRoot}:{qualifyPath}:{className}";
                    }

                    results.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error querying packages for class {className}: {ex.Message}");
            }

            return results;
        }

        public List<string> GetAllClassesForPackage(string packagePath)
        {
            string sql = @"SELECT APPCLASSID FROM PSAPPCLASSDEFN WHERE PACKAGEROOT = @packageRoot AND QUALIFYPATH = @qualifyPath";
            var parts = packagePath.Split(':');
            if (parts.Length == 0)
            {
                return new();
            }

            var packageRoot = parts[0];

            var qualifyPath = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : ":";

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    ["packageRoot"] = packageRoot,
                    ["qualifyPath"] = qualifyPath
                };
                DataTable result = _connection.ExecuteQuery(sql, parameters);
                var classNames = new List<string>();
                foreach (DataRow row in result.Rows)
                {
                    classNames.Add(row["APPCLASSID"].ToString()!);
                }
                return classNames;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error querying PSAPPCLASSDEFN for classes: {ex.Message}");
                return new();
            }
        }
    }
}