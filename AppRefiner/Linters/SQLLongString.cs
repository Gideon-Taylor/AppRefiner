using static AppRefiner.PeopleCode.PeopleCodeParser;

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

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Check if the function being called is "SQLExec" or "CreateSQL"
            if (context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase) ||
                context.genericID().GetText().Equals("CreateSQL", StringComparison.OrdinalIgnoreCase))
            {
                var args = context.functionCallArguments();
                if (args != null && args.expression() != null && args.expression().Length > 0)
                {
                    // Get the first argument
                    var firstArg = args.expression()[0];

                    /* We can only process this rule for calls that have a literal string as the first argument */
                    if (firstArg is LiteralExprContext)
                    {
                        var sqlText = SQLHelper.ExtractSQLFromLiteral(firstArg.GetText());

                        if (sqlText.Length > MaxSqlLength)
                        {
                            /* Report that the SQL statement is too long */
                            AddReport(
                                1,
                                $"Long literal SQL statements (length: {sqlText.Length}) should be SQL objects.",
                                Type,
                                firstArg.Start.Line - 1,
                                (firstArg.Start.StartIndex, firstArg.Stop.StopIndex)
                            );
                        }
                    }
                }
            }
        }

        // Optional: Add a method to check all string literals if CheckOnlySqlFunctions is false
        public override void EnterLiteralExpr(LiteralExprContext context)
        {
            var text = context.GetText();
            
            // Check if it looks like a string literal (starts and ends with quotes)
            if (text.Length >= 2 && (text.StartsWith('"') || text.StartsWith('"'))) 
            {
                var sqlText = SQLHelper.ExtractSQLFromLiteral(text);
                
                if (sqlText.Length > MaxSqlLength)
                {
                    AddReport(
                        2,
                        $"SQL string literal is too long (length: {sqlText.Length}). Consider using a SQL object.",
                        Type,
                        context.Start.Line - 1,
                        (context.Start.StartIndex, context.Stop.StopIndex)
                    );
                }
            }
        }
    }
}
