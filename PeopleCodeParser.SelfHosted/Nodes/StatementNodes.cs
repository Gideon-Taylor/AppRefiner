using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Base class for all statement nodes
/// </summary>
public abstract class StatementNode : AstNode
{
    /// <summary>
    /// True if this statement can transfer control (RETURN, BREAK, etc.)
    /// </summary>
    public virtual bool CanTransferControl => false;

    /// <summary>
    /// True if this statement introduces a new scope
    /// </summary>
    public virtual bool IntroducesScope => false;
}

/// <summary>
/// Block of statements (introduces a new scope)
/// </summary>
public class BlockNode : StatementNode
{
    /// <summary>
    /// Statements in this block
    /// </summary>
    public List<StatementNode> Statements { get; } = new();

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => Statements.Any(s => s.CanTransferControl);

    public BlockNode(IEnumerable<StatementNode>? statements = null)
    {
        if (statements != null)
        {
            AddStatements(statements);
        }
    }

    public void AddStatement(StatementNode statement)
    {
        Statements.Add(statement);
        AddChild(statement);
    }

    public void AddStatements(IEnumerable<StatementNode> statements)
    {
        foreach (var statement in statements)
        {
            AddStatement(statement);
        }
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitBlock(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBlock(this);
    }

    public override string ToString()
    {
        return $"Block ({Statements.Count} statements)";
    }
}

/// <summary>
/// IF statement
/// </summary>
public class IfStatementNode : StatementNode
{
    /// <summary>
    /// Condition expression
    /// </summary>
    public ExpressionNode Condition { get; }

    /// <summary>
    /// THEN block
    /// </summary>
    public BlockNode ThenBlock { get; }

    /// <summary>
    /// ELSE block (optional)
    /// </summary>
    public BlockNode? ElseBlock { get; set; }

    public override bool CanTransferControl => ThenBlock.CanTransferControl || (ElseBlock?.CanTransferControl ?? false);

    public IfStatementNode(ExpressionNode condition, BlockNode thenBlock)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        ThenBlock = thenBlock ?? throw new ArgumentNullException(nameof(thenBlock));

        AddChildren(condition, thenBlock);
    }

    public void SetElseBlock(BlockNode elseBlock)
    {
        if (ElseBlock != null)
            RemoveChild(ElseBlock);

        ElseBlock = elseBlock;
        if (elseBlock != null)
            AddChild(elseBlock);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitIf(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitIf(this);
    }

    public override string ToString()
    {
        return ElseBlock != null ? "IF-THEN-ELSE" : "IF-THEN";
    }
}

/// <summary>
/// FOR statement
/// </summary>
public class ForStatementNode : StatementNode
{
    /// <summary>
    /// Loop variable name
    /// </summary>
    public string Variable { get; }

    /// <summary>
    /// Starting value expression
    /// </summary>
    public ExpressionNode FromValue { get; }

    /// <summary>
    /// Ending value expression
    /// </summary>
    public ExpressionNode ToValue { get; }

    /// <summary>
    /// Step value expression (optional, defaults to 1)
    /// </summary>
    public ExpressionNode? StepValue { get; set; }

    /// <summary>
    /// Loop body
    /// </summary>
    public BlockNode Body { get; }

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => Body.CanTransferControl;

    public ForStatementNode(string variable, ExpressionNode fromValue, ExpressionNode toValue, BlockNode body)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
        FromValue = fromValue ?? throw new ArgumentNullException(nameof(fromValue));
        ToValue = toValue ?? throw new ArgumentNullException(nameof(toValue));
        Body = body ?? throw new ArgumentNullException(nameof(body));

        AddChildren(fromValue, toValue, body);
    }

    public void SetStepValue(ExpressionNode stepValue)
    {
        if (StepValue != null)
            RemoveChild(StepValue);

        StepValue = stepValue;
        if (stepValue != null)
            AddChild(stepValue);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitFor(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitFor(this);
    }

    public override string ToString()
    {
        var stepStr = StepValue != null ? $" STEP {StepValue}" : "";
        return $"FOR {Variable} = {FromValue} TO {ToValue}{stepStr}";
    }
}

/// <summary>
/// WHILE statement
/// </summary>
public class WhileStatementNode : StatementNode
{
    /// <summary>
    /// Condition expression
    /// </summary>
    public ExpressionNode Condition { get; }

