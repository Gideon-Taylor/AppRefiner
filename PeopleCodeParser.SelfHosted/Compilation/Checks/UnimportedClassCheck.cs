using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports application class references whose class name is not covered by any import.
/// Ported from AppRefiner's UnimportedClassStyler: explicit imports contribute their
/// last path segment; wildcard imports (<c>PKG:*</c>) are expanded via
/// <c>ITypeMetadataResolver.GetClassesInPackage</c> (the resolver equivalent of the old
/// DataManager.GetAllClassesForPackage) when a resolver is available.
///
/// Behavioral notes preserved from the styler:
/// - Matching is by SIMPLE class name only (case-insensitive): any import ending in
///   "Bar" vouches for every reference to a class named Bar, including fully-qualified
///   references to a different package's Bar.
/// - A fully-qualified reference (e.g. <c>Local PKG:Foo:Bar &amp;x;</c>) IS flagged when
///   nothing imports "Bar" — PeopleCode requires the import either way.
/// - Without a resolver, wildcard imports vouch for nothing (mirrors the styler's
///   DB-disconnected behavior).
///
/// FixContext = the simple class name (string) — AppRefiner's deferred quick-fix
/// resolver uses it to query candidate packages at Ctrl+. time.
/// </summary>
public sealed class UnimportedClassCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    private readonly HashSet<string> _importedClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _importedPackages = new(StringComparer.OrdinalIgnoreCase);

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        // ProgramNode is dispatched first every run: per-run reset + import collection.
        if (node is ProgramNode program)
        {
            RebuildImportSets(program, ctx);
            return;
        }

        switch (node)
        {
            case ObjectCreationNode creation:
                CheckType(creation.Type, sink);
                break;
            case LocalVariableDeclarationNode local:
                CheckType(local.Type, sink);
                break;
            case LocalVariableDeclarationWithAssignmentNode localAssign:
                CheckType(localAssign.Type, sink);
                break;
            case ProgramVariableNode programVar:
                CheckType(programVar.Type, sink);
                break;
        }
    }

    private void RebuildImportSets(ProgramNode program, CompileCheckContext ctx)
    {
        _importedClasses.Clear();
        _importedPackages.Clear();

        foreach (var import in program.Imports)
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
                // Explicit import - last segment is the class name
                var parts = import.FullPath.Split(':');
                if (parts.Length > 0)
                {
                    _importedClasses.Add(parts[^1]);
                }
            }
        }

        // Expand wildcard imports through the resolver when one is available.
        if (ctx.Resolver != null && _importedPackages.Count > 0)
        {
            foreach (var packagePath in _importedPackages)
            {
                foreach (var className in ctx.Resolver.GetClassesInPackage(packagePath))
                {
                    _importedClasses.Add(className);
                }
            }
        }
    }

    private void CheckType(TypeNode? type, IDiagnosticSink sink)
    {
        if (type is not AppClassTypeNode appClassType)
            return;

        var className = appClassType.ClassName;
        if (_importedClasses.Contains(className))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnimportedClass,
            DiagnosticSeverity.Error,
            appClassType.SourceSpan,
            $"Class '{className}' is not imported",
            className));
    }
}
