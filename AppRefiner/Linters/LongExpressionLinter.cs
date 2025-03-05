using AppRefiner.PeopleCode;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies overly complex expressions that should be simplified
    /// </summary>
    public class LongExpressionLinter : BaseLintRule
    {
        private const int MaxExpressionLength = 200; // Maximum character length for an expression
        private const int MaxOperatorCount = 5; // Maximum number of operators in a single expression
        
        public LongExpressionLinter()
        {
            Description = "Detects overly complex expressions";
            Type = ReportType.Warning;
            Active = false;
        }

        // Handle each specific expression type
        public override void EnterAddSubtrExpr(AddSubtrExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterMultDivExpr(MultDivExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterEqualityExpr(EqualityExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterComparisonExpr(ComparisonExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterAndOrExpr(AndOrExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterConcatenationExpr(ConcatenationExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterExponentialExpr(ExponentialExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        // Handle additional expression types
        public override void EnterParenthesizedExpr(ParenthesizedExprContext context)
        {
            CheckExpressionComplexity(context);
        }

        public override void EnterFunctionCallExpr(FunctionCallExprContext context)
        {
            CheckExpressionComplexity(context);
        }


        private void CheckExpressionComplexity(ExpressionContext context)
        {
            // Skip expressions within expressions to avoid duplication
            if (context.Parent is ExpressionContext)
                return;
                
            // Check expression length
            var expressionText = context.GetText();
            if (expressionText.Length > MaxExpressionLength)
            {
                Reports?.Add(new Report
                {
                    Type = Type,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = $"Expression is too long ({expressionText.Length} chars). Consider breaking it down into smaller parts."
                });
                return;
            }
            
            // Count operators in complex expressions
            int operatorCount = CountOperators(context);
            if (operatorCount > MaxOperatorCount)
            {
                Reports?.Add(new Report
                {
                    Type = Type,
                    Line = context.Start.Line - 1,
                    Span = (context.Start.StartIndex, context.Stop.StopIndex),
                    Message = $"Expression is too complex with {operatorCount} operators. Consider simplifying."
                });
            }
        }
        
        private int CountOperators(ExpressionContext context)
        {
            int count = 0;
            
            // Count operators in this expression
            if (context is AddSubtrExprContext || 
                context is MultDivExprContext || 
                context is EqualityExprContext || 
                context is ComparisonExprContext || 
                context is AndOrExprContext || 
                context is ConcatenationExprContext ||
                context is ExponentialExprContext)
            {
                count++;
            }
            
            // Function calls, method calls, and object creation can add complexity
            if (context is FunctionCallExprContext)
            {
                // Count parameter expressions separately via children iteration
                count++;  // Count the call itself as an operation
            }
            
            // Recursively count operators in child expressions
            if (context.children != null)
            {
                foreach (var child in context.children)
                {
                    if (child is ExpressionContext childExpr)
                    {
                        count += CountOperators(childExpr);
                    }
                }
            }
            
            return count;
        }

        public override void Reset()
        {
        }
    }
}
