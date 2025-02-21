using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class SQLWithConcatenation : BaseLintRule
    {
        public SQLWithConcatenation()
        {
            Type = ReportType.Warning;
            Description = "String concatenation used in SQLExec/CreateSQL";
            Active = false;
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
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

                    // Check recursively if the first argument contains a concatenation operator
                    if (ContainsConcatenation(firstArg))
                    {
                        Reports?.Add(new Report()
                        {
                            Type = ReportType.Warning,
                            Line = firstArg.Start.Line - 1,
                            Span = (firstArg.Start.StartIndex, firstArg.Stop.StopIndex),
                            Message = $"Found SQL using string concatenation."
                        });
                    }
                }
            }
        }

        public override void Reset()
        {
        }


        // Recursive helper method that checks if an expression or any subexpression uses the concatenation operator (|).
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
    }
}
