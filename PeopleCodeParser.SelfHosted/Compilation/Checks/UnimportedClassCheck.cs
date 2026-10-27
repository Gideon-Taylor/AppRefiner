using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports application class references whose class name is not covered by any import.
/// Ported from AppRefiner's UnimportedClassStyler: explicit imports contribute their
/// last path segment; wildcard imports (<c>PKG:*</c>) are expanded via
/// <c>ITypeMetadataResolver.GetClassesInPackage</c> (the resolver equivalent of the old
/// DataManager.GetAllClassesForPackage) when a resolver is available.
///
/// Every app class type reference is checked (declarations, parameters, returns, properties,
/// extends/implements, create), except the import statements themselves.
/// - A short reference (<c>Local Bar &amp;x;</c>) is covered by any import of a class named
///   Bar, explicit or through an expanded wildcard. A wildcard import that can't be expanded
///   (no resolver, or the resolver lists no classes, as when the database isn't connected)
///   is assumed to cover every short name, so nothing is reported rather than everything.
/// - A qualified reference (<c>Local PKG:Foo:Bar &amp;x;</c>) needs that exact class
///   imported, or its own package by wildcard (<c>PKG:Foo:*</c>, which doesn't reach
///   sub-packages). Another package's Bar doesn't cover it. No resolver is needed.
/// - A class may name itself, short or qualified, without an import.
///
/// FixContext = the simple class name for a short reference — AppRefiner's deferred
/// quick-fix resolver queries candidate packages at Ctrl+. time — or the full path for a
/// qualified reference, which is the one import that fixes it.
/// </summary>
public sealed class UnimportedClassCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    private readonly HashSet<string> _importedClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _importedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _importedPackages = new(StringComparer.OrdinalIgnoreCase);
    private bool _unexpandedWildcard;
    private string? _ownName;
    private string? _ownQualifiedName;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        // ProgramNode is dispatched first every run: per-run reset + import collection.
        if (node is ProgramNode program)
        {
            RebuildImportSets(program, ctx);
            return;
        }

        if (node is AppClassTypeNode appClassType && appClassType.Parent is not ImportNode)
            CheckType(appClassType, sink);
    }

    private void RebuildImportSets(ProgramNode program, CompileCheckContext ctx)
    {
        _importedClasses.Clear();
        _importedPaths.Clear();
        _importedPackages.Clear();

        // A class may name itself without an import (App Designer qualifies it on save)
        _ownName = program.AppClass?.Name;
        _ownQualifiedName = ctx.SelfMetadata?.QualifiedName;
        if (!string.IsNullOrEmpty(_ownName))
            _importedClasses.Add(_ownName);

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
                    _importedPaths.Add(import.FullPath);
                }
            }
        }

        // Expand wildcard imports through the resolver. A wildcard it can't expand (no resolver,
        // or no classes listed, as when the database isn't connected) may cover any class.
        _unexpandedWildcard = false;
        foreach (var packagePath in _importedPackages)
        {
            var classes = ctx.Resolver?.GetClassesInPackage(packagePath);
            if (classes == null || classes.Count == 0)
            {
                _unexpandedWildcard = true;
                continue;
            }

            foreach (var className in classes)
            {
                _importedClasses.Add(className);
            }
        }
    }

    private void CheckType(AppClassTypeNode appClassType, IDiagnosticSink sink)
    {
        var className = appClassType.ClassName;
        if (appClassType.PackagePath.Count == 0)
        {
            if (_importedClasses.Contains(className) || _unexpandedWildcard)
                return;

            Report(appClassType, $"Class '{className}' is not imported", className, sink);
            return;
        }

        var packagePath = string.Join(":", appClassType.PackagePath);
        var fullPath = $"{packagePath}:{className}";
        if (_importedPaths.Contains(fullPath) || _importedPackages.Contains(packagePath) || IsOwnClass(fullPath, className))
            return;

        Report(appClassType, $"Class '{fullPath}' is not imported", fullPath, sink);
    }

    // Without the program's qualified name, a qualified reference to a class with its own
    // name is given the benefit of the doubt
    private bool IsOwnClass(string fullPath, string className) =>
        !string.IsNullOrEmpty(_ownQualifiedName)
            ? fullPath.Equals(_ownQualifiedName, StringComparison.OrdinalIgnoreCase)
            : className.Equals(_ownName, StringComparison.OrdinalIgnoreCase);

    private static void Report(AppClassTypeNode appClassType, string message, string fixContext, IDiagnosticSink sink) =>
        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnimportedClass,
            DiagnosticSeverity.Error,
            appClassType.SourceSpan,
            message,
            fixContext));
}
