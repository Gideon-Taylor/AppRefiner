using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.Diagnostics;

namespace AppRefiner.Git
{
    /// <summary>
    /// Manages Git repository interactions using LibGit2Sharp
    /// </summary>
    public class GitRepositoryManager
    {
        private string _repositoryPath;

        /// <summary>
        /// Gets the current repository path
        /// </summary>
        public string RepositoryPath => _repositoryPath;

        /// <summary>
        /// Initializes a new instance of the GitRepositoryManager class
        /// </summary>
        /// <param name="repositoryPath">The path to the Git repository</param>
        public GitRepositoryManager(string repositoryPath)
        {
            _repositoryPath = repositoryPath;
        }

        /// <summary>
        /// Initializes a new Git repository at the specified path
        /// </summary>
        /// <param name="path">The path where the Git repository should be initialized</param>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public static bool InitializeRepository(string path)
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                // Check if it's already a git repository
                if (Repository.IsValid(path))
                {
                    // It's already a git repository, return success
                    return true;
                }

                // Initialize repository using LibGit2Sharp
                Repository.Init(path);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error initializing Git repository: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines if the specified path is a valid Git repository
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is a valid Git repository, false otherwise</returns>
        public static bool IsValidRepository(string path)
        {
            try
            {
                // Check if path exists
                if (!Directory.Exists(path))
                {
                    return false;
                }

                // Use LibGit2Sharp to check if the path is a valid Git repository
                return Repository.IsValid(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the status of files in the repository
        /// </summary>
        /// <returns>A dictionary of file paths and their status</returns>
        public Dictionary<string, string> GetRepositoryStatus()
        {
            var result = new Dictionary<string, string>();
            
            try
            {
                using var repo = new Repository(_repositoryPath);
                var status = repo.RetrieveStatus();
                foreach (var file in status)
                {
                    result.Add(file.FilePath, file.State.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting repository status: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Stages file changes in the repository
        /// </summary>
        /// <param name="filePaths">The file paths to stage, or null to stage all changes</param>
        /// <returns>True if staging was successful, false otherwise</returns>
        public bool StageChanges(IEnumerable<string>? filePaths = null)
        {
            try
            {
                using var repo = new Repository(_repositoryPath);
                
                if (filePaths == null)
                {
                    Commands.Stage(repo, "*");
                }
                else
                {
                    foreach (var file in filePaths)
                    {
                        Commands.Stage(repo, file);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error staging changes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Commits staged changes to the repository
        /// </summary>
        /// <param name="message">The commit message</param>
        /// <param name="author">The author of the commit</param>
        /// <returns>True if commit was successful, false otherwise</returns>
        public bool Commit(string message, string author)
        {
            try
            {
                using var repo = new Repository(_repositoryPath);
                
                var signature = new Signature(author, author, DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error committing changes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the content of an editor to a file in the Git repository
        /// </summary>
        /// <param name="editor">The editor containing the content</param>
        /// <param name="content">The content to save</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        public bool SaveEditorContent(ScintillaEditor editor, string content)
        {
            if (string.IsNullOrEmpty(editor.RelativePath))
            {
                Debug.Log("Cannot save editor content: RelativePath is not set");
                return false;
            }
            
            return SaveFileContent(editor.RelativePath, content);
        }
        
        /// <summary>
        /// Saves content to a file in the Git repository using the relative path
        /// </summary>
        /// <param name="relativePath">The relative path within the repository</param>
        /// <param name="content">The content to save</param>
        /// <returns>True if the save was successful, false otherwise</returns>
        public bool SaveFileContent(string relativePath, string content)
        {
            try
            {
                // Combine the repository path with the relative path
                string fullPath = Path.Combine(_repositoryPath, relativePath);
                
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Write the content to the file
                File.WriteAllText(fullPath, content);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error saving file content: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if an editor's file exists in the Git repository
        /// </summary>
        /// <param name="editor">The editor to check</param>
        /// <returns>True if the file exists, false otherwise</returns>
        public bool EditorFileExists(ScintillaEditor editor)
        {
            if (string.IsNullOrEmpty(editor.RelativePath))
                return false;
                
            string fullPath = Path.Combine(_repositoryPath, editor.RelativePath);
            return File.Exists(fullPath);
        }
        
        /// <summary>
        /// Gets the content of an editor's file from the Git repository
        /// </summary>
        /// <param name="editor">The editor</param>
        /// <returns>The file content or null if the file doesn't exist</returns>
        public string? GetEditorFileContent(ScintillaEditor editor)
        {
            if (string.IsNullOrEmpty(editor.RelativePath))
                return null;
                
            return GetFileContent(editor.RelativePath);
        }
        
        /// <summary>
        /// Gets the content of a file from the Git repository
        /// </summary>
        /// <param name="relativePath">The relative path within the repository</param>
        /// <returns>The file content or null if the file doesn't exist</returns>
        public string? GetFileContent(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(_repositoryPath, relativePath);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllText(fullPath);
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error reading file content: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Creates and initializes a GitRepositoryManager using the path stored in application settings
        /// </summary>
        /// <returns>An initialized GitRepositoryManager object or null if initialization fails</returns>
        public static GitRepositoryManager? CreateFromSettings()
        {
            try
            {
                // Get Git repository path from settings
                string? repoPath = Properties.Settings.Default.GitRepositoryPath;
                if (string.IsNullOrEmpty(repoPath) || !IsValidRepository(repoPath))
                {
                    Debug.Log("Cannot create GitRepositoryManager: Repository path is not set or not valid");
                    return null;
                }
                
                // Create and return a new GitRepositoryManager with the path from settings
                return new GitRepositoryManager(repoPath);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error creating GitRepositoryManager from settings: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Saves, stages, and commits the editor content to the Git repository
        /// </summary>
        /// <param name="editor">The editor containing the content to save</param>
        /// <param name="content">The content to save</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public bool SaveAndCommitEditorContent(ScintillaEditor editor, string content)
        {
            try
            {
                // Check if the editor has a valid relative path
                if (string.IsNullOrEmpty(editor.RelativePath))
                {
                    Debug.Log("Cannot save editor content: RelativePath is not set");
                    return false;
                }
                
                // Save the content to the repository
                bool saveSuccess = SaveEditorContent(editor, content);
                if (!saveSuccess)
                {
                    Debug.Log($"Failed to save editor content to: {editor.RelativePath}");
                    return false;
                }
                
                // Get repository status
                var repoStatus = GetRepositoryStatus();
                
                // Check if file is modified
                bool isModified = repoStatus.ContainsKey(editor.RelativePath) && 
                                 (repoStatus[editor.RelativePath].Contains("ModifiedInWorkdir") || 
                                  repoStatus[editor.RelativePath].Contains("NewInWorkdir"));
                
                // If file has changed, create a commit
                if (isModified)
                {
                    // Stage only this file
                    StageChanges(new[] { editor.RelativePath });
                    
                    // Create a commit message with timestamp and caption
                    string caption = editor.Caption ?? "Unknown";
                    if (caption.Contains("("))
                    {
                        // Remove the type suffix, e.g. " (PeopleCode)"
                        caption = caption.Substring(0, caption.LastIndexOf("(")).Trim();
                    }
                    
                    string commitMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Snapshot of {caption}";
                    
                    // Commit the changes
                    bool commitSuccess = Commit(commitMessage, "AppRefiner");
                    
                    if (commitSuccess)
                    {
                        Debug.Log($"Successfully committed changes for: {editor.RelativePath}");
                        return true;
                    }
                    else
                    {
                        Debug.Log($"Failed to commit changes for: {editor.RelativePath}");
                        return false;
                    }
                }
                
                // Even if no changes were detected, the save operation was successful
                Debug.Log($"No changes detected for: {editor.RelativePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in SaveAndCommitEditorContent: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the commit history for a specific file in the repository
        /// </summary>
        /// <param name="relativePath">The relative path to the file within the repository</param>
        /// <returns>A list of commits for the file, or an empty list if the file has no history</returns>
        public List<CommitInfo> GetFileHistory(string relativePath)
        {
            var result = new List<CommitInfo>();
            
            try
            {
                using var repo = new Repository(_repositoryPath);
                
                // If repository has no commits yet or has a detached HEAD, return empty list
                if (repo.Head == null || repo.Head.Tip == null)
                {
                    return result;
                }
                
                // Create a filter to only include commits that modified the specified file
                var options = new CommitFilter
                {
                    SortBy = CommitSortStrategies.Time,
                    IncludeReachableFrom = repo.Head.Tip
                };
                
                var commits = repo.Commits.QueryBy(options);
                
                foreach (var commit in commits)
                {
                    // Get the parent commit (if available)
                    var parentCommit = commit.Parents.FirstOrDefault();
                    
                    // Compare the file between this commit and its parent
                    if (parentCommit != null)
                    {
                        var comparisonOptions = new CompareOptions();
                        var changes = repo.Diff.Compare<TreeChanges>(parentCommit.Tree, commit.Tree, comparisonOptions);
                        
                        // Check if the file was modified in this commit
                        if (changes.Any(change => change.Path == relativePath))
                        {
                            result.Add(new CommitInfo
                            {
                                CommitId = commit.Id.Sha,
                                Message = commit.Message,
                                Author = commit.Author.Name,
                                Date = commit.Author.When.DateTime,
                                RelativePath = relativePath
                            });
                        }
                    }
                    else
                    {
                        // This is the initial commit, check if the file exists
                        var treeEntry = commit.Tree[relativePath];
                        if (treeEntry != null)
                        {
                            result.Add(new CommitInfo
                            {
                                CommitId = commit.Id.Sha,
                                Message = commit.Message,
                                Author = commit.Author.Name,
                                Date = commit.Author.When.DateTime,
                                RelativePath = relativePath
                            });
                        }
                    }
                }
                
                // Get the max snapshots setting
                int maxSnapshots = Properties.Settings.Default.MaxFileSnapshots;
                
                // Only limit the history if the setting is valid and we have more commits than allowed
                if (maxSnapshots > 0 && result.Count > maxSnapshots)
                {
                    // Sort by date descending (newest first)
                    //result.Sort((a, b) => b.Date.CompareTo(a.Date));
                    
                    // Take only the most recent N commits
                    result = result.Take(maxSnapshots).ToList();
                    
                    Debug.Log($"Limited history for {relativePath} to {maxSnapshots} most recent commits.");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting file history: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Gets the content of a file from a specific commit
        /// </summary>
        /// <param name="relativePath">The relative path to the file within the repository</param>
        /// <param name="commitId">The commit ID (SHA) to retrieve the file from</param>
        /// <returns>The file content as a string, or null if the file doesn't exist in the commit</returns>
        public string? GetFileContentFromCommit(string relativePath, string commitId)
        {
            try
            {
                using var repo = new Repository(_repositoryPath);
                
                // Get the commit by ID
                var commit = repo.Lookup<Commit>(commitId);
                if (commit == null)
                {
                    return null;
                }
                
                // Get the file from the commit's tree
                var treeEntry = commit.Tree[relativePath];
                if (treeEntry == null)
                {
                    return null;
                }
                
                // Get the blob content
                var blob = (Blob)treeEntry.Target;
                using var contentStream = blob.GetContentStream();
                using var reader = new StreamReader(contentStream);
                
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting file content from commit: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reverts an editor's content to a specific commit version
        /// </summary>
        /// <param name="editor">The editor containing the content to revert</param>
        /// <param name="commitId">The commit ID (SHA) to revert to</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        public bool RevertEditorToCommit(ScintillaEditor editor, string commitId)
        {
            try
            {
                if (string.IsNullOrEmpty(editor.RelativePath))
                {
                    Debug.Log("Cannot revert editor content: RelativePath is not set");
                    return false;
                }
                
                // Get the file content from the commit
                var content = GetFileContentFromCommit(editor.RelativePath, commitId);
                if (content == null)
                {
                    Debug.Log($"Failed to get file content from commit: {commitId}");
                    return false;
                }
                
                // Set the content in the editor
                ScintillaManager.SetScintillaText(editor, content);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error reverting editor to commit: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the diff between two versions of a file
        /// </summary>
        /// <param name="relativePath">The relative path to the file within the repository</param>
        /// <param name="currentCommitId">The current commit ID (SHA)</param>
        /// <param name="previousCommitId">The previous commit ID (SHA) to compare with</param>
        /// <returns>The diff content as a string, or null if the diff can't be generated</returns>
        public string? GetFileDiff(string relativePath, string currentCommitId, string? previousCommitId)
        {
            try
            {
                using var repo = new Repository(_repositoryPath);
                
                // Get the current commit
                var currentCommit = repo.Lookup<Commit>(currentCommitId);
                if (currentCommit == null)
                {
                    return null;
                }
                
                // If no previous commit provided or it's the initial commit, return null
                if (string.IsNullOrEmpty(previousCommitId))
                {
                    return null;
                }
                
                // Get the previous commit
                var previousCommit = repo.Lookup<Commit>(previousCommitId);
                if (previousCommit == null)
                {
                    return null;
                }
                
                // Generate patch (diff) between the two commits for this specific file
                var options = new CompareOptions
                {
                    ContextLines = 5,
                    InterhunkLines = 1,
                    Similarity = SimilarityOptions.None
                };

                var patch = repo.Diff.Compare<Patch>(
                    previousCommit.Tree,
                    currentCommit.Tree,
                    new[] { relativePath }, 
                    options);
                
                return patch;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error getting file diff: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Represents information about a Git commit
    /// </summary>
    public class CommitInfo
    {
        /// <summary>
        /// Gets or sets the commit ID (SHA)
        /// </summary>
        public string CommitId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the commit message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the author of the commit
        /// </summary>
        public string Author { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the date of the commit
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Gets or sets the relative path of the file
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;
    }
} 