    /// <summary>
    /// Loop body
    /// </summary>
    public BlockNode Body { get; }

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => Body.CanTransferControl;

    public WhileStatementNode(ExpressionNode condition, BlockNode body)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Body = body ?? throw new ArgumentNullException(nameof(body));

        AddChildren(condition, body);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitWhile(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitWhile(this);
    }

    public override string ToString()
    {
        return $"WHILE {Condition}";
    }
}

/// <summary>
/// REPEAT-UNTIL statement
/// </summary>
public class RepeatStatementNode : StatementNode
{
    /// <summary>
    /// Loop body
    /// </summary>
    public BlockNode Body { get; }

    /// <summary>
    /// Condition expression (loop continues until this is true)
    /// </summary>
    public ExpressionNode Condition { get; }

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => Body.CanTransferControl;

    public RepeatStatementNode(BlockNode body, ExpressionNode condition)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));

        AddChildren(body, condition);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitRepeat(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitRepeat(this);
    }

    public override string ToString()
    {
        return $"REPEAT-UNTIL {Condition}";
    }
}

/// <summary>
/// EVALUATE statement
/// </summary>
public class EvaluateStatementNode : StatementNode
{
    /// <summary>
    /// Expression being evaluated
    /// </summary>
    public ExpressionNode Expression { get; }

    /// <summary>
    /// WHEN clauses
    /// </summary>
    public List<WhenClause> WhenClauses { get; } = new();

    /// <summary>
    /// WHEN-OTHER clause (optional)
    /// </summary>
    public BlockNode? WhenOtherBlock { get; set; }

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => 
        WhenClauses.Any(w => w.Body.CanTransferControl) || 
        (WhenOtherBlock?.CanTransferControl ?? false);

    public EvaluateStatementNode(ExpressionNode expression)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        AddChild(expression);
    }

    public void AddWhenClause(WhenClause whenClause)
    {
        WhenClauses.Add(whenClause);
        AddChildren(whenClause.Condition, whenClause.Body);
    }

    public void SetWhenOtherBlock(BlockNode whenOtherBlock)
    {
        if (WhenOtherBlock != null)
            RemoveChild(WhenOtherBlock);

        WhenOtherBlock = whenOtherBlock;
        if (whenOtherBlock != null)
            AddChild(whenOtherBlock);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitEvaluate(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitEvaluate(this);
    }

    public override string ToString()
    {
        return $"EVALUATE {Expression} ({WhenClauses.Count} WHEN clauses)";
    }
}

/// <summary>
/// Represents a WHEN clause in an EVALUATE statement
/// </summary>
public class WhenClause
{
    /// <summary>
    /// Comparison operator (optional, defaults to equality)
    /// </summary>
    public BinaryOperator? Operator { get; }

    /// <summary>
    /// Condition expression
    /// </summary>
    public ExpressionNode Condition { get; }

    /// <summary>
    /// Body block
    /// </summary>
    public BlockNode Body { get; }

    public WhenClause(ExpressionNode condition, BlockNode body, BinaryOperator? op = null)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        Operator = op;
    }

    public override string ToString()
    {
        var opStr = Operator?.GetSymbol() ?? "=";
        return $"WHEN {opStr} {Condition}";
    }
}

/// <summary>
/// TRY-CATCH statement
/// </summary>
public class TryCatchStatementNode : StatementNode
{
    /// <summary>
    /// TRY block
    /// </summary>
    public BlockNode TryBlock { get; }

    /// <summary>
    /// CATCH clauses
    /// </summary>
    public List<CatchClause> CatchClauses { get; } = new();

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => 
        TryBlock.CanTransferControl || 
        CatchClauses.Any(c => c.Body.CanTransferControl);

    public TryCatchStatementNode(BlockNode tryBlock)
    {
        TryBlock = tryBlock ?? throw new ArgumentNullException(nameof(tryBlock));
        AddChild(tryBlock);
    }

    public void AddCatchClause(CatchClause catchClause)
    {
        CatchClauses.Add(catchClause);
        AddChildren(catchClause.ExceptionType, catchClause.Body);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitTryCatch(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitTryCatch(this);
    }

    public override string ToString()
    {
        return $"TRY-CATCH ({CatchClauses.Count} catch clauses)";
    }
}

/// <summary>
/// Represents a CATCH clause in a TRY-CATCH statement
/// </summary>
public class CatchClause
{
    /// <summary>
    /// Exception type to catch
    /// </summary>
    public TypeNode ExceptionType { get; }

