using PeopleCodeParser.SelfHosted.Lexing;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Base class for compiler directive expressions
/// </summary>
public abstract class DirectiveExpressionNode
{
    /// <summary>
    /// Source span for this expression
    /// </summary>
    public SourceSpan SourceSpan { get; set; }

    /// <summary>
    /// Evaluate this expression given a ToolsVersion context
    /// </summary>
    /// <param name="toolsVersion">The current ToolsVersion, or null if not configured</param>
    /// <returns>True if the condition is met</returns>
    public abstract bool Evaluate(ToolsVersion? toolsVersion);
}

/// <summary>
/// Binary logical operation in directive expressions (&& or ||)
/// </summary>
public class DirectiveBinaryLogicalNode : DirectiveExpressionNode
{
    public DirectiveExpressionNode Left { get; }
    public DirectiveLogicalOperator Operator { get; }
    public DirectiveExpressionNode Right { get; }

    public DirectiveBinaryLogicalNode(DirectiveExpressionNode left, DirectiveLogicalOperator op, DirectiveExpressionNode right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = op;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool Evaluate(ToolsVersion? toolsVersion)
    {
        return Operator switch
        {
            DirectiveLogicalOperator.And => Left.Evaluate(toolsVersion) && Right.Evaluate(toolsVersion),
            DirectiveLogicalOperator.Or => Left.Evaluate(toolsVersion) || Right.Evaluate(toolsVersion),
            _ => throw new InvalidOperationException($"Unknown logical operator: {Operator}")
        };
    }

    public override string ToString()
    {
        var opSymbol = Operator == DirectiveLogicalOperator.And ? "&&" : "||";
        return $"({Left} {opSymbol} {Right})";
    }
}

/// <summary>
/// Comparison operation in directive expressions (#ToolsRel compared to version string)
/// </summary>
public class DirectiveComparisonNode : DirectiveExpressionNode
{
    public DirectiveOperandNode Left { get; }
    public DirectiveComparisonOperator Operator { get; }
    public DirectiveOperandNode Right { get; }

