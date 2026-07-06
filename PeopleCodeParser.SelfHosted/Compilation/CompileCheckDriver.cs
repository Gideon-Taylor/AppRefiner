using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// Runs all registered compile checks in a single AST traversal.
///
/// Extends <see cref="ScopedAstVisitor{T}"/> so the variable registry is populated
/// during the same pass (checks read it from <see cref="CompileCheckContext.ScopeData"/>
/// in <see cref="ICompileCheck.Finish"/>). Dispatch is pre-order: each node is handed to
/// every check before its children are traversed.
///
/// IMPORTANT: dispatch must ride the visitor's Accept path, not a node.Children walk —
/// AstVisitorBase traverses most node types with explicit per-type child walks that
/// bypass DefaultVisit, and Children omits some structural nodes (type nodes are
/// Accept()-ed explicitly). Therefore this class overrides EVERY VisitX method declared
/// on <see cref="IAstVisitor"/> with the identical body
/// <c>{ DispatchNode(node); base.VisitX(node); }</c>. Because each node's Accept calls
/// exactly one VisitX, this dispatches every node exactly once. If a new node type is
/// added to IAstVisitor, a matching override MUST be added here (the guard test in
/// CompileCheckDriverTests catches drift for representative node categories).
/// </summary>
public sealed class CompileCheckDriver : ScopedAstVisitor<object>
{
    private readonly IReadOnlyList<ICompileCheck> _checks;
    private readonly IDiagnosticSink _sink;
    private readonly List<(ICompileCheck Check, Exception Exception)> _failures = new();

    /// <summary>
    /// Context handed to every check. Must be set before <see cref="Run"/> is called.
    /// </summary>
    public CompileCheckContext Context { get; set; } = null!;

    /// <summary>
    /// Checks that threw from OnNode or Finish, with the exception each threw.
    /// A failing check never aborts the pass; its failures are recorded here instead.
    /// </summary>
    public IReadOnlyList<(ICompileCheck Check, Exception Exception)> Failures => _failures;

