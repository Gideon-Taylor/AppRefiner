using AppRefiner.Database;
using AppRefiner.Database.Models;
using Microsoft.Data.Sqlite;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AppRefiner
{
    /// <summary>
    /// Represents a cached function definition
    /// </summary>
    public class FunctionCacheItem
    {
        public int Id { get; set; }
        public string DBName { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionPath { get; set; } = string.Empty;
        public List<string>? ParameterNames { get; set; }
        public List<string>? ParameterTypes { get; set; }
        public string? ReturnType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Represents a function search result
    /// </summary>
    public class FunctionSearchResult
    {
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionPath { get; set; } = string.Empty;
        public List<string>? ParameterNames { get; set; }
        public List<string>? ParameterTypes { get; set; }
        public string? ReturnType { get; set; }

        public FunctionSearchResult(string functionName, string functionPath, List<string>? parameterNames = null, List<string>? parameterTypes = null, string? returnType = null)
        {
            FunctionName = functionName;
            FunctionPath = functionPath;
            ParameterNames = parameterNames;
            ParameterTypes = parameterTypes;
            ReturnType = returnType;
        }

        public string ToDeclaration()
        {
            var parts = FunctionPath.Split(':');

            return $"Declare Function {FunctionName} PeopleCode {parts[0]}.{parts[1]} {parts[2]};";
        }

        public string GetExampleCall()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(ReturnType))
            {
                sb.Append($"Local {ReturnType} &funcResult = ");
            }

            sb.Append($"{FunctionName}({string.Join(", ", ParameterNames ?? [])});");

            return sb.ToString();
        }

    }

    /// <summary>
    /// Manages a local cache of available function definitions from PeopleSoft databases
    /// </summary>
    public class FunctionCacheManager : IDisposable
    {
        private readonly string _databasePath;
        private readonly string _connectionString;
        public delegate void CacheProgressHandler(int processed, int total);
        public event CacheProgressHandler? OnCacheProgressUpdate;
        private SqliteConnection connection;
        private bool _disposed = false;
        /* create delegate here for "report progress" which gets passed in current function count, and total function count */
        
        /// <summary>
        /// Initializes a new instance of the FunctionCacheManager class
        /// </summary>
        /// <param name="databasePath">The path where the SQLite database file should be stored</param>
        /// <param name="settingsService">The settings service for configuration</param>
        public FunctionCacheManager(string databasePath)
        {
            _databasePath = databasePath;

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(databasePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }


            _connectionString = $"Data Source={databasePath}";
             connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Initialize the database
            InitializeDatabase();
        }

        /// <summary>
        /// Creates and initializes a FunctionCacheManager using the path stored in application settings
        /// </summary>
        /// <returns>An initialized FunctionCacheManager object or null if initialization fails</returns>
        public static FunctionCacheManager? CreateFromSettings()
        {
            try
            {
                // Get function cache database path from settings or use default
                string? dbPath = Properties.Settings.Default.FunctionCacheDatabasePath;
                if (string.IsNullOrEmpty(dbPath))
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    dbPath = Path.Combine(appDataPath, "AppRefiner", "function_cache.db");

                    // Save the default path to settings
                    Properties.Settings.Default.FunctionCacheDatabasePath = dbPath;
                    Properties.Settings.Default.Save();
                }

                // Create and return a new FunctionCacheManager
                return new FunctionCacheManager(dbPath);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error creating FunctionCacheManager from settings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initializes the SQLite database and creates necessary tables if they don't exist
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                // Create function cache table if it doesn't exist
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS FunctionCache (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DBName TEXT NOT NULL,
                        FunctionName TEXT NOT NULL,
                        FunctionPath TEXT NOT NULL,
                        ParameterNames TEXT, 
                        ParameterTypes TEXT,
                        ReturnType TEXT,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_functioncache_dbname ON FunctionCache(DBName);
                    CREATE INDEX IF NOT EXISTS idx_functioncache_name ON FunctionCache(FunctionName);
                    CREATE INDEX IF NOT EXISTS idx_functioncache_path ON FunctionCache(FunctionPath);
                ";

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error initializing function cache database: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the function cache by retrieving and parsing programs from the PeopleSoft database
        /// </summary>
        /// <param name="appDesignerProcess">The AppDesignerProcess providing database context and data manager</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        public bool UpdateFunctionCache(AppDesignerProcess appDesignerProcess)
        {
            if (appDesignerProcess?.DataManager == null || !appDesignerProcess.DataManager.IsConnected)
            {
                Debug.Log("Cannot update function cache: Data manager is not connected");
                return false;
            }

            try
            {
                Debug.Log($"Starting function cache update for database: {appDesignerProcess.DBName}");

                // Get list of programs that may contain function definitions
                var programs = appDesignerProcess.DataManager.GetFunctionDefiningPrograms();
                OnCacheProgressUpdate?.Invoke(0, programs.Count);
                Debug.Log($"Found {programs.Count} programs to process");

                int processedCount = 0;
                int functionCount = 0;

                // Start transaction for atomic update
                using var transaction = connection.BeginTransaction();
                try
                {
                    // Delete definitions for this DB (within transaction)
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = @"
                        DELETE FROM FunctionCache
                        WHERE DBName = @DBName
                    ";
                    deleteCommand.Parameters.AddWithValue("@DBName", appDesignerProcess.DBName);
                    int deletedCount = deleteCommand.ExecuteNonQuery();
                    Debug.Log($"Deleted {deletedCount} existing functions for {appDesignerProcess.DBName}");

                    // Process programs and insert functions (within transaction)
                    foreach (var program in programs)
                    {
                        try
                        {
                            var functions = ExtractFunctionsFromProgram(program, appDesignerProcess);

                            foreach (var function in functions)
                            {
                                if (AddFunctionToCache(function, transaction))
                                {
                                    functionCount++;
                                }
                            }

                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"Error processing program {program.Name}: {ex.Message}");
                        }
                        OnCacheProgressUpdate?.Invoke(processedCount, programs.Count);
                    }

                    // Commit all operations together
                    transaction.Commit();
                    Debug.Log($"Function cache update completed for {appDesignerProcess.DBName}. Processed {processedCount} programs, found {functionCount} functions");
                    return true;
                }
                catch (Exception ex)
                {
                    // Rollback transaction on error
                    transaction.Rollback();
                    Debug.Log($"Error during function cache update, transaction rolled back: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error updating function cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Searches the function cache for functions matching the specified search term
        /// </summary>
        /// <param name="appDesignerProcess">The AppDesignerProcess providing database context</param>
        /// <param name="searchTerm">The search term to match against function names (case-insensitive)</param>
        /// <param name="maxResults">Maximum number of results to return (default: 100, use int.MaxValue for unlimited)</param>
        /// <returns>List of FunctionSearchResult objects matching the search criteria</returns>
        public List<FunctionSearchResult> SearchFunctionCache(AppDesignerProcess appDesignerProcess, string searchTerm, int maxResults = 100)
        {
            var results = new List<FunctionSearchResult>();

            if (string.IsNullOrEmpty(searchTerm))
            {
                return results;
            }

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                
                if (appDesignerProcess == null && string.IsNullOrEmpty(appDesignerProcess.DBName))
                {
                    return results;
                }
                
                // Build SQL query with optional LIMIT clause
                string limitClause = maxResults < int.MaxValue ? $" LIMIT {maxResults}" : "";

                command.CommandText = @"
                    SELECT FunctionName, FunctionPath, ParameterNames, ParameterTypes, ReturnType
                    FROM FunctionCache
                    WHERE DBName = @DBName
                    AND (FunctionName LIKE @SearchTerm COLLATE NOCASE
                            OR FunctionPath LIKE @ProgramPrefix COLLATE NOCASE
                        )
                    ORDER BY FunctionName" + limitClause;

                command.Parameters.AddWithValue("@DBName", appDesignerProcess.DBName);
                command.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");
                command.Parameters.AddWithValue("@ProgramPrefix", $"{searchTerm}%");
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string functionName = reader.GetString(0); // FunctionName
                    string functionPath = reader.GetString(1); // FunctionPath
                    string? parameterNamesJson = reader.IsDBNull(2) ? null : reader.GetString(2); // ParameterNames
                    string? parameterTypesJson = reader.IsDBNull(3) ? null : reader.GetString(3); // ParameterTypes
                    string? returnType = reader.IsDBNull(4) ? null : reader.GetString(4); // ReturnType
                    
                    List<string>? parameterNames = DeserializeStringList(parameterNamesJson);
                    List<string>? parameterTypes = DeserializeStringList(parameterTypesJson);
                    
                    results.Add(new FunctionSearchResult(
                        functionName, 
                        functionPath,
                        parameterNames,
                        parameterTypes,
                        returnType
                    ));
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error searching function cache: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Gets the total count of functions matching the specified search term
        /// </summary>
        /// <param name="appDesignerProcess">The AppDesignerProcess providing database context</param>
        /// <param name="searchTerm">The search term to match against function names (case-insensitive)</param>
        /// <returns>Total count of matching functions</returns>
        public int GetSearchResultCount(AppDesignerProcess appDesignerProcess, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return 0;
            }

            try
            {
                using var command = connection.CreateCommand();

                if (appDesignerProcess == null && string.IsNullOrEmpty(appDesignerProcess.DBName))
                {
                    return 0;
                }

                command.CommandText = @"
                    SELECT COUNT(*)
                    FROM FunctionCache
                    WHERE DBName = @DBName
                    AND (FunctionName LIKE @SearchTerm COLLATE NOCASE
                            OR FunctionPath LIKE @ProgramPrefix COLLATE NOCASE
                        )
                ";

                command.Parameters.AddWithValue("@DBName", appDesignerProcess.DBName);
                command.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");
                command.Parameters.AddWithValue("@ProgramPrefix", $"{searchTerm}%");

                var count = command.ExecuteScalar();
                return Convert.ToInt32(count);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting search result count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Extracts function definitions from a program
        /// </summary>
        /// <param name="programTarget">The OpenTarget representing the program to parse</param>
        /// <param name="appDesignerProcess">The AppDesignerProcess providing database context and data manager</param>
        /// <returns>List of FunctionCacheItem objects representing functions found in the program</returns>
        private List<FunctionCacheItem> ExtractFunctionsFromProgram(OpenTarget programTarget, AppDesignerProcess appDesignerProcess)
        {
            var functions = new List<FunctionCacheItem>();
            if (appDesignerProcess.DataManager is null) return functions;
            try
            {
                PeopleCodeItem item = new(
                    [.. programTarget.ObjectIDs.Select(a => (int)a)],
                    [.. programTarget.ObjectValues.Select(a => a ?? " ")],
                    Array.Empty<byte>(),
                    new List<NameReference>()
                );

                // Load the program text using the AppDesignerProcess's DataManager
                if (appDesignerProcess.DataManager.LoadPeopleCodeItemContent(item))
                {
                    PeopleCodeLexer lexer = new PeopleCodeLexer(item.GetProgramTextAsString());
                    var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(lexer.TokenizeAll());

                    var program = parser.ParseProgram();
                    if (parser.Errors.Count > 0)
                    {
                        Debugger.Break();
                    }

                    foreach (var function in program.Functions.Where(f => f.IsImplementation))
                    {
                        var returnTypeString = function.ReturnType?.TypeName ?? "";
                        var newFunc = new FunctionCacheItem
                        {
                            DBName = appDesignerProcess.DBName,
                            FunctionName = function.Name,
                            FunctionPath = programTarget.Path,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            ReturnType = function.ReturnType?.TypeName ?? "",
                            ParameterNames = [.. function.Parameters.Select(p => p.Name)],
                            ParameterTypes = [.. function.Parameters.Select(p => p.Type.TypeName)]
                        };

                        functions.Add(newFunc);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error extracting functions from program {programTarget.Name}: {ex.Message}");
            }

            return functions;
        }

        /// <summary>
        /// Adds a function to the cache database
        /// </summary>
        /// <param name="function">The function to add to the cache</param>
        /// <param name="transaction">Optional transaction to use for the operation. If null, uses the class-level connection.</param>
        /// <returns>True if the function was added successfully, false otherwise</returns>
        private bool AddFunctionToCache(FunctionCacheItem function, SqliteTransaction? transaction = null)
        {
            try
            {
                // Use transaction's connection if provided, otherwise use class-level connection
                var conn = transaction?.Connection ?? connection;

                // Insert new function
                using var insertCommand = conn.CreateCommand();
                if (transaction != null)
                {
                    insertCommand.Transaction = transaction;
                }

                insertCommand.CommandText = @"
                    INSERT INTO FunctionCache (DBName, FunctionName, FunctionPath, ParameterNames, ParameterTypes, ReturnType, CreatedAt, UpdatedAt)
                    VALUES (@DBName, @FunctionName, @FunctionPath, @ParameterNames, @ParameterTypes, @ReturnType, @CreatedAt, @UpdatedAt)
                ";

                insertCommand.Parameters.AddWithValue("@DBName", function.DBName);
                insertCommand.Parameters.AddWithValue("@FunctionName", function.FunctionName);
                insertCommand.Parameters.AddWithValue("@FunctionPath", function.FunctionPath);
                insertCommand.Parameters.AddWithValue("@ParameterNames", SerializeStringList(function.ParameterNames));
                insertCommand.Parameters.AddWithValue("@ParameterTypes", SerializeStringList(function.ParameterTypes));
                insertCommand.Parameters.AddWithValue("@ReturnType", function.ReturnType as object ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insertCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                return insertCommand.ExecuteNonQuery() > 0;

            }
            catch (Exception ex)
            {
                Debug.Log($"Error adding function to cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Serializes a list of strings to JSON for database storage
        /// </summary>
        /// <param name="list">The list to serialize</param>
        /// <returns>JSON string representation or null if list is null/empty</returns>
        private static string SerializeStringList(List<string>? list)
        {
            if (list == null || list.Count == 0)
                return "";

            return JsonSerializer.Serialize(list);
        }

        /// <summary>
        /// Deserializes a JSON string to a list of strings
        /// </summary>
        /// <param name="json">The JSON string to deserialize</param>
        /// <returns>List of strings or null if JSON is null/empty</returns>
        private static List<string>? DeserializeStringList(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clears all cached functions for the specified database
        /// </summary>
        /// <param name="dbName">The database name to clear cache for</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public bool ClearCacheForDatabase(string dbName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM FunctionCache WHERE DBName = @DBName";
                command.Parameters.AddWithValue("@DBName", dbName);

                int deletedCount = command.ExecuteNonQuery();
                Debug.Log($"Cleared {deletedCount} cached functions for database {dbName}");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error clearing cache for database {dbName}: {ex.Message}");
                return false;
            }
        }

        public struct CacheStatistics
        {
            public int TotalFunctions;
            public Dictionary<string, int> FunctionsByDatabase;
            public string LastUpdateTime;
            public string? Error;
        }

        /// <summary>
        /// Gets statistics about the function cache
        /// </summary>
        /// <returns>Dictionary containing cache statistics</returns>
        public CacheStatistics GetCacheStatistics()
        {
            var stats = new CacheStatistics();

            try
            {

                // Total function count
                using var totalCommand = connection.CreateCommand();
                totalCommand.CommandText = "SELECT COUNT(*) FROM FunctionCache";
                stats.TotalFunctions = Convert.ToInt32(totalCommand.ExecuteScalar());

                // Functions per database
                using var dbCommand = connection.CreateCommand();
                dbCommand.CommandText = "SELECT DBName, COUNT(*) as Count FROM FunctionCache GROUP BY DBName";
                var dbStats = new Dictionary<string, int>();
                
                using var reader = dbCommand.ExecuteReader();
                while (reader.Read())
                {
                    dbStats[reader.GetString(0)] = reader.GetInt32(1); // DBName, Count
                }
                stats.FunctionsByDatabase = dbStats;

                // Last update time (most recent UpdatedAt)
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "SELECT MAX(UpdatedAt) FROM FunctionCache";
                var lastUpdate = updateCommand.ExecuteScalar();
                stats.LastUpdateTime = lastUpdate?.ToString() ?? "Never";
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting cache statistics: {ex.Message}");
                stats.Error = ex.Message;
            }

            return stats;
        }

        /// <summary>
        /// Removes all cached functions for a specific program path.
        /// Used when updating the cache for a single program without requiring database access.
        /// </summary>
        /// <param name="dbName">The database name</param>
        /// <param name="functionPath">The function path (program identifier)</param>
        /// <returns>The number of functions removed, or -1 if an error occurred</returns>
        public int RemoveFunctionsByPath(string dbName, string functionPath)
        {
            try
            {

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM FunctionCache
                    WHERE DBName = @DBName AND FunctionPath = @FunctionPath
                ";
                command.Parameters.AddWithValue("@DBName", dbName);
                command.Parameters.AddWithValue("@FunctionPath", functionPath);

                int deletedCount = command.ExecuteNonQuery();
                Debug.Log($"Removed {deletedCount} cached function(s) for path: {functionPath}");

                return deletedCount;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error removing functions by path: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Adds a single function to the cache.
        /// Public wrapper for the private AddFunctionToCache method.
        /// </summary>
        /// <param name="function">The function to add to the cache</param>
        /// <returns>True if the function was added successfully, false otherwise</returns>
        public bool AddFunction(FunctionCacheItem function)
        {
            return AddFunctionToCache(function);
        }

        /// <summary>
        /// Extracts the function path from an editor's caption.
        /// Currently only supports Record Field PeopleCode.
        /// </summary>
        /// <param name="editor">The ScintillaEditor</param>
        /// <returns>The function path, or null if it cannot be determined</returns>
        private string? ExtractFunctionPathFromEditor(ScintillaEditor editor)
        {
            if (editor.Caption == null)
            {
                return null;
            }

            // Record Field PeopleCode: "RECORD.FIELD.EVENT (Record PeopleCode)"
            if (editor.Caption.Contains("(Record PeopleCode)"))
            {
                var parts = editor.Caption.Replace(" (Record PeopleCode)", "").Split('.');
                if (parts.Length >= 3)
                {
                    // Keep dot-separated format (matches database format)
                    return $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }

            // Future: Add support for other program types here
            // Application Package: editor.ClassPath (already colon-separated)
            // Component: editor.EventMapInfo (need to build path)

            return null;
        }

        /// <summary>
        /// Extracts function definitions from a parsed ProgramNode.
        /// Only includes function implementations (not declarations).
        /// </summary>
        /// <param name="program">The parsed ProgramNode from the AST</param>
        /// <param name="dbName">The database name</param>
        /// <param name="functionPath">The function path identifier</param>
        /// <returns>List of FunctionCacheItem objects</returns>
        private List<FunctionCacheItem> ExtractFunctionsFromParsedProgram(
            ProgramNode program,
            string dbName,
            string functionPath)
        {
            var functions = new List<FunctionCacheItem>();

            // Extract functions (only implementations, not declarations)
            foreach (var function in program.Functions.Where(f => f.IsImplementation))
            {
                var newFunc = new FunctionCacheItem
                {
                    DBName = dbName,
                    FunctionName = function.Name,
                    FunctionPath = functionPath,
                    ReturnType = function.ReturnType?.TypeName ?? "",
                    ParameterNames = [.. function.Parameters.Select(p => p.Name)],
                    ParameterTypes = [.. function.Parameters.Select(p => p.Type.TypeName)],
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                functions.Add(newFunc);
            }

            return functions;
        }

        /// <summary>
        /// Updates the function cache for a specific editor after a save.
        /// Only processes Record Field PeopleCode programs where functions can be called from other programs.
        /// This method fails silently (logs only) if the program path cannot be determined or other issues occur.
        /// </summary>
        /// <param name="editor">The ScintillaEditor that was saved</param>
        public void UpdateCacheForEditor(ScintillaEditor editor)
        {
            // Check editor type
            if (editor.Type != EditorType.PeopleCode)
            {
                Debug.Log("UpdateCacheForEditor: Skipping non-PeopleCode editor");
                return;
            }

            // Check for database name
            var dbName = editor.AppDesignerProcess?.DBName;
            if (string.IsNullOrEmpty(dbName))
            {
                Debug.Log("UpdateCacheForEditor: Cannot determine database name, skipping cache update");
                return;
            }

            // Extract function path
            var functionPath = ExtractFunctionPathFromEditor(editor);
            if (string.IsNullOrEmpty(functionPath))
            {
                Debug.Log($"UpdateCacheForEditor: Cannot determine function path for {editor.Caption}, skipping cache update");
                return;
            }

            // Get parsed program
            var program = editor.GetParsedProgram(forceReparse: false);
            if (program == null)
            {
                Debug.Log($"UpdateCacheForEditor: Cannot parse program for {editor.Caption}, skipping cache update");
                return;
            }

            // Extract functions from AST
            var functions = ExtractFunctionsFromParsedProgram(program, dbName, functionPath);
            Debug.Log($"UpdateCacheForEditor: Found {functions.Count} function(s) in {functionPath}");

            try
            {
                // Start transaction for atomic update
                using var transaction = connection.BeginTransaction();
                try
                {
                    // Remove old entries (within transaction)
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = @"
                        DELETE FROM FunctionCache
                        WHERE DBName = @DBName AND FunctionPath = @FunctionPath
                    ";
                    deleteCommand.Parameters.AddWithValue("@DBName", dbName);
                    deleteCommand.Parameters.AddWithValue("@FunctionPath", functionPath);
                    int removedCount = deleteCommand.ExecuteNonQuery();

                    // Add new entries (within transaction)
                    int addedCount = 0;
                    foreach (var function in functions)
                    {
                        if (AddFunctionToCache(function, transaction))
                        {
                            addedCount++;
                        }
                    }

                    // Commit all operations together
                    transaction.Commit();
                    Debug.Log($"UpdateCacheForEditor: Updated cache for {functionPath} - Removed {removedCount}, Added {addedCount}");
                }
                catch (Exception ex)
                {
                    // Rollback transaction on error
                    transaction.Rollback();
                    Debug.Log($"UpdateCacheForEditor: Error updating cache (transaction rolled back): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"UpdateCacheForEditor: Error updating cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the FunctionCacheManager and releases database resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                connection?.Dispose();
                _disposed = true;
            }
        }
    }
}