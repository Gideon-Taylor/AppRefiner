using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// A visitor that annotates a specific target AST node with its scope context.
/// This visitor extends ScopedAstVisitor to build complete scope and variable information,
/// but only stores the scope context on the specified target node to minimize memory overhead.
/// </summary>
public class ScopeAnnotationVisitor : ScopedAstVisitor<object>
{
    private readonly AstNode _targetNode;

    /// <summary>
    /// Attribute key used to store ScopeContext in AstNode.Attributes dictionary
    /// </summary>
    public const string ScopeContextAttributeKey = "ScopeContext";

    /// <summary>
    /// Creates a visitor that will annotate the specified target node with scope information
    /// </summary>
    /// <param name="targetNode">The specific node to annotate during traversal</param>
    public ScopeAnnotationVisitor(AstNode targetNode)
    {
        _targetNode = targetNode;
    }

    /// <summary>
    /// Helper to annotate the target node if it matches the current node
    /// </summary>
    private void AnnotateIfTarget(AstNode node)
    {
        if (node == _targetNode)
        {
            node.Attributes[ScopeContextAttributeKey] = GetCurrentScope();
        }
    }

    #region Visitor Method Overrides

    public override void VisitProgram(ProgramNode node)
    {
        AnnotateIfTarget(node);
        base.VisitProgram(node);
    }

    public override void VisitAppClass(AppClassNode node)
    {
        AnnotateIfTarget(node);
        base.VisitAppClass(node);
    }

    public override void VisitMethod(MethodNode node)
    {
        AnnotateIfTarget(node);
        base.VisitMethod(node);
    }

    public override void VisitFunction(FunctionNode node)
    {
        AnnotateIfTarget(node);
        base.VisitFunction(node);
    }

    public override void VisitProperty(PropertyNode node)
    {
        AnnotateIfTarget(node);
        base.VisitProperty(node);
    }

    public override void VisitPropertyImpl(PropertyImplNode node)
    {
        AnnotateIfTarget(node);
        base.VisitPropertyImpl(node);
    }

    public override void VisitProgramVariable(ProgramVariableNode node)
    {
        AnnotateIfTarget(node);
        base.VisitProgramVariable(node);
    }

    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        AnnotateIfTarget(node);
        base.VisitLocalVariableDeclaration(node);
    }

    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        AnnotateIfTarget(node);
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    public override void VisitConstant(ConstantNode node)
    {
        AnnotateIfTarget(node);
        base.VisitConstant(node);
    }

    public override void VisitIdentifier(IdentifierNode node)
    {
        AnnotateIfTarget(node);
        base.VisitIdentifier(node);
    }

    public override void VisitFor(ForStatementNode node)
    {
        AnnotateIfTarget(node);
        base.VisitFor(node);
    }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        AnnotateIfTarget(node);
        base.VisitFunctionCall(node);
    }

    public override void VisitMemberAccess(MemberAccessNode node)
    {
        AnnotateIfTarget(node);
        base.VisitMemberAccess(node);
    }

    public override void VisitCatch(CatchStatementNode node)
    {
        AnnotateIfTarget(node);
        base.VisitCatch(node);
    }

    public override void VisitAssignment(AssignmentNode node)
    {
        AnnotateIfTarget(node);
        base.VisitAssignment(node);
    }

    public override void VisitExpressionStatement(ExpressionStatementNode node)
    {
        AnnotateIfTarget(node);
        base.VisitExpressionStatement(node);
    }

    #endregion
}
