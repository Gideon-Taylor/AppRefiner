using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// PeopleBooks (Application Classes — constructors / %Super):
/// <list type="bullet">
/// <item>
/// A class that extends another <em>class</em> must initialize its super class by
/// assigning an instance to <c>%Super</c> in the subclass constructor
/// (e.g. <c>%Super = create ExampleBase();</c>). Assignment is only allowed in the
/// constructor.
/// </item>
/// <item>
/// That requirement is relaxed when the super class constructor takes no parameters
/// and the subclass constructor would only assign <c>%Super</c>: the runtime creates
/// the super object automatically, so you may omit the constructor entirely
/// (see <see cref="MissingConstructorCheck"/>). Once you author your own constructor
/// that does more work, you own the chain and must set <c>%Super</c> yourself.
/// </item>
/// <item>
/// Interface parents are different: "If your class implements an interface, you don't
/// need to create an object reference to the superclass since the system does that
/// automatically for you." No <c>%Super</c> assignment is required.
/// </item>
/// <item>
/// <c>implements</c> vs <c>extends</c> is descriptive only — PeopleCode allows either
/// keyword for an interface (or class) parent. This check resolves the parent via
/// <see cref="ITypeMetadataResolver"/> and inspects <see cref="TypeMetadata.Kind"/>;
/// it never keys off the keyword in source.
/// </item>
/// </list>
///
/// Complements <see cref="MissingConstructorCheck"/> (must <em>declare</em> a constructor
/// when the base class has a parameterized constructor). This check applies when a
/// constructor body is present and the parent is a class.
///
/// Requirement is Optional: without a resolver, or when the parent cannot be resolved,
/// the check reports nothing rather than risk a false positive on an interface parent.
/// Builtin bases (e.g. <c>Exception</c>) are treated as classes without a resolver lookup.
///
/// Presence of a <c>%Super = …</c> assignment anywhere in the body is enough
/// (not path-sensitive, not first-statement-only).
/// </summary>
public sealed class MissingSuperCallCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        if (appClass.BaseType == null)
            return;

        // Class parents require %Super; interface parents do not (system auto-binds).
        // extends/implements keyword is unreliable — resolve the actual type Kind.
        if (!BaseRequiresSuper(appClass.BaseType, ctx.Resolver))
            return;

        foreach (var method in appClass.Methods)
        {
            if (!IsConstructor(method, appClass.Name))
                continue;

            var body = method.Body ?? method.Implementation?.Body;
            if (body == null)
                continue;

            if (BodySetsSuper(body))
                continue;

            sink.Report(new CompileDiagnostic(
                DiagnosticCode.MissingSuperCall,
                DiagnosticSeverity.Error,
                method.NameToken.SourceSpan,
                $"Constructor for '{appClass.Name}' must set %Super to initialize the base class."));
        }
    }

    /// <summary>
    /// True when the parent is a class that must be constructed via <c>%Super</c>.
    /// False for interfaces, unresolvable parents, or when no resolver is available
    /// for an app-class path.
    /// </summary>
    private static bool BaseRequiresSuper(TypeNode baseType, ITypeMetadataResolver? resolver)
    {
        // Builtin object types (Exception, etc.) are classes, not interfaces.
        if (baseType is BuiltInTypeNode)
            return true;

        if (resolver == null)
            return false;

        var baseMeta = resolver.GetTypeMetadata(baseType.TypeName);
        if (baseMeta == null)
            return false;

        // Kind is authoritative — do not use extends vs implements from source.
        // ProgramKind.Interface → system creates the super reference automatically.
        return baseMeta.Kind == ProgramKind.AppClass;
    }

    private static bool IsConstructor(MethodNode method, string className) =>
        method.IsConstructor ||
        string.Equals(method.Name, className, StringComparison.OrdinalIgnoreCase);

    private static bool BodySetsSuper(BlockNode body)
    {
        foreach (var assignment in body.FindDescendants<AssignmentNode>())
        {
            if (assignment.Target is IdentifierNode id &&
                (id.IdentifierType == IdentifierType.Super ||
                 id.Name.Equals("%Super", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
