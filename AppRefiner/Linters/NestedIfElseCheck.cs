using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that checks for deeply nested If/Else blocks
    /// PeopleCode lacks "else if" so developers have to nest If statements inside Else blocks
    /// This can lead to code that's hard to read and maintain
    /// </summary>
    public class NestedIfElseCheck : BaseLintRule
    {
        public override string LINTER_ID => "NESTED_IF";
        private const int MaxNestingLevel = 3;
        private int currentNestingLevel = 0;
        private Stack<IfStatementContext> ifContextStack = new();
        private HashSet<IfStatementContext> reportedIfStatements = new();

        public NestedIfElseCheck()
        {
            Description = "Identifies deeply nested If/Else blocks that should be refactored";
            Type = ReportType.Warning;
            Active = true;
        }

        public override void EnterIfStatement(IfStatementContext context)
        {
            currentNestingLevel++;
            ifContextStack.Push(context);

            // Check if we've exceeded the maximum nesting level
            if (currentNestingLevel > MaxNestingLevel)
            {
                // Get the outermost If statement to report on
                var outermost = ifContextStack.Last();

                AddReport(
                    1,
                    $"Deeply nested If/Else blocks (level {currentNestingLevel}). Consider refactoring using Evaluate or early returns.",
                    Type,
                    outermost.Start.Line - 1,
                    (outermost.Start.StartIndex, outermost.Stop.StopIndex)
                );
            }
        }

        public override void ExitIfStatement(IfStatementContext context)
        {
            currentNestingLevel--;
            ifContextStack.Pop();
        }

        // Add specific method to check for If inside Else
        public override void EnterStatementBlock(StatementBlockContext context)
        {
            // If this statement block is inside an ELSE clause and contains an immediate IF statement,
            // that's a pattern we should highlight even if the nesting isn't too deep yet
            if (context.Parent is IfStatementContext ifStmt &&
                context == ifStmt.GetChild(ifStmt.ChildCount - 2) && // The block before END_IF is the ELSE block
                ifStmt.ELSE() != null)
            {
                // Check if the first statement in this block is an IF
                var statements = context.statements();
                if (statements?.statement()?.Length > 0 &&
                    statements.statement(0) is IfStmtContext)
                {
                    if (currentNestingLevel >= 2)
                    {
                        // This is an "else if" pattern that's already nested a bit
                        // and would benefit from using EVALUATE instead
                        var outermost = ifContextStack.Last();

                        // Only report each top-level if statement once
                        if (!reportedIfStatements.Contains(outermost))
                        {
                            AddReport(
                                2,
                                "Multiple IF-ELSE-IF chains detected. Consider using Evaluate statement for better readability.",
                                ReportType.Info,
                                outermost.Start.Line - 1,
                                (outermost.Start.StartIndex, outermost.Stop.StopIndex)
                            );

                            // Mark this if statement as reported
                            reportedIfStatements.Add(outermost);
                        }
                    }
                }
            }
        }

        public override void Reset()
        {
            currentNestingLevel = 0;
            ifContextStack.Clear();
            reportedIfStatements.Clear();
        }
    }
}
