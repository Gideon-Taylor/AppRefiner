using AppRefiner.PeopleCode;
using SqlParser.Ast;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies SQL statements that don't use parameters
    /// which can be a SQL injection risk
    /// </summary>
    public class SQLNoParameters : BaseLintRule
    {
        // Pattern to look for dynamic values being used directly in SQL
        private static readonly Regex DynamicValuePattern = new Regex(@"['""]\s*\|\s*");
        
        public SQLNoParameters()
        {
            Description = "Detects SQL statements that might be vulnerable to SQL injection";
            Type = ReportType.Error;
            Active = false;  // Set to false by default to be consistent with other linters
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Check if the function is related to SQL execution
            if (!context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase) &&
                !context.genericID().GetText().Equals("CreateSQL", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var args = context.functionCallArguments();
            if (args?.expression() == null || args.expression().Length == 0)
            {
                return;
            }

            var firstArg = args.expression()[0];

            // Check if we're dealing with a string concatenation expression
            if (firstArg is ConcatenationExprContext)
            {
                Reports?.Add(new Report
                {
                    Type = ReportType.Error,
                    Line = context.Start.Line - 1,
                    Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                    Message = "SQL injection risk: String concatenation used to build SQL statement"
                });
                return;
            }

            // Check if we have a literal string
            if (firstArg is LiteralExprContext literalExpr)
            {
                var sqlText = SQLHelper.ExtractSQLFromLiteral(firstArg.GetText());
                
                // Check for common patterns of concatenation in the SQL string
                if (DynamicValuePattern.IsMatch(sqlText))
                {
                    Reports?.Add(new Report
                    {
                        Type = ReportType.Warning,
                        Line = context.Start.Line - 1,
                        Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                        Message = "Potential SQL injection: String appears to contain concatenation"
                    });
                    return;
                }

                // Parse the SQL and check for parameters
                var statement = SQLHelper.ParseSQL(sqlText);
                if (statement != null)
                {
                    // If we have SQL with no parameters and it's not a simple statement
                    // it might be using string concatenation elsewhere
                    if (SQLHelper.GetBindCount(statement) == 0 && IsPotentiallyRiskySql(sqlText))
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Warning,
                            Line = context.Start.Line - 1,
                            Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                            Message = "SQL statement has no parameters. Consider using bind variables for dynamic values."
                        });
                    }
                }
            }
        }

        private bool IsPotentiallyRiskySql(string sqlText)
        {
            // Simple statements that don't need parameters
            if (sqlText.Trim().Equals("COMMIT", StringComparison.OrdinalIgnoreCase) ||
                sqlText.Trim().Equals("ROLLBACK", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // SQL with WHERE clauses or VALUES that don't use parameters
            return (sqlText.Contains("WHERE", StringComparison.OrdinalIgnoreCase) ||
                    sqlText.Contains("VALUES", StringComparison.OrdinalIgnoreCase) ||
                    sqlText.Contains("LIKE", StringComparison.OrdinalIgnoreCase));
        }
        
        public override void Reset()
        {
            // No state to reset in this linter
            if (Reports != null)
            {
                Reports.Clear();
            }
        }
    }
}
