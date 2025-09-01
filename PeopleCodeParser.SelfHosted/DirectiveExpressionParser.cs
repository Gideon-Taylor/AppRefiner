using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Parser for PeopleCode compiler directive expressions
/// Handles complex expressions like: #ToolsRel >= "8.54.01" && #ToolsRel < "8.54.03"
/// </summary>
public class DirectiveExpressionParser
{
    private readonly List<Token> _tokens;
    private int _position;
    private readonly List<string> _errors = new();

    public DirectiveExpressionParser(List<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _position = 0;
    }

    /// <summary>
    /// Errors encountered during parsing
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Current token being processed
    /// </summary>
    private Token Current => _position < _tokens.Count ? _tokens[_position] :
                           Token.CreateEof(new SourcePosition(_tokens.LastOrDefault()?.SourceSpan.End.Index ?? 0));

    /// <summary>
    /// Check if we're at the end of tokens
    /// </summary>
    private bool IsAtEnd => _position >= _tokens.Count;

    /// <summary>
    /// Check if current token matches expected type
    /// </summary>
    private bool Check(TokenType expected) => Current.Type == expected;

    /// <summary>
    /// Consume current token if it matches expected type
    /// </summary>
    private bool Match(TokenType expected)
    {
        if (Check(expected))
        {
            _position++;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consume expected token or report error
    /// </summary>
    private Token Consume(TokenType expected, string message)
    {
        if (Check(expected))
        {
            var token = Current;
            _position++;
            return token;
        }

        _errors.Add($"{message} at position {_position}. Found: {Current.Type}");
        return Current; // Return current token to avoid null reference
    }

    /// <summary>
    /// Parse a complete directive expression
    /// Grammar: directiveExpression -> orExpression
    /// </summary>
    public DirectiveExpressionNode? ParseExpression()
    {
        try
        {
            return ParseOrExpression();
        }
        catch (Exception ex)
        {
            _errors.Add($"Error parsing directive expression: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse OR expressions (lowest precedence)
    /// Grammar: orExpression -> andExpression ('||' andExpression)*
    /// </summary>
    private DirectiveExpressionNode? ParseOrExpression()
    {
        var left = ParseAndExpression();
        if (left == null) return null;

        while (Match(TokenType.DirectiveOr))
        {
            var right = ParseAndExpression();
            if (right == null)
            {
                _errors.Add("Expected expression after '||'");
                return left;
            }

            left = new DirectiveBinaryLogicalNode(left, DirectiveLogicalOperator.Or, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse AND expressions (higher precedence than OR)
    /// Grammar: andExpression -> comparison ('&&' comparison)*
    /// </summary>
    private DirectiveExpressionNode? ParseAndExpression()
    {
        var left = ParseComparison();
        if (left == null) return null;

        while (Match(TokenType.DirectiveAnd))
        {
            var right = ParseComparison();
            if (right == null)
            {
                _errors.Add("Expected expression after '&&'");
                return left;
            }

            left = new DirectiveBinaryLogicalNode(left, DirectiveLogicalOperator.And, right)
            {
                SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
            };
        }

        return left;
    }

    /// <summary>
    /// Parse comparison expressions (highest precedence)
    /// Grammar: comparison -> operand comparisonOp operand
    /// </summary>
    private DirectiveExpressionNode? ParseComparison()
    {
        var left = ParseOperand();
        if (left == null) return null;

        // Parse comparison operator
        var comparisonOp = ParseComparisonOperator();
        if (comparisonOp == null)
        {
            _errors.Add($"Expected comparison operator after {left}");
            return left;
        }

        var right = ParseOperand();
        if (right == null)
        {
            _errors.Add($"Expected operand after comparison operator");
            return left;
        }

        return new DirectiveComparisonNode(left, comparisonOp.Value, right)
        {
            SourceSpan = new SourceSpan(left.SourceSpan.Start, right.SourceSpan.End)
        };
    }

    /// <summary>
    /// Parse operands (#ToolsRel or string literals)
    /// Grammar: operand -> '#ToolsRel' | STRING_LITERAL
    /// </summary>
    private DirectiveOperandNode? ParseOperand()
    {
        var startPos = Current.SourceSpan.Start;

        if (Match(TokenType.DirectiveToolsRel))
        {
            return new DirectiveToolsRelNode
            {
                SourceSpan = new SourceSpan(startPos, Current.SourceSpan.End)
            };
        }

        if (Check(TokenType.StringLiteral))
        {
            var token = Current;
            _position++;
            var value = token.Value?.ToString() ?? "";
            return new DirectiveLiteralNode(value)
            {
                SourceSpan = token.SourceSpan
            };
        }

        _errors.Add($"Expected #ToolsRel or version string literal, found: {Current.Type}");
        return null;
    }

    /// <summary>
    /// Parse comparison operators
    /// </summary>
    private DirectiveComparisonOperator? ParseComparisonOperator()
    {
        DirectiveComparisonOperator? op = Current.Type switch
        {
            TokenType.Equal => DirectiveComparisonOperator.Equal,
            TokenType.NotEqual => DirectiveComparisonOperator.NotEqual,
            TokenType.LessThan => DirectiveComparisonOperator.LessThan,
            TokenType.LessThanOrEqual => DirectiveComparisonOperator.LessThanOrEqual,
            TokenType.GreaterThan => DirectiveComparisonOperator.GreaterThan,
            TokenType.GreaterThanOrEqual => DirectiveComparisonOperator.GreaterThanOrEqual,
            _ => null
        };

        if (op.HasValue)
        {
            _position++;
        }

        return op;
    }

    /// <summary>
    /// Validate that the expression doesn't contain parentheses (not allowed in directive conditions)
    /// </summary>
    public static bool ContainsParentheses(List<Token> tokens)
    {
        return tokens.Any(t => t.Type == TokenType.LeftParen || t.Type == TokenType.RightParen);
    }

    /// <summary>
    /// Extract tokens between start and end positions for directive expression parsing
    /// </summary>
    public static List<Token> ExtractTokensBetween(List<Token> allTokens, int startPos, TokenType endToken)
    {
        var result = new List<Token>();

        for (int i = startPos; i < allTokens.Count; i++)
        {
            if (allTokens[i].Type == endToken)
                break;

            // Skip trivia tokens but include directive-specific tokens
            if (!allTokens[i].Type.IsTrivia() ||
                allTokens[i].Type == TokenType.DirectiveToolsRel ||
                allTokens[i].Type == TokenType.DirectiveAnd ||
                allTokens[i].Type == TokenType.DirectiveOr)
            {
                result.Add(allTokens[i]);
            }
        }

        return result;
    }
}