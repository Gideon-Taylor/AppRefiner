using AppRefiner.PeopleCode;
using SqlParser.Ast;
using SqlParser.Dialects;
using SqlParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        private int GetBindCount(Statement statement)
        {
            /* Placeholders look like this: Placeholder { Value = :1 } */
            /* extract out the ":1" */
            var placeHolderRegex = new Regex("Placeholder { Value = (:[0-9]+) }");
            var matches = placeHolderRegex.Matches(statement.ToString());
            HashSet<string> placeHolders = new HashSet<string>();
            foreach (Match match in matches)
            {
                placeHolders.Add(match.Groups[1].Value);
            }

            return placeHolders.Count;
        }


        public override void EnterSimpleFunctionCall(PeopleCodeParser.SimpleFunctionCallContext context)
        {
            // Check if the function being called is "SQLExec"
            if (context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase))
            {
                var args = context.functionCallArguments();
                if (args != null && args.expression() != null && args.expression().Length > 0)
                {
                    // Get the first argument
                    var firstArg = args.expression()[0];

                    /* We can only really process this rule for SQLExec and CreateSQL  that have a literal string as the first argument */
                    if (firstArg is LiteralExprContext)
                    {
                        var sqlText = firstArg.GetText();
                        sqlText = sqlText.Substring(1, sqlText.Length - 2);

                        var ast = new SqlQueryParser().Parse(sqlText, new PeopleSoftSQLDialect());
                        /* check if ast is the Select subclass */

                        var statement = ast.First();
                        if (statement == null)
                        {
                            return;
                        }

                        var outputCount = 0;

                        /* Count the binds */
                        var bindCount = GetBindCount(statement);
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
