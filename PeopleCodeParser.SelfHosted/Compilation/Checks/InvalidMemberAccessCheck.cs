using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Validates that method calls and property accesses target members that actually exist
/// on the resolved type, and that private/protected members are only accessed from
/// allowed callers (T6). Works for builtin object types (via PeopleCodeTypeDatabase)
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

        // Property access on Record/Row/Grid is a dynamic lookup (field name / record name /
        // column name), resolved at runtime — Grid.COLUMNNAME acts as GetColumn("COLUMNNAME").
        // Only method calls on these types are validated against their builtin members.
        if (targetType.PeopleCodeType is PeopleCodeType.Record or PeopleCodeType.Row or PeopleCodeType.Grid && !isMethodCall)
            return;

        var resolution = ResolveMember(targetType, m.MemberName, isMethodCall, ctx);
        switch (resolution.Kind)
        {
            case ResolutionKind.Skip:
                return;

            case ResolutionKind.NotFound:
            {
                string kind = isMethodCall ? "method" : "property";
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.InvalidMemberAccess,
                    DiagnosticSeverity.Error,
                    m.MemberNameSpan,
                    $"'{m.MemberName}' is not a known {kind} on '{DisplayName(targetType)}'"));
                return;
            }

            case ResolutionKind.Found:
                if (!IsAccessible(resolution.Visibility, resolution.DeclaringTypeName, ctx))
                {
                    var visWord = resolution.Visibility switch
                    {
                        MemberVisibility.Private => "private",
                        MemberVisibility.Protected => "protected",
                        _ => "inaccessible",
                    };
                    var from = GetCallerClassName(ctx) ?? "this program";
                    sink.Report(new CompileDiagnostic(
                        DiagnosticCode.InaccessibleMember,
                        DiagnosticSeverity.Error,
                        m.MemberNameSpan,
                        $"'{m.MemberName}' is {visWord} and not accessible from '{from}'"));
                }
                return;
        }
    }

    #region Member resolution

    private enum ResolutionKind { Skip, NotFound, Found }

    private readonly struct MemberResolution
    {
        public ResolutionKind Kind { get; init; }
        public MemberVisibility Visibility { get; init; }
        public string? DeclaringTypeName { get; init; }

        public static MemberResolution Skip => new() { Kind = ResolutionKind.Skip };
        public static MemberResolution NotFound => new() { Kind = ResolutionKind.NotFound };
        public static MemberResolution Found(MemberVisibility visibility, string? declaringType) =>
            new() { Kind = ResolutionKind.Found, Visibility = visibility, DeclaringTypeName = declaringType };
    }

    private static MemberResolution ResolveMember(TypeInfo type, string name, bool isMethodCall, CompileCheckContext ctx)
    {
        return type.Kind switch
        {
            TypeKind.BuiltinObject => ResolveBuiltin(type.PeopleCodeType?.GetTypeName(), name, isMethodCall),
            TypeKind.Array => ResolveBuiltin("array", name, isMethodCall),
            TypeKind.AppClass or TypeKind.Interface =>
                ResolveAppClassMember(type as AppClassTypeInfo, name, isMethodCall, ctx),
            // Unhandled kinds assumed valid (public)
            _ => MemberResolution.Found(MemberVisibility.Public, null),
        };
    }

    private static MemberResolution ResolveBuiltin(string? typeName, string memberName, bool isMethodCall)
    {
        if (string.IsNullOrEmpty(typeName))
            return MemberResolution.Skip;

        bool exists = isMethodCall
            ? PeopleCodeTypeDatabase.GetMethod(typeName, memberName) != null
            : PeopleCodeTypeDatabase.GetProperty(typeName, memberName) != null;

        return exists
            ? MemberResolution.Found(MemberVisibility.Public, typeName)
            : MemberResolution.NotFound;
    }

    /// <summary>
    /// Walks the inheritance chain for an AppClass / Interface. Returns the first matching
    /// member with its visibility and declaring type name. Soft-skips (assume valid) when
    /// metadata cannot be resolved.
    /// </summary>
    private static MemberResolution ResolveAppClassMember(
        AppClassTypeInfo? appClass, string memberName, bool isMethodCall, CompileCheckContext ctx)
    {
        if (appClass == null) return MemberResolution.Skip;

        var resolver = ctx.Resolver;
        var selfMetadata = ctx.SelfMetadata;

        if (resolver == null && selfMetadata == null) return MemberResolution.Skip;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = appClass.QualifiedName;

        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current)) break;

            TypeMetadata? metadata =
                selfMetadata != null && current.Equals(selfMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase)
                    ? selfMetadata
                    : resolver?.GetTypeMetadata(current);
            if (metadata == null) return MemberResolution.Skip;

            if (isMethodCall)
            {
                if (metadata.Methods.TryGetValue(memberName, out var method))
                    return MemberResolution.Found(method.Visibility, current);
            }
            else
            {
                if (metadata.Properties.TryGetValue(memberName, out var property))
                    return MemberResolution.Found(property.Visibility, current);

                // Instance variables: keyed with leading '&'; access is %This.Name without it.
                if (metadata.InstanceVariables.TryGetValue("&" + memberName, out var instanceVar))
                    return MemberResolution.Found(instanceVar.Visibility, current);
            }

            if (metadata.IsBaseClassBuiltin && metadata.BuiltinBaseType.HasValue)
                return ResolveBuiltin(metadata.BuiltinBaseType.Value.GetTypeName(), memberName, isMethodCall);

            current = !string.IsNullOrEmpty(metadata.BaseClassName)
                ? metadata.BaseClassName
                : metadata.InterfaceName;
        }

        return MemberResolution.NotFound;
    }

    #endregion

    #region Visibility

    /// <summary>
    /// Public: anyone. Private: declaring class only. Protected: declaring class + subclasses
    /// of the declarer (siblings cannot access each other's protected members).
    /// </summary>
    private static bool IsAccessible(MemberVisibility visibility, string? declaringTypeName, CompileCheckContext ctx)
    {
        if (visibility == MemberVisibility.Public)
            return true;

        var caller = GetCallerClassName(ctx);
        if (caller == null)
            return false; // non-class program: only public

        if (string.IsNullOrEmpty(declaringTypeName))
            return true; // unknown declarer — don't false-positive

        if (caller.Equals(declaringTypeName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visibility == MemberVisibility.Private)
            return false;

        // Protected: caller must inherit from declaring type
        return CallerInheritsFrom(caller, declaringTypeName, ctx);
    }

    private static string? GetCallerClassName(CompileCheckContext ctx)
    {
        if (ctx.SelfMetadata != null && !string.IsNullOrEmpty(ctx.SelfMetadata.QualifiedName))
            return ctx.SelfMetadata.QualifiedName;

        // Fall back to live program class name when SelfMetadata was not supplied
        // (unqualified; inheritance walks may fail — prefer SelfMetadata in production).
        if (ctx.Program.AppClass != null)
            return ctx.Program.AppClass.Name;

        return null;
    }

    private static bool CallerInheritsFrom(string callerQualifiedName, string ancestorQualifiedName, CompileCheckContext ctx)
    {
        var resolver = ctx.Resolver;
        var selfMetadata = ctx.SelfMetadata;
        if (resolver == null && selfMetadata == null)
            return false;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = callerQualifiedName;

        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current)) break;

            TypeMetadata? metadata =
                selfMetadata != null && current.Equals(selfMetadata.QualifiedName, StringComparison.OrdinalIgnoreCase)
                    ? selfMetadata
                    : resolver?.GetTypeMetadata(current);
            if (metadata == null)
                return false;

            var baseName = !string.IsNullOrEmpty(metadata.BaseClassName)
                ? metadata.BaseClassName
                : metadata.InterfaceName;

            if (string.IsNullOrEmpty(baseName))
                return false;

            if (baseName.Equals(ancestorQualifiedName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Builtin base is not an app-class ancestor for protected visibility
            if (metadata.IsBaseClassBuiltin)
                return false;

            current = baseName;
        }

        return false;
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
