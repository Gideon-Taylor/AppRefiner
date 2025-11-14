using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Visitor interface for traversing the AST without return values
/// </summary>
public interface IAstVisitor
{
    // Program structure nodes
    void VisitProgram(ProgramNode node);
    void VisitAppClass(AppClassNode node);
    void VisitImport(ImportNode node);

    // Type nodes
    void VisitBuiltInType(BuiltInTypeNode node);
    void VisitArrayType(ArrayTypeNode node);
    void VisitAppClassType(AppClassTypeNode node);
    void VisitAppPackageWildcardType(AppPackageWildcardTypeNode node);

    // Declaration nodes
    void VisitMethod(MethodNode node);
    void VisitMethodImpl(MethodImplNode node);
    void VisitProperty(PropertyNode node);
    void VisitPropertyImpl(PropertyImplNode node);
    void VisitProgramVariable(ProgramVariableNode node);
    void VisitConstant(ConstantNode node);
    void VisitFunction(FunctionNode node);

    // Statement nodes
    void VisitBlock(BlockNode node);
    void VisitIf(IfStatementNode node);
    void VisitFor(ForStatementNode node);
    void VisitWhile(WhileStatementNode node);
    void VisitRepeat(RepeatStatementNode node);
    void VisitEvaluate(EvaluateStatementNode node);
    void VisitTry(TryStatementNode node);
    void VisitCatch(CatchStatementNode node);
    void VisitReturn(ReturnStatementNode node);
    void VisitThrow(ThrowStatementNode node);
    void VisitBreak(BreakStatementNode node);
    void VisitContinue(ContinueStatementNode node);
    void VisitExit(ExitStatementNode node);
    void VisitError(ErrorStatementNode node);
    void VisitWarning(WarningStatementNode node);
    void VisitExpressionStatement(ExpressionStatementNode node);

    // Expression nodes
    void VisitBinaryOperation(BinaryOperationNode node);
    void VisitUnaryOperation(UnaryOperationNode node);
    void VisitLiteral(LiteralNode node);
    void VisitIdentifier(IdentifierNode node);
    void VisitPropertyAccess(PropertyAccessNode node);
    void VisitArrayAccess(ArrayAccessNode node);
    void VisitObjectCreation(ObjectCreationNode node);
    void VisitTypeCast(TypeCastNode node);
    void VisitParenthesized(ParenthesizedExpressionNode node);
    void VisitAssignment(AssignmentNode node);
    void VisitFunctionCall(FunctionCallNode node);
    void VisitMemberAccess(MemberAccessNode node);
    void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node);
    void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node);
    void VisitMetadataExpression(MetadataExpressionNode node);
    void VisitClassConstant(ClassConstantNode classConstantNode);

    void VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node);
    void VisitObjectCreateShortHand(ObjectCreateShortHand node);
}

/// <summary>
/// Visitor interface for traversing the AST with return values
/// </summary>
public interface IAstVisitor<out TResult>
{
    // Program structure nodes
    TResult VisitProgram(ProgramNode node);
    TResult VisitAppClass(AppClassNode node);
    TResult VisitImport(ImportNode node);

    // Type nodes
    TResult VisitBuiltInType(BuiltInTypeNode node);
    TResult VisitArrayType(ArrayTypeNode node);
    TResult VisitAppClassType(AppClassTypeNode node);
    TResult VisitAppPackageWildcardType(AppPackageWildcardTypeNode node);

    // Declaration nodes
    TResult VisitMethod(MethodNode node);
    TResult VisitMethodImpl(MethodImplNode node);
    TResult VisitProperty(PropertyNode node);
    TResult VisitPropertyImpl(PropertyImplNode node);
    TResult VisitProgramVariable(ProgramVariableNode node);
    TResult VisitConstant(ConstantNode node);
    TResult VisitFunction(FunctionNode node);

    // Statement nodes
    TResult VisitBlock(BlockNode node);
    TResult VisitIf(IfStatementNode node);
    TResult VisitFor(ForStatementNode node);
    TResult VisitWhile(WhileStatementNode node);
    TResult VisitRepeat(RepeatStatementNode node);
    TResult VisitEvaluate(EvaluateStatementNode node);
    TResult VisitTry(TryStatementNode node);
    TResult VisitCatch(CatchStatementNode node);
    TResult VisitReturn(ReturnStatementNode node);
    TResult VisitThrow(ThrowStatementNode node);
    TResult VisitBreak(BreakStatementNode node);
    TResult VisitContinue(ContinueStatementNode node);
    TResult VisitExit(ExitStatementNode node);
    TResult VisitError(ErrorStatementNode node);
    TResult VisitWarning(WarningStatementNode node);
    TResult VisitExpressionStatement(ExpressionStatementNode node);

