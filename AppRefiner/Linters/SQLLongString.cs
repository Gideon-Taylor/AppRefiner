using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that checks for SQL strings that are too long and should be SQL objects instead
    /// </summary>
    public class SQLLongString : BaseLintRule
    {
        public override string LINTER_ID => "SQL_LONG";
        
        /// <summary>
        /// Maximum allowed length for SQL strings before reporting a warning
        /// </summary>
        public int MaxSqlLength { get; set; } = 120;

        public SQLLongString()
        {
            Description = "Reports SQL strings > 120 characters.";
            Type = ReportType.Warning;
            Active = false;
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Check if the function being called is "SQLExec" or "CreateSQL"
            if (!(node.Function is IdentifierNode functionId))
                return;

            var functionName = functionId.Name;
            if (!functionName.Equals("SQLExec", StringComparison.OrdinalIgnoreCase) &&
                !functionName.Equals("CreateSQL", StringComparison.OrdinalIgnoreCase))
                return;

            if (node.Arguments.Count == 0)
                return;

            // Get the first argument
            var firstArg = node.Arguments[0];

            // We can only process this rule for calls that have a literal string as the first argument
            if (firstArg is LiteralNode literal && literal.LiteralType == LiteralType.String)
            {
                var sqlText = literal.Value?.ToString() ?? "";

                if (sqlText.Length > MaxSqlLength)
                {
                    // Report that the SQL statement is too long
                    AddReport(
                        1,
                        $"Long literal SQL statements (length: {sqlText.Length}) should be SQL objects.",
                        Type,
                        literal.SourceSpan.Start.Line,
                        literal.SourceSpan
                    );
                }
            }

            base.VisitFunctionCall(node);
        }
    }
}
