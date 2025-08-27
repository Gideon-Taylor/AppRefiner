# Refactor Porting Guide: ANTLR to Self-Hosted Parser

This guide provides critical knowledge for porting refactors from the ANTLR-based listener pattern to the new self-hosted parser's AST visitor pattern, based on successful styler porting experiences.

---

## 🔄 **ARCHITECTURE COMPARISON**

### Old System (ANTLR-based)
- **Pattern**: Listener with Enter/Exit methods + CodeChange tracking
- **Base Classes**: `BaseRefactor` and `ScopedRefactor<T>`
- **State Management**: Manual tracking with boolean flags + scope stacks
- **Context Access**: ANTLR parse tree contexts
- **Code Modification**: `CodeChange` objects (Insert, Delete, Replace)
- **Dialog Integration**: `RequiresUserInputDialog` + `DeferDialogUntilAfterVisitor`
- **Location**: `AppRefiner\Refactors\`

### New System (Self-Hosted Parser)
- **Pattern**: Visitor with Visit methods + direct AST manipulation
- **Base Classes**: `BaseRefactor` or `ScopedRefactor` (to be created)
- **State Management**: AST nodes + scope tracking
- **Context Access**: Strongly-typed AST nodes
- **Code Modification**: Direct text editing via `SourceSpan` positioning
- **Dialog Integration**: Similar pattern but with AST-aware validation
- **Location**: `ParserPorting\Refactors\Impl\`

---

## 🏗️ **BASE CLASS SELECTION**

### Use `BaseRefactor` when:
- Simple text transformations without scope awareness
- Processing individual nodes in isolation (AddFlowerBox pattern)
- Code generation at specific positions
- No variable/method context tracking required

### Use `ScopedRefactor` when:
- Need to track variables, methods, or class context
- Require scope-aware analysis (RenameLocalVariable pattern)
- Need to understand method/constructor context
- Working with variable usage patterns
- Complex refactors with multi-scope transformations

```csharp
// Old ANTLR pattern
public class RenameLocalVariable : ScopedRefactor<List<(int, int)>>
{
    private readonly Dictionary<string, List<(int, int)>> targetScope = new();
    
    public override void EnterIdentUserVariable(IdentUserVariableContext context)
    {
        string varName = context.GetText();
        var span = (context.Start.ByteStartIndex(), context.Stop.ByteStopIndex());
        AddOccurrence(varName, span, true);
    }
}

// New self-hosted pattern
public class RenameLocalVariable : ScopedRefactor
{
    private readonly Dictionary<string, List<SourceSpan>> targetScope = new();
    
    public override void VisitIdentifier(IdentifierNode node)
    {
        if (node.IdentifierType == IdentifierType.UserVariable)
        {
            AddOccurrence(node.Name, node.SourceSpan, true);
        }
        base.VisitIdentifier(node);
    }
}
```

---

## 🎯 **KEY AST NODE TYPES FOR REFACTORS**

### Core Program Structure
- `ProgramNode` - Root of AST (replaces program contexts)
- `AppClassNode` - Class declarations
- `InterfaceNode` - Interface declarations
- `ImportNode` - Import statements

### Declarations
- `MethodNode` - Method declarations/implementations
- `PropertyNode` - Property declarations
- `VariableNode` - Variable declarations
- `ConstantNode` - Constant declarations

### Expressions
- `IdentifierNode` - Variable/function references
- `PropertyAccessNode` - Property access (obj.Property)
- `MethodCallNode` - Method calls
- `LiteralNode` - Literal values
- `AssignmentNode` - Assignment expressions

### Statements
- `IfStatementNode`, `ForStatementNode`, `WhileStatementNode`
- `BlockNode` - Statement blocks
- `LocalVariableDeclarationNode` - Local variable declarations

---

## 🔧 **CODE MODIFICATION PATTERNS**

### Old System (CodeChange Objects)
```csharp
// ANTLR approach with tracked changes
protected void ReplaceNode(ParserRuleContext context, string newText, string description)
{
    changes.Add(new ReplaceChange(
        context.Start.ByteStartIndex(),
        context.Stop.ByteStopIndex(),
        newText,
        description
    ));
}

