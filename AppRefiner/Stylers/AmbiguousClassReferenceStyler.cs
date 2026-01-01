using AppRefiner.Database;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Highlights unqualified class references that are ambiguous due to multiple imports
    /// providing the same class name from different packages.
    /// </summary>
    /// <remarks>
    /// This styler detects scenarios like:
    /// <code>
    /// import APP_PKG:UI:FormRenderer;
    /// import APP_PKG:DATA:FormRenderer;
    ///
    /// Local FormRenderer &r;  // AMBIGUOUS!
    /// </code>
    ///
    /// Also detects ambiguity from wildcard imports:
    /// <code>
    /// import OPT_CALL:*;
    /// import IB_INST_VER_SYNC_MSG:*;
    ///
    /// Local RequestHandler &c = create RequestHandler();  // AMBIGUOUS if both packages have RequestHandler
    /// </code>
    ///
    /// It provides deferred quick fixes to replace the unqualified reference with
    /// the user's selected fully qualified name (e.g., APP_PKG:UI:FormRenderer).
    ///
    /// Related stylers:
    /// - UnimportedClassStyler: Handles classes that aren't imported at all
    /// - InvalidAppClass: Handles classes that don't exist in the database
    /// </remarks>
    public class AmbiguousClassReferenceStyler : BaseStyler
    {
        public override string Description => "Highlights ambiguous class references where multiple imports provide the same class name";
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        private const uint HIGHLIGHT_COLOR = 0x00FF00; // Green squiggle (distinct from red for unimported)

        private Dictionary<string, List<string>> _classNameToFullPaths = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _importedPackages = new(StringComparer.OrdinalIgnoreCase);  // For wildcard imports

        public override void VisitProgram(ProgramNode node)
        {
            // Initialize state
            _classNameToFullPaths.Clear();
            _importedPackages.Clear();

            // Collect all explicit imports and build mapping of class names to full paths
            foreach (var import in node.Imports)
            {
                if (import.IsWildcard)
                {
                    // Store wildcard packages - we'll query database for their classes
                    var packagePath = import.FullPath.TrimEnd(':', '*');
                    if (!string.IsNullOrEmpty(packagePath))
                    {
                        _importedPackages.Add(packagePath);
                    }
                }
                else
                {
                    // Extract class name and full path from explicit import
                    string? className = import.ClassName;
                    string fullPath = import.FullPath;

                    if (!string.IsNullOrEmpty(className))
                    {
                        // Add to mapping - class name may map to multiple full paths
                        if (!_classNameToFullPaths.ContainsKey(className))
                        {
                            _classNameToFullPaths[className] = new List<string>();
                        }

                        // Avoid duplicates (same import appearing multiple times)
                        if (!_classNameToFullPaths[className].Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                        {
                            _classNameToFullPaths[className].Add(fullPath);
                        }
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
                            // Build full path for this class in the wildcard package
                            string fullPath = $"{packagePath}:{className}";

                            // Add to mapping
                            if (!_classNameToFullPaths.ContainsKey(className))
                            {
                                _classNameToFullPaths[className] = new List<string>();
                            }

                            if (!_classNameToFullPaths[className].Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                            {
                                _classNameToFullPaths[className].Add(fullPath);
                            }
                        }
                        Debug.Log($"AmbiguousClassReferenceStyler: Loaded {classesInPackage.Count} classes from wildcard import '{packagePath}:*'");
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"AmbiguousClassReferenceStyler: Error loading classes for package '{packagePath}': {ex.Message}");
                    }
                }
            }

            // Continue traversal to visit all nodes
            base.VisitProgram(node);
        }

        public override void VisitObjectCreation(ObjectCreationNode node)
        {
            // Check object creation: create MyClass()
            if (node.Type is AppClassTypeNode appClassType)
            {
                CheckForAmbiguity(appClassType);
            }

            base.VisitObjectCreation(node);
        }

        public override void VisitProgramVariable(ProgramVariableNode node)
        {
            // Check variable declarations: Local MyClass &var;
            if (node.Type is AppClassTypeNode appClassType)
            {
                CheckForAmbiguity(appClassType);
            }

            base.VisitProgramVariable(node);
        }

        public override void VisitAppClass(AppClassNode node)
        {
            // Check base class in extends clause: class MyClass extends BaseClass
            if (node.BaseType is AppClassTypeNode baseType)
            {
                CheckForAmbiguity(baseType);
            }

            base.VisitAppClass(node);
        }

        /// <summary>
        /// Checks if an AppClassTypeNode reference is ambiguous and registers a deferred quick fix if so.
        /// </summary>
        private void CheckForAmbiguity(AppClassTypeNode appClassType)
        {
            // Only check unqualified references (no package path)
            // Qualified references like "PKG:MyClass" are never ambiguous
            if (appClassType.PackagePath.Count > 0)
            {
                return;
            }

            string className = appClassType.ClassName;

            // Check if multiple imports provide this class name
            if (!_classNameToFullPaths.TryGetValue(className, out var fullPaths))
            {
                // Class not imported - this is UnimportedClassStyler's job, not ours
                return;
            }

            if (fullPaths.Count <= 1)
            {
                // Only one import provides this class - no ambiguity
                return;
            }

            // AMBIGUOUS! Multiple imports provide this class name
            // Register deferred quick fix that will be resolved when user presses Ctrl+.
            AddIndicatorWithDeferredQuickFix(
                appClassType.SourceSpan,
                IndicatorType.SQUIGGLE,
                HIGHLIGHT_COLOR,
                $"Ambiguous reference: '{className}' is imported from {fullPaths.Count} different packages",
                ResolveQualificationOptions,
                new AmbiguityContext
                {
                    ClassName = className,
                    ConflictingPaths = fullPaths
                }
            );
        }

        /// <summary>
        /// Context class passed to the deferred quick fix resolver.
        /// </summary>
        private class AmbiguityContext
        {
            public string ClassName { get; set; } = string.Empty;
            public List<string> ConflictingPaths { get; set; } = new();
        }

        /// <summary>
        /// Deferred resolver: Generates quick fix options for each conflicting import path.
        /// Invoked only when user presses Ctrl+. on the indicator.
        /// </summary>
        private List<(Type RefactorClass, string Description)> ResolveQualificationOptions(
            ScintillaEditor editor,
            int position,
            object? context)
        {
            if (context is not AmbiguityContext ambiguityContext)
            {
                Debug.Log("AmbiguousClassReferenceStyler: Invalid context type");
                return new();
            }

            var quickFixes = new List<(Type, string)>();

            // Create one quick fix option per conflicting path
            foreach (var fullPath in ambiguityContext.ConflictingPaths)
            {
                quickFixes.Add((
                    typeof(ReplaceWithQualifiedClassNameQuickFix),
                    $"Use {fullPath}"
                ));
            }

            Debug.Log($"AmbiguousClassReferenceStyler: Generated {quickFixes.Count} qualification options for '{ambiguityContext.ClassName}'");
            return quickFixes;
        }
    }
}
