using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using SqlParser;
using SqlParser.Ast;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class SQLExecVariableCount : BaseLintRule
    {
        public override string LINTER_ID => "SQL_EXEC_VAR";
        
        public SQLExecVariableCount()
        {
            Description = "Validate bind counts in SQLExec functions.";
            Type = ReportType.Error;
            Active = false;
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        private (string? sqlText, int start, int stop) GetSqlText(ExpressionContext expr)
        {
            // Handle SQL.NAME format
            if (expr is DotAccessExprContext dotAccess)
            {
                var leftExpr = dotAccess.expression();
                if (leftExpr is IdentifierExprContext idExpr && 
                    idExpr.ident().GetText().Equals("SQL", StringComparison.OrdinalIgnoreCase))
                {
                    if (dotAccess.children[1] is DotAccessContext dotAccessCtx)
                    {
                        var defName = dotAccessCtx.genericID().GetText();
                        if (DataManager != null)
                        {
                            var sqlText = DataManager.GetSqlDefinition(defName);
                            if (string.IsNullOrWhiteSpace(sqlText))
                            {
                                Reports?.Add(CreateReport(
                                    1,
                                    $"Invalid SQL definition: {defName}",
                                    ReportType.Error,
                                    expr.Start.Line - 1,
                                    (expr.Start.StartIndex, expr.Stop.StopIndex)
                                ));
                                return (null, expr.Start.StartIndex, expr.Stop.StopIndex);
                            }
                            return (sqlText, expr.Start.StartIndex, expr.Stop.StopIndex);
                        }
                        return (null, expr.Start.StartIndex, expr.Stop.StopIndex);
                    }
                }
            }
            // Handle string literal
            else if (expr is LiteralExprContext literalExpr)
            {
                var sqlText = literalExpr.GetText();
                // Remove quotes from SQL text
                sqlText = sqlText.Substring(1, sqlText.Length - 2);
                return (sqlText, expr.Start.StartIndex, expr.Stop.StopIndex);
            }

            return (null, expr.Start.StartIndex, expr.Stop.StopIndex);
        }

        private bool ContainsConcatenation(ExpressionContext expr)
        {
            // If the expression is directly a concatenation, return true.
            if (expr is ConcatenationExprContext)
            {
                return true;
            }

            // If the expression is parenthesized, examine the inner expression.
            if (expr is ParenthesizedExprContext parenthesized)
            {
                return ContainsConcatenation(parenthesized.expression());
            }

            // Otherwise, iterate over all children and check any nested expressions.
            if (expr.children != null)
            {
                foreach (var child in expr.children)
                {
                    if (child is ExpressionContext childExpr && ContainsConcatenation(childExpr))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Check if the function being called is "SQLExec"
            if (!context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase))
                return;

            var args = context.functionCallArguments();
            if (args?.expression() == null || args.expression().Length == 0)
                return;

            // Get the first argument
            var firstArg = args.expression()[0];

            // Check recursively if the first argument contains a concatenation operator
            if (ContainsConcatenation(firstArg))
            {
                Reports?.Add(CreateReport(
                    2,
                    "Found SQL using string concatenation.",
                    Type,
                    firstArg.Start.Line - 1,
                    (firstArg.Start.StartIndex, firstArg.Stop.StopIndex)
                ));
            }


            var (sqlText, start, stop) = GetSqlText(firstArg);

            if (string.IsNullOrWhiteSpace(sqlText))
                return;

            try
            {
                var statement = SQLHelper.ParseSQL(sqlText);
                if (statement == null)
                    return;

                var outputCount = 0;
                if (statement is Statement.Select select)
                {
                    outputCount = SQLHelper.GetOutputCount(select);
                }

                /* Count the binds */
                var bindCount = SQLHelper.GetBindCount(statement);
                var totalInOutArgs = args.expression().Length - 1;
                
                if (totalInOutArgs != (outputCount + bindCount))
                {
                    /* Report that there are an incorrect number of In/Out parameters and how many there should be */
                    Reports?.Add(CreateReport(
                        3,
                        $"SQL has incorrect number of In/Out parameters. Expected {bindCount + outputCount}, got {totalInOutArgs}.",
                        ReportType.Error,
                        context.Start.Line - 1,
                        (start, context.Stop.StopIndex)
                    ));
                }
            }
            catch (Exception)
            {
                // SQL parsing failed - ignore
            }
        }

        public override void Reset()
        {
        }
    }
}
