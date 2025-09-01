using System.Collections.Generic;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

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

        public override void VisitCatch(CatchStatementNode node)
        {
            // Check if the catch block is empty
            if (node.Body == null || node.Body.Statements.Count == 0)
            {
                // Check if there's a comment in the catch block and we're allowing that
                if (AllowWithComments && HasComment(node))
                {
                    return;
                }

                AddReport(
                    1,
                    "Empty catch block silently swallows exceptions. Consider logging or rethrowing.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }

            base.VisitCatch(node);
        }

        private bool HasComment(CatchStatementNode node)
        {
            // This is a simplified implementation - in a real-world scenario,
            // you would need to check for comments associated with the node
            // For now, we'll just return false
            return false;
        }
    }
}
