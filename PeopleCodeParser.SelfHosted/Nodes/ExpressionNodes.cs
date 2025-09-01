using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Base class for all expression nodes
/// </summary>
public abstract class ExpressionNode : AstNode
{
    /// <summary>
    /// Inferred type of this expression (set during semantic analysis)
    /// </summary>
    public TypeNode? InferredType { get; set; }

    /// <summary>
    /// True if this expression can be assigned to (is an l-value)
    /// </summary>
    public virtual bool IsLValue => false;

    /// <summary>
    /// True if this expression has side effects
    /// </summary>
    public virtual bool HasSideEffects => false;
}

public class ClassConstantNode : ExpressionNode
{
    public string ClassName { get; }
    public string ConstantName { get; }
    public override bool HasSideEffects => false;
    public override bool IsLValue => false;

    public ClassConstantNode(string className, string constantName)
    {
        ClassName = className ?? throw new ArgumentNullException(nameof(className));
        ConstantName = constantName ?? throw new ArgumentNullException(nameof(constantName));
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitClassConstant(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitClassConstant(this);
    }
}

/// <summary>
/// Binary operation expression (a + b, a AND b, etc.)
/// </summary>
public class BinaryOperationNode : ExpressionNode
{
    /// <summary>
    /// Left operand
    /// </summary>
    public ExpressionNode Left { get; }

    /// <summary>
    /// Operator
    /// </summary>
    public BinaryOperator Operator { get; }

    /// <summary>
    /// Indicates if the operator is negated (e.g., NOT =, NOT >, etc.)
    /// </summary>
    public bool NotFlag { get; }

    /// <summary>
    /// Right operand
    /// </summary>
    public ExpressionNode Right { get; }

    public override bool HasSideEffects => Left.HasSideEffects || Right.HasSideEffects;

    public BinaryOperationNode(ExpressionNode left, BinaryOperator op, bool notFlag, ExpressionNode right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = op;
        NotFlag = notFlag;
        Right = right ?? throw new ArgumentNullException(nameof(right));

        AddChildren(left, right);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitBinaryOperation(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBinaryOperation(this);
    }

    public override string ToString()
    {
        return $"({Left} {Operator.GetSymbol()} {Right})";
    }
}

/// <summary>
/// Unary operation expression (-a, NOT a)
/// </summary>
public class UnaryOperationNode : ExpressionNode
{
    /// <summary>
    /// Operator
    /// </summary>
    public UnaryOperator Operator { get; }

    /// <summary>
    /// Operand
    /// </summary>
    public ExpressionNode Operand { get; }

    public override bool HasSideEffects => Operand.HasSideEffects;

    public UnaryOperationNode(UnaryOperator op, ExpressionNode operand)
    {
        Operator = op;
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));

        AddChild(operand);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitUnaryOperation(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitUnaryOperation(this);
    }

    public override string ToString()
    {
        return $"({Operator.GetSymbol()}{Operand})";
    }
}

/// <summary>
/// Literal value expression (123, "hello", TRUE)
/// </summary>
public class LiteralNode : ExpressionNode
{
    /// <summary>
    /// The literal value
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// The literal type
    /// </summary>
    public LiteralType LiteralType { get; }

    public LiteralNode(object? value, LiteralType literalType)
    {
        Value = value;
        LiteralType = literalType;
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitLiteral(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitLiteral(this);
    }

    public override string ToString()
    {
        return LiteralType switch
        {
            LiteralType.String => $"\"{Value}\"",
            LiteralType.Null => "NULL",
            _ => Value?.ToString() ?? "NULL"
        };
    }
}

/// <summary>
/// Identifier expression (&variable, %USERID, MyFunction)
/// </summary>
public class IdentifierNode : ExpressionNode
{
    /// <summary>
    /// Identifier name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Identifier type
    /// </summary>
    public IdentifierType IdentifierType { get; }

    public override bool IsLValue => IdentifierType is IdentifierType.UserVariable or IdentifierType.Generic;

    public IdentifierNode(string name, IdentifierType identifierType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IdentifierType = identifierType;
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitIdentifier(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIdentifier(this);
    }

    public override string ToString()
    {
        return Name;
    }
}


/// <summary>
/// Property access expression (obj.Property)
/// </summary>
public class PropertyAccessNode : ExpressionNode
{
    /// <summary>
    /// Target object
    /// </summary>
    public ExpressionNode Target { get; }

    /// <summary>
    /// Property name
    /// </summary>
    public string PropertyName { get; }

