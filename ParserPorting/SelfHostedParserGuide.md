# Self-Hosted Parser Guide

This guide covers the architecture and implementation patterns for working with AppRefiner's self-hosted parser framework.

## Core Architecture

### Parser Ecosystem
```
AppRefiner Self-Hosted Parser Ecosystem
├── Lexer: PeopleCodeLexer (tokenization)
├── Parser: PeopleCodeParser (AST generation)
├── AST Nodes: Strongly-typed node hierarchy
├── Visitors: IAstVisitor pattern for traversal
└── Consumers: Stylers, Refactors, Linters, Tooltips
```

### AST Node Hierarchy
```csharp
ProgramNode (root)
├── AppClassNode / InterfaceNode (declarations)
│   ├── MethodNode[] (methods/constructors)
│   ├── PropertyNode[] (properties)
│   ├── VariableNode[] (instance variables)
│   └── ConstantNode[] (constants)
├── StatementNode[] (executable code)
│   ├── LocalVariableDeclarationNode
│   ├── IfStatementNode / ForStatementNode / WhileStatementNode
│   ├── BlockNode (statement containers)
│   └── AssignmentNode (assignments)
└── ExpressionNode[] (values/operations)
    ├── IdentifierNode (variables/functions)
    ├── MethodCallNode / FunctionCallNode
    ├── PropertyAccessNode / MemberAccessNode
    └── LiteralNode (strings, numbers, etc.)
```

## Base Class Selection

### For Simple Operations
Use `BaseStyler` or `BaseRefactor` when:
- Highlighting/flagging individual nodes
- Simple code generation
- Direct AST node processing
- Database-driven analysis
- Comment/text processing

```csharp
public class TodoFixmeStyler : BaseStyler
{
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
        ProcessComments(node);
    }
}
```

### For Complex Scope Analysis
Use `ScopedStyler` or `ScopedRefactor` when:
- Variable usage tracking across scopes
- Undefined/unused variable detection
- Complex rename operations
- Multi-scope transformations

Features provided automatically:
- Variable declaration tracking
- Scope stack management
- Parameter registration
- Method/property context

```csharp
public class UndefinedVariables : ScopedStyler
{
    protected override void OnVariableDeclared(VariableInfo varInfo, ScopeInfo scope)
    {
        // Automatically called - no manual tracking needed
    }
}
```

## AST Navigation Patterns

### Direct Collection Access
```csharp
// ANTLR approach: Event-driven with manual state tracking
private bool inPublicSection = false;
public override void EnterPublicHeader(PublicHeaderContext ctx) { inPublicSection = true; }
public override void ExitPublicHeader(PublicHeaderContext ctx) { inPublicSection = false; }

// Self-hosted approach: Direct access with built-in filtering
public override void VisitAppClass(AppClassNode node)
{
    // Direct access - no state tracking needed
    var publicProperties = node.Properties
        .Where(p => p.Visibility == VisibilityModifier.Public)
        .ToList();

    var publicMethods = node.Methods
        .Where(m => m.Visibility == VisibilityModifier.Public)
        .ToList();
}
```

### Type-Safe Identifier Detection
```csharp
// Built-in identifier classification
public override void VisitIdentifier(IdentifierNode node)
{
    switch (node.IdentifierType)
    {
        case IdentifierType.UserVariable:      // &variable
            ProcessUserVariable(node.Name);
            break;
        case IdentifierType.SystemVariable:    // %variable
            ProcessSystemVariable(node.Name);
            break;
        case IdentifierType.Function:          // Function calls
            ProcessFunctionCall(node.Name);
            break;
    }
    base.VisitIdentifier(node);
}
```

### Built-in Control Flow Detection
```csharp
// Automatic control flow analysis
public override void VisitBlock(BlockNode block)
{
    bool foundEarlyReturn = false;

    for (int i = 0; i < block.Statements.Count; i++)
    {
        var statement = block.Statements[i];

        if (!foundEarlyReturn)
        {
            statement.Accept(this);
            // Built-in property - no manual detection needed
            if (statement.CanTransferControl) // return, exit, throw
                foundEarlyReturn = true;
        }
        else
        {
            // Automatically flag as dead code
            FlagDeadCode(statement);
        }
    }
    base.VisitBlock(block);
}
```

## Database Integration Patterns

### External Class Parsing
```csharp
private ProgramNode? ParseExternalClass(string classPath)
{
    if (DataManager == null) return null;

    try
    {
        // Get source from database
        string? sourceCode = DataManager.GetAppClassSourceByPath(classPath);
        if (string.IsNullOrEmpty(sourceCode)) return null;

        // Parse with self-hosted parser
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        return parser.ParseProgram();
    }
    catch (Exception)
    {
        return null; // Silently handle parsing errors
    }
}

// Direct AST access to parsed external classes
if (baseProgram?.AppClass != null)
{
    var constructors = baseProgram.AppClass.Methods
        .Where(m => m.IsConstructor)
        .ToList();

    var abstractMethods = baseProgram.AppClass.Methods
        .Where(m => m.IsAbstract)
        .ToList();
}
```

## Cursor Position and Span Management