protected void InsertText(int position, string textToInsert, string description)
{
    changes.Add(new InsertChange(position, textToInsert, description));
}

// Changes applied in reverse order to avoid position shifting
var sortedChanges = changes.OrderByDescending(c => c.StartIndex).ToList();
foreach (var change in sortedChanges)
{
    change.ApplyToScintilla(editor);
}
```

### New System (Direct AST Editing)
```csharp
// Self-hosted approach with direct text replacement
protected void ReplaceNode(AstNode node, string newText, string description)
{
    EditText(node.SourceSpan.Start.Index, node.SourceSpan.End.Index, newText, description);
}

protected void InsertText(SourcePosition position, string textToInsert, string description)
{
    EditText(position.Index, position.Index, textToInsert, description);
}

// Direct application with automatic position tracking
ApplyEdits(); // Built into refactor framework
```

---

## 📍 **LOCATION TRACKING**

### Old System (Token-based)
```csharp
// Using ANTLR tokens for positioning
int startPos = context.Start.ByteStartIndex();
int endPos = context.Stop.ByteStopIndex();
ReplaceText(startPos, endPos, newText, description);

// Manual cursor position tracking
if (span.Item1 <= CurrentPosition && CurrentPosition <= span.Item2 + 1)
{
    variableToRename = varName;
}
```

### New System (Span-based)
```csharp
// Using SourceSpan objects for precise positioning
var span = node.SourceSpan;
EditText(span.Start.Index, span.End.Index, newText, description);

// Automatic cursor position awareness
if (node.ContainsPosition(CurrentCursorPosition))
{
    variableToRename = node.Name;
}
```

**Key Difference**: New system provides more precise location information with built-in cursor position checking.

---

## 🎯 **DIALOG INTEGRATION PATTERNS**

### Modal Dialog with User Input
```csharp
// Old ANTLR pattern
public override bool RequiresUserInputDialog => true;
public override bool DeferDialogUntilAfterVisitor => true;

public override bool ShowRefactorDialog()
{
    using var dialog = new RenameVariableDialog(variableToRename ?? "");
    var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
    DialogResult result = dialog.ShowDialog(wrapper);
    
    if (result == DialogResult.OK)
    {
        newVariableName = dialog.NewVariableName;
        GenerateChanges(); // Called after dialog
        return true;
    }
    return false;
}

// New self-hosted pattern (similar structure)
public override bool RequiresUserInputDialog => true;
public override bool DeferDialogUntilAfterVisitor => true;

public override bool ShowRefactorDialog()
{
    using var dialog = new RenameVariableDialog(variableToRename ?? "");
    var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
    DialogResult result = dialog.ShowDialog(wrapper);
    
    if (result == DialogResult.OK)
    {
        newVariableName = dialog.NewVariableName;
        GenerateEdits(); // AST-aware edit generation
        return true;
    }
    return false;
}
```

---

## ⚡ **COMMON REFACTOR PATTERNS**

### 1. Simple Text Insertion (AddFlowerBox)
**ANTLR Pattern:**
```csharp
public override void EnterProgram(ProgramContext context)
{
    base.EnterProgram(context);
    InsertText(0, GenerateFlowerBoxHeader(), "Add flower box");
}
```

**Self-Hosted Pattern:**
```csharp
public override void VisitProgram(ProgramNode node)
{
    base.VisitProgram(node);
    InsertText(SourcePosition.Zero, GenerateFlowerBoxHeader(), "Add flower box");
}
```

### 2. Variable Collection and Reorganization (LocalVariableCollectorRefactor)
**ANTLR Pattern:**
```csharp
// Track variables across multiple scopes
private readonly Dictionary<string, List<VariableDeclarationInfo>> scopeVariables = new();

