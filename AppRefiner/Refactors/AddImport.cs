using Antlr4.Runtime;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using Antlr4.Runtime.Tree; // Required for ITerminalNode

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
        private ImportsBlockContext? _importsBlockContext;
        // Stores the full text of each existing import statement (e.g., "import PKG:CLASS;")
        private readonly List<string> _existingImportStatements = [];
        // Stores just the path part of existing imports (e.g., "PKG:CLASS", "PKG:*") for coverage check
        private readonly List<string> _existingImportPaths = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="AddImport"/> class with a specific path.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="appClassPathToAdd">The application class path to add.</param>
        public AddImport(ScintillaEditor editor, string appClassPathToAdd) : base(editor)
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

        /// <summary>
        /// Called when entering the imports block. Collects existing import statements and paths.
        /// </summary>
        public override void EnterImportsBlock(ImportsBlockContext context)
        {
            _importsBlockContext = context;
            _existingImportPaths.Clear();
            _existingImportStatements.Clear();

            foreach (var importDecl in context.importDeclaration())
            {
                string? importText = GetOriginalText(importDecl)?.Trim();
                 if (!string.IsNullOrEmpty(importText))
                 {
                     // Ensure it ends with a semicolon for consistency if missing (though parser should enforce)
                     if (!importText.EndsWith(";")) importText += ";";
                     _existingImportStatements.Add(importText);
                 }

                // Extract the path part for coverage checking
                var appClassPathCtx = importDecl.appClassPath();
                if (appClassPathCtx != null)
                {
                    _existingImportPaths.Add(appClassPathCtx.GetText());
                }
                else
                {
                    var appPkgAllCtx = importDecl.appPackageAll();
                    if (appPkgAllCtx != null)
                    {
                        // Store wildcard path (e.g., "APP_PKG:SUB_PKG:*")
                        _existingImportPaths.Add(appPkgAllCtx.GetText());
                    }
                }
            }
        }

        /// <summary>
        /// Called when exiting the program. Performs the import check and modification logic.
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
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
            allImportStatements.Sort((s1, s2) => {
                // Extract path by removing "import " prefix and trailing ";"
                string path1 = s1.Length > 7 ? s1.Substring(7, s1.Length - (s1.EndsWith(";") ? 8 : 7)).Trim() : s1;
                string path2 = s2.Length > 7 ? s2.Substring(7, s2.Length - (s2.EndsWith(";") ? 8 : 7)).Trim() : s2;
                return string.Compare(path1, path2, StringComparison.OrdinalIgnoreCase);
            });

            // Generate the new imports block text, joined by newlines
            string newImportsBlockText = string.Join(Environment.NewLine, allImportStatements);

            // Apply the change: Replace existing block or insert new one
            if (_importsBlockContext != null)
            {
                 // Ensure the original context includes the trailing whitespace/newlines if possible,
                 // otherwise, the replacement might change formatting significantly.
                 // However, ReplaceNode replaces based on token indices, so it should be okay.
                ReplaceNode(_importsBlockContext, newImportsBlockText, $"Add import for {_appClassPathToAdd}");
            }
            else // No existing imports block
            {
                 // Insert the new imports at the beginning of the program
                var firstChild = context.GetChild(0);
                if (firstChild is ITerminalNode || firstChild is ParserRuleContext) // Check if there's anything to insert before
                {
                    var insertionPoint = context.Start.StartIndex;
                    if (firstChild is ParserRuleContext firstChildContext) {
                         insertionPoint = firstChildContext.Start.StartIndex;
                    } else if (firstChild is ITerminalNode firstTerminalNode) {
                         insertionPoint = firstTerminalNode.Symbol.StartIndex;
                    }

                    // Add standard spacing: imports block, blank line, then the rest
                    string insertText = newImportsBlockText + Environment.NewLine + Environment.NewLine;

                    // Check if the first *significant* node is already an import (unlikely if _importsBlockContext is null)
                    // This check might be less reliable without a full token stream analysis,
                    // but InsertBefore should handle placement correctly.
                    // We primarily want to ensure correct spacing.

                    InsertText(insertionPoint, insertText, $"Add import for {_appClassPathToAdd}");
                }
                else // Empty program? Insert at the very beginning.
                {
                    InsertText(context.Start?.StartIndex ?? 0,
                        newImportsBlockText + Environment.NewLine + Environment.NewLine,
                        $"Add import for {_appClassPathToAdd}");
                }
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