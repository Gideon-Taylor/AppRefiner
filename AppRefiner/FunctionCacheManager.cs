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
    public class FunctionCacheManager
    {
        private readonly string _databasePath;
        private readonly string _connectionString;
        public delegate void CacheProgressHandler(int processed, int total);
        public event CacheProgressHandler? OnCacheProgressUpdate;

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
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

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

                foreach (var program in programs)
                {
                    try
                    {
                        var functions = ExtractFunctionsFromProgram(program, appDesignerProcess);
                        
                        foreach (var function in functions)
                        {
                            if (AddFunctionToCache(function))
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

                Debug.Log($"Function cache update completed for {appDesignerProcess.DBName}. Processed {processedCount} programs, found {functionCount} functions");
                return true;
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
        /// <returns>List of FunctionSearchResult objects matching the search criteria</returns>
        public List<FunctionSearchResult> SearchFunctionCache(AppDesignerProcess appDesignerProcess, string searchTerm)
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
                
                command.CommandText = @"
                    SELECT FunctionName, FunctionPath, ParameterNames, ParameterTypes, ReturnType
                    FROM FunctionCache 
                    WHERE DBName = @DBName 
                    AND (FunctionName LIKE @SearchTerm COLLATE NOCASE
                            OR FunctionPath LIKE @ProgramPrefix COLLATE NOCASE
                        )
                    ORDER BY FunctionName
                ";
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

                    foreach (var function in program.Functions)
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
        /// <returns>True if the function was added successfully, false otherwise</returns>
        private bool AddFunctionToCache(FunctionCacheItem function)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Check if function already exists
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) FROM FunctionCache 
                    WHERE DBName = @DBName AND FunctionPath = @FunctionPath AND FunctionName = @FunctionName
                ";
                checkCommand.Parameters.AddWithValue("@DBName", function.DBName);
                checkCommand.Parameters.AddWithValue("@FunctionPath", function.FunctionPath);
                checkCommand.Parameters.AddWithValue("@FunctionName", function.FunctionName);

                int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());
                if (existingCount > 0)
                {
                    // Update existing function
                    using var updateCommand = connection.CreateCommand();
                    updateCommand.CommandText = @"
                        UPDATE FunctionCache 
                        SET FunctionName = @FunctionName,
                            ParameterNames = @ParameterNames,
                            ParameterTypes = @ParameterTypes,
                            ReturnType = @ReturnType,
                            UpdatedAt = @UpdatedAt
                        WHERE DBName = @DBName AND FunctionPath = @FunctionPath AND FunctionName = @FunctionName
                    ";
                    
                    updateCommand.Parameters.AddWithValue("@DBName", function.DBName);
                    updateCommand.Parameters.AddWithValue("@FunctionPath", function.FunctionPath);
                    updateCommand.Parameters.AddWithValue("@FunctionName", function.FunctionName);
                    updateCommand.Parameters.AddWithValue("@ParameterNames", SerializeStringList(function.ParameterNames));
                    updateCommand.Parameters.AddWithValue("@ParameterTypes", SerializeStringList(function.ParameterTypes));
                    updateCommand.Parameters.AddWithValue("@ReturnType", function.ReturnType as object ?? DBNull.Value);
                    updateCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    return updateCommand.ExecuteNonQuery() > 0;
                }
                else
                {
                    // Insert new function
                    using var insertCommand = connection.CreateCommand();
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
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

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
    }
}