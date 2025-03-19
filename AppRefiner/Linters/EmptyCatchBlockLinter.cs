using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies empty catch blocks that silently swallow exceptions
    /// </summary>
    public class EmptyCatchBlockLinter : BaseLintRule
    {
        public override string LINTER_ID => "EMPTY_CATCH";

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
                AddReport(
                    1,
                    "Empty catch block silently swallows exceptions. Consider logging or rethrowing.",
                    Type,
                    context.Start.Line - 1,
                    (context.Start.StartIndex, context.Stop.StopIndex + 1)
                );
            }
        }

        public override void Reset()
        {
        }
    }
}
