using SqlParser.Ast;
using static AppRefiner.PeopleCode.PeopleCodeParser;

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

                    /* We can only process this rule for functions that have a literal string as the first argument */
                    if (firstArg is LiteralExprContext)
                    {
                        var sqlText = SQLHelper.ExtractSQLFromLiteral(firstArg.GetText());
                        var statement = SQLHelper.ParseSQL(sqlText);

                        if (statement == null)
                        {
                            return;
                        }

                        if (statement is Statement.Select select && SQLHelper.HasWildcard(select))
                        {
                            /* Report WARNING that there is a wildcard in a select statement */
                            AddReport(
                                1,
                                "SQL has a wildcard in select statement.",
                                this.Type,
                                firstArg.Start.Line - 1,
                                (firstArg.Start.StartIndex, firstArg.Stop.StopIndex)
                            );
                        }
                    }
                }
            }
        }

        public override void Reset()
        {
        }
    }
}
