using AppRefiner.Database;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using PeopleCodeParser.SelfHosted.Nodes;
using SqlParser.Ast;

namespace AppRefiner.Shared.SQL
{
    /// <summary>
    /// Core validation logic for SQLExec operations extracted from SQLExecVariableCount
    /// Converted to work with self-hosted AST nodes instead of ANTLR contexts
    /// </summary>
    public class SQLExecValidator
    {
        private readonly SQLValidationContext context;

        public SQLExecValidator(SQLValidationContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Reset validator state
        /// </summary>
        public void Reset()
        {
            // SQLExec validator is stateless, no reset needed
        }

        /// <summary>
        /// Validate SQLExec function calls
        /// </summary>
        public List<Report> ValidateSQLExec(FunctionCallNode node)
        {
            var reports = new List<Report>();

            // Check if the function being called is "SQLExec"
            if (!(node.Function is IdentifierNode functionId) || 
                !functionId.Name.Equals("SQLExec", StringComparison.OrdinalIgnoreCase))
                return reports;

            if (node.Arguments.Count == 0)
                return reports;

            // Get the first argument
            var firstArg = node.Arguments[0];

            // Check recursively if the first argument contains a concatenation operator
            if (context.ContainsConcatenation(firstArg))
            {
                reports.Add(new Report
                {
                    ReportNumber = 2,
                    Message = "Found SQL using string concatenation.",
                    Type = ReportType.Warning,
                    Line = firstArg.SourceSpan.Start.Line,
                    Span = (firstArg.SourceSpan.Start.Index, firstArg.SourceSpan.End.Index)
                });
            }

            var (sqlText, sqlSpan) = context.GetSqlText(firstArg);

            if (string.IsNullOrWhiteSpace(sqlText))
                return reports;

            try
            {
                var statement = SQLHelper.ParseSQL(sqlText);
                if (statement == null)
                    return reports;

                var outputCount = 0;
                if (statement is Statement.Select select)
                {
                    outputCount = SQLHelper.GetOutputCount(select);
                }

                // Count the binds
                var bindCount = SQLHelper.GetBindCount(statement);
                var totalInOutArgs = node.Arguments.Count - 1; // Exclude the SQL string itself

                if (totalInOutArgs != (outputCount + bindCount))
                {
                    // Report that there are an incorrect number of In/Out parameters and how many there should be
                    reports.Add(new Report
                    {
                        ReportNumber = 3,
                        Message = $"SQL has incorrect number of In/Out parameters. Expected {bindCount + outputCount}, got {totalInOutArgs}.",
                        Type = ReportType.Error,
                        Line = node.SourceSpan.Start.Line,
                        Span = (sqlSpan.Start.Index, node.SourceSpan.End.Index)
                    });
                }
            }
            catch (Exception)
            {
                // SQL parsing failed - ignore
            }

            return reports;
        }
    }
}