    public DirectiveComparisonNode(DirectiveOperandNode left, DirectiveComparisonOperator op, DirectiveOperandNode right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = op;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool Evaluate(ToolsVersion? toolsVersion)
    {
        // Handle #ToolsRel vs #ToolsRel comparisons
        if (Left is DirectiveToolsRelNode && Right is DirectiveToolsRelNode)
        {
            // #ToolsRel compared to itself - evaluate based on operator
            return Operator switch
            {
                DirectiveComparisonOperator.Equal => true,          // #ToolsRel = #ToolsRel is always true
                DirectiveComparisonOperator.NotEqual => false,      // #ToolsRel <> #ToolsRel is always false
                DirectiveComparisonOperator.LessThan => false,      // #ToolsRel < #ToolsRel is always false
                DirectiveComparisonOperator.LessThanOrEqual => true,    // #ToolsRel <= #ToolsRel is always true
                DirectiveComparisonOperator.GreaterThan => false,   // #ToolsRel > #ToolsRel is always false
                DirectiveComparisonOperator.GreaterThanOrEqual => true, // #ToolsRel >= #ToolsRel is always true
                _ => throw new InvalidOperationException($"Unknown comparison operator: {Operator}")
            };
        }

        // Handle #ToolsRel vs version string comparisons
        var toolsRelOperand = Left is DirectiveToolsRelNode ? Left : Right;
        var versionOperand = Left is DirectiveLiteralNode ? Left : Right;

        if (toolsRelOperand is not DirectiveToolsRelNode || versionOperand is not DirectiveLiteralNode versionLiteral)
        {
            throw new InvalidOperationException("Directive comparison must be between #ToolsRel operands and/or version string literals");
        }

        // Handle case where no ToolsVersion is configured
        if (toolsVersion == null)
        {
            // Use "newer branch" policy - prefer branches that suggest newer versions
            return PreferNewerBranch(Operator, Left == toolsRelOperand);
        }

        try
        {
            var compareVersion = new ToolsVersion(versionLiteral.Value);
            var comparison = toolsVersion.CompareTo(compareVersion);

            // Handle operand order - if ToolsRel is on the right, invert the operator
            var effectiveOperator = Left == toolsRelOperand ? Operator : InvertOperator(Operator);

            return effectiveOperator switch
            {
                DirectiveComparisonOperator.Equal => comparison == 0,
                DirectiveComparisonOperator.NotEqual => comparison != 0,
                DirectiveComparisonOperator.LessThan => comparison < 0,
                DirectiveComparisonOperator.LessThanOrEqual => comparison <= 0,
                DirectiveComparisonOperator.GreaterThan => comparison > 0,
                DirectiveComparisonOperator.GreaterThanOrEqual => comparison >= 0,
                _ => throw new InvalidOperationException($"Unknown comparison operator: {effectiveOperator}")
            };
        }
        catch (ArgumentException)
        {
            // Invalid version string - fall back to "newer branch" policy
            return PreferNewerBranch(Operator, Left == toolsRelOperand);
        }
    }

    private bool PreferNewerBranch(DirectiveComparisonOperator op, bool toolsRelOnLeft)
    {
        // Default to preferring ELSE over THEN branch (newer code)
        // For < and <= operators, return true to prefer THEN branch
        // For >= and > operators, return false to prefer ELSE branch
        var effectiveOperator = toolsRelOnLeft ? op : InvertOperator(op);
        return effectiveOperator is DirectiveComparisonOperator.LessThan or DirectiveComparisonOperator.LessThanOrEqual;
    }

    private DirectiveComparisonOperator InvertOperator(DirectiveComparisonOperator op)
    {
        return op switch
        {
            DirectiveComparisonOperator.LessThan => DirectiveComparisonOperator.GreaterThan,
            DirectiveComparisonOperator.LessThanOrEqual => DirectiveComparisonOperator.GreaterThanOrEqual,
            DirectiveComparisonOperator.GreaterThan => DirectiveComparisonOperator.LessThan,
            DirectiveComparisonOperator.GreaterThanOrEqual => DirectiveComparisonOperator.LessThanOrEqual,
            DirectiveComparisonOperator.Equal => DirectiveComparisonOperator.Equal, // Symmetric
            DirectiveComparisonOperator.NotEqual => DirectiveComparisonOperator.NotEqual, // Symmetric
            _ => throw new InvalidOperationException($"Unknown operator: {op}")
        };
    }

    public override string ToString()
    {
        var opSymbol = Operator switch
        {
            DirectiveComparisonOperator.Equal => "=",
            DirectiveComparisonOperator.NotEqual => "<>",
            DirectiveComparisonOperator.LessThan => "<",
            DirectiveComparisonOperator.LessThanOrEqual => "<=",
            DirectiveComparisonOperator.GreaterThan => ">",
            DirectiveComparisonOperator.GreaterThanOrEqual => ">=",
            _ => "?"
        };
        return $"{Left} {opSymbol} {Right}";
    }
}

/// <summary>
/// Base class for operands in directive expressions
/// </summary>
public abstract class DirectiveOperandNode : DirectiveExpressionNode
{
}

/// <summary>
/// #ToolsRel operand in directive expressions
/// </summary>
public class DirectiveToolsRelNode : DirectiveOperandNode
{
    public override bool Evaluate(ToolsVersion? toolsVersion)
    {
        // This should not be called directly - ToolsRel nodes are evaluated within comparisons
        throw new InvalidOperationException("ToolsRel nodes cannot be evaluated independently");
    }

    public override string ToString()
    {
        return "#ToolsRel";
    }
}

/// <summary>
/// String literal operand in directive expressions (version strings)
/// </summary>
public class DirectiveLiteralNode : DirectiveOperandNode
{
    public string Value { get; }

    public DirectiveLiteralNode(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override bool Evaluate(ToolsVersion? toolsVersion)
    {
        // This should not be called directly - literals are evaluated within comparisons
        throw new InvalidOperationException("Literal nodes cannot be evaluated independently");
    }

    public override string ToString()
    {
        return $"\"{Value}\"";
    }
}

/// <summary>
/// Logical operators for directive expressions
/// </summary>
public enum DirectiveLogicalOperator
{
    And,    // &&
    Or      // ||
}

/// <summary>
/// Comparison operators for directive expressions
/// </summary>
public enum DirectiveComparisonOperator
{
    Equal,                  // =
    NotEqual,               // <> or !=
    LessThan,               // <
    LessThanOrEqual,        // <=
    GreaterThan,            // >
    GreaterThanOrEqual      // >=
}