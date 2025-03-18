using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.Linters.Models;
using AppRefiner.PeopleCode;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /* Issues: 
     * SQL that fails to parse should indicate this on the SQLInfo object, and further checks against this object should be skipped
     * SQL Text starting with "%Insert" should not be evaluated 
     */
    public class CreateSQLVariableCount : ScopedLintRule<SQLStatementInfo>
    {
        private enum ValidationMode
        {
            CreateOrGet, Open, Execute, Fetch
        }

        public CreateSQLVariableCount()
        {
            Description = "Validate bind counts in CreateSQL objects.";
            Type = ReportType.Error;
            Active = true;
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

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
                                Reports?.Add(new Report
                                {
                                    Type = ReportType.Error,
                                    Line = expr.Start.Line - 1,
                                    Span = (expr.Start.StartIndex, expr.Stop.StopIndex),
                                    Message = $"Invalid SQL definition: {defName}"
                                });
                                return (null, expr.Start.StartIndex, expr.Stop.StopIndex);
                            }
                            return (sqlText, expr.Start.StartIndex, expr.Stop.StopIndex);
                        }
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Info,
                            Line = expr.Start.Line - 1,
                            Span = (expr.Start.StartIndex, expr.Stop.StopIndex),
                            Message = "Connect to DB to validate this SQL usage."
                        });
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

        private void ValidateArguments(string? sqlText, FunctionCallArgumentsContext args, SQLStatementInfo sqlInfo, ParserRuleContext context, ValidationMode validationMode)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
                return;

            try
            {
                var statement = SQLHelper.ParseSQL(sqlText);
                if (statement == null)
                    return;

                if (statement is Statement.Select select)
                {
                    sqlInfo.OutputColumnCount = SQLHelper.GetOutputCount(select);
                }

                var bindCount = SQLHelper.GetBindCount(statement);
                var totalInOutArgs = args.expression().Length - 1;

                // Update SQLStatementInfo
                sqlInfo.SqlText = sqlText;
                sqlInfo.BindCount = bindCount;

                if (validationMode == ValidationMode.CreateOrGet && totalInOutArgs == 0 )
                {
                    sqlInfo.NeedsOpenOrExec = true;
                    sqlInfo.InVarsBound = false;
                    return;
                }

                if (validationMode == ValidationMode.CreateOrGet)
                {
                    sqlInfo.InVarsBound = true;

                    if (totalInOutArgs != bindCount)
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Error,
                            Line = context.Start.Line - 1,
                            Span = (args.expression()[0].Start.StartIndex, context.Stop.StopIndex),
                            Message = $"SQL statement has incorrect number of input parameters. Expected {bindCount}, got {totalInOutArgs}."
                        });
                    }
                    return;
                }

                if (validationMode == ValidationMode.Open)
                {
                    if (totalInOutArgs != sqlInfo.BindCount)
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Error,
                            Line = context.Start.Line - 1,
                            Span = (args.expression()[0].Start.StartIndex, context.Stop.StopIndex),
                            Message = $"SQL statement has incorrect number of input parameters. Expected {bindCount}, got {totalInOutArgs}."
                        });
                    }
                    return;
                }

                if (totalInOutArgs != bindCount + sqlInfo.OutputColumnCount)
                {
                    Reports?.Add(new Report
                    {
                        Type = ReportType.Error,
                        Line = context.Start.Line - 1,
                        Span = (args.expression()[0].Start.StartIndex, context.Stop.StopIndex),
                        Message = $"SQL statement has incorrect number of In/Out parameters. Expected {bindCount + sqlInfo.OutputColumnCount}, got {totalInOutArgs}."
                    });
                }
            }
            catch (Exception)
            {
                // SQL parsing failed - ignore
            }
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
            var functionName = context.genericID().GetText();
            if (!functionName.Equals("CreateSQL", StringComparison.OrdinalIgnoreCase) 
                && !functionName.Equals("GetSQL", StringComparison.OrdinalIgnoreCase))
                return;

            var args = context.functionCallArguments();
            if (args?.expression() == null || args.expression().Length == 0)
                return;

            var firstArg = args.expression()[0];

            // Check recursively if the first argument contains a concatenation operator
            if (ContainsConcatenation(firstArg))
            {
                Reports?.Add(new Report()
                {
                    Type = Type,
                    Line = firstArg.Start.Line - 1,
                    Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                    Message = $"Found SQL using string concatenation."
                });
            }


            string? varName = null;

            if (context.Parent.Parent is LocalVariableDeclAssignmentContext localAssign)
            {
                varName = localAssign.GetChild(2).GetText();
            }

            if (context.Parent.Parent is EqualityExprContext equalityAssign)
            {
                varName = equalityAssign.GetChild(0).GetText();
            }

            var (sqlText, start, stop) = GetSqlText(firstArg);
            
            // Create SQLStatementInfo even if sqlText is null to track the variable
            var sqlInfo = new SQLStatementInfo(
                sqlText, 
                0, 
                0, 
                context.Start.Line, 
                (start, stop), 
                varName ?? ""
            );

            if (varName != null)
            {
                ReplaceInFoundScope(varName, sqlInfo);
                if (sqlText != null)
                {
                    ValidateArguments(sqlText, args, sqlInfo, context, ValidationMode.CreateOrGet);
                }
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
                sqlInfo.InVarsBound = true;
                // Existing Execute validation
                var argCount = args.expression()?.Length ?? 0;
                if (argCount == sqlInfo.BindCount)
                {
                    
                    return;
                }

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

            // Check recursively if the first argument contains a concatenation operator
            if (ContainsConcatenation(firstArg))
            {
                Reports?.Add(new Report()
                {
                    Type = Type,
                    Line = firstArg.Start.Line - 1,
                    Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                    Message = $"Found SQL using string concatenation."
                });
            }

            var (sqlText, start, stop) = GetSqlText(firstArg);

            // Validate arguments and update SQLStatementInfo
            ValidateArguments(sqlText, args, sqlInfo, context, ValidationMode.Open);
        }

        private void ValidateFetchCall(DotAccessContext context, FunctionCallArgumentsContext args, SQLStatementInfo sqlInfo)
        {
            /* Cannot validate calls where we don't have the SQL text... */
            if (sqlInfo.SqlText == null) return;

            if (sqlInfo.InVarsBound == false && sqlInfo.BindCount > 0)
            {
                Reports?.Add(new Report
                {
                    Type = ReportType.Error,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = "SQL.Fetch called before bind values were provided. Make sure to call Open/Execute before Fetch."
                });
            }


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
                    if (info != null && info.Type.StartsWith("Array", StringComparison.OrdinalIgnoreCase) || info.Type.Equals("Record",StringComparison.OrdinalIgnoreCase))
                    {
                        // If it's an array type or a Record, we can proceed
                        return;
                    }
                    else
                    {
                        Reports?.Add(new Report
                        {
                            Type = ReportType.Error,
                            Line = context.Start.Line - 1,
                            Span = (context.Start.StartIndex, context.Stop.StopIndex),
                            Message = $"SQL.Fetch parameter is not an array or record which is needed to handle {sqlInfo.OutputColumnCount} output columns."
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
