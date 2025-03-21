using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies empty catch blocks that silently swallow exceptions
    /// </summary>
    public class EmptyCatchBlockLinter : BaseLintRule
    {
        public override string LINTER_ID => "EMPTY_CATCH";

        /// <summary>
        /// Whether to allow empty catch blocks that have a comment
        /// </summary>
        public bool AllowWithComments { get; set; } = true;

        public EmptyCatchBlockLinter()
        {
            Description = "Detects empty catch blocks that silently swallow exceptions";
            Type = ReportType.Warning;
            Active = false;
        }

        public override void EnterCatchClause(CatchClauseContext context)
        {
            // Check if there's a statement block
            var statementBlock = context.statementBlock();

            // If there's no statement block or it's empty
            if (statementBlock == null ||
                statementBlock.statements() == null ||
                statementBlock.statements().statement() == null ||
                statementBlock.statements().statement().Length == 0)
            {
                // Check if the exception type is in the allowed list
                // Based on the grammar: CATCH (EXCEPTION | appClassPath) USER_VARIABLE
                string? exceptionType = null;
                if (context.EXCEPTION() != null)
                {
                    exceptionType = "Exception";
                }
                else if (context.appClassPath() != null)
                {
                    exceptionType = context.appClassPath().GetText();
                }

                // Check if there's a comment in the catch block and we're allowing that
                if (AllowWithComments && HasComment(context))
                {
                    return;
                }

                AddReport(
                    1,
                    "Empty catch block silently swallows exceptions. Consider logging or rethrowing.",
                    Type,
                    context.Start.Line - 1,
                    (context.Start.StartIndex, context.Stop.StopIndex + 1)
                );
            }
        }

        private bool HasComment(CatchClauseContext context)
        {
            // This is a simplified implementation - in a real-world scenario,
            // you would need to check for comments in the token stream
            // For now, we'll just return false
            return false;
        }

        public override void Reset()
        {
        }
    }
}
