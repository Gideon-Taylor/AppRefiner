using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    class SQLLongString : BaseLintRule
    {
        public override string LINTER_ID => "SQL_LONG";
        private const int MaxSqlLength = 120;
        
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
                            Reports?.Add(CreateReport(
                                1,
                                "Long literal SQL statements should be SQL objects.",
                                Type,
                                firstArg.Start.Line - 1,
                                (firstArg.Start.StartIndex, firstArg.Stop.StopIndex)
                            ));
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
