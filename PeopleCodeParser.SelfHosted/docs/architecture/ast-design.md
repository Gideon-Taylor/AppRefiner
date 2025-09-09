# AST Design

The Abstract Syntax Tree (AST) is the core data structure that represents the parsed structure of PeopleCode programs. This document explains the node hierarchy, visitor patterns, and source tracking mechanisms.

## Overview

The AST consists of **56 specialized node types** organized in a hierarchical structure with a common base class `AstNode`. Each node represents a specific language construct and maintains rich metadata about its source location and relationships.

```csharp
public abstract class AstNode
{
    public Token? FirstToken { get; set; }
    public Token? LastToken { get; set; }  
    public SourceSpan SourceSpan { get; }
    public AstNode? Parent { get; set; }
    public IReadOnlyList<AstNode> Children { get; }
    // ... additional members
}
```

## Node Hierarchy

### Base Classes

**AstNode** - Root base class for all AST nodes
- Provides common functionality for source tracking and tree navigation
- Implements visitor pattern with `Accept()` methods
- Manages parent-child relationships automatically

**Abstract Categories:**
- `DeclarationNode` - Base for named declarations (methods, properties, variables)
- `StatementNode` - Base for executable statements and control flow
- `ExpressionNode` - Base for values and computations
- `TypeNode` - Base for type references and specifications

### Node Categories

| Category | Count | Examples |
|----------|-------|----------|
| **Program Structure** | 4 | `ProgramNode`, `AppClassNode`, `InterfaceNode`, `ImportNode` |
| **Declarations** | 7 | `MethodNode`, `PropertyNode`, `VariableNode`, `FunctionNode` |
| **Statements** | 18 | `IfStatementNode`, `ForStatementNode`, `BlockNode`, `ReturnStatementNode` |
| **Expressions** | 16 | `BinaryOperationNode`, `LiteralNode`, `FunctionCallNode`, `AssignmentNode` |
| **Types** | 4 | `BuiltInTypeNode`, `ArrayTypeNode`, `AppClassTypeNode` |
| **Support** | 7 | `ParameterNode`, `WhenClause`, `VariableNameInfo` |

## Source Tracking

### Token-Based Positioning

Every AST node tracks its source location through `FirstToken` and `LastToken` references:

```csharp
public class MethodNode : DeclarationNode
{
    // FirstToken points to "method" keyword
    // LastToken points to "end-method" keyword or last token of declaration
}
```

### SourceSpan Calculation

The `SourceSpan` property is calculated dynamically from the token boundaries:

```csharp
public SourceSpan SourceSpan
{
    get
    {
        if (FirstToken != null && LastToken != null)
        {
            return new SourceSpan(FirstToken.SourceSpan.Start, LastToken.SourceSpan.End);
        }
        return _explicitSourceSpan; // Fallback for compatibility
    }
}
```

### Benefits of Token-Based Tracking

- **Memory Efficient**: No duplicate position storage
- **Always Accurate**: Positions derived from actual tokens
- **Rich Context**: Access to leading/trailing comments and trivia
- **Editor Integration**: Precise highlighting and navigation support

## Parent-Child Relationships

### Automatic Management

The AST maintains bidirectional parent-child relationships automatically:

```csharp
protected void AddChild(AstNode child)
{
    if (child == null) return;
    child.Parent = this; // Automatically sets parent and adds to children collection
}

protected void RemoveChild(AstNode child)  
{
    if (child?.Parent == this)
    {
        child.Parent = null; // Automatically removes from children collection
    }
}
```

### Tree Navigation Utilities

The base `AstNode` class provides utility methods for tree traversal:

```csharp
// Find first ancestor of specific type
public T? FindAncestor<T>() where T : AstNode

// Find all descendants of specific type  
public IEnumerable<T> FindDescendants<T>() where T : AstNode

// Get root node of the AST
public AstNode GetRoot()
```

### Usage Examples

```csharp
// Find the containing method for any statement
var method = statement.FindAncestor<MethodNode>();

// Find all variable references in a method
var variables = method.FindDescendants<IdentifierNode>()
    .Where(id => id.IdentifierType == IdentifierType.UserVariable);

// Navigate to program root
var program = node.GetRoot() as ProgramNode;
```

## Visitor Pattern Implementation

### Dual Visitor Interfaces

The AST supports two visitor patterns:

**Void Visitors** - For side effects and analysis:
```csharp
public interface IAstVisitor
{
    void VisitMethod(MethodNode node);
    void VisitIf(IfStatementNode node);
    // ... other visit methods
}
```

**Generic Visitors** - For computations and transformations:
```csharp
public interface IAstVisitor<out TResult>
{
    TResult VisitMethod(MethodNode node);
    TResult VisitIf(IfStatementNode node);
    // ... other visit methods
}
```

### Accept Method Implementation

Every node implements both visitor patterns:

```csharp
public class MethodNode : DeclarationNode
{
    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitMethod(this);
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitMethod(this);
    }
}
```

### Base Visitor Classes

**AstVisitorBase** provides sensible default traversal:

```csharp
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
    
    public virtual void VisitMethod(MethodNode node)
    {
        // Visit return type, parameters, and body in order
        node.ReturnType?.Accept(this);
        foreach (var param in node.Parameters)
            param.Type.Accept(this);
        node.Body?.Accept(this);
    }
}
```

## Node-Specific Design Patterns

