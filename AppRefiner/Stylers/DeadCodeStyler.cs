using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers;

/// <summary>
/// Highlights dead code (unreachable code after return/exit/throw statements) in PeopleCode blocks.
/// This is a self-hosted equivalent to the ANTLR-based DeadCodeStyler with significantly simplified logic.
/// </summary>
public class DeadCodeStyler : BaseStyler
{
    // Semi-transparent gray color for dead code highlighting (BGRA format)
    private const uint DEAD_CODE_COLOR = 0x73737380;

    public override string Description => "Dead code";

    /// <summary>
    /// Processes the entire program and detects dead code
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
    }

    /// <summary>
    /// Processes method implementations for dead code
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        if (node.Name == "FieldValueInRecord")
        {
            int debug = 0;
        }
        // Check method body for dead code
        if (node.Body != null)
        {
            ProcessBlock(node.Body);
        }
        
        base.VisitMethod(node);
    }

    /// <summary>
    /// Processes function implementations for dead code
    /// </summary>
    public override void VisitFunction(FunctionNode node)
    {
        // Check function body for dead code
        if (node.Body != null)
        {
            ProcessBlock(node.Body);
        }
        
        base.VisitFunction(node);
    }

    /// <summary>
    /// Processes property getter/setter implementations for dead code
    /// </summary>
    public override void VisitProperty(PropertyNode node)
    {
        // Check getter body for dead code
        if (node.GetterBody != null)
        {
            ProcessBlock(node.GetterBody);
        }
        
        // Check setter body for dead code
        if (node.SetterBody != null)
        {
            ProcessBlock(node.SetterBody);
        }
        
        base.VisitProperty(node);
    }

    /// <summary>
    /// Processes if statement blocks for dead code
    /// </summary>
    public override void VisitIf(IfStatementNode node)
    {
        // Check then block for dead code
        ProcessBlock(node.ThenBlock);
        
        // Check else block for dead code if it exists
        if (node.ElseBlock != null)
        {
            ProcessBlock(node.ElseBlock);
        }
        
        base.VisitIf(node);
    }

    /// <summary>
    /// Processes for loop blocks for dead code
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        // Check loop body for dead code
        ProcessBlock(node.Body);
        
        base.VisitFor(node);
    }

    /// <summary>
    /// Processes while loop blocks for dead code
    /// </summary>
    public override void VisitWhile(WhileStatementNode node)
    {
        // Check loop body for dead code
        ProcessBlock(node.Body);
        
        base.VisitWhile(node);
    }

    /// <summary>
    /// Processes repeat loop blocks for dead code
    /// </summary>
    public override void VisitRepeat(RepeatStatementNode node)
    {
        // Check loop body for dead code
        ProcessBlock(node.Body);
        
        base.VisitRepeat(node);
    }

    /// <summary>
    /// Processes evaluate statement blocks for dead code
    /// </summary>
    public override void VisitEvaluate(EvaluateStatementNode node)
    {
        // Check each when clause body for dead code
        foreach (var whenClause in node.WhenClauses)
        {
            ProcessBlock(whenClause.Body);
        }
        
        // Check when-other block for dead code if it exists
        if (node.WhenOtherBlock != null)
        {
            ProcessBlock(node.WhenOtherBlock);
        }
        
        base.VisitEvaluate(node);
    }

    /// <summary>
    /// Processes try-catch blocks for dead code
    /// </summary>
    public override void VisitTryCatch(TryCatchStatementNode node)
    {
        // Check try block for dead code
        ProcessBlock(node.TryBlock);
        
        // Check each catch clause body for dead code
        foreach (var catchClause in node.CatchClauses)
        {
            ProcessBlock(catchClause.Body);
        }
        
        base.VisitTryCatch(node);
    }

    /// <summary>
    /// Core logic: Processes a block of statements to detect dead code after return/exit/throw
    /// </summary>
    private void ProcessBlock(BlockNode block)
    {
        bool foundControlTransfer = false;
        
        for (int i = 0; i < block.Statements.Count; i++)
        {
            var statement = block.Statements[i];
            
            if (!foundControlTransfer)
            {
                // Continue processing normally until we hit a control transfer statement
                statement.Accept(this);
                
                // Check if this statement can transfer control (return, exit, throw, etc.)
                if (statement.DoesTransferControl)
                {
                    foundControlTransfer = true;
                }
            }
            else
            {
                // Mark all subsequent statements as dead code
                MarkDeadCode(statement);
            }
        }
    }

    /// <summary>
    /// Marks a statement as dead code with appropriate highlighting and tooltip
    /// </summary>
    private void MarkDeadCode(StatementNode statement)
    {
        var firstComment = statement.GetLeadingComments().FirstOrDefault();

        SourceSpan targetSpan = statement.SourceSpan;

        if (firstComment != null)
        {
            targetSpan = new SourceSpan(firstComment.SourceSpan.Start, targetSpan.End);
        }

        // Use the statement's source span for precise highlighting
        AddIndicator(targetSpan, IndicatorType.TEXTCOLOR, DEAD_CODE_COLOR,
        "Unreachable code (after return/exit/throw statement)");
        
        // Also continue visiting the statement to handle nested blocks that might also have dead code
        statement.Accept(this);
    }
}