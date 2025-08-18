using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Visitor interface for traversing the AST without return values
/// </summary>
public interface IAstVisitor
{
    // Program structure nodes
    void VisitProgram(ProgramNode node);
    void VisitAppClass(AppClassNode node);
    void VisitInterface(InterfaceNode node);
    void VisitImport(ImportNode node);

    // Type nodes
    void VisitBuiltInType(BuiltInTypeNode node);
    void VisitArrayType(ArrayTypeNode node);
    void VisitAppClassType(AppClassTypeNode node);

    // Declaration nodes
    void VisitMethod(MethodNode node);
    void VisitProperty(PropertyNode node);
    void VisitVariable(VariableNode node);
    void VisitConstant(ConstantNode node);
    void VisitFunction(FunctionNode node);

    // Statement nodes
    void VisitBlock(BlockNode node);
    void VisitIf(IfStatementNode node);
    void VisitFor(ForStatementNode node);
    void VisitWhile(WhileStatementNode node);
    void VisitRepeat(RepeatStatementNode node);
    void VisitEvaluate(EvaluateStatementNode node);
    void VisitTryCatch(TryCatchStatementNode node);
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
    void VisitMethodCall(MethodCallNode node);
    void VisitPropertyAccess(PropertyAccessNode node);
    void VisitArrayAccess(ArrayAccessNode node);
    void VisitObjectCreation(ObjectCreationNode node);
    void VisitTypeCast(TypeCastNode node);
    void VisitParenthesized(ParenthesizedExpressionNode node);
    void VisitAssignment(AssignmentNode node);
}

/// <summary>
/// Visitor interface for traversing the AST with return values
/// </summary>
public interface IAstVisitor<out TResult>
{
    // Program structure nodes
    TResult VisitProgram(ProgramNode node);
    TResult VisitAppClass(AppClassNode node);
    TResult VisitInterface(InterfaceNode node);
    TResult VisitImport(ImportNode node);

    // Type nodes
    TResult VisitBuiltInType(BuiltInTypeNode node);
    TResult VisitArrayType(ArrayTypeNode node);
    TResult VisitAppClassType(AppClassTypeNode node);

    // Declaration nodes
    TResult VisitMethod(MethodNode node);
    TResult VisitProperty(PropertyNode node);
    TResult VisitVariable(VariableNode node);
    TResult VisitConstant(ConstantNode node);
    TResult VisitFunction(FunctionNode node);

    // Statement nodes
    TResult VisitBlock(BlockNode node);
    TResult VisitIf(IfStatementNode node);
    TResult VisitFor(ForStatementNode node);
    TResult VisitWhile(WhileStatementNode node);
    TResult VisitRepeat(RepeatStatementNode node);
    TResult VisitEvaluate(EvaluateStatementNode node);
    TResult VisitTryCatch(TryCatchStatementNode node);
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
    TResult VisitMethodCall(MethodCallNode node);
    TResult VisitPropertyAccess(PropertyAccessNode node);
    TResult VisitArrayAccess(ArrayAccessNode node);
    TResult VisitObjectCreation(ObjectCreationNode node);
    TResult VisitTypeCast(TypeCastNode node);
    TResult VisitParenthesized(ParenthesizedExpressionNode node);
    TResult VisitAssignment(AssignmentNode node);
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