    public CompileCheckDriver(IReadOnlyList<ICompileCheck> checks, IDiagnosticSink sink)
    {
        _checks = checks ?? throw new ArgumentNullException(nameof(checks));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    /// <summary>
    /// Performs the single traversal (OnNode dispatch + variable registry population),
    /// then calls Finish on each check.
    /// </summary>
    public void Run(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (Context is null)
            throw new InvalidOperationException(
                $"{nameof(Context)} must be set before calling {nameof(Run)}.");

        program.Accept(this);

        foreach (var check in _checks)
        {
            try { check.Finish(Context, _sink); }
            catch (Exception ex) { _failures.Add((check, ex)); }
        }
    }

    private void DispatchNode(AstNode node)
    {
        foreach (var check in _checks)
        {
            try { check.OnNode(node, Context, _sink); }
            catch (Exception ex) { _failures.Add((check, ex)); }
        }
    }

    // ------------------------------------------------------------------
    // One override per VisitX declared on IAstVisitor — identical bodies.
    // ------------------------------------------------------------------

    // Program structure nodes
    public override void VisitProgram(ProgramNode node) { DispatchNode(node); base.VisitProgram(node); }
    public override void VisitAppClass(AppClassNode node) { DispatchNode(node); base.VisitAppClass(node); }
    public override void VisitImport(ImportNode node) { DispatchNode(node); base.VisitImport(node); }

    // Type nodes
    public override void VisitBuiltInType(BuiltInTypeNode node) { DispatchNode(node); base.VisitBuiltInType(node); }
    public override void VisitArrayType(ArrayTypeNode node) { DispatchNode(node); base.VisitArrayType(node); }
    public override void VisitAppClassType(AppClassTypeNode node) { DispatchNode(node); base.VisitAppClassType(node); }
    public override void VisitAppPackageWildcardType(AppPackageWildcardTypeNode node) { DispatchNode(node); base.VisitAppPackageWildcardType(node); }

    // Declaration nodes
    public override void VisitMethod(MethodNode node) { DispatchNode(node); base.VisitMethod(node); }
    public override void VisitMethodImpl(MethodImplNode node) { DispatchNode(node); base.VisitMethodImpl(node); }
    public override void VisitProperty(PropertyNode node) { DispatchNode(node); base.VisitProperty(node); }
    public override void VisitPropertyImpl(PropertyImplNode node) { DispatchNode(node); base.VisitPropertyImpl(node); }
    public override void VisitProgramVariable(ProgramVariableNode node) { DispatchNode(node); base.VisitProgramVariable(node); }
    public override void VisitConstant(ConstantNode node) { DispatchNode(node); base.VisitConstant(node); }
    public override void VisitFunction(FunctionNode node) { DispatchNode(node); base.VisitFunction(node); }

    // Statement nodes
    public override void VisitBlock(BlockNode node) { DispatchNode(node); base.VisitBlock(node); }
    public override void VisitIf(IfStatementNode node) { DispatchNode(node); base.VisitIf(node); }
    public override void VisitFor(ForStatementNode node) { DispatchNode(node); base.VisitFor(node); }
    public override void VisitWhile(WhileStatementNode node) { DispatchNode(node); base.VisitWhile(node); }
    public override void VisitRepeat(RepeatStatementNode node) { DispatchNode(node); base.VisitRepeat(node); }
    public override void VisitEvaluate(EvaluateStatementNode node) { DispatchNode(node); base.VisitEvaluate(node); }
    public override void VisitTry(TryStatementNode node) { DispatchNode(node); base.VisitTry(node); }
    public override void VisitCatch(CatchStatementNode node) { DispatchNode(node); base.VisitCatch(node); }
    public override void VisitReturn(ReturnStatementNode node) { DispatchNode(node); base.VisitReturn(node); }
    public override void VisitThrow(ThrowStatementNode node) { DispatchNode(node); base.VisitThrow(node); }
    public override void VisitBreak(BreakStatementNode node) { DispatchNode(node); base.VisitBreak(node); }
    public override void VisitContinue(ContinueStatementNode node) { DispatchNode(node); base.VisitContinue(node); }
    public override void VisitExit(ExitStatementNode node) { DispatchNode(node); base.VisitExit(node); }
    public override void VisitError(ErrorStatementNode node) { DispatchNode(node); base.VisitError(node); }
    public override void VisitWarning(WarningStatementNode node) { DispatchNode(node); base.VisitWarning(node); }
    public override void VisitExpressionStatement(ExpressionStatementNode node) { DispatchNode(node); base.VisitExpressionStatement(node); }

    // Expression nodes
    public override void VisitBinaryOperation(BinaryOperationNode node) { DispatchNode(node); base.VisitBinaryOperation(node); }
    public override void VisitUnaryOperation(UnaryOperationNode node) { DispatchNode(node); base.VisitUnaryOperation(node); }
    public override void VisitLiteral(LiteralNode node) { DispatchNode(node); base.VisitLiteral(node); }
    public override void VisitIdentifier(IdentifierNode node) { DispatchNode(node); base.VisitIdentifier(node); }
    public override void VisitArrayAccess(ArrayAccessNode node) { DispatchNode(node); base.VisitArrayAccess(node); }
    public override void VisitObjectCreation(ObjectCreationNode node) { DispatchNode(node); base.VisitObjectCreation(node); }
    public override void VisitTypeCast(TypeCastNode node) { DispatchNode(node); base.VisitTypeCast(node); }
    public override void VisitParenthesized(ParenthesizedExpressionNode node) { DispatchNode(node); base.VisitParenthesized(node); }
    public override void VisitAssignment(AssignmentNode node) { DispatchNode(node); base.VisitAssignment(node); }
    public override void VisitFunctionCall(FunctionCallNode node) { DispatchNode(node); base.VisitFunctionCall(node); }
    public override void VisitMemberAccess(MemberAccessNode node) { DispatchNode(node); base.VisitMemberAccess(node); }
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node) { DispatchNode(node); base.VisitLocalVariableDeclaration(node); }
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node) { DispatchNode(node); base.VisitLocalVariableDeclarationWithAssignment(node); }
    public override void VisitMetadataExpression(MetadataExpressionNode node) { DispatchNode(node); base.VisitMetadataExpression(node); }
    public override void VisitClassConstant(ClassConstantNode classConstantNode) { DispatchNode(classConstantNode); base.VisitClassConstant(classConstantNode); }
    public override void VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node) { DispatchNode(node); base.VisitPartialShortHandAssignment(node); }
    public override void VisitObjectCreateShortHand(ObjectCreateShortHand node) { DispatchNode(node); base.VisitObjectCreateShortHand(node); }

    // Interpolated string nodes
    public override void VisitInterpolatedString(InterpolatedStringNode node) { DispatchNode(node); base.VisitInterpolatedString(node); }
    public override void VisitStringFragment(StringFragment node) { DispatchNode(node); base.VisitStringFragment(node); }
    public override void VisitInterpolation(Interpolation node) { DispatchNode(node); base.VisitInterpolation(node); }
}