public override void EnterLocalVariableDefinition(LocalVariableDefinitionContext context)
{
    var typeContext = context.typeT();
    string typeName = GetTypeFromContext(typeContext);
    
    foreach (var varNode in context.USER_VARIABLE())
    {
        var variableInfo = new VariableDeclarationInfo
        {
            Name = varNode.GetText(),
            Type = typeName,
            Context = context,
            StartIndex = context.Start.ByteStartIndex(),
            StopIndex = context.Stop?.ByteStopIndex() ?? context.Start.ByteStartIndex()
        };
        AddVariableToRelevantScopes(variableInfo);
    }
}
```

**Self-Hosted Pattern:**
```csharp
// Direct AST navigation for variable collection
private readonly Dictionary<string, List<VariableInfo>> scopeVariables = new();

public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
{
    foreach (var varInfo in node.VariableNameInfos)
    {
        var variableInfo = new VariableInfo
        {
            Name = varInfo.Name,
            Type = node.Type.ToString(),
            Node = node,
            SourceSpan = varInfo.SourceSpan ?? node.SourceSpan
        };
        AddVariableToRelevantScopes(variableInfo);
    }
    base.VisitLocalVariableDeclaration(node);
}
```

### 3. Complex Scope-Aware Refactoring (RenameLocalVariable)
**ANTLR Pattern:**
```csharp
// Manual scope tracking with stack management
protected readonly Stack<Dictionary<string, T>> scopeStack = new();

public override void EnterMethod(MethodContext context)
{
    scopeStack.Push(new Dictionary<string, T>());
    OnEnterScope();
}

public override void ExitMethod(MethodContext context)
{
    var scope = scopeStack.Pop();
    OnExitScope(scope);
}
```

**Self-Hosted Pattern:**
```csharp
// Automatic scope management with built-in tracking
public override void VisitMethod(MethodNode node)
{
    using var scope = EnterScope(); // Automatic scope management
    
    // Process method parameters
    foreach (var param in node.Parameters)
    {
        TrackVariable(param.Name, param.Type, param.SourceSpan);
    }
    
    base.VisitMethod(node);
} // Scope automatically cleaned up
```

### 4. Auto-completion and Code Generation (CreateAutoComplete)
**ANTLR Pattern:**
```csharp
// Complex context navigation for type detection
public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
{
    var expr = context.expression();
    if (expr != null && IsCreateExpressionAtCursor(expr))
    {
        var typeContext = context.typeT();
        if (typeContext is AppClassTypeContext appClass)
        {
            detectedClassType = appClass.appClassPath()?.GetText();
            isAppropriateContext = true;
        }
    }
}
```

**Self-Hosted Pattern:**
```csharp
// Direct AST access for type detection
public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
{
    if (node.InitializerExpression is MethodCallNode call && 
        call.Method.Name.Equals("create", StringComparison.OrdinalIgnoreCase))
    {
        if (node.Type is AppClassTypeNode appClass)
        {
            detectedClassType = appClass.ClassName;
            isAppropriateContext = true;
        }
    }
    base.VisitLocalVariableDeclaration(node);
}
```

---

## 🧪 **TESTING PATTERNS**

### Unit Testing Structure
```csharp
[Fact]
public void RenameLocalVariable_RenamesAllOccurrences()
{
    var code = """
        method TestMethod()
            local string &oldName = "test";
            &oldName = "new value";
            MessageBox(0, &oldName, "", 0);
        end-method;
        """;
    
    var refactor = new RenameLocalVariable(mockEditor);
    var program = ParseCode(code);
    
    // Set cursor position on variable name
    refactor.Initialize(code, tokenStream, 25); // Position of &oldName
    
    program.Accept(refactor);
    refactor.ShowRefactorDialog(); // Simulates user input
    
    var changes = refactor.GetChanges();
    Assert.Equal(3, changes.Count); // Declaration + 2 usages
    Assert.All(changes, c => c.NewText.Contains("&newName"));
}
```

### Integration Testing
- Test with real PeopleCode samples from existing codebase
- Verify position accuracy with Scintilla integration
- Ensure dialog interactions work correctly
- Test performance requirements (should be 2x faster than ANTLR)

---

## 🔧 **COMMON MIGRATION PATTERNS**

### 1. Context Enter/Exit → Single Visit with Scope
```csharp
// Old: Multiple methods for state tracking
public override void EnterMethod(MethodContext context) { /* setup */ }
public override void ExitMethod(MethodContext context) { /* cleanup */ }

