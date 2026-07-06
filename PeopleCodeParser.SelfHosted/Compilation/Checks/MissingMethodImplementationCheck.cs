using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports methods that are declared in an application class header but never
/// implemented (no matching <c>method ... end-method</c> body). Ported from
/// AppRefiner's MissingMethodImplementation styler.
///
/// Pure AST: <see cref="MethodNode.IsDeclaration"/> is true only when the parser found
/// no implementation body to link, so a declared-and-implemented method is not flagged.
/// Abstract methods are exempt (a subclass supplies the body).
///
/// Severity divergence from the styler: the styler used a warning-colored squiggle; a
/// declared-but-unimplemented method is a genuine compile error, so this reports
/// <see cref="DiagnosticSeverity.Error"/>.
/// </summary>
public sealed class MissingMethodImplementationCheck : CompileCheckBase
{
    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        foreach (var method in appClass.Methods)
        {
            if (!method.IsDeclaration || method.IsAbstract)
                continue;

            sink.Report(new CompileDiagnostic(
                DiagnosticCode.MissingMethodImplementation,
                DiagnosticSeverity.Error,
                method.NameToken.SourceSpan,
                $"Method '{method.Name}' is declared but not implemented."));
        }
    }
}
