using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports uses of <c>%This</c> outside an application class (or interface) program body.
/// Pure AST — no type inference or database.
/// </summary>
public sealed class ThisOutsideClassCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not IdentifierNode id)
            return;

        if (id.IdentifierType != IdentifierType.SystemVariable)
            return;

        if (!id.Name.Equals("%This", StringComparison.OrdinalIgnoreCase))
            return;

        // AppClass is set for both class and interface programs.
        if (ctx.Program.AppClass != null)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.ThisOutsideClass,
            DiagnosticSeverity.Error,
            id.SourceSpan,
            "%This is only valid inside an application class."));
    }
}