    // Expression nodes
    TResult VisitBinaryOperation(BinaryOperationNode node);
    TResult VisitUnaryOperation(UnaryOperationNode node);
    TResult VisitLiteral(LiteralNode node);
    TResult VisitIdentifier(IdentifierNode node);
    TResult VisitPropertyAccess(PropertyAccessNode node);
    TResult VisitArrayAccess(ArrayAccessNode node);
    TResult VisitObjectCreation(ObjectCreationNode node);
    TResult VisitTypeCast(TypeCastNode node);
    TResult VisitParenthesized(ParenthesizedExpressionNode node);
    TResult VisitAssignment(AssignmentNode node);
    TResult VisitFunctionCall(FunctionCallNode node);
    TResult VisitMemberAccess(MemberAccessNode node);
    TResult VisitLocalVariableDeclaration(LocalVariableDeclarationNode node);
    TResult VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node);
    TResult VisitMetadataExpression(MetadataExpressionNode node);
    TResult VisitClassConstant(ClassConstantNode classConstantNode);

    TResult VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node);

    TResult VisitObjectCreateShortHand(ObjectCreateShortHand node);
}

/// <summary>
/// Base visitor class that provides default implementations
/// </summary>
public abstract class AstVisitorBase : IAstVisitor
{
    protected virtual void DefaultVisit(AstNode node)
    {
        // Visit all child nodes by default
        foreach (var child in node.Children)
        {
            child.Accept(this);
        }
    }

    public virtual void VisitProgram(ProgramNode node)
    {
        // Visit imports first
        foreach (var import in node.Imports)
        {
            import.Accept(this);
        }

        // Visit component and global variables
        foreach (var variable in node.ComponentAndGlobalVariables)
        {
            variable.Accept(this);
        }

        // Visit program-level local variables
        foreach (var localVar in node.LocalVariables)
        {
            localVar.Accept(this);
        }

        // Visit constants
        foreach (var constant in node.Constants)
        {
            constant.Accept(this);
        }

        /* Visit external function declarations */
        // Visit functions
        foreach (var function in node.Functions.Where(f => f.IsDeclaration))
        {
            function.Accept(this);
        }

        // Visit app class or interface if present
        if (node.AppClass != null)
        {
            node.AppClass.Accept(this);
        }

        // Visit functions
        foreach (var function in node.Functions.Where(f => f.IsImplementation))
        {
            function.Accept(this);
        }

        // Visit main block if present (for non-class programs)
        if (node.MainBlock != null)
        {
            node.MainBlock.Accept(this);
        }
    }

    public virtual void VisitAppClass(AppClassNode node)
    {
        // Visit members in a specific order to ensure proper semantic analysis

        if (node.BaseType != null)
        {
            node.BaseType.Accept(this);
        }

        // 1. First visit instance variables (classes only)
        foreach (var instanceVar in node.InstanceVariables)
        {
            instanceVar.Accept(this);
        }

        // 2. Then visit properties
        foreach (var property in node.Properties)
        {
            property.Accept(this);
        }

        // 3. Then visit constants (classes only)
        foreach (var constant in node.Constants)
        {
            constant.Accept(this);
        }

        // 4. Then visit method declarations
        foreach (var method in node.Methods)
        {
            method.Accept(this);
        }

        foreach(var property in node.Properties)
        {
            property.Getter?.Accept(this);
            property.Setter?.Accept(this);
        }

        // 6. Finally visit any other children that might not be in the specialized collections
        foreach (var child in node.Children)
        {
            // Skip nodes we've already visited
            if (node.InstanceVariables.Contains(child) ||
                node.Properties.Contains(child) ||
                node.Constants.Contains(child) ||
                node.Methods.Contains(child) ||
                node.MethodImplementations.Contains(child))
            {
                continue;
            }

            child.Accept(this);
        }
    }

    public virtual void VisitImport(ImportNode node)
    {
        // Visit the imported type node
        node.ImportedType.Accept(this);
    }

