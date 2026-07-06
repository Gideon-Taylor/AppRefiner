using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Compilation.Checks;

/// <summary>
/// Reports statements that are missing their terminating semicolon. Ported from
/// AppRefiner's MissingSemicolon styler.
///
/// Stateless check keyed on <see cref="BlockNode"/>. For each block the driver dispatches,
/// this inspects <c>block.Statements</c> directly (a property, independent of traversal
/// order). If the block contains ANY <see cref="ErrorStatementNode"/> it is skipped
/// entirely, to avoid false positives amid syntax errors. Otherwise every statement except
/// the last (the final statement before End-X needs no semicolon) is flagged when it lacks
/// a semicolon and appears well-formed.
/// </summary>
public sealed class MissingSemicolonCheck : CompileCheckBase
{
    public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
    {
        if (node is not BlockNode block)
            return;

        // Skip semicolon checking if this block contains error statements.
        // This avoids false positives when there are syntax errors.
        if (block.Statements.Any(s => s is ErrorStatementNode))
            return;

        foreach (var statement in block.Statements.SkipLast(1))
        {
            // Skip error statements completely.
            if (statement is ErrorStatementNode)
                continue;

            // Only flag missing semicolons for statements that appear well-formed.
            if (statement.HasSemicolon == false && IsStatementWellFormed(statement))
                sink.Report(new CompileDiagnostic(
                    DiagnosticCode.MissingSemicolon, DiagnosticSeverity.Error,
                    statement.SourceSpan, "Missing semicolon"));
        }
    }

    /// <summary>
    /// Determines if a statement appears to be well-formed and suitable for semicolon
    /// checking. This helps avoid false positives when there are syntax errors.
    /// </summary>
    private static bool IsStatementWellFormed(StatementNode statement)
    {
        // Basic validation: statement should have a valid source span.
        if (statement.SourceSpan.Start.ByteIndex < 0 ||
            statement.SourceSpan.End.ByteIndex < statement.SourceSpan.Start.ByteIndex)
            return false;

        // Check if the statement has reasonable tokens.
        if (statement.FirstToken == null && statement.LastToken == null)
            return false;

        return true;
    }
}
