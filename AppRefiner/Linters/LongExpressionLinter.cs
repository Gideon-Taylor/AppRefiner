using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted;

namespace AppRefiner.Linters
{
    /// <summary>
    /// Linter that identifies overly complex expressions that should be simplified
    /// </summary>
    public class LongExpressionLinter : BaseLintRule
    {
        public override string LINTER_ID => "LONG_EXPR";
        
        /// <summary>
        /// Maximum character length for an expression before it's considered too long
        /// </summary>
        public int MaxExpressionLength { get; set; } = 200;
        
        /// <summary>
        /// Maximum number of operators in a single expression before it's considered too complex
        /// </summary>
        public int MaxOperatorCount { get; set; } = 5;

        public LongExpressionLinter()
        {
            Description = "Detects overly complex expressions";
            Type = ReportType.Warning;
            Active = false;
        }

        public override void VisitBinaryOperation(BinaryOperationNode node)
        {
            CheckExpressionComplexity(node);
            base.VisitBinaryOperation(node);
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            CheckExpressionComplexity(node);
            base.VisitFunctionCall(node);
        }

        private void CheckExpressionComplexity(AstNode node)
        {
            // Skip expressions within expressions to avoid duplication
            if (node.Parent is ExpressionNode)
                return;

            // Check expression length
            var expressionText = node.SourceSpan.ToString();
            if (expressionText.Length > MaxExpressionLength)
            {
                AddReport(
                    1,
                    $"Expression is too long ({expressionText.Length} chars). Consider breaking it down into smaller parts.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
                return;
            }

            // Count operators in complex expressions
            int operatorCount = CountOperators(node);
            if (operatorCount > MaxOperatorCount)
            {
                AddReport(
                    2,
                    $"Expression is too complex with {operatorCount} operators. Consider simplifying.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }
        }

        private int CountOperators(AstNode node)
        {
            int count = 0;

            // Count operators in binary operations
            if (node is BinaryOperationNode)
            {
                count++;
            }

            // Function calls add complexity
            if (node is FunctionCallNode)
            {
                count++;  // Count the call itself as an operation
            }

            // Recursively count operators in child expressions
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (child is ExpressionNode childExpr)
                    {
                        count += CountOperators(childExpr);
                    }
                }
            }

            return count;
        }
    }
}
