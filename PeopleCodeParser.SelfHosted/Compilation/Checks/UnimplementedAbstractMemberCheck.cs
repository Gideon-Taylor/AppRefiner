using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Inference;
using System.Text;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Flags a derived application class whose base-class/interface hierarchy declares
/// abstract members the class never implements. Ported from AppRefiner's
/// UnimplementedAbstractMembersStyler.
///
/// Resolver re-expression: the styler fetched each base type's SOURCE via the
/// DataManager and re-parsed it. This check instead reads the abstract/concrete member
/// signature sets that <see cref="Visitors.TypeMetadataBuilder"/> now attaches to
/// <see cref="TypeMetadata"/> (AbstractMemberSignatures / ConcreteMemberSignatures),
/// walking the hierarchy through the <see cref="PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver"/>.
/// The signature scheme ("M:{name}({paramCount})" / "P:{name}") is owned by
/// <see cref="TypeMetadata.MethodSignature"/> / <see cref="TypeMetadata.PropertySignature"/>,
/// which both the builder and this check use, so AST-derived and metadata-derived
/// signatures cannot drift.
///
/// Walk semantics match the styler's CollectAbstractMembers recursion: at each level,
/// first record that level's abstract requirements not yet satisfied, THEN add its
/// concrete members to the implemented set (so a mid-hierarchy class satisfies
/// requirements from levels above it, but not its own). An unresolvable type stops the
/// walk but keeps requirements already collected from resolvable levels — same as the
/// styler when a base class's source was unavailable.
///
/// Requirement is Optional: without a resolver the check reports nothing.
/// </summary>
public sealed class UnimplementedAbstractMemberCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        if (ctx.Resolver == null || appClass.BaseType == null)
            return;

        // Signatures the derived class provides, matching the styler's
        // GetImplementedSignatures: every non-constructor method (a declaration in the
        // class header counts) and every non-abstract property.
        var implemented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in appClass.Methods)
        {
            if (!IsConstructor(method, appClass.Name))
                implemented.Add(TypeMetadata.MethodSignature(method.Name, method.Parameters.Count));
        }
        foreach (var property in appClass.Properties)
        {
            if (!property.IsAbstract)
                implemented.Add(TypeMetadata.PropertySignature(property.Name));
        }

        // Walk the base hierarchy collecting unmet abstract requirements (ordered,
        // deduplicated by signature — first occurrence wins, as in the styler's TryAdd).
        var unimplemented = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = appClass.BaseType.TypeName;

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            // Unresolvable type: stop the walk but keep requirements already collected
            // from resolvable levels (a deeper base can never retract a requirement a
            // shallower level failed to satisfy) — same behavior as the styler when a
            // base class's source was unavailable.
            var meta = ctx.Resolver.GetTypeMetadata(current);
            if (meta == null)
                break;

            foreach (var signature in meta.AbstractMemberSignatures)
            {
                if (!implemented.Contains(signature) && seen.Add(signature))
                    unimplemented.Add(signature);
            }

            // Concrete members of this level satisfy requirements from levels above it.
            foreach (var signature in meta.ConcreteMemberSignatures)
                implemented.Add(signature);

            // Base class takes priority; fall back to the implemented interface.
            current = !string.IsNullOrEmpty(meta.BaseClassName) ? meta.BaseClassName : meta.InterfaceName;
        }

        if (unimplemented.Count == 0)
            return;

        // One aggregated diagnostic on the base type span, methods grouped before
        // properties — same tooltip shape as the old styler.
        var tooltip = new StringBuilder("Missing implementations:");
        foreach (var signature in unimplemented)
        {
            if (signature.StartsWith("M:", StringComparison.Ordinal))
                tooltip.Append($"\n - Method: {MethodNameOf(signature)}");
        }
        foreach (var signature in unimplemented)
        {
            if (signature.StartsWith("P:", StringComparison.Ordinal))
                tooltip.Append($"\n - Property: {signature[2..]}");
        }

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UnimplementedAbstractMember,
            DiagnosticSeverity.Error,
            appClass.BaseType.SourceSpan,
            tooltip.ToString()));
    }

    /// <summary>
    /// Extracts the method name from an "M:{name}({paramCount})" signature.
    /// </summary>
    private static string MethodNameOf(string signature)
    {
        int open = signature.IndexOf('(');
        return open > 2 ? signature[2..open] : signature[2..];
    }

    private static bool IsConstructor(MethodNode method, string className)
        => string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);
}