    public virtual void VisitProgram(ProgramNode node) => DefaultVisit(node);
    public virtual void VisitAppClass(AppClassNode node) => DefaultVisit(node);
    public virtual void VisitInterface(InterfaceNode node) => DefaultVisit(node);
    public virtual void VisitImport(ImportNode node) => DefaultVisit(node);
    public virtual void VisitBuiltInType(BuiltInTypeNode node) => DefaultVisit(node);
    public virtual void VisitArrayType(ArrayTypeNode node) => DefaultVisit(node);
    public virtual void VisitAppClassType(AppClassTypeNode node) => DefaultVisit(node);
    public virtual void VisitMethod(MethodNode node) => DefaultVisit(node);
    public virtual void VisitProperty(PropertyNode node) => DefaultVisit(node);
    public virtual void VisitVariable(VariableNode node) => DefaultVisit(node);
    public virtual void VisitConstant(ConstantNode node) => DefaultVisit(node);
    public virtual void VisitFunction(FunctionNode node) => DefaultVisit(node);
    public virtual void VisitBlock(BlockNode node) => DefaultVisit(node);
    public virtual void VisitIf(IfStatementNode node) => DefaultVisit(node);
    public virtual void VisitFor(ForStatementNode node) => DefaultVisit(node);
    public virtual void VisitWhile(WhileStatementNode node) => DefaultVisit(node);
    public virtual void VisitRepeat(RepeatStatementNode node) => DefaultVisit(node);
    public virtual void VisitEvaluate(EvaluateStatementNode node) => DefaultVisit(node);
    public virtual void VisitTryCatch(TryCatchStatementNode node) => DefaultVisit(node);
    public virtual void VisitReturn(ReturnStatementNode node) => DefaultVisit(node);
    public virtual void VisitThrow(ThrowStatementNode node) => DefaultVisit(node);
    public virtual void VisitBreak(BreakStatementNode node) => DefaultVisit(node);
    public virtual void VisitContinue(ContinueStatementNode node) => DefaultVisit(node);
    public virtual void VisitExit(ExitStatementNode node) => DefaultVisit(node);
    public virtual void VisitError(ErrorStatementNode node) => DefaultVisit(node);
    public virtual void VisitWarning(WarningStatementNode node) => DefaultVisit(node);
    public virtual void VisitExpressionStatement(ExpressionStatementNode node) => DefaultVisit(node);
    public virtual void VisitBinaryOperation(BinaryOperationNode node) => DefaultVisit(node);
    public virtual void VisitUnaryOperation(UnaryOperationNode node) => DefaultVisit(node);
    public virtual void VisitLiteral(LiteralNode node) => DefaultVisit(node);
    public virtual void VisitIdentifier(IdentifierNode node) => DefaultVisit(node);
    public virtual void VisitMethodCall(MethodCallNode node) => DefaultVisit(node);
    public virtual void VisitPropertyAccess(PropertyAccessNode node) => DefaultVisit(node);
    public virtual void VisitArrayAccess(ArrayAccessNode node) => DefaultVisit(node);
    public virtual void VisitObjectCreation(ObjectCreationNode node) => DefaultVisit(node);
    public virtual void VisitTypeCast(TypeCastNode node) => DefaultVisit(node);
    public virtual void VisitParenthesized(ParenthesizedExpressionNode node) => DefaultVisit(node);
    public virtual void VisitAssignment(AssignmentNode node) => DefaultVisit(node);
}

/// <summary>
/// Base visitor class that provides default implementations with return values
/// </summary>
public abstract class AstVisitorBase<TResult> : IAstVisitor<TResult>
{
    protected virtual TResult DefaultVisit(AstNode node)
    {
        return default!;
    }

    public virtual TResult VisitProgram(ProgramNode node) => DefaultVisit(node);
    public virtual TResult VisitAppClass(AppClassNode node) => DefaultVisit(node);
    public virtual TResult VisitInterface(InterfaceNode node) => DefaultVisit(node);
    public virtual TResult VisitImport(ImportNode node) => DefaultVisit(node);
    public virtual TResult VisitBuiltInType(BuiltInTypeNode node) => DefaultVisit(node);
    public virtual TResult VisitArrayType(ArrayTypeNode node) => DefaultVisit(node);
    public virtual TResult VisitAppClassType(AppClassTypeNode node) => DefaultVisit(node);
    public virtual TResult VisitMethod(MethodNode node) => DefaultVisit(node);
    public virtual TResult VisitProperty(PropertyNode node) => DefaultVisit(node);
    public virtual TResult VisitVariable(VariableNode node) => DefaultVisit(node);
    public virtual TResult VisitConstant(ConstantNode node) => DefaultVisit(node);
    public virtual TResult VisitFunction(FunctionNode node) => DefaultVisit(node);
    public virtual TResult VisitBlock(BlockNode node) => DefaultVisit(node);
    public virtual TResult VisitIf(IfStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitFor(ForStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitWhile(WhileStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitRepeat(RepeatStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitEvaluate(EvaluateStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitTryCatch(TryCatchStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitReturn(ReturnStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitThrow(ThrowStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitBreak(BreakStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitContinue(ContinueStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitExit(ExitStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitError(ErrorStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitWarning(WarningStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitExpressionStatement(ExpressionStatementNode node) => DefaultVisit(node);
    public virtual TResult VisitBinaryOperation(BinaryOperationNode node) => DefaultVisit(node);
    public virtual TResult VisitUnaryOperation(UnaryOperationNode node) => DefaultVisit(node);
    public virtual TResult VisitLiteral(LiteralNode node) => DefaultVisit(node);
    public virtual TResult VisitIdentifier(IdentifierNode node) => DefaultVisit(node);
    public virtual TResult VisitMethodCall(MethodCallNode node) => DefaultVisit(node);
    public virtual TResult VisitPropertyAccess(PropertyAccessNode node) => DefaultVisit(node);
    public virtual TResult VisitArrayAccess(ArrayAccessNode node) => DefaultVisit(node);
    public virtual TResult VisitObjectCreation(ObjectCreationNode node) => DefaultVisit(node);
    public virtual TResult VisitTypeCast(TypeCastNode node) => DefaultVisit(node);
    public virtual TResult VisitParenthesized(ParenthesizedExpressionNode node) => DefaultVisit(node);
    public virtual TResult VisitAssignment(AssignmentNode node) => DefaultVisit(node);
}