    public override bool IsLValue => true;
    public override bool HasSideEffects => Target.HasSideEffects;

    public PropertyAccessNode(ExpressionNode target, string propertyName)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));

        AddChild(target);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitPropertyAccess(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitPropertyAccess(this);
    }

    public override string ToString()
    {
        return $"{Target}.{PropertyName}";
    }
}

/// <summary>
/// Array access expression (array[index])
/// </summary>
public class ArrayAccessNode : ExpressionNode
{
    /// <summary>
    /// Array expression
    /// </summary>
    public ExpressionNode Array { get; }

    /// <summary>
    /// Index expressions
    /// </summary>
    public List<ExpressionNode> Indices { get; }

    public override bool IsLValue => true;
    public override bool HasSideEffects => Array.HasSideEffects || Indices.Any(i => i.HasSideEffects);

    public ArrayAccessNode(ExpressionNode array, IEnumerable<ExpressionNode> indices)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));
        Indices = indices?.ToList() ?? throw new ArgumentNullException(nameof(indices));

        if (Indices.Count == 0)
            throw new ArgumentException("At least one index is required", nameof(indices));

        AddChild(array);
        AddChildren(Indices);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitArrayAccess(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitArrayAccess(this);
    }

    public override string ToString()
    {
        var indicesStr = string.Join(", ", Indices);
        return $"{Array}[{indicesStr}]";
    }
}

/// <summary>
/// Object creation expression (CREATE Class(args))
/// </summary>
public class ObjectCreationNode : ExpressionNode
{
    /// <summary>
    /// Type being created
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Constructor arguments
    /// </summary>
    public List<ExpressionNode> Arguments { get; }

    public override bool HasSideEffects => true; // Object creation has side effects

    public ObjectCreationNode(TypeNode type, IEnumerable<ExpressionNode> arguments)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Arguments = arguments?.ToList() ?? throw new ArgumentNullException(nameof(arguments));

        AddChild(type);
        AddChildren(Arguments);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitObjectCreation(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitObjectCreation(this);
    }

    public override string ToString()
    {
        var argsStr = string.Join(", ", Arguments);
        return $"CREATE {Type}({argsStr})";
    }
}

/// <summary>
/// Type cast expression (expr AS Type)
/// </summary>
public class TypeCastNode : ExpressionNode
{
    /// <summary>
    /// Expression being cast
    /// </summary>
    public ExpressionNode Expression { get; }

    /// <summary>
    /// Target type
    /// </summary>
    public TypeNode TargetType { get; }

    public override bool HasSideEffects => Expression.HasSideEffects;

    public TypeCastNode(ExpressionNode expression, TypeNode targetType)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));

        AddChildren(expression, targetType);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitTypeCast(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitTypeCast(this);
    }

    public override string ToString()
    {
        return $"({Expression} AS {TargetType})";
    }
}

/// <summary>
/// Parenthesized expression ((expr))
/// </summary>
public class ParenthesizedExpressionNode : ExpressionNode
{
    /// <summary>
    /// Inner expression
    /// </summary>
    public ExpressionNode Expression { get; }

    public override bool IsLValue => Expression.IsLValue;
    public override bool HasSideEffects => Expression.HasSideEffects;

    public ParenthesizedExpressionNode(ExpressionNode expression)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        AddChild(expression);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitParenthesized(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitParenthesized(this);
    }

    public override string ToString()
    {
        return $"({Expression})";
    }
}

/// <summary>
/// Assignment expression (a = b, a += b)
/// </summary>
public class AssignmentNode : ExpressionNode
{
    /// <summary>
    /// Left-hand side (target)
    /// </summary>
    public ExpressionNode Target { get; }

    /// <summary>
    /// Assignment operator
    /// </summary>
    public AssignmentOperator Operator { get; }

    /// <summary>
    /// Right-hand side (value)
    /// </summary>
    public ExpressionNode Value { get; }

    public override bool HasSideEffects => true; // Assignments have side effects

    public AssignmentNode(ExpressionNode target, AssignmentOperator op, ExpressionNode value)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Operator = op;
        Value = value ?? throw new ArgumentNullException(nameof(value));

        AddChildren(target, value);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitAssignment(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitAssignment(this);
    }

    public override string ToString()
    {
        return $"{Target} {Operator.GetSymbol()} {Value}";
    }
}

/// <summary>
/// Binary operators
/// </summary>
public enum BinaryOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,

    // Comparison
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    // Logical
    And,
    Or,

    // String
    Concatenate
}

