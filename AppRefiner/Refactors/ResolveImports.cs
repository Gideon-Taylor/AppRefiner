using Antlr4.Runtime;
using System.Text;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that resolves all class references in the code and creates explicit imports for each one
    /// </summary>
    public class ResolveImports(ScintillaEditor editor) : BaseRefactor(editor)
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
        private readonly HashSet<string> usedClassPaths = [];

        // Tracks existing import paths in their original order (for non-sorting mode)
        private readonly List<string> existingImportPaths = [];
        
        // Tracks existing wildcard imports separately (for wildcard preservation)
        private readonly List<string> existingWildcardImports = [];

        // The imports block if found
        private ImportsBlockContext? importsBlockContext;

        // Whether we're tracking class references after the imports block
        private bool trackingReferences = false;

        /// <summary>
        /// Gets whether this refactor should register a keyboard shortcut
        /// </summary>
        public new static bool RegisterKeyboardShortcut => true;

        /// <summary>
        /// Gets the modifier keys for the keyboard shortcut
        /// </summary>
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;

        /// <summary>
        /// Gets the key for the keyboard shortcut
        /// </summary>
        public new static Keys ShortcutKey => Keys.I;

        /// <summary>
        /// Gets whether this refactor should run on incomplete parses
        /// ResolveImports should only run when parsing is successful to avoid corrupting import statements
        /// </summary>
        public override bool RunOnIncompleteParse => false;

        /// <summary>
        /// When entering an app class path, record it as used if we're in tracking mode
        /// </summary>
        public override void EnterAppClassPath(AppClassPathContext context)
        {
            if (!trackingReferences) return;

            string classPath = context.GetText();
            // Only add if it's a fully qualified class path
            if (classPath.Contains(":"))
            {
                usedClassPaths.Add(classPath);
            }
        }

        /// <summary>
        /// When we find the imports block, store it and start tracking class references
        /// </summary>
        public override void ExitImportsBlock(ImportsBlockContext context)
        {
            importsBlockContext = context;
            
            // Capture existing imports in their original order
            if (context.importDeclaration() != null)
            {
                foreach (var importDecl in context.importDeclaration())
                {
                    var appClassPath = importDecl.appClassPath();
                    if (appClassPath != null)
                    {
                        string importPath = appClassPath.GetText();
                        existingImportPaths.Add(importPath);
                        
                        // Track wildcard imports separately
                        if (IsWildcardImport(importPath))
                        {
                            existingWildcardImports.Add(importPath);
                        }
                    }
                }
            }
            
            trackingReferences = true;
        }

        /// <summary>
        /// When entering the program, start tracking if no imports block was found
        /// </summary>
        public override void EnterProgram(ProgramContext context)
        {
            
            if (context.importsBlock == null)
            {
                trackingReferences = true;
            }
        }

        /// <summary>
        /// When we finish the program, generate the new imports block
        /// </summary>
        public override void ExitProgram(ProgramContext context)
        {
            // Skip if no class references were found
            if (usedClassPaths.Count == 0) return;

            // Generate the new imports block with explicit imports
            var newImports = new StringBuilder();
            
            List<string> finalImportList;
            
            if (PreserveWildcardImports)
            {
                // Handle wildcard preservation logic
                finalImportList = new List<string>();
                var uncoveredClasses = new HashSet<string>();
                
                // First, determine which classes are covered by wildcards
                foreach (var usedPath in usedClassPaths)
                {
                    if (!IsClassCoveredByWildcard(usedPath))
                    {
                        uncoveredClasses.Add(usedPath);
                    }
                }
                
                if (SortImportsAlphabetically)
                {
                    // Add relevant wildcards and uncovered classes, sorted
                    var relevantWildcards = existingWildcardImports
                        .Where(wildcard => HasUsedClassesInPackage(GetWildcardPackage(wildcard)))
                        .ToList();
                    
                    var allImports = relevantWildcards.Concat(uncoveredClasses).OrderBy(path => path);
                    finalImportList.AddRange(allImports);
                }
                else
                {
                    // Maintain original order for existing imports, add new ones at bottom
                    foreach (var existingPath in existingImportPaths)
                    {
                        if (IsWildcardImport(existingPath))
                        {
                            // Keep wildcard if it covers any used classes
                            if (HasUsedClassesInPackage(GetWildcardPackage(existingPath)))
                            {
                                finalImportList.Add(existingPath);
                            }
                        }
                        else if (uncoveredClasses.Contains(existingPath))
                        {
                            // Keep explicit import if it's still needed and not covered by wildcard
                            finalImportList.Add(existingPath);
                        }
                    }
                    
                    // Add new uncovered classes at the bottom
                    foreach (var uncoveredClass in uncoveredClasses)
                    {
                        if (!existingImportPaths.Contains(uncoveredClass))
                        {
                            finalImportList.Add(uncoveredClass);
                        }
                    }
                }
            }
            else if (SortImportsAlphabetically)
            {
                // Sort all imports alphabetically (existing + new)
                finalImportList = usedClassPaths
                    .OrderBy(path => path)
                    .ToList();
            }
            else
            {
                // Maintain original order and add new imports at the bottom
                finalImportList = new List<string>();
                
                // Add existing imports in their original order
                foreach (var existingPath in existingImportPaths)
                {
                    if (usedClassPaths.Contains(existingPath))
                    {
                        finalImportList.Add(existingPath);
                    }
                }
                
                // Add new imports at the bottom (those not in existing imports)
                foreach (var usedPath in usedClassPaths)
                {
                    if (!existingImportPaths.Contains(usedPath))
                    {
                        finalImportList.Add(usedPath);
                    }
                }
            }

            // Generate explicit import for each class path
            foreach (var classPath in finalImportList)
            {
                newImports.AppendLine($"import {classPath};");
            }

            // Trim trailing newlines to prevent accumulation of blank lines
            string imports = newImports.ToString().TrimEnd();

            if (importsBlockContext?.importDeclaration().Length == 0)
            {
                imports += "\r\n\r\n";
            }

            if (importsBlockContext != null)
            {
                // Replace the existing imports block
                ReplaceNode(importsBlockContext, imports, "Resolve imports");
            }
            else
            {
                // No existing imports block, so add one at the beginning of the program
                var firstChild = context.GetChild(0);
                if (firstChild != null)
                {
                    // Check if firstChild is a parser rule context
                    if (firstChild is ParserRuleContext firstChildContext)
                    {
                        // Add exactly two newlines after imports (consistent spacing)
                        string insertText = imports + Environment.NewLine + Environment.NewLine;
                        
                        // Check if the first node already contains imports to avoid adding excessive spacing
                        string? firstNodeText = GetOriginalText(firstChildContext);
                        if (firstNodeText != null && firstNodeText.TrimStart().StartsWith("import "))
                        {
                            // If first node already has imports, don't add extra newlines
                            insertText = imports + Environment.NewLine;
                        }
                        
                        InsertBefore(firstChildContext, insertText, "Add missing imports");
                    }
                    else
                    {
                        // Fall back to using InsertText if the cast fails
                        InsertText(context.Start.StartIndex,
                            imports + Environment.NewLine + Environment.NewLine,
                            "Add missing imports");
                    }
                }
                else
                {
                    // Empty program, so just add the imports at the start
                    InsertText(context.Start.StartIndex,
                        imports + Environment.NewLine + Environment.NewLine,
                        "Add missing imports");
                }
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
        /// Checks if a class path is covered by any existing wildcard imports
        /// </summary>
        private bool IsClassCoveredByWildcard(string classPath)
        {
            if (!PreserveWildcardImports || existingWildcardImports.Count == 0)
                return false;

            string classPackage = GetPackageFromClassPath(classPath);
            
            foreach (var wildcardImport in existingWildcardImports)
            {
                string wildcardPackage = GetWildcardPackage(wildcardImport);
                if (classPackage.Equals(wildcardPackage, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
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
