using AppRefiner.Database.Models;
using Microsoft.Data.Sqlite;

namespace AppRefiner.Snapshots
{
    /// <summary>
    /// Manages code editor file snapshots using a SQLite database
    /// </summary>
    public class SnapshotManager
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the SnapshotManager class
        /// </summary>
        /// <param name="databasePath">The path where the SQLite database file should be stored</param>
        public SnapshotManager(string databasePath)
        {
            _databasePath = databasePath;

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={databasePath}";

            // Initialize the database
            InitializeDatabase();
        }

        /// <summary>
        /// Creates and initializes a SnapshotManager using the path stored in application settings
        /// </summary>
        /// <returns>An initialized SnapshotManager object or null if initialization fails</returns>
        public static SnapshotManager? CreateFromSettings()
        {
            try
            {
                // Get snapshots database path from settings or use default
                string? dbPath = Properties.Settings.Default.SnapshotDatabasePath;
                if (string.IsNullOrEmpty(dbPath))
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    dbPath = Path.Combine(appDataPath, "AppRefiner", "Snapshots.db");

                    // Save the default path to settings
                    Properties.Settings.Default.SnapshotDatabasePath = dbPath;
                    Properties.Settings.Default.Save();
                }

                // Create and return a new SnapshotManager
                return new SnapshotManager(dbPath);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error creating SnapshotManager from settings: {ex.Message}");
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

                // Create snapshots table if it doesn't exist
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Snapshots (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DBName TEXT,
                        FilePath TEXT NOT NULL,
                        Caption TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        Content TEXT NOT NULL
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_snapshots_filepath ON Snapshots(FilePath);
                    CREATE INDEX IF NOT EXISTS idx_snapshots_dbname ON Snapshots(DBName);
                ";

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error initializing snapshot database: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the content of an editor to the database as a snapshot
        /// </summary>
        /// <param name="editor">The editor containing the content</param>
        /// <param name="content">The content to save</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        public bool SaveEditorSnapshot(ScintillaEditor editor, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                Debug.Log("Cannot save snapshot: Content is empty");
                return false;
            }

            try
            {
                string filePath = editor.RelativePath ?? "unknown-file";
                string caption = editor.Caption ?? "Unknown";

                // Remove any type suffix from caption, e.g. " (PeopleCode)"
                if (caption.Contains("("))
                {
                    caption = caption.Substring(0, caption.LastIndexOf("(")).Trim();
                }

                // Create a new snapshot in the database
                return SaveSnapshot(new Snapshot
                {
                    DBName = editor.AppDesignerProcess.DBName,
                    FilePath = filePath,
                    Caption = caption,
                    CreatedAt = DateTime.Now,
                    Content = content
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in SaveEditorSnapshot: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves a snapshot to the database
        /// </summary>
        /// <param name="snapshot">The snapshot to save</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        public bool SaveSnapshot(Snapshot snapshot)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Snapshots (DBName, FilePath, Caption, CreatedAt, Content)
                    VALUES (@DBName, @FilePath, @Caption, @CreatedAt, @Content)
                ";

                command.Parameters.AddWithValue("@DBName", snapshot.DBName as object ?? DBNull.Value);
                command.Parameters.AddWithValue("@FilePath", snapshot.FilePath);
                command.Parameters.AddWithValue("@Caption", snapshot.Caption);
                command.Parameters.AddWithValue("@CreatedAt", snapshot.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Content", snapshot.Content);

                int rowsAffected = command.ExecuteNonQuery();

                // Clean up old snapshots for this file if needed
                CleanupOldSnapshots(snapshot.FilePath, snapshot.DBName);

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error saving snapshot: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleans up old snapshots for a file, keeping only the most recent ones
        /// </summary>
        /// <param name="filePath">The file path to clean up snapshots for</param>
        /// <param name="dbName">The database name associated with the file</param>
        private void CleanupOldSnapshots(string filePath, string? dbName)
        {
            try
            {
                // Get the max snapshots setting
                int maxSnapshots = Properties.Settings.Default.MaxFileSnapshots;

                // Only limit if the setting is valid
                if (maxSnapshots <= 0)
                {
                    return;
                }

                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Count existing snapshots for this file
                using var countCommand = connection.CreateCommand();

                if (dbName != null)
                {
                    countCommand.CommandText = @"
                        SELECT COUNT(*) FROM Snapshots 
                        WHERE FilePath = @FilePath AND DBName = @DBName
                    ";
                    countCommand.Parameters.AddWithValue("@DBName", dbName);
                }
                else
                {
                    countCommand.CommandText = @"
                        SELECT COUNT(*) FROM Snapshots 
                        WHERE FilePath = @FilePath AND DBName IS NULL
                    ";
                }

                countCommand.Parameters.AddWithValue("@FilePath", filePath);

                int count = Convert.ToInt32(countCommand.ExecuteScalar());

                // If we have more snapshots than allowed, delete the oldest ones
                if (count > maxSnapshots)
                {
                    using var deleteCommand = connection.CreateCommand();

                    if (dbName != null)
                    {
                        deleteCommand.CommandText = @"
                            DELETE FROM Snapshots 
                            WHERE Id IN (
                                SELECT Id FROM Snapshots
                                WHERE FilePath = @FilePath AND DBName = @DBName
                                ORDER BY CreatedAt ASC
                                LIMIT @LimitCount
                            )
                        ";
                        deleteCommand.Parameters.AddWithValue("@DBName", dbName);
                    }
                    else
                    {
                        deleteCommand.CommandText = @"
                            DELETE FROM Snapshots 
                            WHERE Id IN (
                                SELECT Id FROM Snapshots
                                WHERE FilePath = @FilePath AND DBName IS NULL
                                ORDER BY CreatedAt ASC
                                LIMIT @LimitCount
                            )
                        ";
                    }

                    deleteCommand.Parameters.AddWithValue("@FilePath", filePath);
                    deleteCommand.Parameters.AddWithValue("@LimitCount", count - maxSnapshots);

                    deleteCommand.ExecuteNonQuery();

                    Debug.Log($"Cleaned up old snapshots for {filePath}, keeping {maxSnapshots} most recent snapshots");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error cleaning up old snapshots: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the snapshot history for a specific file
        /// </summary>
        /// <param name="relativePath">The relative path to the file</param>
        /// <param name="dbName">The database name associated with the file</param>
        /// <returns>A list of snapshots for the file, or an empty list if the file has no history</returns>
        public List<Snapshot> GetFileHistory(string relativePath, string? dbName = null)
        {
            var result = new List<Snapshot>();

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();

                if (dbName != null)
                {
                    command.CommandText = @"
                        SELECT Id, DBName, FilePath, Caption, CreatedAt, Content
                        FROM Snapshots
                        WHERE FilePath = @FilePath AND DBName = @DBName
                        ORDER BY CreatedAt DESC
                    ";
                    command.Parameters.AddWithValue("@DBName", dbName);
                }
                else
                {
                    command.CommandText = @"
                        SELECT Id, DBName, FilePath, Caption, CreatedAt, Content
                        FROM Snapshots
                        WHERE FilePath = @FilePath AND DBName IS NULL
                        ORDER BY CreatedAt DESC
                    ";
                }

                command.Parameters.AddWithValue("@FilePath", relativePath);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    result.Add(new Snapshot
                    {
                        Id = reader.GetInt32(0),
                        DBName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        FilePath = reader.GetString(2),
                        Caption = reader.GetString(3),
                        CreatedAt = DateTime.Parse(reader.GetString(4)),
                        Content = reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting file history: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets a specific snapshot by its ID
        /// </summary>
        /// <param name="snapshotId">The ID of the snapshot to retrieve</param>
        /// <returns>The snapshot, or null if not found</returns>
        public Snapshot? GetSnapshot(int snapshotId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, DBName, FilePath, Caption, CreatedAt, Content
                    FROM Snapshots
                    WHERE Id = @Id
                ";

                command.Parameters.AddWithValue("@Id", snapshotId);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    return new Snapshot
                    {
                        Id = reader.GetInt32(0),
                        DBName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        FilePath = reader.GetString(2),
                        Caption = reader.GetString(3),
                        CreatedAt = DateTime.Parse(reader.GetString(4)),
                        Content = reader.GetString(5)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting snapshot: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets the editor content to a specific snapshot
        /// </summary>
        /// <param name="editor">The editor to set content for</param>
        /// <param name="snapshotId">The ID of the snapshot to apply</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public bool ApplySnapshotToEditor(ScintillaEditor editor, int snapshotId)
        {
            try
            {
                var snapshot = GetSnapshot(snapshotId);
                if (snapshot == null)
                {
                    Debug.Log($"Failed to get snapshot with ID: {snapshotId}");
                    return false;
                }

                // Set the content in the editor
                ScintillaManager.SetScintillaText(editor, snapshot.Content);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error applying snapshot to editor: {ex.Message}");
                return false;
            }
        }

    }
}