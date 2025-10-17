using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Base class for all statement nodes
/// </summary>
public abstract class StatementNode : AstNode
{
    /// <summary>
    /// True if this statement can transfer control (RETURN, BREAK, etc.)
    /// </summary>
    public virtual bool CanTransferControl => DoesTransferControl;

    /// <summary>
    /// True if this statement definitely transfers control, making subsequent statements unreachable
    /// Used for dead code detection - only true for statements that unconditionally transfer control
    /// </summary>
    public virtual bool DoesTransferControl => false;

    /// <summary>
    /// True if this statement introduces a new scope
    /// </summary>
    public virtual bool IntroducesScope => false;

    /// <summary>
    /// True if this statement had a semicolon in the source code
    /// This is used for style checking, as PeopleCode allows but doesn't require
    /// semicolons after statements (especially the last statement in a block)
    /// </summary>
    public bool HasSemicolon { get; set; } = false;

    /// <summary>
    /// The sequential number of this statement in the program
    /// This is useful for consumers of the library to track statement execution order
    /// </summary>

    public abstract void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode);

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

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* Blocks themselves do not have statement numbers */
        foreach(var statement in Statements)
        {
            statement.RegisterStatementNumbers(parser, programNode);
        }
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

    public Token ElseToken { get; set; }
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

    public void SetElseBlock(Token elseToken, BlockNode elseBlock)
    {
        if (ElseBlock != null)
            RemoveChild(ElseBlock);
        ElseToken = elseToken;
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
        return ElseBlock != null ? "If-Then-Else" : "If-Then";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* register the If */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);

        /* process the body */
        ThenBlock.RegisterStatementNumbers(parser, programNode);

        var previousBlock = ThenBlock;

        if (ElseToken != null && ElseBlock != null)
        {
            if (ThenBlock.Statements.Count == 0 || (ThenBlock.Statements.Last().HasSemicolon))
            {
                programNode.SetStatementNumber( ElseToken.SourceSpan.Start.Line);
            }
            previousBlock = ElseBlock;

            ElseBlock.RegisterStatementNumbers(parser, programNode);
        }

        /* register end if, if last block was empty or if last blocks last statement ended with a semicolon */
        if (previousBlock.Statements.Count == 0 || previousBlock.Statements.Last().HasSemicolon)
        {
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }
    }
}

/// <summary>
/// FOR statement
/// </summary>
public class ForStatementNode : StatementNode
{
    /// <summary>
    /// Loop iterator expression (user variable or record.field)
    /// </summary>
    public ExpressionNode Iterator { get; }

    /// <summary>
    /// First token of the iterator (for error reporting)
    /// </summary>
    public Token IteratorToken { get; }

    /// <summary>
    /// Iterator name (backward compatibility helper)
    /// Returns variable name for user variables, "RECORD.FIELD" for member access
    /// </summary>
    public string IteratorName => GetIteratorName();

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

    public ForStatementNode(ExpressionNode iterator, Token iteratorToken, ExpressionNode fromValue, ExpressionNode toValue, BlockNode body)
    {
        Iterator = iterator ?? throw new ArgumentNullException(nameof(iterator));

        // Validate iterator is either IdentifierNode or MemberAccessNode
        if (iterator is not IdentifierNode and not MemberAccessNode)
        {
            throw new ArgumentException("Iterator must be either IdentifierNode (user variable) or MemberAccessNode (record.field)", nameof(iterator));
        }

        FromValue = fromValue ?? throw new ArgumentNullException(nameof(fromValue));
        ToValue = toValue ?? throw new ArgumentNullException(nameof(toValue));
        Body = body ?? throw new ArgumentNullException(nameof(body));
        IteratorToken = iteratorToken;
        AddChildren(iterator, fromValue, toValue, body);
    }

