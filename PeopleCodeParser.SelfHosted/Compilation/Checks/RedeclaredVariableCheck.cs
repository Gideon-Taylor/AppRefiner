using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports local variable declarations that redeclare or shadow a variable already
/// visible in the same scope. Ported from AppRefiner's RedeclaredVariables styler.
///
/// Stateless check: the driver dispatches OnNode pre-order (before it registers the
/// current declaration's variable) with enclosing scopes already entered, so at a local
/// declaration node <c>ctx.ScopeData.GetVariablesInScope(GetCurrentScope())</c> exposes
/// the accessible variables (current scope plus enclosing scopes). A same-name variable
/// declared textually BEFORE this one is a redeclaration/shadow; the positional test is
/// what distinguishes a genuine conflict (e.g. a Global declared above this function) from
/// a harmless later declaration (a Global or program-level local below it), since
/// Global/Component variables live in the Global scope regardless of source position. The
/// kind of the existing variable selects one of several distinct messages.
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
        var declPosition = varInfo.SourceSpan.Start.ByteIndex;

        // A same-name variable only conflicts if its declaration appears textually BEFORE
        // this one. This matters because Global/Component variables are registered in the
        // Global scope regardless of where they sit in the source, so an accessible
        // same-name variable is a real conflict only when declared earlier: a Global above
        // this function shadows a local here, but a Global (or program-level local) below it
        // does not. Positional filtering also keeps program-level-local-vs-function-local
        // correct without depending on traversal order.
        var declaredVar = ctx.ScopeData.GetVariablesInScope(currentScope)
            .Where(v => v.Name.Equals(varName, StringComparison.OrdinalIgnoreCase))
            .Where(v => v.VariableNameInfo.SourceSpan.Start.ByteIndex < declPosition)
            .FirstOrDefault();

        if (declaredVar == null)
            return;

        string? message = declaredVar.Kind switch
        {
            VariableKind.Parameter => $"Variable '{varName}' already declared as parameter in this scope",
            VariableKind.Instance => $"Variable '{varName}' shadows instance variable '{declaredVar.Name}'",
            VariableKind.Property => $"Variable '{varName}' shadows property '{declaredVar.Name}'",
            VariableKind.Global => $"Variable '{varName}' shadows global variable '{declaredVar.Name}'",
            VariableKind.Component => $"Variable '{varName}' shadows component variable '{declaredVar.Name}'",
            VariableKind.Local => $"Variable '{varName}' already declared in this scope",
            _ => null // Unknown kind - do nothing (matches the original styler's default).
        };

        if (message != null)
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.RedeclaredVariable, DiagnosticSeverity.Error, varInfo.SourceSpan, message));
    }
}
