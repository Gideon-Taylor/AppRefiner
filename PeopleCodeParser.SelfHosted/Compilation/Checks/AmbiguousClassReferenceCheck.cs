using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports unqualified application class references whose simple name is provided by
/// two or more imports (explicit or wildcard-expanded), making the reference ambiguous.
/// Ported from AppRefiner's AmbiguousClassReferenceStyler.
///
/// Behavioral notes preserved from the styler:
/// - Only UNQUALIFIED references (empty PackagePath) can be ambiguous; qualified
///   references are never flagged.
/// - Exactly three contexts are checked (matching the styler — deliberately not all
///   AppClassTypeNodes): object creation types, program variable (Global/Component/
///   Instance) declaration types, and the base type of an <c>extends</c> clause.
/// - Wildcard imports (<c>PKG:*</c>) are expanded via
///   <c>ITypeMetadataResolver.GetClassesInPackage</c> (the resolver equivalent of the
///   old DataManager.GetAllClassesForPackage) when a resolver is available; without
///   one, wildcards contribute nothing (mirrors the styler's DB-disconnected behavior).
/// - A class name that is not imported at all is UnimportedClassCheck's finding, not ours.
///
/// FixContext = <see cref="AmbiguousClassReferenceFix"/> (class name + all conflicting
/// full paths) — AppRefiner maps it to one ReplaceWithQualifiedClassName quick fix per path.
/// </summary>
public sealed class AmbiguousClassReferenceCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    private readonly Dictionary<string, List<string>> _classNameToFullPaths = new(StringComparer.OrdinalIgnoreCase);

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        // ProgramNode is dispatched first every run: per-run reset + import collection.
        if (node is ProgramNode program)
        {
            RebuildClassNameMap(program, ctx);
            return;
        }

        switch (node)
        {
            case ObjectCreationNode creation:
                CheckForAmbiguity(creation.Type as AppClassTypeNode, sink);
                break;
            case ProgramVariableNode programVar:
                CheckForAmbiguity(programVar.Type as AppClassTypeNode, sink);
                break;
            case AppClassNode appClass:
                CheckForAmbiguity(appClass.BaseType as AppClassTypeNode, sink);
                break;
        }
    }

    private void RebuildClassNameMap(ProgramNode program, CompileCheckContext ctx)
    {
        _classNameToFullPaths.Clear();
        var wildcardPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var import in program.Imports)
        {
            if (import.IsWildcard)
            {
                // Wildcard import - remember the package; expanded via the resolver below.
                var packagePath = import.FullPath.TrimEnd(':', '*');
                if (!string.IsNullOrEmpty(packagePath))
                {
                    wildcardPackages.Add(packagePath);
                }
            }
            else if (!string.IsNullOrEmpty(import.ClassName))
            {
                // Explicit import - class name may map to multiple full paths.
                AddPath(import.ClassName, import.FullPath);
            }
        }

        // Expand wildcard imports through the resolver when one is available.
        if (ctx.Resolver != null && wildcardPackages.Count > 0)
        {
            foreach (var packagePath in wildcardPackages)
            {
                foreach (var className in ctx.Resolver.GetClassesInPackage(packagePath))
                {
                    AddPath(className, $"{packagePath}:{className}");
                }
            }
        }
    }

    private void AddPath(string className, string fullPath)
    {
        if (!_classNameToFullPaths.TryGetValue(className, out var paths))
        {
            paths = new List<string>();
            _classNameToFullPaths[className] = paths;
        }

        // Avoid duplicates (same import appearing multiple times).
        if (!paths.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(fullPath);
        }
    }

    private void CheckForAmbiguity(AppClassTypeNode? appClassType, IDiagnosticSink sink)
    {
        // Only unqualified references (no package path) can be ambiguous.
        if (appClassType == null || appClassType.PackagePath.Count > 0)
            return;

        var className = appClassType.ClassName;
        if (!_classNameToFullPaths.TryGetValue(className, out var fullPaths) || fullPaths.Count <= 1)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.AmbiguousClassReference,
            DiagnosticSeverity.Error,
            appClassType.SourceSpan,
            $"Ambiguous reference: '{className}' is imported from {fullPaths.Count} different packages",
            new AmbiguousClassReferenceFix(className, fullPaths.ToArray())));
    }
}
