using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Compilation;

namespace AppRefiner.Services;

/// <summary>
/// Maps a library CompileDiagnostic to AppRefiner quick-fix entries. Keeps refactor
/// references out of the parser library. Some codes produce deferred resolvers
/// (DB-query-on-Ctrl+.), added in later phases.
/// </summary>
internal static class CompileDiagnosticQuickFixMap
{
    public static List<QuickFixEntry> GetQuickFixes(CompileDiagnostic d)
    {
        switch (d.Code)
        {
            case DiagnosticCode.TypeError when d.FixContext is Refactors.QuickFixes.AssignToVariableContext ctx:
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.AssignToNewVariable),
                        "Assign result to a new local variable", ctx)
                };
            case DiagnosticCode.UndefinedVariable when d.FixContext is UndefinedForLoopIteratorFix:
                // Undefined for-loop iterator: offer the static DeclareForLoopIterator fix
                // (re-derives the iterator from the cursor/AST, no context needed), matching
                // the surviving non-class UndefinedVariables styler. Plain undefined
                // variables carry a null FixContext and fall through to the default (no fix).
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.DeclareForLoopIterator), "Declare iterator")
                };
            case DiagnosticCode.ClassNameMismatch:
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.CorrectClassName), "Correct class name")
                };
            case DiagnosticCode.MissingMethodImplementation:
                // Static fix: ImplementMissingMethod re-derives the target method from the
                // cursor/AST, so no FixContext is needed.
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.ImplementMissingMethod), "Implement missing method")
                };
            case DiagnosticCode.UnimplementedAbstractMember:
                // Static fix: ImplementAbstractMembers re-derives the missing members
                // from the editor's AST/DB, so no FixContext is needed.
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.ImplementAbstractMembers), "Implement missing abstract members")
                };
            case DiagnosticCode.MissingConstructor:
                // Static fix: GenerateBaseConstructor re-derives the target class and
                // base constructor from the AST/DB, so no FixContext is needed.
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.QuickFixes.GenerateBaseConstructor), "Generate missing constructor")
                };
            case DiagnosticCode.UndeclaredFunction when d.FixContext is UndeclaredFunctionForwardRefFix forwardRef:
                // Forward reference: the implementation exists below the call, so the fix
                // is static (no DB needed). MoveFunctionAbove reads the impl name from
                // editor.QuickFixContext; the description was computed at check time.
                return new List<QuickFixEntry>
                {
                    new(typeof(Refactors.MoveFunctionAbove), forwardRef.MoveDescription, forwardRef.ImplName)
                };
            case DiagnosticCode.AmbiguousClassReference when d.FixContext is AmbiguousClassReferenceFix ambiguity:
            {
                // Conflicting paths were resolved at check time, so this stays a static
                // (non-deferred) mapping: one option per candidate path. The refactor
                // reads editor.QuickFixContext as a string and strips an optional
                // "Use " prefix, so pass the bare path as the rich context payload.
                var fixes = new List<QuickFixEntry>();
                foreach (var path in ambiguity.ConflictingPaths)
                {
                    fixes.Add(new(
                        typeof(Refactors.QuickFixes.ReplaceWithQualifiedClassNameQuickFix),
                        $"Use {path}",
                        path));
                }
                return fixes;
            }
            default:
                return new List<QuickFixEntry>();
        }
    }

    /// <summary>
    /// True when the diagnostic's quick fixes must be resolved lazily (DB/cache query at
    /// Ctrl+. time) via <see cref="GetDeferredResolver"/> instead of the static list
    /// above. Takes the full diagnostic (not just the code) because one code can carry
    /// both static and deferred fix shapes: UndeclaredFunction is deferred only for the
    /// unknown-function FixContext — its forward-reference FixContext maps statically.
    /// </summary>
    public static bool HasDeferredResolver(CompileDiagnostic d)
        => d.Code == DiagnosticCode.UnimportedClass
           || (d.Code == DiagnosticCode.UndeclaredFunction && d.FixContext is UndeclaredFunctionUnknownFix);

    /// <summary>
    /// Returns the deferred resolver for a diagnostic. Only valid when
    /// <see cref="HasDeferredResolver"/> is true. The resolver receives the diagnostic's
    /// FixContext as its context argument (CompilerErrorsStyler passes it through
    /// AddIndicatorWithDeferredQuickFix), so payload-record contexts are unwrapped to
    /// the string the ported resolver expects.
    /// </summary>
    public static Func<ScintillaEditor, int, object?, List<QuickFixEntry>> GetDeferredResolver(CompileDiagnostic d)
        => d.Code switch
        {
            DiagnosticCode.UnimportedClass => GetImportOptionsResolver,
            DiagnosticCode.UndeclaredFunction when d.FixContext is UndeclaredFunctionUnknownFix =>
                static (editor, position, context) => ResolveUnknownFunctionFixes(
                    editor, position, (context as UndeclaredFunctionUnknownFix)?.FunctionName),
            _ => throw new ArgumentOutOfRangeException(nameof(d), d.Code, "No deferred resolver for this diagnostic"),
        };

    /// <summary>
    /// Deferred resolver for UnimportedClass (ported from UnimportedClassStyler):
    /// queries the database for all packages containing the class and offers one
    /// AddImportQuickFix per package. Invoked only when the user presses Ctrl+.
    /// The context is the class-name string attached by the library check (FixContext).
    /// </summary>
    private static List<QuickFixEntry> GetImportOptionsResolver(
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
        var prioritized = PrioritizeByExistingImports(editor, packagePaths);
        Debug.Log($"Found {prioritized.Count} packages for class {className}");
        // Generate QuickFix options - one per package
        var quickFixes = new List<QuickFixEntry>();
        foreach (var packagePath in prioritized)
        {
            // Description shows the full path: "Import APP_PACKAGE:SUBPKG:CriteriaUI"
            quickFixes.Add((
                typeof(Refactors.QuickFixes.AddImportQuickFix),
                $"Import {packagePath}"
            ));
        }

        Debug.Log($"Generated {quickFixes.Count} import options for class {className}");
        return quickFixes;
    }

    private const int MAX_IMPORT_OPTIONS = 10;

    /// <summary>
    /// Deferred resolver for UndeclaredFunction/unknown (ported from
    /// UndeclaredFunctionStyler.ResolveUnknownFunctionFixes, runs at Ctrl+. time):
    /// import options from the local function cache — usable without a live DB
    /// connection — plus a pre-filled search dialog entry when connected. Empty
    /// result = squiggle only, no popup.
    /// </summary>
    private static List<QuickFixEntry> ResolveUnknownFunctionFixes(ScintillaEditor editor, int position, object? context)
    {
        var fixes = new List<QuickFixEntry>();
        if (context is not string functionName || string.IsNullOrEmpty(functionName))
            return fixes;

        var cache = MainForm.FunctionCache;
        var process = editor.AppDesignerProcess;
        if (cache != null && process != null)
        {
            var matches = cache.SearchFunctionCache(process, functionName)
                .Where(r => string.Equals(r.FunctionName, functionName, StringComparison.OrdinalIgnoreCase))
                .Take(MAX_IMPORT_OPTIONS)
                .ToList();

            // The event is omitted from descriptions (shared functions live almost
            // exclusively in FieldFormula, so it's noise) — EXCEPT when the same
            // REC.FIELD appears more than once in the result set, where the event is
            // the only disambiguator (and the description string is the selection key)
            var recFieldCounts = matches
                .Select(RecFieldOf)
                .GroupBy(rf => rf, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var match in matches)
            {
                var parts = match.FunctionPath.Split(':');
                string recField = RecFieldOf(match);
                string source = parts.Length >= 3 && recFieldCounts[recField] > 1
                    ? $"{recField} ({parts[2]})"
                    : recField;
                fixes.Add(new(typeof(Refactors.QuickFixes.DeclareFunctionQuickFix), $"Import '{match.FunctionName}' from {source}", match));
            }
        }

        if (editor.DataManager != null && editor.DataManager.IsConnected)
        {
            fixes.Add(new(typeof(Refactors.QuickFixes.OpenDeclareFunctionDialogQuickFix), $"Search for function '{functionName}'...", functionName));
        }

        return fixes;
    }

    /// <summary>
    /// REC.FIELD portion of a cache result's path (format REC:FIELD:EVENT);
    /// falls back to the raw path when it doesn't have the expected shape.
    /// </summary>
    private static string RecFieldOf(FunctionSearchResult match)
    {
        var parts = match.FunctionPath.Split(':');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : match.FunctionPath;
    }

    private static List<string> PrioritizeByExistingImports(ScintillaEditor editor, List<string> packagePaths)
    {
        // Prioritize packages whose base package is already imported
        // Example: If "APP_PACKAGE:*" is imported, prioritize "APP_PACKAGE:CriteriaUI"
        // The old styler read its VisitProgram-time field; a deferred resolver has no
        // per-run state, so re-derive the wildcard packages from the current program.
        var importedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imports = editor.GetParsedProgram()?.Imports;
        if (imports != null)
        {
            foreach (var import in imports)
            {
                if (import.FullPath.EndsWith(":*"))
                {
                    var packagePath = import.FullPath.TrimEnd(':', '*');
                    if (!string.IsNullOrEmpty(packagePath))
                    {
                        importedPackages.Add(packagePath);
                    }
                }
            }
        }

        var prioritized = new List<string>();
        var deprioritized = new List<string>();

        foreach (var path in packagePaths)
        {
            var basePackage = GetBasePackage(path);
            if (importedPackages.Contains(basePackage))
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

    private static string GetBasePackage(string fullPath)
    {
        // Extract base package from "APP_PACKAGE:SUBPKG:CriteriaUI" -> "APP_PACKAGE"
        var parts = fullPath.Split(':');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }
}
