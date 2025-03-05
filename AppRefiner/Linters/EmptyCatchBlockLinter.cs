using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies empty catch blocks that silently swallow exceptions
    /// </summary>
    public class EmptyCatchBlockLinter : BaseLintRule
    {
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
                Reports?.Add(new Report
                {
                    Type = Type,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex + 1),
                    Message = "Empty catch block silently swallows exceptions. Consider logging or rethrowing."
                });
            }
        }

        public override void Reset()
        {
        }
    }
}
