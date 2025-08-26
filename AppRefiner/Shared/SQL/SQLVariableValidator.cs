using AppRefiner.Database;
using AppRefiner.Linters;
using AppRefiner.PeopleCode;
using AppRefiner.Shared.SQL.Models;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted;
using SqlParser.Ast;
using SQLStatementInfo = AppRefiner.Shared.SQL.Models.SQLStatementInfo;

namespace AppRefiner.Shared.SQL
{
    /// <summary>
    /// Core validation logic for CreateSQL/GetSQL operations extracted from CreateSQLVariableCount
    /// Converted to work with self-hosted AST nodes instead of ANTLR contexts
    /// </summary>
    public class SQLVariableValidator
    {
        private readonly SQLValidationContext context;
        private readonly Dictionary<string, SQLStatementInfo> trackedVariables = new();

        public SQLVariableValidator(SQLValidationContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Reset validator state (equivalent to linter Reset)
        /// </summary>
        public void Reset()
        {
            trackedVariables.Clear();
        }

        /// <summary>
        /// Validate local variable declaration of SQL type
        /// </summary>
        public List<Report> ValidateVariableDeclaration(LocalVariableDeclarationNode node)
        {
            var reports = new List<Report>();
            
            if (!IsTypeSQL(node.Type))
                return reports;

            foreach (var varInfo in node.VariableNameInfos)
            {
                var varName = varInfo.Name;
                var span = varInfo.SourceSpan;
                
                var defaultInfo = new SQLStatementInfo(
                    sqlText: "",
                    bindCount: 0,
                    outputColumnCount: 0,
                    line: span.Start.Line,
                    span: span,
                    varName: varName
                );

                trackedVariables[varName] = defaultInfo;
            }

            return reports;
        }

        /// <summary>
        /// Validate local variable declaration with assignment of SQL type
        /// </summary>
        public List<Report> ValidateVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            var reports = new List<Report>();
            
            if (!IsTypeSQL(node.Type))
                return reports;

            var varName = node.VariableName;
            var span = node.VariableNameInfo?.SourceSpan ?? node.SourceSpan;
            
            var defaultInfo = new SQLStatementInfo(
                sqlText: "",
                bindCount: 0,
                outputColumnCount: 0,
                line: span.Start.Line,
                span: span,
                varName: varName
            );

            trackedVariables[varName] = defaultInfo;
            return reports;
        }

        /// <summary>
        /// Validate CreateSQL or GetSQL function calls
        /// </summary>
        public List<Report> ValidateCreateSQL(FunctionCallNode node)
        {
            var reports = new List<Report>();

            if (!(node.Function is IdentifierNode functionId))
                return reports;

            var functionName = functionId.Name;
            if (!functionName.Equals("CreateSQL", StringComparison.OrdinalIgnoreCase) &&
                !functionName.Equals("GetSQL", StringComparison.OrdinalIgnoreCase))
                return reports;

            if (node.Arguments.Count == 0)
                return reports;

            var firstArg = node.Arguments[0];

            // Check for string concatenation
            if (context.ContainsConcatenation(firstArg))
            {
                reports.Add(new Report
                {
                    ReportNumber = 7,
                    Message = "Found SQL using string concatenation.",
                    Type = ReportType.Warning,
                    Line = firstArg.SourceSpan.Start.Line,
                    Span = (firstArg.SourceSpan.Start.Index, firstArg.SourceSpan.End.Index)
                });
            }

            // Try to find the variable being assigned to
            string? varName = FindAssignmentTarget(node);
            var (sqlText, sqlSpan) = context.GetSqlText(firstArg);

            // Create SQLStatementInfo for tracking
            var sqlInfo = new SQLStatementInfo(
                sqlText,
                0,
                0,
                node.SourceSpan.Start.Line,
                sqlSpan,
                varName ?? ""
            );

            if (varName != null)
            {
                trackedVariables[varName] = sqlInfo;
                if (sqlText != null)
                {
                    var validationReports = ValidateArguments(sqlText, node.Arguments.Skip(1).ToList(), sqlInfo, node, ValidationMode.CreateOrGet);
                    reports.AddRange(validationReports);
                }
            }

            return reports;
        }