### Cursor-Aware Targeting
```csharp
// Automatic cursor position awareness - no manual calculation
public override void VisitAppClass(AppClassNode node)
{
    var methodsNeedingImplementation = FindMethodsNeedingImplementation(node);

    // Use cursor position for precise targeting
    var targetMethod = methodsNeedingImplementation
        .FirstOrDefault(m => m.SourceSpan.ContainsPosition(CurrentPosition))
        ?? methodsNeedingImplementation.FirstOrDefault();

    if (targetMethod != null)
    {
        GenerateImplementation(targetMethod);
    }
}

// Built-in SourceSpan provides precise positioning
AddIndicator(
    (node.SourceSpan.Start.ByteIndex, node.SourceSpan.End.ByteIndex),
    IndicatorType.SQUIGGLE,
    WARNING_COLOR,
    tooltip
);
```

### SourceSpan vs Token-Based Positioning
```csharp
// ANTLR approach: Manual position calculation with potential errors
int startPos = context.Start.ByteStartIndex();
int endPos = context.Stop?.ByteStopIndex() ?? startPos;
if (startPos <= CurrentPosition && CurrentPosition <= endPos + 1)

// Self-hosted approach: Built-in precision with automatic validation
if (node.SourceSpan.IsValid && node.ContainsPosition(CurrentCursorPosition))
{
    // Automatic span handling - no calculation needed
    ProcessNode(node);
}
```

## Code Generation Patterns

### Parameter Collision Detection
```csharp
private readonly HashSet<string> existingMemberNames = new(StringComparer.OrdinalIgnoreCase);

private void CollectExistingMemberNames(AppClassNode node)
{
    existingMemberNames.Clear();

    // Collect all existing names to prevent collisions
    foreach (var method in node.Methods)
    {
        existingMemberNames.Add(method.Name);
        foreach (var param in method.Parameters)
        {
            if (!string.IsNullOrEmpty(param.Name))
                existingMemberNames.Add(param.Name);
        }
    }

    foreach (var property in node.Properties)
    {
        existingMemberNames.Add(property.Name);
    }
}

private string GenerateSafeParameterName(string baseName)
{
    string safeName = baseName;
    int counter = 1;
    while (existingMemberNames.Contains(safeName))
    {
        safeName = $"{baseName}{counter++}";
    }
    existingMemberNames.Add(safeName);
    return safeName;
}
```

### Recursive Hierarchy Traversal
```csharp
private void CollectAbstractMembers(string typePath, HashSet<string> implementedSignatures,
    Dictionary<string, MethodNode> abstractMethods, Dictionary<string, PropertyNode> abstractProperties)
{
    var program = ParseExternalClass(typePath);
    if (program == null) return;

    var isInterface = program.Interface != null;
    var methods = isInterface ? program.Interface!.Methods : program.AppClass?.Methods;
    var properties = isInterface ? program.Interface!.Properties : program.AppClass?.Properties;

    // Process methods - all interface methods are abstract
    if (methods != null)
    {
        foreach (var method in methods.Where(m => isInterface || m.IsAbstract))
        {
            string signature = $"M:{method.Name}({method.Parameters.Count})";
            if (!implementedSignatures.Contains(signature))
                abstractMethods.TryAdd(signature, method);
        }
    }

    // Recurse to parent
    string? parentPath = isInterface
        ? program.Interface?.BaseInterface?.TypeName
        : program.AppClass?.BaseClass?.TypeName;

    if (parentPath != null)
    {
        CollectAbstractMembers(parentPath, implementedSignatures, abstractMethods, abstractProperties);
    }
}
```

## Performance and Memory Patterns

### Leverage Built-in Collections
```csharp
// Inefficient: Multiple AST traversals
public override void VisitProgram(ProgramNode node)
{
    // First pass for methods
    base.VisitProgram(node);
    // Second pass for properties
    base.VisitProgram(node);
    // Third pass for variables
    base.VisitProgram(node);
}

// Efficient: Single pass with direct access
public override void VisitAppClass(AppClassNode node)
{
    // Direct access - O(1) per collection
    ProcessMethods(node.Methods);
    ProcessProperties(node.Properties);
    ProcessVariables(node.InstanceVariables);

    base.VisitAppClass(node);
}
```

### Memory-Efficient Node Processing
```csharp
// Use direct collections instead of intermediate objects
var publicMembers = node.Methods
    .Where(m => m.Visibility == VisibilityModifier.Public)
    .Concat(node.Properties.Where(p => p.Visibility == VisibilityModifier.Public))
    .Select(m => m.Name)
    .ToHashSet(); // Single allocation, direct processing
```

## Anti-Patterns to Avoid

### Manual State Tracking
```csharp
// Avoid: Manual scope/context tracking
private bool inMethod = false;
private bool inPublicSection = false;
private string? currentMethodName = null;

// Better: Use direct AST access or ScopedStyler
public override void VisitMethod(MethodNode node)
{
    // Direct access to method context
    string methodName = node.Name;
    bool isPublic = node.Visibility == VisibilityModifier.Public;

    // Or use ScopedStyler for automatic tracking
    var scope = GetCurrentScopeInfo();
}
```

