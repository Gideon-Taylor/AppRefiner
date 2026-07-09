using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports properties declared with <c>get</c> and/or <c>set</c> that lack a matching
/// getter/setter body. Abstract properties are exempt. Orphan implementations without a
/// declaration are already parse errors and are not re-reported here.
/// Pure AST — no type inference or database.
/// </summary>
public sealed class MissingPropertyAccessorCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass || appClass.IsInterface)
            return;

        foreach (var property in appClass.Properties)
        {
            if (property.IsAbstract)
                continue;

            if (property.HasGet && property.Getter == null)
            {
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.MissingPropertyAccessor,
                    DiagnosticSeverity.Error,
                    property.NameToken.SourceSpan,
                    $"Property '{property.Name}' is declared with get but has no getter implementation."));
            }

            if (property.HasSet && property.Setter == null)
            {
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.MissingPropertyAccessor,
                    DiagnosticSeverity.Error,
                    property.NameToken.SourceSpan,
                    $"Property '{property.Name}' is declared with set but has no setter implementation."));
            }
        }
    }
}
