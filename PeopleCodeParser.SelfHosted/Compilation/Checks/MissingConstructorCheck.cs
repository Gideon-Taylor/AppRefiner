using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// PeopleBooks (Application Classes — constructors):
/// <list type="bullet">
/// <item>
/// A class that does not extend some other class does not need any constructor.
/// </item>
/// <item>
/// A class that extends another <em>class</em> must have a constructor (to initialize
/// the super class). This is relaxed when the super class constructor takes no
/// parameters and would only assign <c>%Super</c>: the runtime creates the super
/// object automatically, so no constructor is required at design time.
/// </item>
/// <item>
/// Interface parents never require a constructor for super initialization — the system
/// creates the super reference automatically (see also <see cref="MissingSuperCallCheck"/>).
/// </item>
/// <item>
/// <c>implements</c> vs <c>extends</c> is not authoritative; the parent is resolved via
/// the type metadata resolver and inspected by <see cref="TypeMetadata.Kind"/>.
/// </item>
/// </list>
///
/// This check flags a derived class that defines no constructor of its own while its
/// base <em>class</em> has a parameterized constructor (which the subclass must call
/// from its own constructor). Ported from AppRefiner's MissingConstructor styler.
///
/// TypeMetadataBuilder always synthesizes a zero-parameter default constructor when a
/// class/interface has no explicit one, so <c>Constructor</c> is never null for a
/// resolvable type — the operative condition is <c>Parameters.Count &gt; 0</c>.
///
/// Requirement is Optional: without a resolver, or when the base cannot be resolved,
/// the check reports nothing rather than risk a false positive.
/// </summary>
public sealed class MissingConstructorCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.Optional;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        if (ctx.Resolver == null || appClass.BaseType == null)
            return;

        // A class that defines its own constructor (a method named after the class)
        // satisfies the base class's requirement.
        if (appClass.Methods.Any(m =>
                string.Equals(m.Name, appClass.Name, StringComparison.OrdinalIgnoreCase)))
            return;

        var baseMeta = ctx.Resolver.GetTypeMetadata(appClass.BaseType.TypeName);
        if (baseMeta == null)
            return;

        // Interfaces: system creates the super reference automatically — no constructor
        // required. Kind is authoritative (extends/implements keywords are interchangeable).
        if (baseMeta.Kind == ProgramKind.Interface)
            return;

        // Parameterless base ctor: runtime auto-creates super; constructor optional.
        // Parameterized base ctor: subclass must declare a constructor to pass args.
        if (baseMeta.Constructor is { } ctor && ctor.Parameters.Count > 0)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.MissingConstructor,
                DiagnosticSeverity.Error,
                appClass.NameToken.SourceSpan,
                $"Class '{appClass.Name}' is missing a constructor required by '{appClass.BaseType.TypeName}'."));
        }
    }
}