    /// <summary>
    /// Exception variable name
    /// </summary>
    public string VariableName { get; }

    /// <summary>
    /// Catch block body
    /// </summary>
    public BlockNode Body { get; }

    public CatchClause(TypeNode exceptionType, string variableName, BlockNode body)
    {
        ExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
        VariableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public override string ToString()
    {
        return $"CATCH {ExceptionType} {VariableName}";
    }
}

/// <summary>
/// RETURN statement
/// </summary>
public class ReturnStatementNode : StatementNode
{
    /// <summary>
    /// Return value expression (optional)
    /// </summary>
    public ExpressionNode? Value { get; set; }

    public override bool CanTransferControl => true;

    public ReturnStatementNode(ExpressionNode? value = null)
    {
        Value = value;
        if (value != null)
            AddChild(value);
    }

    public void SetValue(ExpressionNode value)
    {
        if (Value != null)
            RemoveChild(Value);

        Value = value;
        if (value != null)
            AddChild(value);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitReturn(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitReturn(this);
    }

    public override string ToString()
    {
        return Value != null ? $"RETURN {Value}" : "RETURN";
    }
}

/// <summary>
/// THROW statement
/// </summary>
public class ThrowStatementNode : StatementNode
{
    /// <summary>
    /// Exception expression to throw
    /// </summary>
    public ExpressionNode Exception { get; }

    public override bool CanTransferControl => true;

    public ThrowStatementNode(ExpressionNode exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        AddChild(exception);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitThrow(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitThrow(this);
    }

    public override string ToString()
    {
        return $"THROW {Exception}";
    }
}

/// <summary>
/// BREAK statement
/// </summary>
public class BreakStatementNode : StatementNode
{
    public override bool CanTransferControl => true;

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitBreak(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBreak(this);
    }

    public override string ToString()
    {
        return "BREAK";
    }
}

/// <summary>
/// CONTINUE statement
/// </summary>
public class ContinueStatementNode : StatementNode
{
    public override bool CanTransferControl => true;

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitContinue(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitContinue(this);
    }

    public override string ToString()
    {
        return "CONTINUE";
    }
}

/// <summary>
/// EXIT statement
/// </summary>
public class ExitStatementNode : StatementNode
{
    /// <summary>
    /// Exit code expression (optional)
    /// </summary>
    public ExpressionNode? ExitCode { get; set; }

    public override bool CanTransferControl => true;

    public ExitStatementNode(ExpressionNode? exitCode = null)
    {
        ExitCode = exitCode;
        if (exitCode != null)
            AddChild(exitCode);
    }

    public void SetExitCode(ExpressionNode exitCode)
    {
        if (ExitCode != null)
            RemoveChild(ExitCode);

        ExitCode = exitCode;
        if (exitCode != null)
            AddChild(exitCode);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitExit(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitExit(this);
    }

    public override string ToString()
    {
        return ExitCode != null ? $"EXIT {ExitCode}" : "EXIT";
    }
}

/// <summary>
/// ERROR statement
/// </summary>
public class ErrorStatementNode : StatementNode
{
    /// <summary>
    /// Error message expression
    /// </summary>
    public ExpressionNode Message { get; }

    public override bool CanTransferControl => true;

    public ErrorStatementNode(ExpressionNode message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        AddChild(message);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitError(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitError(this);
    }

    public override string ToString()
    {
        return $"ERROR {Message}";
    }
}

/// <summary>
/// WARNING statement
/// </summary>
public class WarningStatementNode : StatementNode
{
    /// <summary>
    /// Warning message expression
    /// </summary>
    public ExpressionNode Message { get; }

    public WarningStatementNode(ExpressionNode message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        AddChild(message);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitWarning(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitWarning(this);
    }

    public override string ToString()
    {
        return $"WARNING {Message}";
    }
}

/// <summary>
/// Expression statement (expression used as a statement)
/// </summary>
public class ExpressionStatementNode : StatementNode
{
    /// <summary>
    /// The expression
    /// </summary>
    public ExpressionNode Expression { get; }

    public override bool CanTransferControl => false; // Expression statements don't transfer control

    public ExpressionStatementNode(ExpressionNode expression)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        AddChild(expression);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitExpressionStatement(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitExpressionStatement(this);
    }

    public override string ToString()
    {
        return Expression.ToString() + ";";
    }
}