using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Database;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Flags calls to functions PeopleCode cannot resolve at the call site: not builtin,
/// not Declare-d, and not implemented above the call (PeopleCode is single-pass, so
/// forward references to later implementations are compile errors). Ported from
/// AppRefiner's UndeclaredFunctionStyler.
///
/// Two FixContext shapes under the one diagnostic code:
/// - <see cref="UndeclaredFunctionForwardRefFix"/>: implementation exists below the
///   call — static "move it above" fix.
/// - <see cref="UndeclaredFunctionUnknownFix"/>: nothing matches — the UI layer
///   resolves declare-function fixes lazily from its function cache / database.
///
/// Pure AST + static builtin DB: needs no resolver, so Requirement stays NotRequired.
/// </summary>
public sealed class UndeclaredFunctionCheck : CompileCheckBase
{
    private FunctionVisibilityIndex? _index;

    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        // ProgramNode is dispatched first every run: per-run index (re)build.
        if (node is ProgramNode program)
        {
            _index = FunctionVisibilityIndex.Build(program);
            return;
        }

        if (node is not FunctionCallNode call)
            return;

        // Only bare-identifier calls: method calls, create expressions, and
        // %This.X() never have a plain IdentifierNode callee. User-variable callees
        // are default-method calls (e.g. &rowset(1)), not function calls.
        if (call.Function is not IdentifierNode ident || ident.IdentifierType != IdentifierType.Generic)
            return;

        string name = ident.Name;

        // Declares must precede implementations and executable code, so existence
        // alone makes the name visible everywhere
        if (_index == null || _index.Declarations.ContainsKey(name))
            return;

        if (_index.Implementations.TryGetValue(name, out var impl))
        {
            if (impl.SourceSpan.Start.ByteIndex < call.SourceSpan.Start.ByteIndex)
                return; // Defined above the call — valid

            // Forward reference: the implementation exists but below this call
            var caller = call.FindAncestor<FunctionNode>();
            string moveDescription = caller != null
                ? $"Move Function '{impl.Name}' above '{caller.Name}'"
                : $"Move Function '{impl.Name}' above this statement";

            sink.Report(new CompileDiagnostic(
                DiagnosticCode.UndeclaredFunction,
                DiagnosticSeverity.Error,
                ident.SourceSpan,
                $"Function '{name}' is defined below its first use",
                new UndeclaredFunctionForwardRefFix(name, impl.Name, moveDescription)));
            return;
        }

        if (PeopleCodeTypeDatabase.GetFunction(name) != null)
            return; // Builtin

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.UndeclaredFunction,
            DiagnosticSeverity.Error,
            ident.SourceSpan,
            $"Function '{name}' is not declared or defined",
            new UndeclaredFunctionUnknownFix(name)));
    }
}