    public virtual void VisitBuiltInType(BuiltInTypeNode node) => DefaultVisit(node);
    public virtual void VisitArrayType(ArrayTypeNode node)
    {
        // Visit element type
        if (node.ElementType != null)
            node.ElementType.Accept(this);
    }
    public virtual void VisitAppClassType(AppClassTypeNode node) => DefaultVisit(node);
    public virtual void VisitAppPackageWildcardType(AppPackageWildcardTypeNode node) => DefaultVisit(node);
    public virtual void VisitMethod(MethodNode node)
    {
        // Visit return type first if present
        if (node.ReturnType != null)
        {
            node.ReturnType.Accept(this);
        }

        // Visit implemented interfaces if any
        foreach (var interfaceType in node.ImplementedInterfaces)
        {
            interfaceType.Accept(this);
        }

        // Visit parameters
        foreach (var parameter in node.Parameters)
        {
            // Parameters don't have their own Accept method, so visit their type
            parameter.Type.Accept(this);
        }

        // Visit parameter annotations
        foreach (var annotation in node.ParameterAnnotations)
        {
            annotation.Accept(this);
        }

        // Visit method implementation if present
        if (node.Implementation != null)
        {
            node.Implementation.Accept(this);
        }
    }
    public virtual void VisitMethodImpl(MethodImplNode node)
    {
        // Visit parameter annotations
        foreach (var annotation in node.ParameterAnnotations)
        {
            annotation.Type.Accept(this);
        }

        // Visit return type annotation if present
        if (node.ReturnTypeAnnotation != null)
        {
            node.ReturnTypeAnnotation.Accept(this);
        }

        // Visit implemented interfaces if any
        foreach (var interfaceType in node.ImplementedInterfaces)
        {
            interfaceType.Accept(this);
        }

        // Visit method body
        node.Body.Accept(this);
    }
    public virtual void VisitProperty(PropertyNode node)
    {
        // Visit property type first
        node.Type.Accept(this);
    }
    public virtual void VisitProgramVariable(ProgramVariableNode node)
    {
        // Visit type first
        node.Type.Accept(this);

        // Visit initial value if present
        if (node.InitialValue != null)
        {
            node.InitialValue.Accept(this);
        }
    }
    public virtual void VisitConstant(ConstantNode node)
    {
        // Visit value expression
        node.Value.Accept(this);
    }
    public virtual void VisitFunction(FunctionNode node)
    {
        // Visit return type first if present
        if (node.ReturnType != null)
        {
            node.ReturnType.Accept(this);
        }

        // Visit parameters
        foreach (var parameter in node.Parameters)
        {
            // Parameters don't have their own Accept method, so visit their type
            parameter.Type.Accept(this);
        }

        // Visit function body if present (for implementations)
        if (node.Body != null)
        {
            node.Body.Accept(this);
        }
    }
    public virtual void VisitBlock(BlockNode node)
    {
        // Visit statements in order
        foreach (var statement in node.Statements)
        {
            statement.Accept(this);
        }
    }
    public virtual void VisitIf(IfStatementNode node)
    {
        // Visit condition first
        node.Condition.Accept(this);

        // Visit then block
        node.ThenBlock.Accept(this);

        // Visit else block if present
        if (node.ElseBlock != null)
        {
            node.ElseBlock.Accept(this);
        }
    }
    public virtual void VisitFor(ForStatementNode node)
    {
        // Visit from value first
        node.FromValue.Accept(this);

        // Visit to value
        node.ToValue.Accept(this);

        // Visit step value if present
        if (node.StepValue != null)
        {
            node.StepValue.Accept(this);
        }

        // Visit loop body
        node.Body.Accept(this);
    }
    public virtual void VisitWhile(WhileStatementNode node)
    {
        // Visit condition first
        node.Condition.Accept(this);

        // Visit loop body
        node.Body.Accept(this);
    }
    public virtual void VisitRepeat(RepeatStatementNode node)
    {
        // Visit body first (since it executes before the condition is checked)
        node.Body.Accept(this);

        // Visit condition
        node.Condition.Accept(this);
    }
    public virtual void VisitEvaluate(EvaluateStatementNode node)
    {
        // Visit expression being evaluated first
        node.Expression.Accept(this);

        // Visit when clauses
        foreach (var whenClause in node.WhenClauses)
        {
            // Visit condition
            whenClause.Condition.Accept(this);

            // Visit body
            whenClause.Body.Accept(this);
        }

        // Visit when-other block if present
        if (node.WhenOtherBlock != null)
        {
            node.WhenOtherBlock.Accept(this);
        }
    }
    public virtual void VisitTry(TryStatementNode node)
    {
        // Visit try block first
        node.TryBlock.Accept(this);

        // Visit catch clauses
        foreach (var catchClause in node.CatchClauses)
        {
            catchClause.Accept(this);
        }
    }
    public virtual void VisitReturn(ReturnStatementNode node)
    {
        // Visit return value if present
        if (node.Value != null)
        {
            node.Value.Accept(this);
        }
    }
    public virtual void VisitThrow(ThrowStatementNode node)
    {
        // Visit exception expression
        node.Exception.Accept(this);
    }
    public virtual void VisitBreak(BreakStatementNode node) => DefaultVisit(node);
    public virtual void VisitContinue(ContinueStatementNode node) => DefaultVisit(node);
    public virtual void VisitExit(ExitStatementNode node)
    {
        // Visit exit code if present
        if (node.ExitCode != null)
        {
            node.ExitCode.Accept(this);
        }
    }
    public virtual void VisitError(ErrorStatementNode node)
    {
        // Visit message expression
        node.Message.Accept(this);
    }
    public virtual void VisitWarning(WarningStatementNode node)
    {
        // Visit message expression
        node.Message.Accept(this);
    }
    public virtual void VisitExpressionStatement(ExpressionStatementNode node)
    {
        // Visit the expression
        node.Expression.Accept(this);
    }
    public virtual void VisitBinaryOperation(BinaryOperationNode node)
    {
        // Visit left operand first
        node.Left.Accept(this);

        // Visit right operand
        node.Right.Accept(this);
    }
    public virtual void VisitUnaryOperation(UnaryOperationNode node)
    {
        // Visit operand
        node.Operand.Accept(this);
    }
    public virtual void VisitLiteral(LiteralNode node) => DefaultVisit(node);
    public virtual void VisitIdentifier(IdentifierNode node) => DefaultVisit(node);
    public virtual void VisitPropertyAccess(PropertyAccessNode node)
    {
        // Visit target object first
        node.Target.Accept(this);
    }
    public virtual void VisitArrayAccess(ArrayAccessNode node)
    {
        // Visit array object first
        node.Array.Accept(this);

        // Visit index expression
        foreach (var index in node.Indices)
        {
            index.Accept(this);
        }
    }
    public virtual void VisitObjectCreation(ObjectCreationNode node)
    {
        // Visit type first
        node.Type.Accept(this);

        // Visit arguments
        foreach (var argument in node.Arguments)
        {
            argument.Accept(this);
        }
    }
    public virtual void VisitTypeCast(TypeCastNode node)
    {
        // Visit target type first
        node.TargetType.Accept(this);

        // Visit expression being cast
        node.Expression.Accept(this);
    }
    public virtual void VisitParenthesized(ParenthesizedExpressionNode node)
    {
        // Visit the inner expression
        node.Expression.Accept(this);
    }
    public virtual void VisitAssignment(AssignmentNode node)
    {
        // Visit left side (target) first
        node.Target.Accept(this);

        // Visit right side (value)
        node.Value.Accept(this);
    }
    public virtual void VisitFunctionCall(FunctionCallNode node)
    {
        // Visit function expression first
        node.Function.Accept(this);

        // Visit arguments
        foreach (var argument in node.Arguments)
        {
            argument.Accept(this);
        }
    }
    public virtual void VisitMemberAccess(MemberAccessNode node)
    {
        // Visit target object first
        node.Target.Accept(this);
    }

    public virtual void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        // Visit type first
        node.Type.Accept(this);
    }
    public virtual void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        // Visit type first
        node.Type.Accept(this);

        // Visit initial value
        node.InitialValue.Accept(this);
    }
    public virtual void VisitCatch(CatchStatementNode node)
    {
        // Visit exception variable if present
        if (node.ExceptionVariable != null)
        {
            node.ExceptionVariable.Accept(this);
        }

        // Visit exception type if present
        if (node.ExceptionType != null)
        {
            node.ExceptionType.Accept(this);
        }

        // Visit catch body
        node.Body.Accept(this);
    }
    public virtual void VisitMetadataExpression(MetadataExpressionNode node) => DefaultVisit(node);
    public virtual void VisitClassConstant(ClassConstantNode classConstantNode) => DefaultVisit(classConstantNode);

    public virtual void VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node)
    {
        node.Target.Accept(this);
    }

    public virtual void VisitObjectCreateShortHand(ObjectCreateShortHand node) => DefaultVisit(node);

    public virtual void VisitPropertyImpl(PropertyImplNode node)
    {
        node.ImplementedInterface?.Accept(this);
        node.Body?.Accept(this);
    }
}
