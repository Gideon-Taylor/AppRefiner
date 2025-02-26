using Antlr4.Runtime;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class CreateSQLVariableCount : ScopedLintRule<SQLStatementInfo>
    {
        public CreateSQLVariableCount()
        {
            Description = "Validate bind counts in CreateSQL objects.";
            Type = ReportType.Error;
            Active = true;
        }
        private int GetOutputCount(Statement.Select statement)
        {
            return statement.Query.Body.AsSelectExpression().Select.Projection.Count;
        }
        private int GetBindCount(Statement statement)
        {
            var placeHolderRegex = new Regex("Placeholder { Value = (:[0-9]+) }");
            var matches = placeHolderRegex.Matches(statement.ToString());
            HashSet<string> placeHolders = new HashSet<string>();
            foreach (Match match in matches)
            {
                placeHolders.Add(match.Groups[1].Value);
            }
            return placeHolders.Count;
        }

        private bool IsTypeSQL(TypeTContext typeContext)
        {
            if (typeContext is SimpleTypeTypeContext simpleType)
            {
                if (simpleType.simpleType() is SimpleGenericIDContext genericId)
                {
                    return genericId.GENERIC_ID_LIMITED()?.GetText().Equals("SQL", StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }
            return false;
        }

        public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
        {
            base.EnterLocalVariableDefinition(context);
            if (!IsTypeSQL(context.typeT()))
                return;

            foreach (var varNode in context.USER_VARIABLE())
            {
                var varName = varNode.GetText();
                var defaultInfo = new SQLStatementInfo(
                    sqlText: "",
                    bindCount: 0,
                    outputColumnCount: 0,
                    line: varNode.Symbol.Line,
                    span: (varNode.Symbol.StartIndex, varNode.Symbol.StopIndex),
                    varName: varName
                );

                AddToCurrentScope(varName, defaultInfo);
            }
        }

        public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
        {
            base.EnterLocalVariableDeclAssignment(context);

            if (!IsTypeSQL(context.typeT()))
                return;

            var varName = context.USER_VARIABLE().GetText();
            var defaultInfo = new SQLStatementInfo(
                sqlText: "",
                bindCount: 0,
                outputColumnCount: 0,
                line: context.USER_VARIABLE().Symbol.Line,
                span: (context.USER_VARIABLE().Symbol.StartIndex, context.USER_VARIABLE().Symbol.StopIndex),
                varName: varName
            );

            AddToCurrentScope(varName, defaultInfo);
        }

        private void ValidateArguments(string sqlText, FunctionCallArgumentsContext args, SQLStatementInfo sqlInfo, ParserRuleContext context, bool AllowZeroBinds = false)
        {
            try
            {
                var ast = new SqlQueryParser().Parse(sqlText, new PeopleSoftSQLDialect());
                var statement = ast.FirstOrDefault();
                if (statement == null)
                    return;


                if (statement is Statement.Select select)
                {
                    sqlInfo.OutputColumnCount = GetOutputCount(select);
                }

                var bindCount = GetBindCount(statement);
                var totalInOutArgs = args.expression().Length - 1;

                // Update SQLStatementInfo
                sqlInfo.SqlText = sqlText;
                sqlInfo.BindCount = bindCount;

                if (totalInOutArgs == 0 && AllowZeroBinds) return;

                if (totalInOutArgs != bindCount)
                {
                    Reports?.Add(new Report
                    {
                        Type = ReportType.Error,
                        Line = context.Start.Line - 1,
                        Span = (args.expression()[0].Start.StartIndex, context.Stop.StopIndex),
                        Message = $"SQL statement has incorrect number of bind parameters. Expected {bindCount}, got {totalInOutArgs}."
                    });
                }


            }
            catch (Exception)
            {
                // SQL parsing failed - ignore
            }
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            if (!context.genericID().GetText().Equals("CreateSQL", StringComparison.OrdinalIgnoreCase))
                return;

            var args = context.functionCallArguments();
            if (args?.expression() == null || args.expression().Length == 0)
                return;

            if (args.expression().Length == 1)
            {
                /* Warn that no binds are provided, make sure you call Execute before fetching */
                Reports?.Add(new Report
                {
                    Type = ReportType.Warning,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = "CreateSQL with no binds provided. Make sure to call Execute before Fetch."
                });

            }

            var firstArg = args.expression()[0];
            if (firstArg is not LiteralExprContext literalExpr)
                return;

            var sqlText = literalExpr.GetText();
            // Remove quotes from SQL text
            sqlText = sqlText.Substring(1, sqlText.Length - 2);

            // Validate arguments and update SQLStatementInfo
            var sqlInfo = new SQLStatementInfo(sqlText, 0, 0, context.Start.Line, (context.Start.StartIndex, context.Stop.StopIndex), "");


            string? varName = null;
            if (context.Parent.Parent is LocalVariableDeclAssignmentContext localAssign)
            {
                varName = localAssign.GetChild(2).GetText();
            }

            if (context.Parent.Parent is EqualityExprContext equalityAssign)
            {
                varName = equalityAssign.GetChild(0).GetText();
            }
            
            //context.Parent.Parent.GetChild(2).GetText();
            if (varName != null)
            {
                ReplaceInFoundScope(varName, sqlInfo);
                ValidateArguments(sqlText, args, sqlInfo, context, true);
            }

        }

        public override void EnterDotAccess(DotAccessContext context)
        {
            var functionName = context.genericID().GetText();
            if (!functionName.Equals("Open", StringComparison.OrdinalIgnoreCase) &&
                !functionName.Equals("Execute", StringComparison.OrdinalIgnoreCase) &&
                !functionName.Equals("Fetch", StringComparison.OrdinalIgnoreCase))
                return;

            // Get the expression before the dot
            var expr = context.Parent as DotAccessExprContext;
            if (expr?.expression() is not IdentifierExprContext idExpr)
                return;

            var varName = idExpr.ident().GetText();

            // Check if this is a SQL variable we're tracking
            if (!TryFindInScopes(varName, out var sqlInfo))
                return;

            // Validate the function call arguments
            var args = context.functionCallArguments();
            if (args == null)
                return;

            if (functionName.Equals("Fetch", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFetchCall(context, args, sqlInfo);
            }
            else if (functionName.Equals("Open", StringComparison.OrdinalIgnoreCase))
            {
                ValidateOpenCall(context, args, sqlInfo);
            }
            else
            {
                // Existing Execute validation
                var argCount = args.expression()?.Length ?? 0;
                if (argCount != sqlInfo.BindCount)
                {
                    Reports?.Add(new Report
                    {
                        Type = ReportType.Error,
                        Line = context.Start.Line - 1,
                        Span = (context.Start.StartIndex, context.Stop.StopIndex),
                        Message = $"SQL.{functionName} has incorrect number of bind parameters. Expected {sqlInfo.BindCount}, got {argCount}."
                    });
                }
            }
        }

        private void ValidateOpenCall(DotAccessContext context, FunctionCallArgumentsContext args, SQLStatementInfo sqlInfo)
        {
            var firstArg = args.expression()[0];
            if (firstArg is not LiteralExprContext literalExpr)
                return;

            var sqlText = literalExpr.GetText();
            // Remove quotes from SQL text
            sqlText = sqlText.Substring(1, sqlText.Length - 2);

            // Validate arguments and update SQLStatementInfo
            ValidateArguments(sqlText, args, sqlInfo, context);
        }

        private void ValidateFetchCall(DotAccessContext context, FunctionCallArgumentsContext args, SQLStatementInfo sqlInfo)
        {
            var expressions = args.expression();
            if (expressions == null || expressions.Length == 0)
            {
                Reports?.Add(new Report
                {
                    Type = ReportType.Error,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = "SQL.Fetch requires at least one output parameter."
                });
                return;
            }

            // If there's only one argument, we need to check if it's an array type
            if (expressions.Length == 1)
            {
                // If it's not an array type and we have output columns, report an error
                if (sqlInfo.OutputColumnCount > 1)
                {

                    var singleArg = expressions[0];

                    TryGetVariableInfo(singleArg.GetText(), out var info);
                    if (info != null && info.Type.StartsWith("Array", StringComparison.OrdinalIgnoreCase))
                    {
                        // If it's an array type, we can proceed
                        return;
                    }
                    else
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Error,
                            Line = context.Start.Line - 1,
                            Span = (context.Start.StartIndex, context.Stop.StopIndex),
                            Message = $"SQL.Fetch parameter is not an array which is needed to handle {sqlInfo.OutputColumnCount} output columns."
                        });
                    }
                }

            }
            else
            {
                // Multiple parameters must match the exact number of output columns
                if (expressions.Length != sqlInfo.OutputColumnCount)
                {
                    Reports?.Add(new Report
                    {
                        Type = ReportType.Error,
                        Line = context.Start.Line - 1,
                        Span = (context.Start.StartIndex, context.Stop.StopIndex),
                        Message = $"SQL.Fetch has incorrect number of output parameters. Expected {sqlInfo.OutputColumnCount}, got {expressions.Length}."
                    });
                }
            }
        }
    }
}