        /// <summary>
        /// Validate SQL object method calls (Open, Execute, Fetch)
        /// </summary>
        public List<Report> ValidateSQLMethodCall(MemberAccessNode memberAccess, FunctionCallNode functionCall)
        {
            var reports = new List<Report>();

            var methodName = memberAccess.MemberName;
            if (!methodName.Equals("Open", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Equals("Execute", StringComparison.OrdinalIgnoreCase) &&
                !methodName.Equals("Fetch", StringComparison.OrdinalIgnoreCase))
                return reports;

            // Get the variable name from the target
            if (!(memberAccess.Target is IdentifierNode varId))
                return reports;

            var varName = varId.Name;

            // Check if this is a SQL variable we're tracking
            if (!trackedVariables.TryGetValue(varName, out SQLStatementInfo? sqlInfo))
                return reports;

            if (sqlInfo == null)
                return reports;

            if (methodName.Equals("Fetch", StringComparison.OrdinalIgnoreCase))
            {
                var fetchReports = ValidateFetchCall(functionCall, sqlInfo);
                reports.AddRange(fetchReports);
            }
            else if (methodName.Equals("Open", StringComparison.OrdinalIgnoreCase))
            {
                var openReports = ValidateOpenCall(functionCall, sqlInfo);
                reports.AddRange(openReports);
            }
            else // Execute
            {
                sqlInfo.InVarsBound = true;

                if (!sqlInfo.HasValidSqlText)
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 8,
                        Message = $"Cannot validate SQL.{methodName} - SQL text is empty or could not be resolved.",
                        Type = ReportType.Info,
                        Line = functionCall.SourceSpan.Start.Line,
                        Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                    });
                    return reports;
                }

