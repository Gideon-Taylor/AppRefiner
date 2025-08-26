using AppRefiner.Database;
using AppRefiner.Shared.SQL.Models;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace AppRefiner.Shared.SQL
{
    /// <summary>
    /// Context for SQL validation operations, providing shared state and helper methods
    /// </summary>
    public class SQLValidationContext
    {
        public IDataManager? DataManager { get; set; }
        
        /// <summary>
        /// Current scope information for variable tracking
        /// </summary>
        public object? CurrentScope { get; set; }
        
        /// <summary>
        /// Helper method to extract SQL text from an expression node
        /// </summary>
        public (string? sqlText, PeopleCodeParser.SelfHosted.SourceSpan span) GetSqlText(ExpressionNode expr)
        {
            // Handle SQL.NAME format (metadata expression)
            if (expr is MemberAccessNode memberAccess)
            {
                if (memberAccess.Target is IdentifierNode idNode &&
                    idNode.Name.Equals("SQL", StringComparison.OrdinalIgnoreCase))
                {
                    var defName = memberAccess.MemberName;
                    if (DataManager != null)
                    {
                        var sqlText = DataManager.GetSqlDefinition(defName);
                        return (sqlText, expr.SourceSpan);
                    }
                    return (null, expr.SourceSpan);
                }
            }
            // Handle string literal
            else if (expr is LiteralNode literal && literal.LiteralType == LiteralType.String)
            {
                var sqlText = literal.Value?.ToString();
                return (sqlText, expr.SourceSpan);
            }

            return (null, expr.SourceSpan);
        }
        
        /// <summary>
        /// Check if an expression contains string concatenation recursively
        /// </summary>
        public bool ContainsConcatenation(ExpressionNode expr)
        {
            // Direct concatenation
            if (expr is BinaryOperationNode binOp && binOp.Operator == BinaryOperator.Concatenate)
            {
                return true;
            }

            // Parenthesized expression
            if (expr is ParenthesizedExpressionNode parenthesized)
            {
                return ContainsConcatenation(parenthesized.Expression);
            }

            // Check children recursively
            if (expr.Children != null)
            {
                foreach (var child in expr.Children.OfType<ExpressionNode>())
                {
                    if (ContainsConcatenation(child))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}