// New: Single method with automatic scope management
public override void VisitMethod(MethodNode node)
{
    using var scope = EnterScope();
    // Process method with automatic cleanup
    base.VisitMethod(node);
}
```

### 2. Manual Position Tracking → AST Navigation
```csharp
// Old: Manual byte position calculation
int startPos = context.Start.ByteStartIndex();
int endPos = context.Stop.ByteStopIndex();
if (startPos <= CurrentPosition && CurrentPosition <= endPos)
{
    // Process context
}

// New: Built-in position checking
if (node.ContainsPosition(CurrentCursorPosition))
{
    // Process node with automatic span handling
}
```

### 3. CodeChange Objects → Direct Editing
```csharp
// Old: Tracked change objects
var change = new ReplaceChange(startIndex, endIndex, newText, description);
changes.Add(change);

// New: Direct text editing
EditText(node.SourceSpan, newText, description);
```

---

## ❌ **COMMON PITFALLS**

### 1. **Forgetting to Call Base Methods**
```csharp
public override void VisitIdentifier(IdentifierNode node)
{
    // Your processing here...
    
    base.VisitIdentifier(node); // ⚠️ Don't forget this!
}
```

### 2. **Incorrect Position Assumptions**
```csharp
// ❌ Wrong: Assuming positions are always valid
var position = node.SourceSpan.Start.Index;
EditText(position, position, newText);

// ✅ Correct: Validate positions first
if (node.SourceSpan.IsValid && node.SourceSpan.Start.Index >= 0)
{
    EditText(node.SourceSpan, newText, description);
}
```

### 3. **Missing Dialog State Management**
```csharp
// ❌ Wrong: Not handling deferred dialog properly
public override void ExitProgram(ProgramNode node)
{
    GenerateChanges(); // Called before dialog!
}

// ✅ Correct: Validate state, defer changes to dialog
public override void ExitProgram(ProgramNode node)
{
    if (variableToRename == null)
    {
        SetFailure("No variable found at cursor position.");
    }
    // Changes generated in ShowRefactorDialog after user input
}
```

### 4. **Incorrect Scope Handling**
```csharp
// ❌ Wrong: Manual scope management without cleanup
private readonly Stack<Dictionary<string, object>> scopes = new();