                var argCount = functionCall.Arguments.Count;
                if (argCount != sqlInfo.BindCount)
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 9,
                        Message = $"SQL.{methodName} has incorrect number of bind parameters. Expected {sqlInfo.BindCount}, got {argCount}.",
                        Type = ReportType.Error,
                        Line = functionCall.SourceSpan.Start.Line,
                        Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                    });
                }
            }

            return reports;
        }

        private bool IsTypeSQL(TypeNode typeNode)
        {
            // Check if the type is "SQL" (built-in SQL type)
            if (typeNode is BuiltInTypeNode builtInType)
            {
                return builtInType.Type == BuiltInType.Sql;
            }
            return false;
        }

        private string? FindAssignmentTarget(FunctionCallNode functionCall)
        {
            // Navigate up the AST to find assignment context
            var parent = functionCall.Parent;
            
            // Check for local variable declaration with assignment
            if (parent is LocalVariableDeclarationWithAssignmentNode localDecl)
            {
                return localDecl.VariableName;
            }
            
            // Check for assignment expression
            if (parent is AssignmentNode assignment && assignment.Target is IdentifierNode targetId)
            {
                return targetId.Name;
            }
            
            return null;
        }

        private enum ValidationMode
        {
            CreateOrGet, Open, Execute, Fetch
        }

        private List<Report> ValidateArguments(string? sqlText, List<ExpressionNode> args, SQLStatementInfo sqlInfo, PeopleCodeParser.SelfHosted.AstNode context, ValidationMode validationMode)
        {
            var reports = new List<Report>();
            
            if (string.IsNullOrWhiteSpace(sqlText))
                return reports;

            string[] metaSQLToSkip = ["%UpdatePairs", "%KeyEqual", "%Insert", "%SelectAll", "%Delete", "%Update"];

            foreach (var metaSQL in metaSQLToSkip)
            {
                if (sqlText.Trim().Contains(metaSQL, StringComparison.OrdinalIgnoreCase))
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 3,
                        Message = "Cannot validate SQL using certain MetaSQL constructs like %Insert, %Update, %SelectAll etc.",
                        Type = ReportType.Info,
                        Line = context.SourceSpan.Start.Line,
                        Span = (args.FirstOrDefault()?.SourceSpan.Start.Index ?? context.SourceSpan.Start.Index, context.SourceSpan.End.Index)
                    });
                    return reports;
                }
            }

            try
            {
                var statement = SQLHelper.ParseSQL(sqlText);
                if (statement == null)
                {
                    sqlInfo.ParseFailed = true;
                    return reports;
                }

                if (statement is Statement.Select select)
                {
                    sqlInfo.OutputColumnCount = SQLHelper.GetOutputCount(select);
                }

                var bindCount = SQLHelper.GetBindCount(statement);
                var totalInOutArgs = args.Count;

                // Update SQLStatementInfo
                sqlInfo.SqlText = sqlText;
                sqlInfo.BindCount = bindCount;
                sqlInfo.ParseFailed = false;

                if (validationMode == ValidationMode.CreateOrGet && totalInOutArgs == 0)
                {
                    sqlInfo.NeedsOpenOrExec = true;
                    sqlInfo.InVarsBound = false;
                    return reports;
                }

                if (validationMode == ValidationMode.CreateOrGet)
                {
                    sqlInfo.InVarsBound = true;

                    if (totalInOutArgs != bindCount)
                    {
                        reports.Add(new Report
                        {
                            ReportNumber = 4,
                            Message = $"SQL statement has incorrect number of input parameters. Expected {bindCount}, got {totalInOutArgs}.",
                            Type = ReportType.Error,
                            Line = context.SourceSpan.Start.Line,
                            Span = (args.FirstOrDefault()?.SourceSpan.Start.Index ?? context.SourceSpan.Start.Index, context.SourceSpan.End.Index)
                        });
                    }
                    return reports;
                }

                if (validationMode == ValidationMode.Open)
                {
                    if (totalInOutArgs != sqlInfo.BindCount)
                    {
                        reports.Add(new Report
                        {
                            ReportNumber = 5,
                            Message = $"SQL statement has incorrect number of input parameters. Expected {bindCount}, got {totalInOutArgs}.",
                            Type = ReportType.Error,
                            Line = context.SourceSpan.Start.Line,
                            Span = (args.FirstOrDefault()?.SourceSpan.Start.Index ?? context.SourceSpan.Start.Index, context.SourceSpan.End.Index)
                        });
                    }
                    return reports;
                }

                if (totalInOutArgs != bindCount + sqlInfo.OutputColumnCount)
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 6,
                        Message = $"SQL statement has incorrect number of In/Out parameters. Expected {bindCount + sqlInfo.OutputColumnCount}, got {totalInOutArgs}.",
                        Type = ReportType.Error,
                        Line = context.SourceSpan.Start.Line,
                        Span = (args.FirstOrDefault()?.SourceSpan.Start.Index ?? context.SourceSpan.Start.Index, context.SourceSpan.End.Index)
                    });
                }
            }
            catch (Exception)
            {
                // SQL parsing failed - mark as failed and skip further validation
                sqlInfo.ParseFailed = true;
            }

            return reports;
        }

        private List<Report> ValidateOpenCall(FunctionCallNode functionCall, SQLStatementInfo sqlInfo)
        {
            var reports = new List<Report>();

            if (functionCall.Arguments.Count == 0)
                return reports;

            if (!sqlInfo.HasValidSqlText && functionCall.Arguments.Count > 0)
            {
                var firstArg = functionCall.Arguments[0];

                // Check for string concatenation
                if (context.ContainsConcatenation(firstArg))
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 10,
                        Message = "Found SQL using string concatenation.",
                        Type = ReportType.Warning,
                        Line = firstArg.SourceSpan.Start.Line,
                        Span = (firstArg.SourceSpan.Start.Index, firstArg.SourceSpan.End.Index)
                    });
                }

                var (sqlText, sqlSpan) = context.GetSqlText(firstArg);

                // If we now have SQL text, update the sqlInfo
                if (!string.IsNullOrWhiteSpace(sqlText))
                {
                    sqlInfo.SqlText = sqlText;
                    var validationReports = ValidateArguments(sqlText, functionCall.Arguments.ToList(), sqlInfo, functionCall, ValidationMode.Open);
                    reports.AddRange(validationReports);
                    return reports;
                }
            }

            // If we still don't have valid SQL text, check if this requires parameters
            if (!sqlInfo.HasValidSqlText && functionCall.Arguments.Count > 0)
            {
                reports.Add(new Report
                {
                    ReportNumber = 11,
                    Message = "Cannot validate SQL.Open - SQL text is empty or could not be resolved.",
                    Type = ReportType.Info,
                    Line = functionCall.SourceSpan.Start.Line,
                    Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                });
            }
            else if (sqlInfo.HasValidSqlText)
            {
                var validationReports = ValidateArguments(sqlInfo.SqlText, functionCall.Arguments.ToList(), sqlInfo, functionCall, ValidationMode.Open);
                reports.AddRange(validationReports);
            }

            return reports;
        }

        private List<Report> ValidateFetchCall(FunctionCallNode functionCall, SQLStatementInfo sqlInfo)
        {
            var reports = new List<Report>();
            
            // Cannot validate calls where we don't have the SQL text
            if (!sqlInfo.HasValidSqlText)
            {
                reports.Add(new Report
                {
                    ReportNumber = 12,
                    Message = "Cannot validate SQL.Fetch - SQL text is empty or could not be resolved.",
                    Type = ReportType.Info,
                    Line = functionCall.SourceSpan.Start.Line,
                    Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                });
                return reports;
            }

            if (sqlInfo.InVarsBound == false && sqlInfo.BindCount > 0)
            {
                reports.Add(new Report
                {
                    ReportNumber = 13,
                    Message = "SQL.Fetch called before bind values were provided. Make sure to call Open/Execute before Fetch.",
                    Type = ReportType.Error,
                    Line = functionCall.SourceSpan.Start.Line,
                    Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                });
            }

            var args = functionCall.Arguments;
            if (args.Count == 0)
            {
                reports.Add(new Report
                {
                    ReportNumber = 14,
                    Message = "SQL.Fetch requires at least one output parameter.",
                    Type = ReportType.Error,
                    Line = functionCall.SourceSpan.Start.Line,
                    Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                });
                return reports;
            }

            // If there's only one argument, we need to check if it's an array type
            if (args.Count == 1)
            {
                // If it's not an array type and we have output columns, report an error
                if (sqlInfo.OutputColumnCount > 1)
                {
                    var singleArg = args[0];
                    
                    // For now, we'll skip detailed type checking since we don't have variable type tracking in this validator
                    // This would need to be enhanced with type information from the scope
                    
                    reports.Add(new Report
                    {
                        ReportNumber = 15,
                        Message = $"SQL.Fetch parameter may not be an array or record which is needed to handle {sqlInfo.OutputColumnCount} output columns.",
                        Type = ReportType.Warning,
                        Line = functionCall.SourceSpan.Start.Line,
                        Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                    });
                }
            }
            else
            {
                // Multiple parameters must match the exact number of output columns
                if (args.Count != sqlInfo.OutputColumnCount)
                {
                    reports.Add(new Report
                    {
                        ReportNumber = 16,
                        Message = $"SQL.Fetch has incorrect number of output parameters. Expected {sqlInfo.OutputColumnCount}, got {args.Count}.",
                        Type = ReportType.Error,
                        Line = functionCall.SourceSpan.Start.Line,
                        Span = (functionCall.SourceSpan.Start.Index, functionCall.SourceSpan.End.Index)
                    });
                }
            }

            return reports;
        }
    }
}