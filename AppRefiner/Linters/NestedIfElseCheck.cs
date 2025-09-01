using PeopleCodeParser.SelfHosted.Nodes;

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

        /// <summary>
        /// Maximum allowed nesting level for If/Else blocks before reporting a warning
        /// </summary>
        public int MaxNestingLevel { get; set; } = 3;

        /// <summary>
        /// Whether to report on "else if" chains that could be replaced with Evaluate statements
        /// </summary>
        public bool ReportElseIfChains { get; set; } = true;

        private int currentNestingLevel = 0;
        private Stack<IfStatementNode> ifContextStack = new();
        private HashSet<IfStatementNode> reportedIfStatements = new();
        private Dictionary<IfStatementNode, int> maxNestingLevelMap = new();

        public NestedIfElseCheck()
        {
            Description = "Identifies deeply nested If/Else blocks that should be refactored";
            Type = ReportType.Warning;
            Active = true;
        }

        public override void VisitIf(IfStatementNode node)
        {
            currentNestingLevel++;
            ifContextStack.Push(node);

            // Initialize the max nesting level for this context
            maxNestingLevelMap[node] = currentNestingLevel;

            // Visit the if statement
            base.VisitIf(node);

            // When exiting an if statement, check if its max depth exceeded the threshold
            // and issue a report if needed, but only for the outermost if that wasn't already reported
            if (ifContextStack.Count == 1 && maxNestingLevelMap[node] > MaxNestingLevel && !reportedIfStatements.Contains(node))
            {
                AddReport(
                    1,
                    $"Deeply nested If/Else blocks (max level {maxNestingLevelMap[node]}). Consider refactoring using Evaluate or early returns.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );

                // Mark this if statement as reported
                reportedIfStatements.Add(node);
            }

            currentNestingLevel--;
            ifContextStack.Pop();
        }

        // Add specific method to check for If inside Else
        public override void VisitBlock(BlockNode node)
        {
            // If this block is inside an ELSE clause and contains an immediate IF statement,
            // that's a pattern we should highlight even if the nesting isn't too deep yet
            if (node.Parent is IfStatementNode parentIf && parentIf.ElseBlock == node)
            {
                // Check if the first statement in this block is an IF
                if (node.Statements.Count > 0 && node.Statements[0] is IfStatementNode)
                {
                    if (ReportElseIfChains && currentNestingLevel >= 2)
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
                                outermost.SourceSpan.Start.Line,
                                outermost.SourceSpan
                            );

                            // Mark this if statement as reported
                            reportedIfStatements.Add(outermost);
                        }
                    }
                }
            }

            base.VisitBlock(node);
        }

        // Update the max nesting level for all parent contexts in the stack
        private void UpdateMaxNestingLevels()
        {
            foreach (var ctx in ifContextStack)
            {
                maxNestingLevelMap[ctx] = Math.Max(maxNestingLevelMap[ctx], currentNestingLevel);
            }
        }

        // Override the default visit to update nesting levels
        public override void VisitProgram(ProgramNode node)
        {
            // Update max nesting levels for all if statements in the stack
            if (ifContextStack.Count > 0)
            {
                UpdateMaxNestingLevels();
            }

            base.VisitProgram(node);
        }

        public override void Reset()
        {
            currentNestingLevel = 0;
            ifContextStack.Clear();
            reportedIfStatements.Clear();
            maxNestingLevelMap.Clear();
        }
    }
}