### Manual Position Calculation
```csharp
// Avoid: Manual byte position calculation
int start = token.StartIndex;
int end = token.StopIndex;
if (start <= cursor && cursor <= end + 1)

// Better: Use built-in SourceSpan
if (node.SourceSpan.ContainsPosition(CurrentCursorPosition))
{
    // Automatic precision - no calculation errors
}
```

### Forgetting Base Method Calls
```csharp
// Avoid: Forget base calls
public override void VisitIdentifier(IdentifierNode node)
{
    ProcessIdentifier(node);
    // Missing: base.VisitIdentifier(node); - BREAKS child processing
}

// Correct: Always call base methods
public override void VisitIdentifier(IdentifierNode node)
{
    ProcessIdentifier(node);
    base.VisitIdentifier(node); // Essential for complete traversal
}
```

## Testing Strategies

### AST-Based Unit Testing
```csharp
[Fact]
public void DetectsIssueCorrectly()
{
    var code = """
        class MyClass
        public
            property string MyProp get set;

        method DoSomething()
            &MyProp = "value";  // Should be detected
        end-method;
        end-class;
        """;

    // Parse with self-hosted parser
    var lexer = new PeopleCodeLexer(code);
    var tokens = lexer.TokenizeAll();
    var parser = new PeopleCodeParser(tokens);
    var program = parser.ParseProgram();

    // Test with your component
    var styler = new MyStyler();
    program.Accept(styler);

    // Verify results
    Assert.Single(styler.Indicators);
    Assert.Contains("MyProp", styler.Indicators[0].Tooltip);
}
```

## Migration Patterns

### ANTLR to Self-Hosted Conversion
```csharp
// Common migration patterns for all components

// Pattern 1: Context drilling → Direct access
// ANTLR: context.parent.methodDeclaration().identifier().GetText()
// Self-hosted: method.Name

// Pattern 2: Enter/Exit events → Single visit
// ANTLR: EnterMethod() + ExitMethod() with state tracking
// Self-hosted: VisitMethod() with direct node access

// Pattern 3: Token symbols → SourceSpan
// ANTLR: token.Symbol with byte position calculation
// Self-hosted: node.SourceSpan with built-in precision

// Pattern 4: Manual type detection → Built-in properties
// ANTLR: Parse context type and extract information
// Self-hosted: node.IdentifierType, node.Visibility, etc.
```

## Shared Validation Architecture

### Reusable Validation Logic
```csharp
// When stylers and linters need identical validation logic
ParserPorting/
  Shared/
    [Domain]/
      [Domain]Validator.cs    // Core validation logic
      [Domain]Context.cs      // Shared context/helpers
      Models/
        [Domain]Info.cs       // Data models

// Validator accepts AST nodes, returns standardized Reports
public class SQLVariableValidator
{
    public List<Report> ValidateCreateSQL(FunctionCallNode node) { }
    public List<Report> ValidateSQLExecution(MethodCallNode node) { }
}

// Styler uses shared validator
public class SQLStyler : BaseStyler
{
    private readonly SQLVariableValidator validator;

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        var reports = validator.ValidateCreateSQL(node);
        ProcessReports(reports); // Convert to Indicators
        base.VisitFunctionCall(node);
    }
}

// Linter uses same validator
public class SQLLinter : BaseLintRule
{
    private readonly SQLVariableValidator validator;

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        var reports = validator.ValidateCreateSQL(node);
        Reports.AddRange(reports); // Direct Report usage
        base.VisitFunctionCall(node);
    }
}
```

## Key Implementation Patterns

### Core Patterns
1. **Direct AST Access**: Use `node.Methods`, `node.Properties` instead of context drilling
2. **Built-in Properties**: Leverage `CanTransferControl`, `IdentifierType`, `Visibility`
3. **SourceSpan Positioning**: Use automatic precision instead of manual calculation
4. **Database Integration**: Parse external classes seamlessly
5. **Cursor Awareness**: Use `ContainsPosition()` for precise targeting
6. **Parameter Safety**: Implement collision detection for code generation
7. **Hierarchy Recursion**: Handle complete parent class analysis

## Component-Specific Quick Reference

### Stylers (Visual highlighting/indicators)
- Use `BaseStyler` for simple highlighting
- Use `ScopedStyler` for variable analysis
- `AddIndicator()` with `SourceSpan` positioning
- Process comments via `ProgramNode.Comments`

### Refactors (Code transformations)
- Use `BaseRefactor` for targeted operations
- Use `ScopedRefactor` for complex scope-aware changes
- `InsertText()`, `EditText()`, `DeleteText()` for modifications
- Cursor position awareness for precise targeting

### Linters (Issue detection and reporting)
- Use `BaseLintRule` for simple rule checking
- Use `ScopedLintRule` for variable/scope analysis
- Return `List<Report>` objects
- Consider shared validation architecture for complex rules

### Tooltips (Contextual information)
- Use AST node navigation for context determination
- Access `DataManager` for external class information
- Leverage built-in node properties for type information
- Use `SourceSpan` for precise hover targeting