### Declaration Nodes

Declaration nodes inherit from `DeclarationNode` and share common properties:

```csharp
public abstract class DeclarationNode : AstNode
{
    public string Name { get; }
    public Token NameToken { get; }
    public bool HasSemicolon { get; set; }
    public VisibilityModifier Visibility { get; set; } = VisibilityModifier.Public;
}
```

**Examples:**
- `MethodNode` - Method declarations and implementations
- `PropertyNode` - Property declarations with get/set accessors
- `VariableNode` - Variable declarations with type and scope
- `ConstantNode` - Named constant definitions

### Statement Nodes  

Statement nodes inherit from `StatementNode` and provide control flow information:

```csharp
public abstract class StatementNode : AstNode
{
    public virtual bool CanTransferControl => DoesTransferControl;
    public virtual bool DoesTransferControl => false;
    public virtual bool IntroducesScope => false;
    public bool HasSemicolon { get; set; } = false;
    public int StatementNumber { get; set; } = 0;
}
```

**Control Flow Analysis:**
- `CanTransferControl` - Statement might transfer control (has `break`, `return`, etc.)
- `DoesTransferControl` - Statement definitely transfers control (unconditional)
- `IntroducesScope` - Statement creates new variable scope

### Expression Nodes

Expression nodes inherit from `ExpressionNode` and support semantic analysis:

```csharp
public abstract class ExpressionNode : AstNode
{
    public TypeNode? InferredType { get; set; }
    public virtual bool IsLValue => false;
    public virtual bool HasSideEffects => false;
}
```

**Semantic Properties:**
- `InferredType` - Type determined during semantic analysis
- `IsLValue` - Can be assigned to (left-hand side of assignment)
- `HasSideEffects` - Expression causes observable changes

### Type Nodes

Type nodes inherit from `TypeNode` and represent type specifications:

```csharp
public abstract class TypeNode : AstNode
{
    public abstract string TypeName { get; }
    public virtual bool IsNullable => true;
    public virtual bool IsBuiltIn => false;
}
```

## Extensibility Features

### Custom Attributes

Nodes can store arbitrary metadata through the `Attributes` dictionary:

```csharp
public Dictionary<string, object> Attributes { get; } = new();

// Usage example
node.Attributes["SymbolInfo"] = symbolTableEntry;
node.Attributes["TypeInfo"] = inferredType;
node.Attributes["UsageCount"] = referenceCount;
```

### Comment Access

Nodes provide access to associated comments through token trivia:

```csharp
// Get leading comments (before the node)
public IEnumerable<Token> GetLeadingComments()

// Get trailing comments (after the node)  
public IEnumerable<Token> GetTrailingComments()

// Get all comments associated with the node
public IEnumerable<Token> GetAllComments()
```

## Memory and Performance Considerations

### Lazy Evaluation

SourceSpan calculation is performed on-demand to avoid storing redundant position data:

```csharp
public SourceSpan SourceSpan
{
    get
    {
        // Calculate from tokens only when requested
        if (FirstToken != null && LastToken != null)
            return new SourceSpan(FirstToken.SourceSpan.Start, LastToken.SourceSpan.End);
        return _explicitSourceSpan;
    }
}
```

### Efficient Parent-Child Management

Parent-child relationships use direct references rather than expensive lookups:

```csharp
private AstNode? _parent;
private readonly List<AstNode> _children = new();

public IReadOnlyList<AstNode> Children => _children.AsReadOnly();
```

### Visitor Pattern Overhead

The visitor pattern adds minimal runtime overhead:
- Virtual method dispatch for `Accept()` calls
- Type checking in visitor implementations
- Stack overhead for recursive traversal

[PLACEHOLDER - AST node memory usage can be measured in node constructors]
[PLACEHOLDER - Visitor traversal performance can be measured in visitor base classes]

## Error Handling in AST

### Partial Nodes

When parsing encounters errors, the AST may contain partial or incomplete nodes:

```csharp
public class MethodNode : DeclarationNode
{
    // Body may be null if parsing failed
    public BlockNode? Body => Implementation?.Body;
    
    // Parameters may be incomplete
    public List<ParameterNode> Parameters { get; } = new();
}
```

### Error Nodes

Special error nodes represent unparseable constructs:

```csharp
public class ErrorExpressionNode : ExpressionNode
{
    public string ErrorText { get; }
    public ParseError Error { get; }
}
```

### Recovery Information

Nodes may contain metadata about parsing recovery:

```csharp
node.Attributes["ParseError"] = error;
node.Attributes["RecoveryPoint"] = tokenPosition;
```

## AST Validation

### Consistency Checks

The AST provides methods to validate tree consistency:

```csharp
public bool ValidateTree()
{
    return ValidateParentChildRelationships() &&
           ValidateSourceSpans() &&
           ValidateVisitorIntegration();
}
```

### Semantic Validation

Higher-level validation can be implemented through visitors:

```csharp
public class SemanticValidator : AstVisitorBase
{
    public List<SemanticError> Errors { get; } = new();
    
    public override void VisitVariable(VariableNode node)
    {
        if (node.Type == null)
            Errors.Add(new SemanticError("Variable missing type declaration", node.SourceSpan));
        
        base.VisitVariable(node);
    }
}
```

This design provides a robust, extensible foundation for representing and analyzing PeopleCode programs while maintaining performance and memory efficiency.