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
        private const string PendingMessage = "SqlProxyDataManager: pending Task 8+";
        private const int MultiChunkMaxRows = 10000;

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

        // --- Remaining IDataManager members: stubs for later tasks ---

        public List<RecordFieldInfo>? GetRecordFields(string recordName)
            => throw new NotImplementedException(PendingMessage);

        public PackageItems GetAppPackageItems(string packagePath)
            => throw new NotImplementedException(PendingMessage);

        public List<EventMapItem> GetEventMapItems(EventMapInfo eventMapInfo)
            => throw new NotImplementedException(PendingMessage);

        public List<EventMapInfo> GetEventMapXrefs(string classPath)
            => throw new NotImplementedException(PendingMessage);

        public List<OpenTarget> GetOpenTargets(OpenTargetSearchOptions options)
            => throw new NotImplementedException(PendingMessage);

        public List<string> GetAllClassesForPackage(string packagePath)
            => throw new NotImplementedException(PendingMessage);

        public PeopleCodeType GetFieldType(string fieldName)
            => throw new NotImplementedException(PendingMessage);

        public List<string> GetPackagesForClass(string className)
            => throw new NotImplementedException(PendingMessage);

        public List<MessageSetInfo> GetMessageSets()
            => throw new NotImplementedException(PendingMessage);

        public List<MessageCatalogEntry> GetMessagesForSet(int setNumber)
            => throw new NotImplementedException(PendingMessage);

        public MessageCatalogEntry? GetMessageCatalogEntry(int setNumber, int messageNumber)
            => throw new NotImplementedException(PendingMessage);

        public List<MessageCatalogEntry> SearchMessageCatalog(string searchTerm, IReadOnlyCollection<int>? setNumbers, int limit)
            => throw new NotImplementedException(PendingMessage);
    }
}
