using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.Stylers;

/// <summary>
/// Validates that method calls and property accesses target members that actually exist
/// on the resolved type.  Works for builtin object types (via PeopleCodeTypeDatabase)
/// and application classes (walking the inheritance chain via TypeResolver).
///
/// Type inference is run by StylerManager before stylers execute, so every node
/// visited here already has type information attached by TypeInferenceVisitor.
/// When the target type cannot be resolved (Unknown / Any / null) the access is
/// silently skipped to avoid false positives.
/// </summary>
public class InvalidMemberAccess : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFA0; // Red — matches UndefinedVariables

    public override string Description => "Invalid member access";

    /// <summary>
    /// AppClass member resolution uses the database-backed TypeResolver, but the
    /// styler degrades gracefully when no connection is available.
    /// </summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

    public InvalidMemberAccess()
    {
        Active = true;
    }

    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        // Children first — target's type info is already set by TypeInferenceVisitor
        base.VisitMemberAccess(node);

        // Dynamic member access (obj."name") cannot be validated statically
        if (node.IsDynamic) return;

        var targetType = node.Target.GetInferredType();
        if (targetType == null) return;

        // Types we cannot meaningfully validate against
        if (targetType.Kind is TypeKind.Unknown or TypeKind.Any or TypeKind.Invalid
            or TypeKind.Void or TypeKind.Primitive or TypeKind.Reference)
            return;

        // Union return types cannot be validated reliably (member may exist on
        // some constituent types but not others)
        if (targetType is UnionReturnTypeInfo) return;

        // Explicit method call vs property / implicit access
        bool isMethodCall = node.Parent is FunctionCallNode fc && ReferenceEquals(fc.Function, node);

        // Record property access is a dynamic field lookup — only validate method calls on Records
        if (targetType is RecordTypeInfo && !isMethodCall) return;

        if (targetType.PeopleCodeType is PeopleCodeType.Record or PeopleCodeType.Row && !isMethodCall)
            return;

        if (!MemberExists(targetType, node.MemberName, isMethodCall))
        {
            string kind   = isMethodCall ? "method" : "property";
            string tooltip = $"'{node.MemberName}' is not a known {kind} on '{DisplayName(targetType)}'";
            AddIndicator(node.MemberNameSpan, IndicatorType.SQUIGGLE, ERROR_COLOR, tooltip);
        }
    }

    #region Member existence checks

    private bool MemberExists(TypeInfo type, string name, bool isMethodCall)
    {
        return type.Kind switch
        {
            TypeKind.BuiltinObject                  => BuiltinHasMember(type.PeopleCodeType?.GetTypeName(), name, isMethodCall),
            TypeKind.Array                          => BuiltinHasMember("array", name, isMethodCall),
            TypeKind.AppClass or TypeKind.Interface => AppClassHasMember(type as AppClassTypeInfo, name, isMethodCall),
            _                                       => true   // Unhandled kinds assumed valid
        };
    }

    private static bool BuiltinHasMember(string? typeName, string memberName, bool isMethodCall)
    {
        if (string.IsNullOrEmpty(typeName)) return true;

        return isMethodCall
            ? PeopleCodeTypeDatabase.GetMethod(typeName, memberName)   != null
            : PeopleCodeTypeDatabase.GetProperty(typeName, memberName) != null;
    }

    /// <summary>
    /// Walks the inheritance chain for an AppClass / Interface, checking Methods,
    /// Properties, and InstanceVariables at each level.  Falls through to a builtin
    /// member lookup when the chain terminates at a builtin base type.
    ///
    /// Returns <c>true</c> (assume valid) whenever metadata cannot be resolved,
    /// to avoid false positives against classes that are simply not yet loaded or
    /// are defined in another module.
    /// </summary>
    private bool AppClassHasMember(AppClassTypeInfo? appClass, string memberName, bool isMethodCall)
    {
        if (appClass == null) return true;

        var typeResolver = Editor?.AppDesignerProcess?.TypeResolver;
        if (typeResolver == null) return true;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? current = appClass.QualifiedName;

        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current)) break;   // Circular inheritance guard

            var metadata = typeResolver.GetTypeMetadata(current);
            if (metadata == null) return true;  // Unresolvable class — assume valid

            if (isMethodCall)
            {
                if (metadata.Methods.ContainsKey(memberName)) return true;
            }
            else
            {
                if (metadata.Properties.ContainsKey(memberName))       return true;
                if (metadata.InstanceVariables.ContainsKey(memberName)) return true;
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
        RecordTypeInfo   rec => rec.RecordName  != null ? $"Record({rec.RecordName})"                        : "Record",
        FieldTypeInfo    fld => fld.RecordName  != null ? $"Field({fld.RecordName}.{fld.FieldName})"        : "Field",
        AppClassTypeInfo app => app.QualifiedName ?? app.Name,
        _                                              => type.Name
    };
}