// ✅ Correct: Use automatic scope management
using var scope = EnterScope();
// Automatic cleanup when scope exits
```

---

## 📚 **REFERENCE MATERIALS**

### Key Files to Study
- **Base Classes**: `ParserPorting\Refactors\BaseRefactor.cs`, `ScopedRefactor.cs`
- **Example Implementations**: `ParserPorting\Refactors\Impl\RenameLocalVariable.cs`
- **AST Nodes**: `PeopleCodeParser.SelfHosted\Nodes\*.cs`
- **Visitor Interface**: `PeopleCodeParser.SelfHosted\Visitors\IAstVisitor.cs`
- **Original ANTLR Examples**: `AppRefiner\Refactors\*.cs`

### Migration Checklist
- [ ] Choose appropriate base class (`BaseRefactor` vs `ScopedRefactor`)
- [ ] Replace Enter/Exit methods with Visit methods
- [ ] Convert context access to AST node properties
- [ ] Update position tracking from tokens to spans
- [ ] Replace CodeChange objects with direct editing
- [ ] Handle dialog integration and user input validation
- [ ] Implement proper scope management if needed
- [ ] Write comprehensive unit tests
- [ ] Verify performance improvements

---

## 🎯 **SUCCESS METRICS**

A successful port should achieve:
- **Functional Parity**: Same refactoring behavior as original
- **Performance Improvement**: 2x faster execution than ANTLR version
- **Memory Efficiency**: 70% of ANTLR memory usage
- **Code Quality**: Cleaner, more maintainable implementation
- **Test Coverage**: Comprehensive unit and integration tests
- **Dialog Integration**: Proper user experience with validation

---

## 🚀 **REFACTOR-SPECIFIC PORTING PATTERNS**

### **Simple Text Generation Refactors**
*Pattern used by: AddFlowerBox*

```csharp
// ANTLR Pattern: Entry point insertion
public override void EnterProgram(ProgramContext context)
{
    base.EnterProgram(context);
    InsertText(0, GenerateFlowerBoxHeader(), "Add flower box");
}

// Self-Hosted Pattern: Direct insertion
public override void VisitProgram(ProgramNode node)
{
    base.VisitProgram(node);
    InsertText(SourcePosition.Zero, GenerateFlowerBoxHeader(), "Add flower box");
}
```

### **Multi-Scope Variable Processing**
*Pattern used by: LocalVariableCollectorRefactor*

```csharp
// ANTLR Pattern: Complex scope tracking
private readonly Dictionary<string, List<VariableDeclarationInfo>> scopeVariables = new();

private void ProcessScope(ParserRuleContext context, string scopeType)
{
    bool shouldProcess = selectedScopeMode == ScopeProcessingMode.AllScopes || 
                       (CurrentPosition >= contextStart && CurrentPosition <= contextEnd);
    
    if (shouldProcess)
    {
        int insertionPoint = FindScopeInsertionPoint(context, scopeType);
        string scopeKey = $"{scopeType}_{contextStart}_{contextEnd}";
        scopeVariables[scopeKey] = new List<VariableDeclarationInfo>();
        scopeInsertionPoints[scopeKey] = insertionPoint;
    }
}

// Self-Hosted Pattern: Direct scope enumeration
public override void VisitProgram(ProgramNode node)
{
    if (selectedScopeMode == ScopeProcessingMode.AllScopes)
    {
        ProcessAllScopes(node);
    }
    else if (node.ContainsPosition(CurrentCursorPosition))
    {
        ProcessCurrentScope(node);
    }
    base.VisitProgram(node);
}

private void ProcessAllScopes(ProgramNode node)
{
    // Direct access to all methods and properties
    if (node is AppClassNode appClass)
    {
        foreach (var method in appClass.Methods)
        {
            ProcessMethodScope(method);
        }
        foreach (var property in appClass.Properties)
        {
            if (property.Getter != null) ProcessPropertyScope(property.Getter);
            if (property.Setter != null) ProcessPropertyScope(property.Setter);
        }
    }
}
```

### **Complex Rename Operations**
*Pattern used by: RenameLocalVariable*

```csharp
// ANTLR Pattern: Manual occurrence tracking
private readonly Dictionary<string, List<(int, int)>> targetScope = new();

public override void EnterIdentUserVariable(IdentUserVariableContext context)
{
    string varName = context.GetText();
    var span = (context.Start.ByteStartIndex(), context.Stop.ByteStopIndex());
    AddOccurrence(varName, span, true);
}

private void AddOccurrence(string varName, (int, int) span, bool mustExist = false)
{
    foreach (var scope in scopeStack)
    {
        if (scope.ContainsKey(varName))
        {
            scope[varName].Add(span);
            return;
        }
    }
}

// Self-Hosted Pattern: Automatic reference tracking
private readonly Dictionary<string, List<SourceSpan>> targetScope = new();

