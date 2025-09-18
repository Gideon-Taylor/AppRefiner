using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers
{
    internal class MissingSemicolon : BaseStyler
    {
        public override string Description => "Marks statements that require a semicolon but are missing it.";

        public override void VisitBlock(BlockNode node)
        {
            base.VisitBlock(node);

            // Skip semicolon checking if this block contains error statements
            // This avoids false positives when there are syntax errors
            if (node.Statements.Any(s => s is ErrorStatementNode))
            {
                return;
            }

            foreach (var statement in node.Statements.SkipLast(1))
            {
                // Skip error statements completely
                if (statement is ErrorStatementNode)
                {
                    continue;
                }

                // Only flag missing semicolons for statements that appear well-formed
                if (statement.HasSemicolon == false && IsStatementWellFormed(statement))
                {
                    AddIndicator(statement.SourceSpan, IndicatorType.SQUIGGLE, 0x0000FFA0, "Missing semicolon");
                }
            }
        }

        /// <summary>
        /// Determines if a statement appears to be well-formed and suitable for semicolon checking.
        /// This helps avoid false positives when there are syntax errors.
        /// </summary>
        private bool IsStatementWellFormed(StatementNode statement)
        {
            // Basic validation: statement should have valid source span
            if (statement.SourceSpan.Start.ByteIndex < 0 || statement.SourceSpan.End.ByteIndex < statement.SourceSpan.Start.ByteIndex)
            {
                return false;
            }

            // Check if the statement has reasonable tokens
            if (statement.FirstToken == null && statement.LastToken == null)
            {
                return false;
            }

            // If the statement contains error tokens or incomplete parsing, be conservative
            // This is a basic heuristic - we could expand this with more sophisticated checks
            return true;
        }
    }
}
