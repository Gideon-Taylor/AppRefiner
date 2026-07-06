using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Flags a derived application class that defines no constructor of its own while its
/// base class has an explicit parameterized constructor (which the subclass must call,
/// so it needs a constructor to do it from). Ported from AppRefiner's MissingConstructor
/// styler.
///
/// Resolver re-expression: the styler fetched the base class SOURCE via the DataManager
/// and re-parsed it to locate the base constructor. This check instead reads
/// <see cref="PeopleCodeTypeInfo.Inference.TypeMetadata.Constructor"/> from the
/// <see cref="PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver"/>. Note that
/// TypeMetadataBuilder always synthesizes a zero-parameter default constructor when a
/// class/interface has no explicit one, so <c>Constructor</c> is never null for a
/// resolvable class — the operative condition is <c>Parameters.Count &gt; 0</c>, which
/// is faithful to the styler's <c>baseClassConstructor?.Parameters.Count &gt; 0</c>
/// (a synthetic or explicit parameterless base constructor is not flagged).
///
/// Requirement is Optional: without a resolver, or when the base class cannot be
/// resolved (e.g. it lives in an unloaded module), the check reports nothing rather
/// than risk a false positive — matching the old styler's silent skip when the base
/// source was unavailable.
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
        if (baseMeta?.Constructor is { } ctor && ctor.Parameters.Count > 0)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.MissingConstructor,
                DiagnosticSeverity.Error,
                appClass.NameToken.SourceSpan,
                $"Class '{appClass.Name}' is missing a constructor required by '{appClass.BaseType.TypeName}'."));
        }
    }
}
