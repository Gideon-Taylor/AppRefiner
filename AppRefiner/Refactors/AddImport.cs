using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that adds a specific application class import if it's not already covered by an existing explicit or wildcard import.
    /// </summary>
    public class AddImport : BaseRefactor
    {
        public new static string RefactorName => "Add Import";
        public new static string RefactorDescription => "Adds a specific application class import if not already covered.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from discovery.
        /// </summary>
        public new static bool IsHidden => true;

        private readonly string _appClassPathToAdd;
        private readonly List<string> _existingImportStatements = new();
        private readonly List<string> _existingImportPaths = new();
        private ProgramNode? _programNode;
        private bool _hasExistingImports;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddImport"/> class with a specific path.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="appClassPathToAdd">The application class path to add.</param>
        public AddImport(AppRefiner.ScintillaEditor editor, string appClassPathToAdd) : base(editor)
        {
            if (string.IsNullOrWhiteSpace(appClassPathToAdd))
                throw new ArgumentException("Application class path cannot be null or empty", nameof(appClassPathToAdd));

            _appClassPathToAdd = appClassPathToAdd.Trim();

            if (!IsValidAppClassPath(_appClassPathToAdd))
                throw new ArgumentException($"Invalid Application Class Path format: {_appClassPathToAdd}", nameof(appClassPathToAdd));
        }

        /// <summary>
        /// Basic validation for Application Class Path format.
        /// Ensures it contains a colon and does not contain a wildcard.
        /// </summary>
        private bool IsValidAppClassPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.Contains(':') && !path.Contains('*');
        }

        public override void VisitProgram(ProgramNode node)
        {
            _programNode = node;
            _existingImportPaths.Clear();
            _existingImportStatements.Clear();
            _hasExistingImports = node.Imports.Count > 0;

            // Collect existing imports
            foreach (var import in node.Imports)
            {
                // Get the full import statement text by reconstructing from the import node
                var importText = import.ToString()?.Trim();
                if (!string.IsNullOrEmpty(importText))
                {
                    // Ensure it ends with a semicolon for consistency
                    if (!importText.EndsWith(";"))
                        importText += ";";
                    _existingImportStatements.Add(importText);
                }

                // Extract the path part for coverage checking
                _existingImportPaths.Add(import.FullPath);
            }

            base.VisitProgram(node);

            // Check if the class path is already covered by existing imports
            if (IsCovered(_appClassPathToAdd, _existingImportPaths))
            {
                // No changes needed
                return;
            }

            // Prepare the new import statement
            string newImportStatement = $"import {_appClassPathToAdd};";

            // Combine existing and new imports
            var allImportStatements = new List<string>(_existingImportStatements);
            allImportStatements.Add(newImportStatement);

            // Sort the imports alphabetically by the path part, case-insensitively
            allImportStatements.Sort((s1, s2) =>
            {
                // Extract path by removing "import " prefix and trailing ";"
                string path1 = s1.Length > 7 ? s1.Substring(7, s1.Length - (s1.EndsWith(";") ? 8 : 7)).Trim() : s1;
                string path2 = s2.Length > 7 ? s2.Substring(7, s2.Length - (s2.EndsWith(";") ? 8 : 7)).Trim() : s2;
                return string.Compare(path1, path2, StringComparison.OrdinalIgnoreCase);
            });

            // Generate the new imports block text, joined by newlines
            string newImportsBlockText = string.Join(Environment.NewLine, allImportStatements);

            if (!_hasExistingImports)
            {
                newImportsBlockText += "\r\n\r\n";
            }

            // Apply the change: Replace existing imports or insert new ones
            if (_hasExistingImports && node.Imports.Count > 0)
            {
                // Replace the entire imports section
                var firstImport = node.Imports.First();
                var lastImport = node.Imports.Last();

                if (firstImport.SourceSpan.IsValid && lastImport.SourceSpan.IsValid)
                {
                    EditText(firstImport.SourceSpan.Start.ByteIndex, lastImport.SourceSpan.End.ByteIndex,
                             newImportsBlockText, $"Add import for {_appClassPathToAdd}");
                }
            }
            else
            {
                // No existing imports - insert at the beginning of the program
                var insertionPoint = 0;

                // If there's an app class, insert before it
                if (node.AppClass?.SourceSpan.IsValid == true)
                {
                    insertionPoint = node.AppClass.SourceSpan.Start.ByteIndex;
                }
                // If there's an interface, insert before it
                else if (node.Interface?.SourceSpan.IsValid == true)
                {
                    insertionPoint = node.Interface.SourceSpan.Start.ByteIndex;
                }
                // If there are functions, insert before the first one
                else if (node.Functions.Count > 0 && node.Functions[0].SourceSpan.IsValid)
                {
                    insertionPoint = node.Functions[0].SourceSpan.Start.ByteIndex;
                }
                // If there are variables, insert before the first one
                else if (node.Variables.Count > 0 && node.Variables[0].SourceSpan.IsValid)
                {
                    insertionPoint = node.Variables[0].SourceSpan.Start.ByteIndex;
                }
                // If there's a main block, insert before it
                else if (node.MainBlock?.SourceSpan.IsValid == true)
                {
                    insertionPoint = node.MainBlock.SourceSpan.Start.ByteIndex;
                }

                // Add standard spacing: imports block, blank line, then the rest
                InsertText(insertionPoint, newImportsBlockText, $"Add import for {_appClassPathToAdd}");
            }
        }

        /// <summary>
        /// Checks if a target application class path is covered by a list of existing import paths,
        /// considering both exact matches and wildcard imports.
        /// </summary>
        /// <param name="targetPath">The application class path to check (e.g., "PKG:SUB:CLASS").</param>
        /// <param name="existingImportPaths">A collection of existing import paths (e.g., "PKG:SUB:CLASS", "PKG:SUB:*", "PKG:*").</param>
        /// <returns><c>true</c> if the target path is covered; <c>false</c> otherwise.</returns>
        private bool IsCovered(string targetPath, IEnumerable<string> existingImportPaths)
        {
            // Extract the package part of the target path (e.g., "PKG:SUB" from "PKG:SUB:CLASS")
            string targetPackage = GetPackagePath(targetPath);
            // A valid class path must contain ':' and thus have a non-empty package path.
            if (string.IsNullOrEmpty(targetPackage) || targetPackage == targetPath) return false;

            foreach (string existingPath in existingImportPaths)
            {
                if (string.IsNullOrWhiteSpace(existingPath)) continue; // Skip empty paths

                if (existingPath.EndsWith(":*")) // It's a wildcard import
                {
                    // Get the package part of the wildcard (e.g., "PKG:SUB" from "PKG:SUB:*" or "PKG" from "PKG:*")
                    string wildcardPackage = GetPackagePath(existingPath); // Reuse helper

                    // Check if the target package IS the wildcard package (e.g., target PKG:SUB covered by PKG:SUB:*)
                    // OR if the target package starts with the wildcard package followed by a colon (e.g., target PKG:SUB:SUB2 covered by PKG:SUB:*)
                    if (targetPackage.Equals(wildcardPackage, StringComparison.OrdinalIgnoreCase) ||
                        targetPackage.StartsWith(wildcardPackage + ":", StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Covered by wildcard
                    }
                }
                else // It's an explicit import
                {
                    if (existingPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Exact match
                    }
                }
            }
            return false; // Not covered by any existing import
        }

        /// <summary>
        /// Helper to get the package part of a full application path (class or wildcard).
        /// Returns the part before the last colon. For "PKG:*", returns "PKG".
        /// </summary>
        /// <param name="fullPath">The full path (e.g., "PKG:SUB:CLASS" or "PKG:SUB:*").</param>
        /// <returns>The package path (e.g., "PKG:SUB"), or empty string if no colon exists or path is invalid.</returns>
        private string GetPackagePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

            int lastColon = fullPath.LastIndexOf(':');
            if (lastColon > 0) // Ensure colon is not the first character
            {
                return fullPath.Substring(0, lastColon);
            }
            // Handle cases like "PKG" (invalid class/wildcard path) - should not match anything
            return string.Empty;
        }
    }
}