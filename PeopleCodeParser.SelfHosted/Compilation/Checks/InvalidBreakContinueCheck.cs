using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports Break/Continue statements that are not inside a valid binder.
/// Break binds to For/While/Repeat/Evaluate; Continue binds only to For/While/Repeat.
/// </summary>
public sealed class InvalidBreakContinueCheck : CompileCheckBase
{
    public override CheckRequirement Requirement => CheckRequirement.NotRequired;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        switch (node)
        {
            case BreakStatementNode br when !HasBreakBinder(br):
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.InvalidBreakContinue,
                    DiagnosticSeverity.Error,
                    br.SourceSpan,
                    "Break is not inside a loop or Evaluate."));
                break;

            case ContinueStatementNode cont when !HasContinueBinder(cont):
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.InvalidBreakContinue,
                    DiagnosticSeverity.Error,
                    cont.SourceSpan,
                    "Continue is not inside a loop."));
                break;
        }
    }

    private static bool HasBreakBinder(AstNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (IsRoutineBoundary(current))
                return false;
            if (current is ForStatementNode or WhileStatementNode or RepeatStatementNode
                or EvaluateStatementNode)
                return true;
        }
        return false;
    }

    private static bool HasContinueBinder(AstNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (IsRoutineBoundary(current))
                return false;
            if (current is ForStatementNode or WhileStatementNode or RepeatStatementNode)
                return true;
            // Evaluate is not a Continue binder — keep walking past it.
        }
        return false;
    }

    private static bool IsRoutineBoundary(AstNode node) =>
        node is FunctionNode or MethodImplNode or PropertyImplNode or ProgramNode;
}
