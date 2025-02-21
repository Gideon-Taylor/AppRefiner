using AppRefiner.PeopleCode;
using SqlParser.Ast;
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
    class SQLWildCard : BaseLintRule
    {
        public SQLWildCard()
        {
            Description = "Reports any SQL using * wildcards";
            Type = ReportType.Error;
            Active = false;
        }

        public override void EnterSimpleFunctionCall(PeopleCodeParser.SimpleFunctionCallContext context)
        {
            // Check if the function being called is "SQLExec"
            if (context.genericID().GetText().Equals("SQLExec", StringComparison.OrdinalIgnoreCase) ||
                context.genericID().GetText().Equals("CreateSQL", StringComparison.OrdinalIgnoreCase))
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

                        if (statement is Statement.Select)
                        {
                            // Set a variable to the select statement's body
                            var select = statement as Statement.Select;
                            var body = select.Query.Body as SetExpression.SelectExpression;
                            var columns = body.Select.Projection;
                            outputCount = columns.Count;

                            if (columns.Any(x => x is SelectItem.Wildcard || x is SelectItem.QualifiedWildcard))
                            {
                                /* Report ERROR that there is a Wildcard in a select statement */
                                Reports?.Add(new Report()
                                {
                                    Type = ReportType.Error,
                                    Line = firstArg.Start.Line - 1,
                                    Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                                    Message = $"SQL has a wildcard in select statement."
                                });
                            }
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