    /// <summary>
    /// Get the iterator name as a string
    /// </summary>
    private string GetIteratorName()
    {
        return Iterator switch
        {
            IdentifierNode id => id.Name,
            MemberAccessNode ma => $"{((IdentifierNode)ma.Target).Name}.{ma.MemberName}",
            _ => Iterator.ToString()
        };
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
        var stepStr = StepValue != null ? $" Step {StepValue}" : "";
        return $"For {IteratorName} = {FromValue} To {ToValue}{stepStr}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* register the Repeat */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);

        /* process the body */
        Body.RegisterStatementNumbers(parser, programNode);

        if (Body.Statements.Count == 0 || (Body.Statements.Last().HasSemicolon))
        {
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }
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
        return $"While {Condition}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* register the Repeat */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);

        /* process the body */
        Body.RegisterStatementNumbers(parser, programNode);

        if (Body.Statements.Count == 0 || (Body.Statements.Last().HasSemicolon))
        {
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }
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
        return $"Repeat-Until {Condition}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* register the Repeat */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);

        /* process the body */
        Body.RegisterStatementNumbers(parser, programNode);

        if (Body.Statements.Count == 0 || (Body.Statements.Last().HasSemicolon))
        {
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }
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
    public Token? WhenOtherToken { get; set; }
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

    public void SetWhenOtherBlock(Token whenOtherToken, BlockNode whenOtherBlock)
    {
        if (WhenOtherBlock != null)
            RemoveChild(WhenOtherBlock);

        WhenOtherBlock = whenOtherBlock;
        WhenOtherToken = whenOtherToken;
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
        return $"Evaluate {Expression} ({WhenClauses.Count} When clauses)";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {

        /* register the "evaluate" */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
        BlockNode? previousBlock = null;

        foreach(var whenClause in WhenClauses)
        {
            if (previousBlock == null)
            {
                /* I *think* its safe to use the starting line of the expression of the when clause */
                programNode.SetStatementNumber( whenClause.Condition.SourceSpan.Start.Line);
            } else
            {
                if (previousBlock.Statements.Count > 0 && previousBlock.Statements.Last().HasSemicolon)
                {
                    /* Register this when clause */
                    programNode.SetStatementNumber( whenClause.Condition.SourceSpan.Start.Line);
                }
            }

            whenClause.Body.RegisterStatementNumbers(parser, programNode);
            previousBlock = whenClause.Body;
        }

        /* 
         * if previous when body was empty, dont register "when"
         * if previous body ended without semicolon, don't register "when"
         */

        if (WhenOtherToken != null && WhenOtherBlock != null)
        {
            if (previousBlock == null || previousBlock.Statements.Count == 0 || previousBlock.Statements.Last().HasSemicolon)
            {
                programNode.SetStatementNumber( WhenOtherToken.SourceSpan.Start.Line);
            }
            WhenOtherBlock.RegisterStatementNumbers(parser, programNode);
            previousBlock = WhenOtherBlock;
        }


        if (previousBlock == null || 
            previousBlock.Statements.Count == 0 || 
            (previousBlock.Statements.Count > 0 && previousBlock.Statements.Last().HasSemicolon))
        {
            /* register end-evaluate if previous when block was empty or ended with a semicolon*/
            programNode.SetStatementNumber( SourceSpan.End.Line);
        }
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
        return $"When {opStr} {Condition}";
    }
}

/// <summary>
/// TRY statement
/// </summary>
public class TryStatementNode : StatementNode
{
    /// <summary>
    /// TRY block
    /// </summary>
    public BlockNode TryBlock { get; }

    /// <summary>
    /// CATCH clauses
    /// </summary>
    public List<CatchStatementNode> CatchClauses { get; } = new();

    public override bool IntroducesScope => true;
    public override bool CanTransferControl =>
        TryBlock.CanTransferControl ||
        CatchClauses.Any(c => c.CanTransferControl);

    public TryStatementNode(BlockNode tryBlock, IEnumerable<CatchStatementNode>? catchClauses = null)
    {
        TryBlock = tryBlock ?? throw new ArgumentNullException(nameof(tryBlock));
        AddChild(tryBlock);

        if (catchClauses != null)
        {
            foreach (var catchClause in catchClauses)
            {
                AddCatchClause(catchClause);
            }
        }
    }

    public void AddCatchClause(CatchStatementNode catchClause)
    {
        CatchClauses.Add(catchClause);
        AddChild(catchClause);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitTry(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitTry(this);
    }

    public override string ToString()
    {
        return $"try ({CatchClauses.Count} catch clauses)";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        
        /* register the "try" */
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);

        /* register statement numbers in the block here */
        TryBlock.RegisterStatementNumbers(parser, programNode);
        BlockNode previousBlock = TryBlock;
        foreach (var catchClause in CatchClauses)
        {
            /* register the "catch" */
            if (previousBlock.Statements.Count == 0 || (previousBlock.Statements.Count > 0 && previousBlock.Statements.Last().HasSemicolon))
            {
                programNode.SetStatementNumber( catchClause.SourceSpan.Start.Line);
            }

            catchClause.Body.RegisterStatementNumbers(parser, programNode);
            previousBlock = catchClause.Body;
        }

        /* register end-try */
        if (previousBlock.Statements.Count == 0 || (previousBlock.Statements.Count > 0 && previousBlock.Statements.Last().HasSemicolon))
        {
            programNode.SetStatementNumber(SourceSpan.End.Line);
        }
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

    public override bool DoesTransferControl => true;

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
        return Value != null ? $"Return {Value}" : "Return";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
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

    public override bool DoesTransferControl => true;

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
        return $"Throw {Exception}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
    }
}

/// <summary>
/// BREAK statement
/// </summary>
public class BreakStatementNode : StatementNode
{
    public override bool DoesTransferControl => true;

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
        return "break";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
    }
}

/// <summary>
/// CONTINUE statement
/// </summary>
public class ContinueStatementNode : StatementNode
{
    public override bool DoesTransferControl => true;

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
        return "Continue";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
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

