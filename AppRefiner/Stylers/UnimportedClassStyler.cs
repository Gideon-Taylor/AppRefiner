using AppRefiner.Database;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Highlights class names that are not imported and provides QuickFix options to add imports.
    /// Uses deferred QuickFix resolution to query the database only when the user presses Ctrl+.
    /// Handles both explicit imports and wildcard imports by querying the database for classes in wildcard packages.
    /// </summary>
    public class UnimportedClassStyler : BaseStyler
    {
        public override string Description => "Unimported classes";
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        private const uint HIGHLIGHT_COLOR = 0x0000FF; // Red squiggle
        private ProgramNode? _programNode;
        private HashSet<string> _importedClasses = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _importedPackages = new(StringComparer.OrdinalIgnoreCase); // For wildcard imports

        public override void VisitProgram(ProgramNode node)
        {
            _programNode = node;
            _importedClasses.Clear();
            _importedPackages.Clear();

            // Collect all imported classes and packages
            foreach (var import in node.Imports)
            {
                if (import.FullPath.EndsWith(":*"))
                {
                    // Wildcard import - store the package path
                    var packagePath = import.FullPath.TrimEnd(':', '*');
                    if (!string.IsNullOrEmpty(packagePath))
                    {
                        _importedPackages.Add(packagePath);
                    }
                }
                else
                {
                    // Explicit import - extract class name
                    var parts = import.FullPath.Split(':');
                    if (parts.Length > 0)
                    {
                        _importedClasses.Add(parts[^1]); // Last segment is class name
                    }
                }
            }

            // Query database for wildcard imports if available
            if (DataManager != null && DataManager.IsConnected && _importedPackages.Count > 0)
            {
                foreach (var packagePath in _importedPackages)
                {
                    try
                    {
                        var classesInPackage = DataManager.GetAllClassesForPackage(packagePath);
                        foreach (var className in classesInPackage)
                        {
                            _importedClasses.Add(className);
                        }
                        Debug.Log($"UnimportedClassStyler: Loaded {classesInPackage.Count} classes from wildcard import '{packagePath}:*'");
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"UnimportedClassStyler: Error loading classes for package '{packagePath}': {ex.Message}");
                    }
                }
            }

            base.VisitProgram(node);
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            // Check if the class being created is imported
            if (node.Type is AppClassTypeNode appClassType)
            {
                string className = appClassType.ClassName;
                CheckClassImport(className, appClassType.SourceSpan);
            }

            base.VisitObjectCreation(node);
        }

        public override void VisitProgramVariable(ProgramVariableNode node)
        {
            // Check local/instance variable declarations
            CheckVariableTypeImport(node);
            base.VisitProgramVariable(node);
        }

        private void CheckVariableTypeImport(ProgramVariableNode node)
        {
            if (node.Type is AppClassTypeNode appClassType)
            {
                string className = appClassType.ClassName;
                CheckClassImport(className, appClassType.SourceSpan);
            }
        }

        private void CheckClassImport(string className, SourceSpan span)
        {
            // Skip if already imported (either as explicit import or via wildcard)
            // The _importedClasses set includes classes from both explicit imports and wildcard imports
            if (_importedClasses.Contains(className))
                return;

            // Class is not imported - register deferred QuickFix
            AddIndicatorWithDeferredQuickFix(
                span,
                IndicatorType.SQUIGGLE,
                HIGHLIGHT_COLOR,
                $"Class '{className}' is not imported",
                GetImportOptionsResolver,
                className  // Pass class name as context
            );
        }

        /// <summary>
        /// Deferred resolver: Queries database for all packages containing this class.
        /// Invoked only when user presses Ctrl+.
        /// </summary>
        private List<(Type RefactorClass, string Description)> GetImportOptionsResolver(
            ScintillaEditor editor,
            int position,
            object? context)
        {
            var className = context as string;
            if (string.IsNullOrEmpty(className))
                return new();

            var dataManager = editor.DataManager;
            if (dataManager == null || !dataManager.IsConnected)
            {
                Debug.Log($"Database not connected - cannot query packages for class {className}");
                return new();
            }

            // Query database for all packages containing this class
            var packagePaths = dataManager.GetPackagesForClass(className);

            if (packagePaths.Count == 0)
            {
                Debug.Log($"No packages found for class {className}");
                return new();
            }

            // Prioritize packages whose base package is already imported
            var prioritized = PrioritizeByExistingImports(packagePaths);
            Debug.Log($"Found {prioritized.Count} packages for class {className}");
            // Generate QuickFix options - one per package
            var quickFixes = new List<(Type, string)>();
            foreach (var packagePath in prioritized)
            {
                // Description shows the full path: "Import APP_PACKAGE:SUBPKG:CriteriaUI"
                quickFixes.Add((
                    typeof(AddImportQuickFix),
                    $"Import {packagePath}"
                ));
            }

            Debug.Log($"Generated {quickFixes.Count} import options for class {className}");
            return quickFixes;
        }

        private List<string> PrioritizeByExistingImports(List<string> packagePaths)
        {
            // Prioritize packages whose base package is already imported
            // Example: If "APP_PACKAGE:*" is imported, prioritize "APP_PACKAGE:CriteriaUI"

            var prioritized = new List<string>();
            var deprioritized = new List<string>();

            foreach (var path in packagePaths)
            {
                var basePackage = GetBasePackage(path);
                if (_importedPackages.Contains(basePackage))
                {
                    prioritized.Add(path);
                }
                else
                {
                    deprioritized.Add(path);
                }
            }

            prioritized.AddRange(deprioritized);
            return prioritized;
        }

        private string GetBasePackage(string fullPath)
        {
            // Extract base package from "APP_PACKAGE:SUBPKG:CriteriaUI" -> "APP_PACKAGE"
            var parts = fullPath.Split(':');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }
    }
}
