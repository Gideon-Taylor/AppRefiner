using PeopleCodeParser.SelfHosted.Nodes;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that resolves all class references in the code and creates explicit imports for each one
    /// </summary>
    public class ResolveImports : BaseRefactor
    {
        public new static string RefactorName => "Resolve Imports";
        public new static string RefactorDescription => "Resolves all class references in the code and creates explicit imports for each one";

        /// <summary>
        /// Whether to sort imports alphabetically. If false, maintains original order and adds new imports at the bottom.
        /// </summary>
        [ConfigurablePropertyAttribute("Sort Imports", "Sorts imports alphabetically, with wildcards at the top.")]
        public bool SortImportsAlphabetically { get; set; } = true;

        /// <summary>
        /// Whether to preserve existing wildcard imports that cover used classes. 
        /// If true, keeps wildcard imports when they match used class packages.
        /// </summary>
        [ConfigurablePropertyAttribute("Preserve Wildcard Imports", "Keeps existing wildcard imports.")]
        public bool PreserveWildcardImports { get; set; } = false;

        // Tracks unique application class paths used in the code
        private readonly HashSet<string> usedClassPaths = new();

        // Tracks existing import paths in their original order
        private readonly List<string> existingImportPaths = new();

        // The program node
        private ProgramNode? programNode;

        /// <summary>
        /// Gets whether this refactor should register a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => true;

        /// <summary>
        /// Gets the modifier keys for the keyboard shortcut
        /// </summary>
        public new static AppRefiner.ModifierKeys ShortcutModifiers => AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift;

        /// <summary>
        /// Gets the key for the keyboard shortcut
        /// </summary>
        public new static Keys ShortcutKey => Keys.I;

        /// <summary>
        /// Gets whether this refactor should run on incomplete parses
        /// ResolveImports should only run when parsing is successful to avoid corrupting import statements
        /// </summary>
        public override bool RunOnIncompleteParse => false;

        public ResolveImports(AppRefiner.ScintillaEditor editor) : base(editor)
        {
        }

        public override void VisitProgram(ProgramNode node)
        {
            programNode = node;
            usedClassPaths.Clear();
            existingImportPaths.Clear();

            // Capture existing imports in their original order
            foreach (var import in node.Imports)
            {
                existingImportPaths.Add(import.FullPath);
            }

            base.VisitProgram(node);

            // After visiting all nodes, generate the new imports
            GenerateResolvedImports(node);
        }

        public override void VisitAppClassType(AppClassTypeNode node)
        {
            /* Don't count app classes used in import statements as "used" */
            if (node.Parent is not ImportNode)
            {
                // Record app class type usage
                if (!string.IsNullOrEmpty(node.ClassName) && node.QualifiedName.Contains(':'))
                {
                    usedClassPaths.Add(node.QualifiedName);
                }
            }
            base.VisitAppClassType(node);
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            // Record object creation with app class types
            if (node.Type is AppClassTypeNode appClassType &&
                !string.IsNullOrEmpty(appClassType.ClassName) &&
                appClassType.ClassName.Contains(':'))
            {
                usedClassPaths.Add(appClassType.QualifiedName);
            }

            base.VisitObjectCreation(node);
        }


        private bool IsImportUsed(ImportNode import, HashSet<string> usedClasses)
        {
            
            if (import.IsWildcard)
            {
                var pathString = string.Join(':', import.PackagePath);
                return usedClasses.Where(u => u.StartsWith(pathString)).Any();
            } else
            {
                return usedClasses.Contains(import.FullPath);
            }
        }

        /// <summary>
        /// Generates the resolved imports based on used class paths
        /// </summary>
        private void GenerateResolvedImports(ProgramNode node)
        {
            // Skip if no class references were found
            if (usedClassPaths.Count == 0) return;

            var existingImports = node.Imports;
            var wildcards = node.Imports.Where(i => i.IsWildcard).ToList();

            var importsForUsedClasses = usedClassPaths.Select(p => new ImportNode(p)).ToList();

            var newImports = importsForUsedClasses.Where(i => !(existingImports.Select(e => e.FullPath).Contains(i.FullPath))).ToList();

            var finalImports = new List<ImportNode>(PreserveWildcardImports ? existingImports : existingImports.Where(e => !e.IsWildcard)).ToList();

            foreach( var newImport in newImports)
            {
                if (PreserveWildcardImports)
                {
                    /* no matching wild card */
                    if (!wildcards.Where(w => string.Join(':', w.PackagePath) == string.Join(':',newImport.PackagePath)).Any())
                    {
                        finalImports.Add(newImport);
                    }
                } else
                {
                    finalImports.Add(newImport);
                }
            }

            /* Clean up any unused imports */
            var unusedImports = finalImports.Where(e => !IsImportUsed(e, usedClassPaths)).ToList();

            foreach(var unusedImport in unusedImports)
            {
                finalImports.Remove(unusedImport);
            }

            // Step 6: Sort if requested
            if (SortImportsAlphabetically)
            {
                finalImports = [.. finalImports.OrderByDescending(import => import.IsWildcard).ThenBy(import => string.Join(':', import.PackagePath))];
            }

            // Step 7: Generate the imports block
            var newImportString = new StringBuilder();
            foreach (var import in finalImports)
            {
                newImportString.AppendLine($"{import.ToString()};");
            }

            if (programNode?.Imports.Count == 0)
            {
                newImportString.Append("\r\n");
            }

            if (programNode?.Imports.Count > 0)
            {
                // Replace the existing imports block
                var firstImport = programNode.Imports.First();
                var lastImport = programNode.Imports.Last();

                if (firstImport.SourceSpan.IsValid && lastImport.SourceSpan.IsValid)
                {
                    EditText(firstImport.SourceSpan.Start.ByteIndex, lastImport.SourceSpan.End.ByteIndex, newImportString.ToString(), "Resolve imports");
                }
            }
            else
            {
                // No existing imports block, so add one at the beginning of the program
                var insertionPoint = 0;

                // Find the best insertion point
                if (programNode?.AppClass?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.AppClass.SourceSpan.Start.ByteIndex;
                }
                else if (programNode?.Interface?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.Interface.SourceSpan.Start.ByteIndex;
                }
                else if (programNode?.Functions.Count > 0 && programNode.Functions[0].SourceSpan.IsValid)
                {
                    insertionPoint = programNode.Functions[0].SourceSpan.Start.ByteIndex;
                }
                else if (programNode?.Variables.Count > 0 && programNode.Variables[0].SourceSpan.IsValid)
                {
                    insertionPoint = programNode.Variables[0].SourceSpan.Start.ByteIndex;
                }
                else if (programNode?.MainBlock?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.MainBlock.SourceSpan.Start.ByteIndex;
                }

                InsertText(insertionPoint, newImportString.ToString(), "Add missing imports");
            }
        }

        /// <summary>
        /// Checks if an import path is a wildcard import (ends with :*)
        /// </summary>
        private static bool IsWildcardImport(string importPath)
        {
            return importPath.EndsWith(":*");
        }

        /// <summary>
        /// Gets the package path from a wildcard import by removing the :* suffix
        /// </summary>
        private static string GetWildcardPackage(string wildcardImport)
        {
            return wildcardImport.Replace(":*", "");
        }

        /// <summary>
        /// Extracts the package path from a full class path
        /// Example: IS_CO_BASE:JSON:JsonObject → IS_CO_BASE:JSON
        /// </summary>
        private static string GetPackageFromClassPath(string classPath)
        {
            int lastColonIndex = classPath.LastIndexOf(':');
            return lastColonIndex > 0 ? classPath.Substring(0, lastColonIndex) : classPath;
        }

        /// <summary>
        /// Checks if there are any used classes in the specified package
        /// </summary>
        private bool HasUsedClassesInPackage(string packagePath)
        {
            foreach (var usedPath in usedClassPaths)
            {
                string classPackage = GetPackageFromClassPath(usedPath);
                if (classPackage.Equals(packagePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}