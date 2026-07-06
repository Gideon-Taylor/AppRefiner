using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports when an application class's declared name does not match the expected name
/// (the last <c>:</c>-segment of the editor's class path). Ported from AppRefiner's
/// ClassNameMismatch styler.
///
/// Divergence from the styler: this check guards on a non-empty
/// <see cref="CompileCheckContext.ExpectedClassName"/> so it does not flag when the
/// expected name is unavailable (e.g. headless/MCP runs). The styler compared against ""
/// unconditionally. In the app, CompilerErrorsStyler always supplies the ClassPath-derived
/// name for class programs, so behavior is identical there.
/// </summary>
public sealed class ClassNameMismatchCheck : CompileCheckBase
{
    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not AppClassNode appClass)
            return;

        if (string.IsNullOrEmpty(ctx.ExpectedClassName))
            return;

        if (string.Equals(appClass.Name, ctx.ExpectedClassName, StringComparison.OrdinalIgnoreCase))
            return;

        sink.Report(new CompileDiagnostic(
            DiagnosticCode.ClassNameMismatch,
            DiagnosticSeverity.Error,
            appClass.NameToken.SourceSpan,
            $"Class name '{appClass.Name}' does not match expected name '{ctx.ExpectedClassName}'."));
    }
}
