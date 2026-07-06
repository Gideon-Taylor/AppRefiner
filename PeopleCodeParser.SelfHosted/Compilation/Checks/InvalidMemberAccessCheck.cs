using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Validates that method calls and property accesses target members that actually exist
/// on the resolved type. Works for builtin object types (via PeopleCodeTypeDatabase)
/// and application classes / interfaces (walking the inheritance chain via the resolver).
///
/// Ported from AppRefiner's InvalidMemberAccess styler. Type inference is a documented
/// precondition of <see cref="CompileChecker.Check"/>: every MemberAccessNode visited
/// here is expected to already carry TypeInferenceVisitor results. When the target type
/// cannot be resolved (Unknown / Any / null) the access is silently skipped to avoid
/// false positives.
///
/// Divergence from the styler: no per-instance member-existence cache. Checks are
/// constructed fresh per Check() call, so an instance cache never outlives one run, and
/// a static cache would re-introduce shared mutable state across concurrent runs. The
/// resolver's own TypeCache (GetTypeMetadata) and the static PeopleCodeTypeDatabase
/// already cache the expensive lookups; re-walking a cached chain is cheap.
/// </summary>
public sealed class InvalidMemberAccessCheck : CompileCheckBase
{
    /// <summary>
    /// AppClass member resolution uses the resolver, but the check degrades gracefully
    /// (builtin validation still runs) when none is available.
    /// </summary>
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not MemberAccessNode m)
            return;

        // Dynamic member access (obj."name") cannot be validated statically
        if (m.IsDynamic) return;

        var targetType = m.Target.GetInferredType();
        if (targetType == null) return;

        // Types we cannot meaningfully validate against
        if (targetType.Kind is TypeKind.Unknown or TypeKind.Any or TypeKind.Invalid
            or TypeKind.Void or TypeKind.Primitive or TypeKind.Reference)
            return;

        // Union return types cannot be validated reliably (member may exist on
        // some constituent types but not others)
        if (targetType is UnionReturnTypeInfo) return;

        // Explicit method call vs property / implicit access
        bool isMethodCall = m.Parent is FunctionCallNode fc && ReferenceEquals(fc.Function, m);

        // Record property access is a dynamic field lookup — only validate method calls on Records
        if (targetType is RecordTypeInfo && !isMethodCall) return;

        if (targetType.PeopleCodeType is PeopleCodeType.Record or PeopleCodeType.Row && !isMethodCall)
            return;

        if (!MemberExists(targetType, m.MemberName, isMethodCall, ctx))
        {
            string kind = isMethodCall ? "method" : "property";
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.InvalidMemberAccess,
                DiagnosticSeverity.Error,
                m.MemberNameSpan,
                $"'{m.MemberName}' is not a known {kind} on '{DisplayName(targetType)}'"));
        }
    }

    #region Member existence checks

    private static bool MemberExists(TypeInfo type, string name, bool isMethodCall, CompileCheckContext ctx)
    {
        return type.Kind switch
        {
            TypeKind.BuiltinObject => BuiltinHasMember(type.PeopleCodeType?.GetTypeName(), name, isMethodCall),
            TypeKind.Array => BuiltinHasMember("array", name, isMethodCall),
            TypeKind.AppClass or TypeKind.Interface => AppClassHasMember(type as AppClassTypeInfo, name, isMethodCall, ctx),
            _ => true   // Unhandled kinds assumed valid
        };
    }

    private static bool BuiltinHasMember(string? typeName, string memberName, bool isMethodCall)
    {
        if (string.IsNullOrEmpty(typeName)) return true;

        return isMethodCall
            ? PeopleCodeTypeDatabase.GetMethod(typeName, memberName) != null
            : PeopleCodeTypeDatabase.GetProperty(typeName, memberName) != null;
    }

    /// <summary>
    /// Walks the inheritance chain for an AppClass / Interface, checking Methods,
    /// Properties, and InstanceVariables at each level.  Falls through to a builtin
    /// member lookup when the chain terminates at a builtin base type.
    ///
    /// The "self" level (the class currently open in the editor) resolves from the live
    /// in-editor metadata (<see cref="CompileCheckContext.SelfMetadata"/>) when available,
    /// so members added but not yet saved are recognized; every other level comes from the
    /// DB-backed resolver. This mirrors the self special-case in
    /// TypeInferenceVisitor.LookupMethodInInheritanceChain and keeps this check in agreement
    /// with the inferred types it consumes.
    ///
    /// Returns <c>true</c> (assume valid) whenever metadata cannot be resolved,
    /// to avoid false positives against classes that are simply not yet loaded or
    /// are defined in another module.
    /// </summary>
    private static bool AppClassHasMember(AppClassTypeInfo? appClass, string memberName, bool isMethodCall, CompileCheckContext ctx)
    {
        if (appClass == null) return true;

        var resolver = ctx.Resolver;
        var selfMetadata = ctx.SelfMetadata;

        // Nothing to validate against: no DB resolver and no live self metadata.
        if (resolver == null && selfMetadata == null) return true;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = appClass.QualifiedName;

        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current)) break;   // Circular inheritance guard

            // Self level → live in-editor metadata (recognizes unsaved members); every
            // other level → DB-backed resolver. Only the source of each level's metadata
            // changes; the chain walk below is identical for both.
            TypeMetadata? metadata =
                selfMetadata != null && current.Equals(selfMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase)
                    ? selfMetadata
                    : resolver?.GetTypeMetadata(current);
            if (metadata == null) return true;  // Unresolvable class — assume valid

            if (isMethodCall)
            {
                if (metadata.Methods.ContainsKey(memberName)) return true;
            }
            else
            {
                if (metadata.Properties.ContainsKey(memberName)) return true;
                // Instance variables are keyed by their declared name, which includes the
                // leading '&' (e.g. "&CVProduct"), but they are accessed as a property via
                // %This.CVProduct without it. Prepend '&' to match — this mirrors how
                // TypeInferenceVisitor resolves the same access (it looks up $"&{name}").
                if (metadata.InstanceVariables.ContainsKey("&" + memberName)) return true;
            }

            // Chain terminates at a builtin base — delegate to builtin lookup
            if (metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
                return BuiltinHasMember(metadata.BuiltinBaseType.Value.GetTypeName(), memberName, isMethodCall);

            // Advance: base class takes priority, fall back to implemented interface
            current = !string.IsNullOrEmpty(metadata.BaseClassName)
                ? metadata.BaseClassName
                : metadata.InterfaceName;
        }

        return false;   // Member not found anywhere in the chain
    }

    #endregion

    private static string DisplayName(TypeInfo type) => type switch
    {
        RecordTypeInfo rec => rec.RecordName != null ? $"Record({rec.RecordName})" : "Record",
        FieldTypeInfo fld => fld.RecordName != null ? $"Field({fld.RecordName}.{fld.FieldName})" : "Field",
        AppClassTypeInfo app => app.QualifiedName ?? app.Name,
        _ => type.Name
    };
}
