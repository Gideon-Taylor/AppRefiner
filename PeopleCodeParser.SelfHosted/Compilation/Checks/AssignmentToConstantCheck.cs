using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports assignments whose target resolves to a <see cref="VariableKind.Constant"/>.
/// Pure AST + scope registry — no type inference or database.
/// </summary>
public sealed class AssignmentToConstantCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AssignmentNode assignment)
            return;

        if (assignment.Target is not IdentifierNode id)
            return;

        // Only user variables (e.g. &MYCONST) can be declared as constants.
        if (id.IdentifierType != IdentifierType.UserVariable)
            return;

        var currentScope = ctx.ScopeData.GetCurrentScope();
        var constant = ctx.ScopeData.GetVariablesInScope(currentScope)
            .FirstOrDefault(v =>
                v.Kind == VariableKind.Constant &&
                v.Name.Equals(id.Name, StringComparison.OrdinalIgnoreCase));

        if (constant == null)
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.AssignmentToConstant,
            DiagnosticSeverity.Error,
            id.SourceSpan,
            $"Cannot assign to constant '{id.Name}'."));
    }
}
