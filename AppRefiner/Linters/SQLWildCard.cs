using SqlParser.Ast;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    class SQLWildCard : BaseLintRule
    {
        public override string LINTER_ID => "SQL_WILDCARD";

        public SQLWildCard()
        {
            Description = "Reports any SQL using * wildcards";
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

            // We can only process this rule for functions that have a literal string as the first argument
            if (firstArg is LiteralNode literal && literal.LiteralType == LiteralType.String)
            {
                var sqlText = literal.Value?.ToString() ?? "";
                var statement = SQLHelper.ParseSQL(sqlText);

                if (statement == null)
                {
                    return;
                }

                if (statement is Statement.Select select && SQLHelper.HasWildcard(select))
                {
                    // Report WARNING that there is a wildcard in a select statement
                    AddReport(
                        1,
                        "SQL has a wildcard in select statement.",
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