    public override bool DoesTransferControl => true;

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
        return ExitCode != null ? $"Exit {ExitCode}" : "Exit";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
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

    public override bool DoesTransferControl => true;

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
        return $"Error {Message}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
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
        return $"Warning {Message}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
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

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
    }
}

/// <summary>
/// Local variable declaration statement without assignment: LOCAL type &var1, &var2;
/// </summary>
public class LocalVariableDeclarationNode : StatementNode
{
    /// <summary>
    /// Variable type
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Variable names
    /// </summary>
    public List<string> VariableNames { get; }

    /// <summary>
    /// Variable name information including tokens
    /// </summary>
    public List<VariableNameInfo> VariableNameInfos { get; } = new();

    public override bool IntroducesScope => false; // Local variables don't introduce new scopes

    public LocalVariableDeclarationNode(TypeNode type, IEnumerable<(string, Token)> variableNames)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        VariableNames = variableNames?.Select(v => v.Item1).ToList() ?? throw new ArgumentNullException(nameof(variableNames));
        VariableNameInfos.AddRange(variableNames.Select(v => new VariableNameInfo(v.Item1, v.Item2)));
        if (VariableNames.Count == 0)
            throw new ArgumentException("At least one variable name is required", nameof(variableNames));

        AddChild(type);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitLocalVariableDeclaration(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitLocalVariableDeclaration(this);
    }

    public override string ToString()
    {
        return $"Local {Type} {string.Join(", ", VariableNames)}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
    }
}

/// <summary>
/// Local variable declaration with assignment: LOCAL type &var = expression;
/// </summary>
public class LocalVariableDeclarationWithAssignmentNode : StatementNode
{
    /// <summary>
    /// Variable type
    /// </summary>
    public TypeNode Type { get; }

    /// <summary>
    /// Variable name
    /// </summary>
    public string VariableName => VariableNameInfo.Name;

    /// <summary>
    /// Variable name information including token
    /// </summary>
    public VariableNameInfo VariableNameInfo { get; set; }

    /// <summary>
    /// Initial value expression
    /// </summary>
    public ExpressionNode InitialValue { get; }

    public override bool IntroducesScope => false; // Local variables don't introduce new scopes

    public LocalVariableDeclarationWithAssignmentNode(TypeNode type, VariableNameInfo nameInfo, ExpressionNode initialValue)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        VariableNameInfo = nameInfo;
        InitialValue = initialValue ?? throw new ArgumentNullException(nameof(initialValue));

        AddChild(type);
        AddChild(initialValue);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitLocalVariableDeclarationWithAssignment(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitLocalVariableDeclarationWithAssignment(this);
    }

    public override string ToString()
    {
        return $"Local {Type} {VariableName} = {InitialValue}";
    }
    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        programNode.SetStatementNumber( SourceSpan.Start.Line);
        programNode.RegisterPPCStatementAtLine( SourceSpan.Start.Line, this);
    }
}


/// <summary>
/// CATCH statement for exception handling
/// </summary>
public class CatchStatementNode : StatementNode
{
    /// <summary>
    /// Exception variable
    /// </summary>
    public IdentifierNode? ExceptionVariable { get; }

    /// <summary>
    /// Exception type (EXCEPTION or appClassPath)
    /// </summary>
    public TypeNode? ExceptionType { get; }

    /// <summary>
    /// Catch block body
    /// </summary>
    public BlockNode Body { get; }

    public override bool IntroducesScope => true;
    public override bool CanTransferControl => Body.CanTransferControl;

    public CatchStatementNode(IdentifierNode? exceptionVariable, BlockNode body, TypeNode? exceptionType = null)
    {
        ExceptionVariable = exceptionVariable;
        ExceptionType = exceptionType;
        Body = body ?? throw new ArgumentNullException(nameof(body));

        if (exceptionVariable != null)
            AddChild(exceptionVariable);
        if (exceptionType != null)
            AddChild(exceptionType);
        AddChild(body);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitCatch(this);
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitCatch(this);
    }

    public override string ToString()
    {
        var typeStr = ExceptionType != null ? $" {ExceptionType}" : "";
        var varStr = ExceptionVariable != null ? $" {ExceptionVariable.Name}" : "";
        return $"catch{typeStr}{varStr}";
    }

    public override void RegisterStatementNumbers(PeopleCodeParser parser, ProgramNode programNode)
    {
        /* Do nothing here, "catch" statements are processed by their containing "try" */
    }
}