public override void VisitIdentifier(IdentifierNode node)
{
    if (node.IdentifierType == IdentifierType.UserVariable)
    {
        AddOccurrence(node.Name, node.SourceSpan, true);
    }
    base.VisitIdentifier(node);
}

private void AddOccurrence(string varName, SourceSpan span, bool mustExist = false)
{
    if (TryFindInCurrentScopes(varName, out var scopeEntry))
    {
        scopeEntry.Occurrences.Add(span);
    }
}
```

### **Auto-Completion with Type Detection**
*Pattern used by: CreateAutoComplete*

```csharp
// ANTLR Pattern: Complex context navigation
public override void EnterLocalVariableDeclAssignment(LocalVariableDeclAssignmentContext context)
{
    var expr = context.expression();
    if (expr != null && IsCreateExpressionAtCursor(expr))
    {
        var typeContext = context.typeT();
        if (typeContext is AppClassTypeContext appClass)
        {
            detectedClassType = appClass.appClassPath()?.GetText();
            isAppropriateContext = true;
        }
    }
}

private bool IsCreateExpressionAtCursor(ExpressionContext expr)
{
    if (expr is FunctionCallExprContext functionCallExpr)
    {
        var simpleFunc = functionCallExpr.simpleFunctionCall();
        if (simpleFunc?.genericID()?.GetText().ToLower() == "create")
        {
            return CurrentPosition > createStartPos && CurrentPosition <= createEndPos;
        }
    }
    return false;
}

// Self-Hosted Pattern: Direct AST pattern matching
public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
{
    if (node.InitializerExpression is MethodCallNode call && 
        IsCreateCall(call) && 
        call.SourceSpan.ContainsPosition(CurrentCursorPosition))
    {
        if (node.Type is AppClassTypeNode appClass)
        {
            detectedClassType = appClass.ClassName;
            isAppropriateContext = true;
        }
    }
    base.VisitLocalVariableDeclaration(node);
}

private bool IsCreateCall(MethodCallNode call)
{
    return call.Method is IdentifierNode id && 
           id.Name.Equals("create", StringComparison.OrdinalIgnoreCase);
}
```

### **Method and Property Reordering**
*Pattern used by: SortMethods*

```csharp
// ANTLR Pattern: Separate declaration/implementation tracking
private readonly List<MethodInfo> methodDeclarations = new();
private readonly List<MethodInfo> methodImplementations = new();

public override void EnterMethodHeader(MethodHeaderContext context)
{
    var methodName = context.genericID()?.GetText();
    if (methodName != null)
    {
        methodDeclarations.Add(new MethodInfo(
            methodName,
            context.Start.ByteStartIndex(),
            context.Stop.ByteStopIndex(),
            GetOriginalText(context)!
        ));
    }
}

public override void EnterMethod(MethodContext context)
{
    var methodName = context.genericID()?.GetText();
    if (methodName != null)
    {
        methodImplementations.Add(new MethodInfo(
            methodName,
            context.Start.ByteStartIndex(),
            context.Stop.ByteStopIndex(),
            GetOriginalText(context, true)!
        ));
    }
}

// Self-Hosted Pattern: Direct AST structure analysis
public override void VisitAppClass(AppClassNode node)
{
    // Direct access to ordered declarations and implementations
    var orderedDeclarations = node.Methods
        .Concat(node.Properties.SelectMany(p => new[] { p.Getter, p.Setter }.Where(a => a != null)))
        .OrderBy(m => m.DeclarationOrder)
        .ToList();
    
    var orderedImplementations = node.MethodImplementations
        .Concat(node.PropertyImplementations)
        .OrderBy(m => m.SourceSpan.Start.Index)
        .ToList();
    
    if (!AreInCorrectOrder(orderedDeclarations, orderedImplementations))
    {
        GenerateReorderingChanges(orderedDeclarations, orderedImplementations);
    }
    
    base.VisitAppClass(node);
}
```

---

## 💡 **CRITICAL SUCCESS FACTORS FOR REFACTOR PORTING**

### **1. Leverage Built-in AST Structure**
- **Don't manually parse**: Use direct AST node access instead of context drilling
- **Don't track positions**: Use built-in `SourceSpan` from AST nodes
- **Don't manage scope manually**: Use `ScopedRefactor` for automatic scope management

### **2. Choose the Right Editing Strategy**
```csharp
// Simple insertions → BaseRefactor with direct editing
public class AddFlowerBox : BaseRefactor
{
    public override void VisitProgram(ProgramNode node)
    {
        InsertText(SourcePosition.Zero, GenerateFlowerBox(), "Add flower box");
        base.VisitProgram(node);
    }
}

