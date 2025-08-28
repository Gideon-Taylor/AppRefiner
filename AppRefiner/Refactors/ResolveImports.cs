using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using AppRefiner.Services;
using AppRefiner;
using System.Text;
using System.Windows.Forms;

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
        public bool SortImportsAlphabetically { get; set; } = true;
        
        /// <summary>
        /// Whether to preserve existing wildcard imports that cover used classes. 
        /// If true, keeps wildcard imports when they match used class packages.
        /// </summary>
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
            GenerateResolvedImports();
        }

        public override void VisitAppClassType(AppClassTypeNode node)
        {
            // Record app class type usage
            if (!string.IsNullOrEmpty(node.ClassName) && node.ClassName.Contains(":"))
            {
                usedClassPaths.Add(node.ClassName);
            }

            base.VisitAppClassType(node);
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            // Record object creation with app class types
            if (node.Type is AppClassTypeNode appClassType && 
                !string.IsNullOrEmpty(appClassType.ClassName) && 
                appClassType.ClassName.Contains(":"))
            {
                usedClassPaths.Add(appClassType.ClassName);
            }

            base.VisitObjectCreation(node);
        }

        /// <summary>
        /// Generates the resolved imports based on used class paths
        /// </summary>
        private void GenerateResolvedImports()
        {
            // Skip if no class references were found
            if (usedClassPaths.Count == 0) return;

            // Step 1: Start with existing imports in their original order
            var finalImportList = new List<string>(existingImportPaths);
            
            // Step 2: Create a working copy of used classes to process
            var remainingUsedClasses = new HashSet<string>(usedClassPaths);

            // Step 3: Handle wildcards based on preserve flag
            if (PreserveWildcardImports)
            {
                // Preserve wildcards: Remove classes covered by existing wildcards from used classes list
                foreach (var import in existingImportPaths)
                {
                    if (IsWildcardImport(import))
                    {
                        var wildcardPackage = GetWildcardPackage(import);
                        var coveredClasses = remainingUsedClasses
                            .Where(usedClass => GetPackageFromClassPath(usedClass).Equals(wildcardPackage, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        // Remove covered classes from remaining list
                        foreach (var coveredClass in coveredClasses)
                        {
                            remainingUsedClasses.Remove(coveredClass);
                        }
                    }
                }
            }
            else
            {
                // Don't preserve wildcards: Expand wildcards to explicit classes
                for (int i = 0; i < finalImportList.Count; i++)
                {
                    var import = finalImportList[i];
                    if (IsWildcardImport(import))
                    {
                        var wildcardPackage = GetWildcardPackage(import);
                        var matchingClasses = remainingUsedClasses
                            .Where(usedClass => GetPackageFromClassPath(usedClass).Equals(wildcardPackage, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(className => className) // Sort expanded classes
                            .ToList();

                        if (matchingClasses.Count > 0)
                        {
                            // Replace wildcard with explicit classes
                            finalImportList.RemoveAt(i);
                            finalImportList.InsertRange(i, matchingClasses);
                            i += matchingClasses.Count - 1; // Adjust index for inserted items

                            // Remove processed classes from remaining list
                            foreach (var matchingClass in matchingClasses)
                            {
                                remainingUsedClasses.Remove(matchingClass);
                            }
                        }
                        else
                        {
                            // Remove unused wildcard
                            finalImportList.RemoveAt(i);
                            i--; // Adjust index for removed item
                        }
                    }
                }
            }

            // Step 4: Add any remaining used classes to the end
            finalImportList.AddRange(remainingUsedClasses.OrderBy(className => className));

            // Step 5: Remove only used imports and deduplicate while preserving order
            var seenImports = new HashSet<string>();
            var usedImportsOnly = new List<string>();
            
            foreach (var import in finalImportList)
            {
                // Keep import if it's a wildcard that covers used classes, or if it's a used explicit class
                bool shouldKeep = false;

                if (IsWildcardImport(import))
                {
                    shouldKeep = HasUsedClassesInPackage(GetWildcardPackage(import));
                }
                else
                {
                    shouldKeep = usedClassPaths.Contains(import);
                }

                if (shouldKeep && seenImports.Add(import))
                {
                    usedImportsOnly.Add(import);
                }
            }
            
            finalImportList = usedImportsOnly;

            // Step 6: Sort if requested
            if (SortImportsAlphabetically)
            {
                finalImportList = finalImportList.OrderBy(import => import).ToList();
            }

            // Step 7: Generate the imports block
            var newImports = new StringBuilder();
            foreach (var classPath in finalImportList)
            {
                newImports.AppendLine($"import {classPath};");
            }

            // Trim trailing newlines to prevent accumulation of blank lines
            string imports = newImports.ToString().TrimEnd();

            if (programNode?.Imports.Count == 0)
            {
                imports += "\r\n\r\n";
            }

            if (programNode?.Imports.Count > 0)
            {
                // Replace the existing imports block
                var firstImport = programNode.Imports.First();
                var lastImport = programNode.Imports.Last();
                
                if (firstImport.SourceSpan.IsValid && lastImport.SourceSpan.IsValid)
                {
                    EditText(firstImport.SourceSpan.Start.Index, lastImport.SourceSpan.End.Index, imports, "Resolve imports");
                }
            }
            else
            {
                // No existing imports block, so add one at the beginning of the program
                var insertionPoint = 0;
                
                // Find the best insertion point
                if (programNode?.AppClass?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.AppClass.SourceSpan.Start.Index;
                }
                else if (programNode?.Interface?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.Interface.SourceSpan.Start.Index;
                }
                else if (programNode?.Functions.Count > 0 && programNode.Functions[0].SourceSpan.IsValid)
                {
                    insertionPoint = programNode.Functions[0].SourceSpan.Start.Index;
                }
                else if (programNode?.Variables.Count > 0 && programNode.Variables[0].SourceSpan.IsValid)
                {
                    insertionPoint = programNode.Variables[0].SourceSpan.Start.Index;
                }
                else if (programNode?.MainBlock?.SourceSpan.IsValid == true)
                {
                    insertionPoint = programNode.MainBlock.SourceSpan.Start.Index;
                }

                // Add exactly two newlines after imports (consistent spacing)
                string insertText = imports + Environment.NewLine + Environment.NewLine;
                InsertText(insertionPoint, insertText, "Add missing imports");
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