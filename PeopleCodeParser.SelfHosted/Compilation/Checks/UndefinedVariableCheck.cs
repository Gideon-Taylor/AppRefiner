using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports references to user variables that are not declared in any accessible scope.
/// Ported from AppRefiner's UndefinedVariables styler.
///
/// Scope split with the surviving styler: undefined variables are a hard compile error
/// only in Application Class code, so this check gates on
/// <c>ctx.Program.AppClass != null</c> and does nothing for non-class programs. The
/// trimmed AppRefiner styler handles the non-class (code smell) case and early-returns
/// for class programs, so exactly one of the two ever flags a given program.
///
/// Stateful check: identifiers/iterators are accumulated during OnNode (dispatched
/// pre-order, so the scope state matches what the original styler observed) and reported
/// in Finish. This instance is constructed fresh per Check() run (see
/// CompileChecker.CreateChecks), so it starts empty; the ProgramNode reset in OnNode is
/// belt-and-suspenders and must not be removed — it keeps the accumulators safe even if a
/// future change were to reuse an instance across runs.
/// </summary>
public sealed class UndefinedVariableCheck : CompileCheckBase
{
    private readonly HashSet<(string Name, SourceSpan Location)> _undefined = new();
    private readonly HashSet<(string Name, SourceSpan Location)> _forLoopIterators = new();

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is ProgramNode)
        {
            // ProgramNode is always dispatched first; reset per-run state here.
            _undefined.Clear();
            _forLoopIterators.Clear();
            return;
        }

        // Class-code gate: non-class programs are the styler's responsibility.
        if (ctx.Program.AppClass == null)
            return;

        if (node is IdentifierNode id && id.IdentifierType == IdentifierType.UserVariable)
        {
            string varName = id.Name;

            // Check if variable is defined in any accessible scope
            var curScope = ctx.ScopeData.GetCurrentScope();
            var varsInScope = ctx.ScopeData.GetVariablesInScope(curScope);
            if (!varsInScope.Any(v => v.Name.Equals(varName) ||
                (varName.StartsWith('&') && v.Name.Equals(varName.Substring(1)) && v.Kind == VariableKind.Property)))
            {
                _undefined.Add((varName, id.SourceSpan));
            }
        }

        if (node is ForStatementNode forNode)
        {
            string varName = forNode.IteratorName;

            var curScope = ctx.ScopeData.GetCurrentScope();
            var varsInScope = ctx.ScopeData.GetVariablesInScope(curScope);

            // Normalize variable name (remove & prefix for comparison)
            string normalizedVarName = varName.StartsWith('&') ? varName.Substring(1) : varName;

            if (!varsInScope.Any(v => v.Name.Equals(normalizedVarName) || v.Name.Equals(varName)))
            {
                _forLoopIterators.Add((varName, forNode.IteratorToken.SourceSpan));
            }
        }
    }

    public override void Finish(CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (ctx.Program.AppClass == null)
            return;

        // For loop iterators first (matches the styler's ordering). Attach an
        // UndefinedForLoopIteratorFix so the AppRefiner quick-fix map can offer the
        // "Declare iterator" fix, matching the surviving non-class UndefinedVariables styler.
        foreach (var (name, location) in _forLoopIterators)
        {
            sink.Report(new CompileDiagnostic(
                DiagnosticCode.UndefinedVariable,
                DiagnosticSeverity.Error,
                location,
                $"Undefined for loop iterator: {name}",
                new UndefinedForLoopIteratorFix(name)));
        }

        // Then other undefined variables, excluding locations already reported as iterators.
        foreach (var (name, location) in _undefined)
        {
            if (_forLoopIterators.Contains((name, location)))
                continue;

            sink.Report(new CompileDiagnostic(
                DiagnosticCode.UndefinedVariable,
                DiagnosticSeverity.Error,
                location,
                $"Undefined variable: {name}"));
        }
    }
}