// Complex transformations → ScopedRefactor with tracked changes
public class RenameLocalVariable : ScopedRefactor
{
    public override void VisitProgram(ProgramNode node)
    {
        // Collect all references first
        base.VisitProgram(node);
        
        // Apply all changes in optimal order
        ApplyCollectedChanges();
    }
}
```

### **3. Handle Dialog Integration Properly**
```csharp
// Proper deferred dialog pattern
public override bool RequiresUserInputDialog => true;
public override bool DeferDialogUntilAfterVisitor => true;

public override void ExitProgram(ProgramNode node)
{
    // Only validate preconditions here
    if (targetVariable == null)
    {
        SetFailure("No variable found at cursor position.");
        return;
    }
    
    // Don't generate changes - wait for dialog
}

public override bool ShowRefactorDialog()
{
    // Now generate changes with user input
    if (ShowUserDialog() == DialogResult.OK)
    {
        GenerateChangesWithUserInput();
        return true;
    }
    return false;
}
```

### **4. Maintain Performance and Memory Efficiency**
- Use direct AST access instead of multiple traversals
- Leverage built-in collections (`node.Methods`, `node.Properties`)
- Avoid creating unnecessary intermediate objects
- Use `SourceSpan` efficiently for position calculations

---

## 📊 **EXPECTED PORTING BENEFITS**

### **Consistent Performance Improvements**
- **Average Code Reduction**: 30-50% fewer lines due to direct AST access
- **Complexity Reduction**: Eliminated manual position tracking and context drilling
- **Readability Improvement**: Type-safe AST navigation vs context parsing
- **Maintainability**: Clear separation of concerns with helper methods

### **Architecture Benefits**
1. **Direct AST Access**: `node.Methods` vs `context.methodDeclaration()`
2. **Built-in Positioning**: `node.SourceSpan` vs manual byte calculations
3. **Type Safety**: Pattern matching vs context casting and null checks
4. **Natural Collections**: `node.VariableDeclarations` vs event-based collection

---

## 🎓 **LESSONS LEARNED FROM STYLER PORTING**

### **What Works Exceptionally Well for Refactors**
1. **Direct AST Manipulation**: Built-in collections eliminate manual traversal
2. **SourceSpan Positioning**: Precise position tracking without manual calculation
3. **Scoped Processing**: Automatic scope management for complex refactors
4. **Pattern Matching**: Type-safe node identification and processing

### **What Requires Careful Attention for Refactors**
1. **Edit Order Management**: Ensure changes don't conflict with each other
2. **Dialog Timing**: Proper deferred dialog implementation
3. **User Input Validation**: AST-aware validation in dialogs
4. **Position Accuracy**: Ensuring edits apply to correct locations

### **Refactor-Specific Time Savings**
- **Manual Position Tracking**: Eliminated entirely (saves 40-60 lines typically)
- **Context Navigation**: Replaced with direct AST access (saves 20-40 lines)
- **Scope Management**: Automated with ScopedRefactor (saves 30-50 lines)
- **Change Coordination**: Built-in edit management (saves 20-30 lines)

---

*Last Updated: 2025-01-26 - Based on analysis of 5 production refactors and successful styler porting patterns*