/// <summary>
/// Unary operators
/// </summary>
public enum UnaryOperator
{
    Negate,
    Not,
    Reference
}

/// <summary>
/// Assignment operators
/// </summary>
public enum AssignmentOperator
{
    Assign,           // =
    AddAssign,        // +=
    SubtractAssign,   // -=
    ConcatenateAssign // |=
}

/// <summary>
/// Literal types
/// </summary>
public enum LiteralType
{
    Integer,
    Decimal,
    String,
    Boolean,
    Null
}

/// <summary>
/// Identifier types
/// </summary>
public enum IdentifierType
{
    Generic,
    UserVariable,
    SystemVariable,
    SystemConstant,
    Super
}

/// <summary>
/// Extension methods for operators
/// </summary>
public static class OperatorExtensions
{
    public static string GetSymbol(this BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Power => "**",
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.And => "AND",
        BinaryOperator.Or => "OR",
        BinaryOperator.Concatenate => "|",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    public static string GetSymbol(this UnaryOperator op) => op switch
    {
        UnaryOperator.Negate => "-",
        UnaryOperator.Not => "NOT ",
        UnaryOperator.Reference => "@",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    public static string GetSymbol(this AssignmentOperator op) => op switch
    {
        AssignmentOperator.Assign => "=",
        AssignmentOperator.AddAssign => "+=",
        AssignmentOperator.SubtractAssign => "-=",
        AssignmentOperator.ConcatenateAssign => "|=",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    public static int GetPrecedence(this BinaryOperator op) => op switch
    {
        BinaryOperator.Or => 1,
        BinaryOperator.And => 2,
        BinaryOperator.Equal or BinaryOperator.NotEqual => 3,
        BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or 
        BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => 4,
        BinaryOperator.Concatenate => 5,
        BinaryOperator.Add or BinaryOperator.Subtract => 6,
        BinaryOperator.Multiply or BinaryOperator.Divide => 7,
        BinaryOperator.Power => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    public static bool IsRightAssociative(this BinaryOperator op) => op switch
    {
        BinaryOperator.Power => true,
        _ => false
    };
}

/// <summary>
/// Function call expression (Function(args) or obj.Method(args))
/// </summary>
public class FunctionCallNode : ExpressionNode
{
    /// <summary>
    /// Function expression (identifier or member access)
    /// </summary>
    public ExpressionNode Function { get; }

    /// <summary>
    /// Function arguments
    /// </summary>
    public List<ExpressionNode> Arguments { get; }

    public override bool HasSideEffects => true; // Function calls generally have side effects

    public FunctionCallNode(ExpressionNode function, IEnumerable<ExpressionNode> arguments)
    {
        Function = function ?? throw new ArgumentNullException(nameof(function));
        Arguments = arguments?.ToList() ?? new List<ExpressionNode>();

        AddChild(function);
        AddChildren(Arguments);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitFunctionCall(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitFunctionCall(this);
    }

    public override string ToString()
    {
        var argsStr = string.Join(", ", Arguments);
        return $"{Function}({argsStr})";
    }
}

/// <summary>
/// Member access expression (obj.member) - unified property and method access
/// </summary>
public class MemberAccessNode : ExpressionNode
{
    /// <summary>
    /// Target object
    /// </summary>
    public ExpressionNode Target { get; }

    /// <summary>
    /// Member name
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// True if this is dynamic member access (obj."string")
    /// </summary>
    public bool IsDynamic { get; }

    public override bool IsLValue => true;
    public override bool HasSideEffects => Target.HasSideEffects;

    public MemberAccessNode(ExpressionNode target, string memberName, bool isDynamic = false)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
        IsDynamic = isDynamic;

        AddChild(target);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitMemberAccess(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMemberAccess(this);
    }

    public override string ToString()
    {
        var memberStr = IsDynamic ? $"\"{MemberName}\"" : MemberName;
        return $"{Target}.{memberStr}";
    }
}

/// <summary>
/// Represents a metadata expression (app class path used as an expression)
/// </summary>
public class MetadataExpressionNode : ExpressionNode
{
    /// <summary>
    /// The app class type referenced in the metadata expression
    /// </summary>
    public AppClassTypeNode AppClassType { get; }

    public MetadataExpressionNode(AppClassTypeNode appClassType)
    {
        AppClassType = appClassType ?? throw new ArgumentNullException(nameof(appClassType));
        AddChild(appClassType);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitMetadataExpression(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMetadataExpression(this);
    }

    public override string ToString()
    {
        return AppClassType.ToString();
    }
}