using AppRefiner.PeopleCode;
using SqlParser.Ast;
using System;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class SQLExecVariableCount : BaseLintRule
    {
        public SQLExecVariableCount()
        {
            Description = "Validate bind counts in SQLExec functions.";
            Type = ReportType.Error;
            Active = false;
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Check if the function being called is "SQLExec"
            if (context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase))
            {
                var args = context.functionCallArguments();
                if (args != null && args.expression() != null && args.expression().Length > 0)
                {
                    // Get the first argument
                    var firstArg = args.expression()[0];

                    /* We can only really process this rule for SQLExec and CreateSQL that have a literal string as the first argument */
                    if (firstArg is LiteralExprContext)
                    {
                        var sqlText = SQLHelper.ExtractSQLFromLiteral(firstArg.GetText());
                        var statement = SQLHelper.ParseSQL(sqlText);
                        
                        if (statement == null)
                        {
                            return;
                        }

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
                            Reports?.Add(new Report()
                            {
                                Type = ReportType.Error,
                                Line = context.Start.Line - 1,
                                Span = (firstArg.Start.StartIndex, context.Stop.StopIndex),
                                Message = $"SQL has incorrect number of In/Out parameters. Expected {bindCount + outputCount}, got {totalInOutArgs}."
                            });
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
