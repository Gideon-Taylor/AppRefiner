using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports local variable declarations that redeclare or shadow a variable already
/// visible in the same scope. Ported from AppRefiner's RedeclaredVariables styler.
///
/// Stateless check: the driver dispatches OnNode pre-order (before it registers the
/// current declaration's variable) with enclosing scopes already entered, so at a local
/// declaration node <c>ctx.ScopeData.GetVariablesInScope(GetCurrentScope())</c> returns
/// exactly the "declared-so-far in this scope" set the original styler observed. A
/// same-name variable already present means a redeclaration/shadow. The kind of the
/// existing variable selects one of four distinct messages, carried over verbatim.
/// </summary>
public sealed class RedeclaredVariableCheck : CompileCheckBase
{
    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        switch (node)
        {
            case LocalVariableDeclarationNode d1:
                foreach (var varInfo in d1.VariableNameInfos)
                    CheckForVariable(varInfo, ctx, sink);
                break;
            case LocalVariableDeclarationWithAssignmentNode d2:
                CheckForVariable(d2.VariableNameInfo, ctx, sink);
                break;
        }
    }

    private static void CheckForVariable(VariableNameInfo varInfo, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        var currentScope = ctx.ScopeData.GetCurrentScope();
        var varName = varInfo.Name;

        // Try to get the variable in scope with the name "varName".
        var variablesInScope = ctx.ScopeData.GetVariablesInScope(currentScope)
            .Where(v => v.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));

        if (!variablesInScope.Any())
            return;

        var declaredVar = variablesInScope.First();
        string? message = declaredVar.Kind switch
        {
            VariableKind.Parameter => $"Variable '{varName}' already declared as parameter in this scope",
            VariableKind.Instance => $"Variable '{varName}' shadows instance variable '{declaredVar.Name}'",
            VariableKind.Property => $"Variable '{varName}' shadows property '{declaredVar.Name}'",
            VariableKind.Local => $"Variable '{varName}' already declared in this scope",
            _ => null // Unknown kind - do nothing (matches the original styler's default).
        };

        if (message != null)
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.RedeclaredVariable, DiagnosticSeverity.Error, varInfo.SourceSpan, message));
    }
}
