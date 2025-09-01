# Refactor Porting Guide: ANTLR to Self-Hosted Parser

This guide provides critical knowledge for porting refactors from the ANTLR-based listener pattern to the new self-hosted parser's AST visitor pattern, based on successful quick fix porting experiences and proven patterns from three production implementations:

**✅ Successfully Ported Quick Fixes:**
- **GenerateBaseConstructor** - Creates constructors with parameter collision detection
- **ImplementMissingMethod** - Generates missing method implementations with override annotations  
- **ImplementAbstractMembers** - Comprehensively implements abstract members with hierarchy traversal

This guide reflects real-world implementation patterns and proven solutions from these successful ports.

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
- **Base Classes**: `BaseRefactor` (✅ implemented in `AppRefiner\Refactors\`)
- **State Management**: AST nodes + automatic scope management
- **Context Access**: Strongly-typed AST nodes with built-in properties
- **Code Modification**: Direct text editing via `InsertText()`, `EditText()`, `DeleteText()`
- **Dialog Integration**: Same pattern with AST-aware validation
- **Location**: `AppRefiner\Refactors\QuickFixes\` (✅ proven)

---

## ✅ **PROVEN IMPLEMENTATION PATTERNS**

Based on three successfully ported quick fixes, these patterns have been validated in production:

### **Pattern 1: Database-Driven Code Generation (GenerateBaseConstructor)**
```csharp
public class GenerateBaseConstructor : BaseRefactor
{
    private AppClassNode? targetClass;
    private List<MethodNode> baseConstructors = new();
    private HashSet<string> existingMemberNames = new(StringComparer.OrdinalIgnoreCase);
    
    public override void VisitAppClass(AppClassNode node)
    {
        base.VisitAppClass(node);
        targetClass = node;
        
        // Collect existing members to avoid naming conflicts
        CollectExistingMemberNames(node);
        
        if (Editor.DataManager != null && node.BaseClass != null)
        {
            AnalyzeBaseClassWithSelfHostedParser(node.BaseClass.TypeName);
            GenerateConstructorWithSafeParameterNames();
        }
    }
    
    private void AnalyzeBaseClassWithSelfHostedParser(string baseClassPath)
    {
        var baseClassSource = Editor.DataManager.GetAppClassSourceByPath(baseClassPath);
        var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(baseClassSource);
        var tokens = lexer.TokenizeAll();
        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
        var baseProgram = parser.ParseProgram();
        
        // Direct AST access - much cleaner than ANTLR context navigation
        if (baseProgram?.AppClass != null)
        {
            baseConstructors.AddRange(baseProgram.AppClass.Methods.Where(m => m.IsConstructor));
        }
    }
}
```

### **Pattern 2: Cursor-Aware Quick Fixes (ImplementMissingMethod)**
```csharp
public class ImplementMissingMethod : BaseRefactor
{
    private AppClassNode? targetClass;
    private MethodNode? targetMethod;
    
    public override void VisitAppClass(AppClassNode node)
    {
        base.VisitAppClass(node);
        targetClass = node;
        
        var methodsNeedingImplementation = FindMethodsNeedingImplementation(node);
        
        // Use cursor position to target specific method - proven pattern
        targetMethod = FindTargetMethodByCursorPosition(methodsNeedingImplementation) 
                      ?? methodsNeedingImplementation.FirstOrDefault();
        
        if (targetMethod != null)
        {
            GenerateMethodImplementation();
        }
    }
    
    private MethodNode? FindTargetMethodByCursorPosition(List<MethodNode> methods)
    {
        var currentPosition = CurrentPosition;
        return methods.FirstOrDefault(m => m.SourceSpan.ContainsPosition(currentPosition));
    }
}
```

### **Pattern 3: Recursive Hierarchy Analysis (ImplementAbstractMembers)**
```csharp
public class ImplementAbstractMembers : BaseRefactor
{
    public override void VisitAppClass(AppClassNode node)
    {
        base.VisitAppClass(node);
        
        if (node.BaseClass != null || node.ImplementedInterface != null)
        {
            // Use same comprehensive hierarchy traversal as stylers
            var abstractMethodsDict = new Dictionary<string, MethodNode>();
            var abstractPropertiesDict = new Dictionary<string, PropertyNode>();
            var implementedSignatures = GetImplementedSignatures(node);
            
            // Check both inheritance chains
            if (node.BaseClass != null)
                CollectAbstractMembers(node.BaseClass.TypeName, implementedSignatures, 
                                     abstractMethodsDict, abstractPropertiesDict);
            
            if (node.ImplementedInterface != null)
                CollectAbstractMembers(node.ImplementedInterface.TypeName, implementedSignatures,
                                     abstractMethodsDict, abstractPropertiesDict);
            
            GenerateAllAbstractMemberImplementations(abstractMethodsDict.Values, 
                                                   abstractPropertiesDict.Values);
        }
    }
}
```

### **Key Success Factors from Real Implementations:**

1. **✅ Use BaseRefactor for Quick Fixes**: All three successful ports used `BaseRefactor` - simpler than `ScopedRefactor` for targeted operations
2. **✅ Database Integration**: Self-hosted parser works seamlessly with `DataManager.GetAppClassSourceByPath()`
3. **✅ Cursor Position Awareness**: `CurrentPosition` and `SourceSpan.ContainsPosition()` enable precise targeting
4. **✅ Parameter Name Safety**: Always check for naming conflicts with existing class members
5. **✅ Hierarchy Recursion**: Use same logic as stylers for consistency between detection and implementation

---

## 🏗️ **BASE CLASS SELECTION**

### Use `BaseRefactor` when: ✅ **PROVEN CHOICE**
- **Quick Fixes**: All three successful ports used `BaseRefactor`
- **Targeted Operations**: Single class/method focused refactors
- **Database Integration**: Refactors that analyze external classes via DataManager
- **Code Generation**: Creating constructors, methods, or implementations
- **AST-based Processing**: Direct AST node manipulation and analysis

### Use `ScopedRefactor` when: ⏳ **FOR FUTURE COMPLEX REFACTORS**
- **Variable Renaming**: Complex scope-aware variable tracking (RenameLocalVariable)
- **Multi-scope Analysis**: Processing variables across nested scopes
- **Usage Tracking**: Following variable/method references across contexts
- **Complex Transformations**: Refactors requiring comprehensive scope management

**📊 Real Data**: 3/3 successful quick fix ports used `BaseRefactor` - it's simpler and sufficient for most refactoring operations.

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

---

## 🎯 **QUICK FIX INTEGRATION PATTERNS** 

### **Styler-to-Quick-Fix Coordination** 
*Pattern proven by: MissingConstructor, MissingMethodImplementation, UnimplementedAbstractMembersStyler*

```csharp
// Styler detects problem and offers quick fix
public class MissingConstructor : BaseStyler
{
    public override void VisitAppClass(AppClassNode node)
    {
        if (DetectMissingConstructor(node))
        {
            var quickFixes = new List<(Type RefactorClass, string Description)>
            {
                (typeof(GenerateBaseConstructor), "Generate missing constructor")
            };
            
            AddIndicator((node.NameToken.SourceSpan.Start.ByteIndex, node.NameToken.SourceSpan.End.ByteIndex), 
                        IndicatorType.SQUIGGLE, WARNING_COLOR, tooltip, quickFixes);
        }
        base.VisitAppClass(node);
    }
}

// Quick fix implements the solution
public class GenerateBaseConstructor : BaseRefactor
{
    public override void VisitAppClass(AppClassNode node)
    {
        // Same detection logic as styler
        if (NeedsConstructor(node))
        {
            GenerateConstructorImplementation(node);
        }
        base.VisitAppClass(node);
    }
}
```

### **Critical Integration Requirements:**
1. **✅ Identical Detection Logic**: Quick fix must use same analysis as styler
2. **✅ SourceSpan Conversion**: Convert SourceSpan to (ByteIndex, ByteIndex) tuples for AddIndicator
3. **✅ Type Registration**: Register quick fix type with styler's indicator
4. **✅ Cursor Awareness**: Use cursor position to target specific issues when multiple exist

### **Proven Parameter Collision Detection** 
*Essential for constructor/method generation quick fixes*

```csharp
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

private void CollectExistingMemberNames(AppClassNode node)
{
    existingMemberNames.Clear();
    
    // Collect all method names
    foreach (var method in node.Methods)
    {
        existingMemberNames.Add(method.Name);
        foreach (var param in method.Parameters)
        {
            if (!string.IsNullOrEmpty(param.Name))
                existingMemberNames.Add(param.Name);
        }
    }
    
    // Collect property names
    foreach (var property in node.Properties)
    {
        existingMemberNames.Add(property.Name);
    }
}
```

### **Recursive Hierarchy Traversal Pattern**
*Critical for abstract member detection and implementation*

```csharp
// Proven pattern from UnimplementedAbstractMembersStyler
private void CollectAbstractMembers(string typePath, HashSet<string> implementedSignatures, 
    Dictionary<string, MethodNode> abstractMethods, Dictionary<string, PropertyNode> abstractProperties)
{
    try
    {
        var program = ParseClassAst(typePath);
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

        // Process properties - all interface properties are abstract
        if (properties != null)
        {
            foreach (var property in properties.Where(p => isInterface || p.IsAbstract))
            {
                string signature = $"P:{property.Name}";
                if (!implementedSignatures.Contains(signature))
                    abstractProperties.TryAdd(signature, property);
            }
        }

        // Add concrete implementations to prevent propagation (classes only)
        if (!isInterface && program.AppClass != null)
        {
            foreach (var method in program.AppClass.Methods.Where(m => !m.IsAbstract && !IsConstructor(m, program.AppClass.Name)))
                implementedSignatures.Add($"M:{method.Name}({method.Parameters.Count})");
            foreach (var property in program.AppClass.Properties.Where(p => !p.IsAbstract))
                implementedSignatures.Add($"P:{property.Name}");
        }

        // Recurse to parent
        string? parentPath = isInterface ? program.Interface?.BaseInterface?.TypeName : program.AppClass?.BaseClass?.TypeName;
        if (parentPath != null)
        {
            CollectAbstractMembers(parentPath, implementedSignatures, abstractMethods, abstractProperties);
        }
    }
    catch (Exception)
    {
        // Silently handle parsing errors
    }
}
```

---

## 🔧 **PROVEN CODE GENERATION PATTERNS**

### **PeopleCode Constructor Generation**
```csharp
private void GenerateConstructorImplementation(AppClassNode classNode, List<ParameterNode> baseParameters)
{
    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine($"method {classNode.Name}");
    
    // Add parameter annotations
    foreach (var param in baseParameters)
    {
        string safeName = GenerateSafeParameterName(param.Name ?? "param");
        sb.AppendLine($"/+ &{safeName} as {param.Type} +/");
    }
    
    sb.AppendLine("(");
    
    // Add parameters
    for (int i = 0; i < baseParameters.Count; i++)
    {
        string safeName = GenerateSafeParameterName(baseParameters[i].Name ?? "param");
        sb.Append($"   &{safeName} as {baseParameters[i].Type}");
        if (i < baseParameters.Count - 1)
            sb.AppendLine(",");
        else
            sb.AppendLine();
    }
    
    sb.AppendLine(")");
    sb.AppendLine();
    
    // Add %Super call with parameters
    if (baseParameters.Any())
    {
        sb.Append("   %Super = create ");
        sb.Append(classNode.BaseClass.TypeName);
        sb.Append("(");
        
        for (int i = 0; i < baseParameters.Count; i++)
        {
            string safeName = GenerateSafeParameterName(baseParameters[i].Name ?? "param");
            sb.Append($"&{safeName}");
            if (i < baseParameters.Count - 1)
                sb.Append(", ");
        }
        
        sb.AppendLine(");");
    }
    else
    {
        sb.AppendLine($"   %Super = create {classNode.BaseClass.TypeName}();");
    }
    
    sb.AppendLine();
    sb.AppendLine("end-method;");
    
    // Insert after class declaration
    InsertText((classNode.SourceSpan.End.ByteIndex, classNode.SourceSpan.End.ByteIndex), sb.ToString(), "Generate missing constructor");
}
```

### **Method Implementation with Override Annotations**
```csharp
private void GenerateMethodImplementationCode(MethodNode methodDeclaration)
{
    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine($"/+ Extends/implements {targetClass.BaseClass?.TypeName ?? targetClass.ImplementedInterface?.TypeName}.{methodDeclaration.Name} +/");
    sb.AppendLine($"method {methodDeclaration.Name}");
    
    // Add parameter annotations
    foreach (var param in methodDeclaration.Parameters)
    {
        sb.AppendLine($"/+ &{param.Name} as {param.Type} +/");
    }
    
    // Add parameters
    if (methodDeclaration.Parameters.Any())
    {
        sb.AppendLine("(");
        for (int i = 0; i < methodDeclaration.Parameters.Count; i++)
        {
            var param = methodDeclaration.Parameters[i];
            sb.Append($"   &{param.Name} as {param.Type}");
            if (i < methodDeclaration.Parameters.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
        sb.AppendLine(")");
    }
    else
    {
        sb.AppendLine("()");
    }
    
    // Add return type if present
    if (methodDeclaration.ReturnType != null)
    {
        sb.AppendLine($"   Returns {methodDeclaration.ReturnType}");
    }
    
    sb.AppendLine();
    sb.AppendLine("   /+ TODO: Implement method +/");
    
    // Add appropriate return statement
    if (methodDeclaration.ReturnType != null)
    {
        string defaultValue = GetDefaultValueForType(methodDeclaration.ReturnType.ToString());
        sb.AppendLine($"   Return {defaultValue};");
    }
    
    sb.AppendLine();
    sb.AppendLine("end-method;");
    
    // Insert at end of class
    InsertText((targetClass.SourceSpan.End.ByteIndex, targetClass.SourceSpan.End.ByteIndex), sb.ToString(), $"Implement {methodDeclaration.Name} method");
}
```

---

## 🚨 **CRITICAL QUICK FIX IMPLEMENTATION LESSONS**

### **Essential Patterns That Work:**
1. **✅ Identical Detection Logic**: Quick fix MUST match styler's detection exactly
2. **✅ Cursor-Aware Targeting**: Use `CurrentPosition` and `SourceSpan.ContainsPosition()` for precision
3. **✅ Parameter Collision Prevention**: Always check existing member names before generating new ones
4. **✅ Recursive Hierarchy Analysis**: Use same traversal logic as corresponding stylers
5. **✅ Database Integration**: Self-hosted parser works seamlessly with `DataManager.GetAppClassSourceByPath()`

### **Critical Errors to Avoid:**
1. **❌ Different Detection Logic**: Quick fix detects different issues than styler
2. **❌ SourceSpan Format Errors**: Must convert SourceSpan to (ByteIndex, ByteIndex) for AddIndicator
3. **❌ Parameter Name Collisions**: Not checking existing names leads to invalid code
4. **❌ Shallow Analysis**: Missing parent class analysis breaks abstract member detection
5. **❌ Wrong Base Class Choice**: ScopedRefactor unnecessary for targeted quick fixes

### **Build Integration Success Factors:**
- **Type Registration**: Ensure quick fix types are properly registered with stylers
- **Namespace Consistency**: All quick fixes in `AppRefiner.Refactors.QuickFixes` namespace
- **File Naming**: Match class name exactly (e.g., `GenerateBaseConstructor.cs`)
- **Compilation Testing**: Always run `dotnet build` after implementation

---

*Last Updated: 2025-01-26 - Based on successful porting of 3 production quick fixes with proven integration patterns*