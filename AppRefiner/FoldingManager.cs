using Microsoft.Data.Sqlite; // Added for SQLite support
using System.Security.Cryptography; // Added for SHA256
using System.Text;
using System.Text.Json; // Added for JSON serialization

namespace AppRefiner
{
    // FoldLevelInfo struct and related methods will be removed.

    public class FoldingManager
    {
        private const string FoldsDbFileName = "AppRefinerFolds.db";
        private static string _dbPath;

        private static string GetDatabasePath()
        {
            if (string.IsNullOrEmpty(_dbPath))
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _dbPath = Path.Combine(appDataPath, "AppRefiner", FoldsDbFileName);
            }
            return _dbPath;
        }

        private static void InitializeDatabase()
        {
            string dbPath = GetDatabasePath();
            string dbDirectory = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            string tableCommand = @"
                    CREATE TABLE IF NOT EXISTS EditorFoldStates (
                        full_path TEXT NOT NULL,
                        content_hash TEXT NOT NULL,
                        fold_paths TEXT NOT NULL,
                        PRIMARY KEY (full_path, content_hash)
                    );";
            var command = connection.CreateCommand();
            command.CommandText = tableCommand;
            command.ExecuteNonQuery();
        }

        private static string CalculateContentHash(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "0"; // Or handle as an error/empty case appropriately
            }
            using SHA256 sha256Hash = SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(content));
            StringBuilder builder = new();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public static void UpdatePersistedFolds(ScintillaEditor editor)
        {
            if (editor == null) return;

            string fullPath = editor.RelativePath;
            if (string.IsNullOrEmpty(fullPath)) return;

            // Use existing ContentString, assuming it's populated.
            string content = editor.ContentString;
            if (string.IsNullOrEmpty(content)) return; // Cannot proceed without content

            string contentHash = CalculateContentHash(content);
            List<List<int>> pathsToStore = editor.CollapsedFoldPaths;

            InitializeDatabase();

            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            var command = connection.CreateCommand();

            if (pathsToStore == null || pathsToStore.Count == 0)
            {
                // If the list is empty, delete the specific record for this path and hash
                command.CommandText = @"
                        DELETE FROM EditorFoldStates
                        WHERE full_path = @full_path AND content_hash = @content_hash;";
                command.Parameters.AddWithValue("@full_path", fullPath);
                command.Parameters.AddWithValue("@content_hash", contentHash);
            }
            else
            {
                string serializedPaths = JsonSerializer.Serialize(pathsToStore);
                command.CommandText = @"
                        INSERT OR REPLACE INTO EditorFoldStates (full_path, content_hash, fold_paths)
                        VALUES (@full_path, @content_hash, @fold_paths);";
                command.Parameters.AddWithValue("@full_path", fullPath);
                command.Parameters.AddWithValue("@content_hash", contentHash);
                command.Parameters.AddWithValue("@fold_paths", serializedPaths);
            }
            command.ExecuteNonQuery();
        }

        public static List<List<int>> RetrievePersistedFolds(ScintillaEditor editor)
        {
            if (editor == null) return new List<List<int>>();

            string fullPath = editor.RelativePath;
            if (string.IsNullOrEmpty(fullPath)) return new List<List<int>>();

            // Use existing ContentString, assuming it's populated.
            string content = editor.ContentString;
            if (string.IsNullOrEmpty(content)) return new List<List<int>>();

            string currentContentHash = CalculateContentHash(content);

            InitializeDatabase();

            using var connection = new SqliteConnection($"Data Source={GetDatabasePath()}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT fold_paths FROM EditorFoldStates
                    WHERE full_path = @full_path AND content_hash = @current_content_hash;";
            command.Parameters.AddWithValue("@full_path", fullPath);
            command.Parameters.AddWithValue("@current_content_hash", currentContentHash);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                string serializedPaths = reader.GetString(0);
                try
                {
                    return JsonSerializer.Deserialize<List<List<int>>>(serializedPaths) ?? new List<List<int>>();
                }
                catch (JsonException)
                {
                    // Handle malformed JSON, perhaps log an error
                    return new List<List<int>>();
                }
            }
            else
            {
                // No match for current content hash, clear out all stored folds for this path
                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = @"
                            DELETE FROM EditorFoldStates
                            WHERE full_path = @full_path;";
                deleteCommand.Parameters.AddWithValue("@full_path", fullPath);
                deleteCommand.ExecuteNonQuery();
                return new List<List<int>>();
            }
        }

        private struct PathBuilderNode
        {
            public int Level { get; } // Scintilla fold level
            public int CurrentChildIndex { get; set; } // 0-based index for the next child of THIS node

            public PathBuilderNode(int level)
            {
                Level = level;
                CurrentChildIndex = 0;
            }
        }

        public static List<List<int>> GetCollapsedFoldPathsDirectly(ScintillaEditor editor)
        {
            List<List<int>> collapsedPaths = new();
            if (editor == null) return collapsedPaths;

            Stack<PathBuilderNode> hierarchyStack = new();
            List<int> currentPath = new(); // Stores the 0-based indices forming the current path

            int lineCount = ScintillaManager.GetLineCount(editor);
            int rootFoldCount = 0; // To assign indices to top-level folds

            for (int i = 0; i < lineCount; i++)
            {
                var (numericLevel, isHeader) = ScintillaManager.GetCurrentLineFoldLevel(editor, i);

                if (isHeader)
                {
                    // Pop from stack while new level is not a child of the stack top
                    // or if it's a sibling (same level) or an uncle (lower level).
                    while (hierarchyStack.Count > 0 && numericLevel <= hierarchyStack.Peek().Level)
                    {
                        hierarchyStack.Pop();
                        if (currentPath.Count > 0)
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                        }
                    }

                    int assignedIndex;
                    if (hierarchyStack.Count == 0)
                    {
                        // This is a root-level fold
                        assignedIndex = rootFoldCount;
                        rootFoldCount++;
                    }
                    else
                    {
                        // This is a child of the fold at the top of the stack
                        PathBuilderNode parentNode = hierarchyStack.Pop(); // Pop to update CurrentChildIndex
                        assignedIndex = parentNode.CurrentChildIndex;
                        parentNode.CurrentChildIndex++;
                        hierarchyStack.Push(parentNode); // Push updated parent back
                    }

                    currentPath.Add(assignedIndex);
                    hierarchyStack.Push(new PathBuilderNode(numericLevel));

                    bool isCollapsed = ScintillaManager.IsLineFolded(editor, i);
                    if (isCollapsed)
                    {
                        collapsedPaths.Add(new List<int>(currentPath));
                    }
                }
            }
            return collapsedPaths;
        }

        public static void ProcessFolding(ScintillaEditor editor)
        {
            if (editor.FoldingEnabled && (!editor.HasLexilla || editor.Type == EditorType.SQL || editor.Type == EditorType.Other))
            {
                // Ensure the activeEditor is not null before proceeding
                if (editor != null)
                {
                    ScintillaManager.SetFoldRegions(editor);
                }
            }
        }

        public static void FoldAppRefinerRegions(ScintillaEditor editor)
        {

            if (editor == null || editor.ContentString == null) return;

            var content = editor.ContentString;
            if (!content.Contains("/* #region")) return; // No regions to fold

            var lines = content.Split('\n', StringSplitOptions.None);
            var regionStart = 0;
            var regionEnd = 0;
            var collapseByDefault = false;
            for (var x = 0; x < lines.Length; x++)
            {
                var line = lines[x].TrimStart();

                if (line.StartsWith("/* #region"))
                {
                    /* If the next character is a - we will default this region to collapsed */
                    if (line.Length > 10 && line[10] == '-')
                    {
                        collapseByDefault = true;
                    }

                    // Fold the line
                    regionStart = x;

                }
                else if (line.StartsWith("/* #endregion"))
                {
                    // Unfold the line
                    regionEnd = x;


                    if (regionStart != 0 && regionEnd != 0 && regionEnd > regionStart)
                    {
                        // Unfold the line
                        ScintillaManager.SetExplicitFoldRegion(editor, regionStart, regionEnd, collapseByDefault);

                    }
                }

            }
        }

        public static void PrintCollapsedFoldPathsDebug(List<List<int>> collapsedPaths)
        {
            Debug.Log("---- Start Collapsed Fold Paths ----");
            if (collapsedPaths == null || collapsedPaths.Count == 0)
            {
                Debug.Log("No collapsed folds found.");
            }
            else
            {
                foreach (var path in collapsedPaths)
                {
                    Debug.Log("Path: " + string.Join(" -> ", path));
                }
            }
            Debug.Log("---- End Collapsed Fold Paths ----");
        }

        private static string PathToString(List<int> path)
        {
            return string.Join("_", path);
        }

        public static void ApplyCollapsedFoldPaths(ScintillaEditor editor)
        {
            if (editor == null || editor.CollapsedFoldPaths.Count == 0)
            {
                return;
            }

            var pathsToCollapse = editor.CollapsedFoldPaths;


            // For efficient lookup
            HashSet<string> collapseTargetPaths = new(pathsToCollapse.Select(PathToString));

            Stack<PathBuilderNode> hierarchyStack = new();
            List<int> currentPath = new(); // Stores the 0-based indices forming the current path

            int lineCount = ScintillaManager.GetLineCount(editor);
            int rootFoldCount = 0; // To assign 0-based indices to top-level folds

            for (int currentLineNumber = 0; currentLineNumber < lineCount; currentLineNumber++)
            {
                var (numericLevel, isHeader) = ScintillaManager.GetCurrentLineFoldLevel(editor, currentLineNumber);

                if (isHeader)
                {
                    // Pop from stack while new level is not a child of the stack top
                    // or if it's a sibling (same level) or an uncle (lower level).
                    while (hierarchyStack.Count > 0 && numericLevel <= hierarchyStack.Peek().Level)
                    {
                        hierarchyStack.Pop();
                        if (currentPath.Count > 0)
                        {
                            currentPath.RemoveAt(currentPath.Count - 1);
                        }
                    }

                    int assignedIndex;
                    if (hierarchyStack.Count == 0)
                    {
                        // This is a root-level fold
                        assignedIndex = rootFoldCount;
                        rootFoldCount++;
                    }
                    else
                    {
                        // This is a child of the fold at the top of the stack
                        PathBuilderNode parentNode = hierarchyStack.Pop(); // Pop to update CurrentChildIndex
                        assignedIndex = parentNode.CurrentChildIndex;
                        parentNode.CurrentChildIndex++;
                        hierarchyStack.Push(parentNode); // Push updated parent back
                    }

                    currentPath.Add(assignedIndex);
                    hierarchyStack.Push(new PathBuilderNode(numericLevel)); // Level and CurrentChildIndex=0 for the new node

                    string currentPathStr = PathToString(currentPath);
                    if (collapseTargetPaths.Contains(currentPathStr))
                    {
                        ScintillaManager.SetLineFoldStatus(editor, currentLineNumber, true);
                    }
                }
            }
        }
    }
}
