using AppRefiner.Database;
using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Inference;
using System.Text;

namespace AppRefiner.Stylers;

/// <summary>
/// Flags an application class whose base-class/interface hierarchy declares abstract
/// members the class never implements, highlighting the base type with a warning squiggle
/// and offering an "implement missing members" quick fix.
///
/// This is an advisory styler, deliberately NOT part of the unified "Compiler errors"
/// check: an unimplemented abstract member is a code smell worth surfacing, but it does not
/// by itself stop the program from compiling, so it must not trip the compiler-error
/// indicator. Off by default; enable it in styler settings.
///
/// Reads the abstract/concrete member signatures that TypeMetadataBuilder attaches to each
/// class's <see cref="TypeMetadata"/>, walking the hierarchy through the editor's type
/// resolver. The signature scheme ("M:{name}({paramCount})" / "P:{name}") is owned by
/// <see cref="TypeMetadata.MethodSignature"/> / <see cref="TypeMetadata.PropertySignature"/>,
/// so AST-derived and metadata-derived signatures cannot drift.
/// </summary>
public class UnimplementedAbstractMembersStyler : BaseStyler
{
    private const uint WARNING_COLOR = 0xFF00A5FF; // Orange (BGRA) for the advisory squiggle

    public override string Description => "Missing abstract implementations";

    /// <summary>Requires a resolver (hence a database connection) to read base-class metadata.</summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
    }

    public override void VisitAppClass(AppClassNode node)
    {
        var resolver = Editor?.AppDesignerProcess?.TypeResolver;
        if (resolver == null || node.BaseType == null)
        {
            base.VisitAppClass(node);
            return;
        }

        // Signatures the class provides: every non-constructor method (a declaration in the
        // header counts) and every non-abstract property.
        var implemented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in node.Methods)
        {
            if (!string.Equals(method.Name, node.Name, StringComparison.OrdinalIgnoreCase))
                implemented.Add(TypeMetadata.MethodSignature(method.Name, method.Parameters.Count));
        }
        foreach (var property in node.Properties)
        {
            if (!property.IsAbstract)
                implemented.Add(TypeMetadata.PropertySignature(property.Name));
        }

        // Walk the base hierarchy collecting unmet abstract requirements (ordered,
        // deduplicated). At each level: record its abstract requirements not yet satisfied,
        // THEN add its concrete members (so a mid-hierarchy class satisfies requirements from
        // levels above it, not its own). An unresolvable type stops the walk but keeps
        // requirements already collected from resolvable levels.
        var unimplemented = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = node.BaseType.TypeName;

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            var meta = resolver.GetTypeMetadata(current);
            if (meta == null)
                break;

            foreach (var signature in meta.AbstractMemberSignatures)
            {
                if (!implemented.Contains(signature) && seen.Add(signature))
                    unimplemented.Add(signature);
            }

            foreach (var signature in meta.ConcreteMemberSignatures)
                implemented.Add(signature);

            current = !string.IsNullOrEmpty(meta.BaseClassName) ? meta.BaseClassName : meta.InterfaceName;
        }

        if (unimplemented.Count > 0)
        {
            // One aggregated squiggle on the base type, methods grouped before properties.
            var tooltip = new StringBuilder("Missing implementations:");
            foreach (var signature in unimplemented)
            {
                if (signature.StartsWith("M:", StringComparison.Ordinal))
                {
                    int open = signature.IndexOf('(');
                    var name = open > 2 ? signature[2..open] : signature[2..];
                    tooltip.Append($"\n - Method: {name}");
                }
            }
            foreach (var signature in unimplemented)
            {
                if (signature.StartsWith("P:", StringComparison.Ordinal))
                    tooltip.Append($"\n - Property: {signature[2..]}");
            }

            var quickFixes = new List<QuickFixEntry>
            {
                new(typeof(ImplementAbstractMembers), "Implement missing abstract members")
            };

            AddIndicator(node.BaseType.SourceSpan, IndicatorType.SQUIGGLE, WARNING_COLOR,
                tooltip.ToString(), quickFixes);
        }

        base.VisitAppClass(node);
    }
}
