using AppRefiner.Database.Models;
using AppRefiner.Database.SqlProxy;
using AppRefiner.Database.SqlProxyQueries;
using PeopleCodeTypeInfo.Types;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace AppRefiner.Database
{
    /// <summary>
    /// IDataManager over App Designer SAM via static Meta-SQL and <see cref="SqlProxyDbConnection"/>.
    /// </summary>
    public sealed class SqlProxyDataManager : IDataManager
    {
        private const int MultiChunkMaxRows = 10000;

        /// <summary>
        /// PeopleCode flavors covered by OpenTargetType.NonClassPeopleCode in Oracle's combined query.
        /// </summary>
        private static readonly OpenTargetType[] NonClassPeopleCodeFlavors =
        {
            OpenTargetType.PagePeopleCode,
            OpenTargetType.ComponentPeopleCode,
            OpenTargetType.ComponentRecordPeopleCode,
            OpenTargetType.ComponentRecFieldPeopleCode,
            OpenTargetType.RecordFieldPeopleCode,
            OpenTargetType.MenuPeopleCode,
            OpenTargetType.AppEnginePeopleCode,
            OpenTargetType.ComponentInterfacePeopleCode,
            OpenTargetType.MessagePeopleCode,
        };

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

        /// <summary>
        /// Builds 14 ordered binds alternating I(objId), S(objVal) for 7 program field pairs.
        /// </summary>
        private static List<SqlProxyBind> BuildProgramFieldBinds(
            IReadOnlyList<Tuple<int, string>> programFields,
            string defaultObjVal = " ")
        {
            var binds = new List<SqlProxyBind>(14);
            for (int i = 0; i < 7; i++)
            {
                int objId = i < programFields.Count ? programFields[i].Item1 : 0;
                string objVal = i < programFields.Count ? programFields[i].Item2 : defaultObjVal;
                binds.Add(I(objId));
                binds.Add(S(objVal));
            }
            return binds;
        }

        public string GetToolsVersion()
        {
            DataTable result = Query(SqlProxyQueryCatalog.GetToolsVersion);
            if (result.Rows.Count == 0)
            {
                return string.Empty;
            }
            return result.Rows[0][0]?.ToString() ?? string.Empty;
        }

        public string GetSqlDefinition(string objectName)
        {
            DataTable result = Query(
                SqlProxyQueryCatalog.GetSqlDefinition,
                new[] { S(objectName) },
                MultiChunkMaxRows);

            if (result.Rows.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sqlDef = new();
            foreach (DataRow row in result.Rows)
            {
                sqlDef.Append(row["SQLTEXT"]);
            }

            return sqlDef.ToString();
        }

        public Dictionary<string, string> GetAllSqlDefinitions()
        {
            Dictionary<string, string> definitions = new();

            DataTable namesResult = Query(SqlProxyQueryCatalog.GetAllSqlDefinitionNames);

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

        public HtmlDefinition GetHtmlDefinition(string objectName)
        {
            DataTable result = Query(
                SqlProxyQueryCatalog.GetHtmlDefinition,
                new[] { S(objectName) },
                MultiChunkMaxRows);

            if (result.Rows.Count == 0)
            {
                return new HtmlDefinition(string.Empty, 0);
            }

            StringBuilder htmlContent = new();
            foreach (DataRow row in result.Rows)
            {
                if (row["CONTDATA"] is byte[] blob)
                {
                    htmlContent.Append(Encoding.Unicode.GetString(blob));
                }
            }

            string content = htmlContent.ToString();

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

        public Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions()
        {
            Dictionary<string, HtmlDefinition> definitions = new();

            DataTable namesResult = Query(SqlProxyQueryCatalog.GetAllHtmlDefinitionNames);

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

        public List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName)
        {
            List<PeopleCodeItem> results = new();

            DataTable projectItems = Query(
                SqlProxyQueryCatalog.GetPeopleCodeItemMetadataForProject,
                new[] { S(projectName) });

            foreach (DataRow row in projectItems.Rows)
            {
                int objectType = Convert.ToInt32(row["OBJECTTYPE"]);

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
                    List<Tuple<int, string>> programFields = projectItem.ToProgramFields();

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
                        Array.Empty<byte>(),
                        new List<NameReference>()
                    );

                    results.Add(peopleCodeItem);
                }
            }

            return results;
        }

        public bool LoadPeopleCodeItemContent(PeopleCodeItem item)
        {
            List<Tuple<int, string>> programFields = new();
            for (int i = 0; i < item.ObjectIDs.Length; i++)
            {
                programFields.Add(new Tuple<int, string>(item.ObjectIDs[i], item.ObjectValues[i]));
            }

            List<SqlProxyBind> progBinds = BuildProgramFieldBinds(programFields, defaultObjVal: " ");

            DataTable programData = Query(
                SqlProxyQueryCatalog.LoadPeopleCodeItemContent,
                progBinds,
                MultiChunkMaxRows);

            List<byte[]> progTextParts = new();
            foreach (DataRow progRow in programData.Rows)
            {
                if (progRow["PROGTXT"] is byte[] blobData)
                {
                    progTextParts.Add(blobData);
                }
            }

            byte[] combinedProgramText = CombineBinaryData(progTextParts);
            if (combinedProgramText.Length == 0)
            {
                return false;
            }

            List<NameReference> nameReferences = GetNameReferencesForProgram(programFields);

            item.SetProgramText(combinedProgramText);
            item.SetNameReferences(nameReferences);

            return true;
        }

        public List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName)
        {
            List<PeopleCodeItem> results = GetPeopleCodeItemMetadataForProject(projectName);

            foreach (var item in results)
            {
                LoadPeopleCodeItemContent(item);
            }

            return results;
        }

        private static byte[] CombineBinaryData(List<byte[]> byteArrays)
        {
            if (byteArrays == null || byteArrays.Count == 0)
                return Array.Empty<byte>();

            int totalLength = 0;
            foreach (byte[] array in byteArrays)
            {
                totalLength += array.Length;
            }

            byte[] result = new byte[totalLength];
            int offset = 0;

            foreach (byte[] array in byteArrays)
            {
                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }

            return result;
        }

        private List<NameReference> GetNameReferencesForProgram(List<Tuple<int, string>> programFields)
        {
            List<NameReference> nameReferences = new();

            List<SqlProxyBind> binds = BuildProgramFieldBinds(programFields, defaultObjVal: "");

            DataTable nameData = Query(
                SqlProxyQueryCatalog.GetNameReferencesForProgram,
                binds);

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
            string[] parts = appClassPath.Split(':');

            if (parts.Length < 2)
            {
                return false;
            }

            var binds = new List<SqlProxyBind>(14);

            for (int i = 0; i < 7; i++)
            {
                int objId = 0;
                string objVal = " ";

                if (i < parts.Length)
                {
                    if (i == parts.Length - 1)
                    {
                        objId = 107;
                        objVal = parts[i].ToUpper();
                    }
                    else
                    {
                        objId = 104 + i;
                        objVal = parts[i].ToUpper();
                    }
                }
                else if (i == parts.Length)
                {
                    objId = 12;
                    objVal = "ONEXECUTE";
                }

                binds.Add(I(objId));
                binds.Add(S(objVal));
            }

            DataTable result = Query(SqlProxyQueryCatalog.CheckAppClassExists, binds);

            if (result.Rows.Count > 0 && result.Rows[0][0] is string exists)
            {
                return exists == "Y";
            }

            // SAM may return non-string column types; accept any first-cell "Y"
            if (result.Rows.Count > 0)
            {
                return string.Equals(result.Rows[0][0]?.ToString(), "Y", StringComparison.Ordinal);
            }

            return false;
        }

        public string? GetAppClassSourceByPath(string appClassPath)
        {
            if (!CheckAppClassExists(appClassPath))
            {
                return null;
            }

            string[] parts = appClassPath.Split(':');

            List<Tuple<int, string>> programFields = new();

            for (int i = 0; i < parts.Length; i++)
            {
                int objId;
                string objVal = parts[i];

                if (i == parts.Length - 1)
                {
                    objId = 107;
                }
                else
                {
                    objId = 104 + i;
                }

                programFields.Add(new Tuple<int, string>(objId, objVal));
            }

            programFields.Add(new Tuple<int, string>(12, "OnExecute"));

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

            PeopleCodeItem item = new(
                objectIDs,
                objectValues,
                Array.Empty<byte>(),
                new List<NameReference>()
            );

            if (LoadPeopleCodeItemContent(item))
            {
                return item.GetProgramTextAsString();
            }

            return null;
        }

        public List<OpenTarget> GetFunctionDefiningPrograms()
        {
            var results = new List<OpenTarget>();

            try
            {
                DataTable result = Query(SqlProxyQueryCatalog.GetFunctionDefiningPrograms);

                foreach (DataRow row in result.Rows)
                {
                    var objectPairs = new List<(PSCLASSID ObjectID, string ObjectValue)>();

                    for (int i = 0; i < 7; i++)
                    {
                        int objIdIndex = i * 2;
                        int objValueIndex = objIdIndex + 1;

                        if (!row.IsNull(objIdIndex) && !row.IsNull(objValueIndex))
                        {
                            var objectId = (PSCLASSID)Convert.ToInt32(row[objIdIndex]);
                            var objectValue = row[objValueIndex].ToString() ?? string.Empty;

                            if (objectId != PSCLASSID.NONE && !string.IsNullOrEmpty(objectValue))
                            {
                                objectPairs.Add((objectId, objectValue));
                            }
                        }
                    }

                    if (objectPairs.Count > 0)
                    {
                        var name = BuildProgramName(objectPairs);
                        results.Add(new OpenTarget(
                            OpenTargetType.UNKNOWN,
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

        private static string BuildProgramName(List<(PSCLASSID ObjectID, string ObjectValue)> objectPairs)
        {
            var values = objectPairs.Where(o => o.ObjectID != PSCLASSID.NONE).Select(p => p.ObjectValue).Where(v => !string.IsNullOrEmpty(v));
            return string.Join(".", values);
        }

        public string? GetPeopleCodeProgram(OpenTarget openTarget)
        {
            try
            {
                var item = new PeopleCodeItem(
                    [.. openTarget.ObjectIDs.Select(a => (int)a)],
                    [.. openTarget.ObjectValues.Select(a => a ?? " ")],
                    Array.Empty<byte>(),
                    new List<NameReference>()
                );

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

        public List<(PSCLASSID[] ObjectIds, string[] ObjectValues)> GetProgramObjectIds(string[] objectValues)
        {
            if (!IsConnected)
            {
                return new List<(PSCLASSID[] ObjectIds, string[] ObjectValues)>();
            }

            string[] paddedValues = new string[7];
            for (int i = 0; i < 7; i++)
            {
                paddedValues[i] = i < objectValues.Length && !string.IsNullOrEmpty(objectValues[i])
                    ? objectValues[i]
                    : " ";
            }

            var results = new List<(PSCLASSID[] ObjectIds, string[] ObjectValues)>();

            try
            {
                var binds = new List<SqlProxyBind>(7)
                {
                    S(paddedValues[0]),
                    S(paddedValues[1]),
                    S(paddedValues[2]),
                    S(paddedValues[3]),
                    S(paddedValues[4]),
                    S(paddedValues[5]),
                    S(paddedValues[6]),
                };

                DataTable result = Query(SqlProxyQueryCatalog.GetProgramObjectIds, binds);

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

        public List<RecordFieldInfo>? GetRecordFields(string recordName)
        {
            EnsureConnected();

            recordName = recordName.ToUpper();

            int recordVersion = GetRecordVersion(recordName);
            if (recordVersion < 0)
            {
                return null;
            }

            if (recordFieldCache.TryGetValue(recordName, out RecordFieldCache? cache))
            {
                if (cache.Version == recordVersion)
                {
                    return cache.Fields;
                }
                recordFieldCache.Remove(recordName);
            }

            List<RecordFieldInfo>? fields = FetchRecordFieldsFromDb(recordName);
            if (fields != null && fields.Count > 0)
            {
                recordFieldCache[recordName] = new RecordFieldCache(recordName, recordVersion, fields);
            }

            return fields;
        }

        /// <summary>
        /// Gets the VERSION field value from PSRECDEFN for a specific record.
        /// </summary>
        /// <returns>Version number or -1 if record doesn't exist</returns>
        private int GetRecordVersion(string recordName)
        {
            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetRecordFieldsRecExists,
                    new[] { S(recordName) },
                    maxRows: 1);

                if (result.Rows.Count == 0)
                {
                    return -1;
                }

                return Convert.ToInt32(result.Rows[0]["VERSION"]);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// Fetches record field information directly from the database.
        /// </summary>
        private List<RecordFieldInfo>? FetchRecordFieldsFromDb(string recordName)
        {
            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetRecordFields,
                    new[] { S(recordName) });

                if (result.Rows.Count == 0)
                {
                    return null;
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
            catch (Exception)
            {
                return null;
            }
        }

        public PackageItems GetAppPackageItems(string packagePath)
        {
            EnsureConnected();

            List<string> subpackages = new();
            List<string> classes = new();

            string[] parts = packagePath.Split(':');
            string rootPackage = parts.Length > 0 ? parts[0] : string.Empty;

            int packageLevel = parts.Length - 1;
            if (packageLevel < 0) packageLevel = 0;

            string qualifyPath = ":";
            if (packageLevel > 0)
            {
                qualifyPath = string.Join(":", parts.Skip(1));
            }

            // Only root and first subpackage level can have subpackages (max depth 3).
            if (packageLevel < 2)
            {
                if (packageLevel == 0)
                {
                    DataTable subpackageResults = Query(
                        SqlProxyQueryCatalog.GetAppPackageItemsSubpackagesRoot,
                        new[] { S(rootPackage) });

                    foreach (DataRow row in subpackageResults.Rows)
                    {
                        string? subpackageName = row["PACKAGEID"].ToString();
                        if (!string.IsNullOrEmpty(subpackageName))
                        {
                            subpackages.Add(subpackageName);
                        }
                    }
                }
                else if (packageLevel == 1)
                {
                    DataTable subpackageResults = Query(
                        SqlProxyQueryCatalog.GetAppPackageItemsSubpackagesNested,
                        new[] { S(rootPackage), S(qualifyPath) });

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

            DataTable classResults = Query(
                SqlProxyQueryCatalog.GetAppPackageItemsClasses,
                new[] { S(rootPackage), S(qualifyPath) });

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

        public List<string> GetAllClassesForPackage(string packagePath)
        {
            var parts = packagePath.Split(':');
            if (parts.Length == 0)
            {
                return new();
            }

            var packageRoot = parts[0];
            var qualifyPath = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : ":";

            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetAllClassesForPackage,
                    new[] { S(packageRoot), S(qualifyPath) });

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
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetPackagesForClass,
                    new[] { S(className) });

                foreach (DataRow row in result.Rows)
                {
                    string packageRoot = row[0].ToString() ?? string.Empty;
                    string qualifyPath = row[1].ToString() ?? string.Empty;

                    if (string.IsNullOrEmpty(packageRoot))
                        continue;

                    string fullPath;
                    if (qualifyPath == ":")
                    {
                        fullPath = $"{packageRoot}:{className}";
                    }
                    else
                    {
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

        public PeopleCodeType GetFieldType(string fieldName)
        {
            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetFieldType,
                    new[] { S(fieldName) },
                    maxRows: 1);

                if (result.Rows.Count == 0)
                {
                    return PeopleCodeType.Unknown;
                }

                int fieldType = Convert.ToInt32(result.Rows[0][0]);

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

        public List<MessageSetInfo> GetMessageSets()
        {
            EnsureConnected();

            List<MessageSetInfo> sets = new();
            try
            {
                DataTable result = Query(SqlProxyQueryCatalog.GetMessageSets);
                foreach (DataRow row in result.Rows)
                {
                    sets.Add(new MessageSetInfo(
                        Convert.ToInt32(row["MESSAGE_SET_NBR"]),
                        row["DESCR"].ToString() ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessageSets failed: {ex.Message}");
            }
            return sets;
        }

        public List<MessageCatalogEntry> GetMessagesForSet(int setNumber)
        {
            EnsureConnected();

            List<MessageCatalogEntry> messages = new();
            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetMessagesForSet,
                    new[] { I(setNumber) });

                foreach (DataRow row in result.Rows)
                {
                    messages.Add(new MessageCatalogEntry(
                        setNumber,
                        Convert.ToInt32(row["MESSAGE_NBR"]),
                        row["MSG_SEVERITY"].ToString() ?? string.Empty,
                        row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                        row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessagesForSet({setNumber}) failed: {ex.Message}");
            }
            return messages;
        }

        public MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber)
        {
            EnsureConnected();

            try
            {
                DataTable result = Query(
                    SqlProxyQueryCatalog.GetMessageCatalogEntry,
                    new[] { I(setNumber), I(messageNumber) },
                    maxRows: 1);

                if (result.Rows.Count == 0)
                {
                    return null;
                }

                DataRow row = result.Rows[0];
                return new MessageCatalogEntry(
                    setNumber,
                    messageNumber,
                    row["MSG_SEVERITY"].ToString() ?? string.Empty,
                    row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                    row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.Log($"GetMessageCatalogEntry({setNumber}, {messageNumber}) failed: {ex.Message}");
                return null;
            }
        }

        public List<MessageCatalogEntry> SearchMessageCatalog(
            string searchTerm, IReadOnlyCollection<int>? setNumbers, int limit)
        {
            EnsureConnected();

            // Static SQL only — no BuildMessageSetFilter / dynamic IN injection.
            // :1 and :2 bind the same uppercased %term% pattern (SAM positional).
            string pattern = $"%{searchTerm.ToUpperInvariant()}%";
            List<MessageCatalogEntry> messages = new();

            try
            {
                if (setNumbers == null || setNumbers.Count == 0)
                {
                    DataTable result = Query(
                        SqlProxyQueryCatalog.SearchMessageCatalog,
                        new[] { S(pattern), S(pattern) },
                        maxRows: limit);
                    AppendMessageCatalogRows(messages, result, limit);
                }
                else
                {
                    // One static SQL per set (MESSAGE_SET_NBR = :3); merge until limit.
                    foreach (int setNumber in setNumbers)
                    {
                        if (messages.Count >= limit)
                            break;

                        int remaining = limit - messages.Count;
                        DataTable result = Query(
                            SqlProxyQueryCatalog.SearchMessageCatalogInSet,
                            new[] { S(pattern), S(pattern), I(setNumber) },
                            maxRows: remaining);
                        AppendMessageCatalogRows(messages, result, limit);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"SearchMessageCatalog failed: {ex.Message}");
            }
            return messages;
        }

        private static void AppendMessageCatalogRows(
            List<MessageCatalogEntry> messages, DataTable result, int limit)
        {
            foreach (DataRow row in result.Rows)
            {
                if (messages.Count >= limit)
                    break;

                messages.Add(new MessageCatalogEntry(
                    Convert.ToInt32(row["MESSAGE_SET_NBR"]),
                    Convert.ToInt32(row["MESSAGE_NBR"]),
                    row["MSG_SEVERITY"].ToString() ?? string.Empty,
                    row["MESSAGE_TEXT"].ToString() ?? string.Empty,
                    row["DESCRLONG"] == DBNull.Value ? string.Empty : row["DESCRLONG"].ToString() ?? string.Empty));
            }
        }

        public List<EventMapInfo> GetEventMapXrefs(string classPath)
        {
            EnsureConnected();

            var parts = classPath.Split(':');
            var root = parts[0];
            var className = parts[parts.Length - 1];
            var path = string.Join(":", parts.Skip(1).SkipLast(1));
            if (path == "")
            {
                path = ":";
            }

            // :1 root, :2 path, :3 class (SqlProxyQueryCatalog.GetEventMapXrefs)
            DataTable result = Query(
                SqlProxyQueryCatalog.GetEventMapXrefs,
                new[] { S(root), S(path), S(className) });

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
                info.ComponentEvent = row["ptcs_cmpevent"].ToString()!.Trim() ?? string.Empty;
                info.ComponentRecordEvent = row["ptcs_cmprecevent"].ToString()!.Trim() ?? string.Empty;
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
            EnsureConnected();

            string sql;
            List<SqlProxyBind> binds;
            List<EventMapItem> results = new();

            switch (eventMapInfo.Type)
            {
                case EventMapType.Component:
                    sql = SqlProxyQueryCatalog.GetEventMapItemsComponent;
                    binds = new List<SqlProxyBind>
                    {
                        S(eventMapInfo.Component),
                        S(eventMapInfo.Segment),
                        S(eventMapInfo.ComponentEvent),
                    };
                    break;

                case EventMapType.ComponentRecord:
                    sql = SqlProxyQueryCatalog.GetEventMapItemsComponentRecord;
                    binds = new List<SqlProxyBind>
                    {
                        S(eventMapInfo.Component),
                        S(eventMapInfo.Segment),
                        S(eventMapInfo.Record),
                        S(eventMapInfo.ComponentRecordEvent),
                    };
                    break;

                case EventMapType.ComponentRecordField:
                    sql = SqlProxyQueryCatalog.GetEventMapItemsComponentRecordField;
                    binds = new List<SqlProxyBind>
                    {
                        S(eventMapInfo.Component),
                        S(eventMapInfo.Segment),
                        S(eventMapInfo.Record),
                        S(eventMapInfo.Field),
                        S(eventMapInfo.ComponentRecordEvent),
                    };
                    break;

                case EventMapType.Page:
                    sql = SqlProxyQueryCatalog.GetEventMapItemsPage;
                    binds = new List<SqlProxyBind>
                    {
                        S(eventMapInfo.Page),
                        S(eventMapInfo.ComponentRecordEvent),
                    };
                    break;

                default:
                    return results;
            }

            try
            {
                DataTable result = Query(sql, binds);

                foreach (DataRow row in result.Rows)
                {
                    EventMapSequence sequence;
                    string procSeq = row["ptcs_procseq"].ToString() ?? string.Empty;

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

                    results.Add(new EventMapItem
                    {
                        ContentReference = cref,
                        Sequence = sequence,
                        SeqNumber = Convert.ToInt32(row["SEQNUM"]),
                        Component = component,
                        Segment = segment,
                        PackageRoot = row["packageroot"].ToString() ?? string.Empty,
                        PackagePath = row["qualifypath"].ToString() ?? string.Empty,
                        ClassName = row["appclassid"].ToString() ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"GetEventMapItems failed: {ex.Message}");
            }

            return results;
        }

        public List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options)
        {
            EnsureConnected();

            var results = new List<OpenTarget>();

            string idRaw = options.IDSearchTerm?.Trim() ?? string.Empty;
            string descrRaw = options.DescriptionSearchTerm?.Trim() ?? string.Empty;

            // Oracle required at least one non-empty search term (LENGTH > 0 guards).
            if (idRaw.Length == 0 && descrRaw.Length == 0)
            {
                return results;
            }

            bool idEmpty = idRaw.Length == 0;
            bool descrEmpty = descrRaw.Length == 0;

            string escapedId = EscapeLike(idRaw);
            string escapedDescr = EscapeLike(descrRaw);

            // Default ID pattern: prefix-style term% (ApplicationClass uses contains — built per type).
            string idPrefixPattern = idEmpty ? "%" : escapedId + "%";
            string idContainsPattern = idEmpty ? "%" : "%" + escapedId + "%";
            string descrPattern = descrEmpty ? "%" : "%" + escapedDescr + "%";

            string idEmptyFlag = idEmpty ? "Y" : "N";
            string descrEmptyFlag = descrEmpty ? "Y" : "N";

            int maxPerType = options.MaxRowsPerType > 0 ? options.MaxRowsPerType : 50;

            // Expand NonClassPeopleCode parent flag to concrete flavors; dedupe with any explicit flavors.
            var typesToQuery = new HashSet<OpenTargetType>();
            foreach (OpenTargetType type in options.EnabledTypes)
            {
                if (type == OpenTargetType.NonClassPeopleCode)
                {
                    foreach (OpenTargetType flavor in NonClassPeopleCodeFlavors)
                    {
                        typesToQuery.Add(flavor);
                    }
                }
                else if (type != OpenTargetType.UNKNOWN)
                {
                    typesToQuery.Add(type);
                }
            }

            try
            {
                foreach (OpenTargetType type in typesToQuery)
                {
                    string? sql = SqlProxyOpenTargetQueries.ForType(type);
                    if (sql == null)
                    {
                        continue;
                    }

                    // ApplicationClass: contains on ID; all others: prefix on ID (per meta docs).
                    string idPattern = type == OpenTargetType.ApplicationClass
                        ? idContainsPattern
                        : idPrefixPattern;

                    SqlProxyBind[] binds =
                    {
                        S(idEmptyFlag),
                        S(idPattern),
                        S(descrEmptyFlag),
                        S(descrPattern),
                    };

                    DataTable data = Query(sql, binds, maxRows: maxPerType);

                    foreach (DataRow row in data.Rows)
                    {
                        string defnType = row["DEFN_TYPE"]?.ToString() ?? string.Empty;
                        string id = row["ID"]?.ToString() ?? string.Empty;
                        string description = row["DESCR"]?.ToString() ?? string.Empty;

                        if (IDataManager.TryMapStringToTargetType(defnType, out OpenTargetType targetType))
                        {
                            var objectPairs = IDataManager.CreateObjectPairs(targetType, id, description);

                            var target = new OpenTarget(
                                targetType,
                                id,
                                description,
                                objectPairs);

                            if (targetType is OpenTargetType.NonClassPeopleCode)
                            {
                                target.DisplayTargetType = defnType;
                            }

                            results.Add(target);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error retrieving open targets: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Escapes LIKE wildcards the same way Oracle GetOpenTargets did (underscore only).
        /// </summary>
        private static string EscapeLike(string value)
            => value.Replace("_", "\\_");
    }
}
