# Enhanced Scoped AST Visitor System

## Overview

The Enhanced Scoped AST Visitor system provides comprehensive variable tracking, scope management, and reference collection for robust PeopleCode analysis and refactoring. This is a complete rewrite of the original `ScopedAstVisitor` with significantly enhanced capabilities.

## Key Features

### ✅ Complete Variable Tracking
- **Every variable** in the program with **all references**
- **Variable classification**: Local, Instance, Global, Component, Parameter, Constant, Property
- **Safety classification**: Identifies which variables are safe to refactor
- **Reference tracking**: Declaration, Read, Write, Parameter Annotation references

### ✅ Hierarchical Scope Management
- **Full parent-child relationships** with proper lifecycle management  
- **Scope types**: Global, Class, Method, Function, Property
- **Accessibility rules**: Determine which variables are accessible from any scope
- **Proper lifecycle**: OnExit called BEFORE scope pop (critical for GetCurrentScope())

### ✅ Comprehensive Query API
- `GetAccessibleVariables(scope)` - All variables accessible from a scope
- `GetVariableReferences(variableName, scope)` - All references to a variable
- `IsVariableSafeToRefactor(variableName, scope)` - Safety check for refactoring
- `GetUnusedVariables()` - Find all unused variables
- Complete scope hierarchy access

### ✅ Event-Driven Architecture
- **OnEnter/OnExit** for each scope type with proper parameters
- **OnVariableDeclared/OnVariableReferenced** events
- **Custom scope data** management per scope

## Core Components

### 1. ScopeContext
Rich scope information with hierarchical relationships:
```csharp
public class ScopeContext
{
    public EnhancedScopeType Type { get; }        // Global, Class, Method, Function, Property
    public string Name { get; }                   // Scope name
    public ScopeContext? Parent { get; }          // Parent scope
    public List<ScopeContext> Children { get; }   // Child scopes
    public AstNode SourceNode { get; }            // AST node that created this scope
    public string FullQualifiedName { get; }      // "Global.MyClass.MyMethod"
    
    // Query methods
    public IEnumerable<ScopeContext> GetScopeChain();     // This scope + all ancestors
    public bool CanAccessScope(ScopeContext targetScope); // PeopleCode scoping rules
    public ScopeContext? GetClassScope();                 // Find containing class scope
}
```

### 2. EnhancedVariableInfo  
Comprehensive variable information with reference tracking:
```csharp
public class EnhancedVariableInfo
{
    public string Name { get; }                           // Variable name
    public VariableKind Kind { get; }                     // Local, Instance, Global, etc.
    public ScopeContext DeclarationScope { get; }         // Where variable was declared
    public List<VariableReference> References { get; }    // All references to this variable
    public bool IsSafeToRefactor { get; }                 // Safe to rename within program
    public bool IsUsed { get; }                           // Has any references
    
    // Query methods  
    public IEnumerable<VariableReference> GetReadReferences();
    public IEnumerable<VariableReference> GetWriteReferences();
    public IEnumerable<VariableReference> GetParameterAnnotationReferences();
}
```

### 3. VariableReference
Individual variable usage with precise location tracking:
```csharp
public class VariableReference
{
    public string VariableName { get; }        // Name of referenced variable
    public ReferenceType ReferenceType { get; } // Declaration, Read, Write, ParameterAnnotation
    public SourceSpan SourceSpan { get; }       // Precise source location
    public ScopeContext Scope { get; }          // Scope where reference occurs
    public string? Context { get; }             // Additional context info
    
    // Factory methods for creating different reference types
    public static VariableReference CreateDeclaration(...);
    public static VariableReference CreateRead(...);
    public static VariableReference CreateWrite(...);
    public static VariableReference CreateParameterAnnotation(...);
}
```

### 4. VariableRegistry
Centralized registry for all variables and scopes:
```csharp
public class VariableRegistry  
{
    // Core queries
    public IEnumerable<EnhancedVariableInfo> GetAccessibleVariables(ScopeContext scope);
    public EnhancedVariableInfo? FindVariableInScope(string name, ScopeContext scope);
    public IEnumerable<EnhancedVariableInfo> GetUnusedVariables();
    public IEnumerable<EnhancedVariableInfo> GetSafeToRefactorVariables();
    
    // Scope queries
    public ScopeContext? GetGlobalScope();
    public IEnumerable<ScopeContext> GetScopesByType(EnhancedScopeType type);
    
    // Statistics and debugging
    public VariableRegistryStatistics GetStatistics();
    public string GetDebugInfo();
}
```

## Usage Guide

### Creating a Custom Visitor

```csharp
public class MyAnalysisVisitor : EnhancedScopedAstVisitor<string>
{
    protected override void OnEnterGlobalScope(ScopeContext scope, ProgramNode node)
    {
        // Called when entering global scope
        AddToCurrentScope("start_time", DateTime.Now.ToString());
    }
    
    protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, string> customData)
    {
        // Called BEFORE scope is popped - GetCurrentScope() works correctly here!
        var currentScope = GetCurrentScope(); // Returns global scope
        
        // Analyze all variables and scopes
        var allVariables = GetAllVariables();
        var unusedVars = GetUnusedVariables();
        
        // Your analysis logic here...
    }
    
    protected override void OnEnterMethodScope(ScopeContext scope, MethodNode node)
    {
        // Method entered - parameters automatically registered
        Console.WriteLine($"Entering method {node.Name} with {node.Parameters.Count} parameters");
    }
    
    protected override void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, string> customData)
    {
        // Analyze method variables
        var methodVars = GetVariablesInScope(scope);
        var localVars = methodVars.Where(v => v.Kind == VariableKind.Local);
        var parameters = methodVars.Where(v => v.Kind == VariableKind.Parameter);
    }
    
    protected override void OnVariableDeclared(EnhancedVariableInfo variable)
    {
        // Called whenever a variable is declared anywhere
        Console.WriteLine($"Variable declared: {variable.Name} ({variable.Kind}) - Safe to refactor: {variable.IsSafeToRefactor}");
    }
    
    protected override void OnVariableReferenced(string variableName, VariableReference reference)
    {
        // Called whenever a variable is referenced
        if (reference.ReferenceType == ReferenceType.ParameterAnnotation)
        {
            Console.WriteLine($"Parameter annotation: {variableName} at {reference.Line}:{reference.Column}");
        }
    }
}
```

### Using the Visitor

```csharp
// Create your visitor
var visitor = new MyAnalysisVisitor();

// Visit the AST (assuming you have a ProgramNode)
programNode.Accept(visitor);

// After visiting, access complete analysis results
var allScopes = visitor.GetAllScopes();
var allVariables = visitor.GetAllVariables();

// Query specific information
var globalScope = visitor.VariableRegistry.GetGlobalScope();
var unusedVariables = visitor.GetUnusedVariables();
var safeToRefactor = visitor.GetAllVariables().Where(v => v.IsSafeToRefactor);

// Get variables accessible from a scope (MOST COMMON - includes parent scopes)
var methodScope = allScopes.First(s => s.Type == EnhancedScopeType.Method);
var accessibleVars = visitor.GetVariablesInScope(methodScope); // Instance vars, properties, globals, etc.

// Get only variables declared directly in a specific scope (LESS COMMON)
var declaredVars = visitor.GetVariablesDeclaredInScope(methodScope); // Only locals and parameters

// Find all references to a specific variable
var variable = visitor.VariableRegistry.FindVariableInScope("myVar", methodScope);
if (variable != null)
{
    foreach (var reference in variable.References)
    {
        Console.WriteLine($"Reference at {reference.Line}:{reference.Column} ({reference.ReferenceType})");
    }
}
```

## Variable Scope Queries

### Understanding GetVariablesInScope() vs GetVariablesDeclaredInScope()

There are two methods for getting variables related to a scope, and it's important to understand the difference:

#### `GetVariablesInScope(scope)` - Variables Accessible From Scope (MOST COMMON)
Returns all variables that can be accessed/used from the specified scope, following PeopleCode scoping rules:

```csharp
// For a method scope, this includes:
// - Method parameters and local variables (declared in method)  
// - Instance variables and properties (declared in class)
// - Global variables and constants (declared at program level)

var methodScope = GetMethodScope();
var accessibleVars = visitor.GetVariablesInScope(methodScope);

foreach (var variable in accessibleVars)
{
    Console.WriteLine($"{variable.Kind} {variable.Name} from {variable.DeclarationScope.Name}");
}
// Output might be:
// Parameter quacker from Method A
// Local y from Method A  
// Instance f from Class A
// Property ABC from Class A
// Global myGlobal from Global
```

#### `GetVariablesDeclaredInScope(scope)` - Variables Declared In Specific Scope (LESS COMMON)
Returns only variables declared directly in that specific scope:

```csharp
// For a method scope, this includes ONLY:
// - Method parameters and local variables

var methodScope = GetMethodScope();  
var declaredVars = visitor.GetVariablesDeclaredInScope(methodScope);

foreach (var variable in declaredVars)
{
    Console.WriteLine($"{variable.Kind} {variable.Name}");
}
// Output:
// Parameter quacker
// Local y
// (no instance variables, properties, or globals)
```

### When to Use Which Method

- **Use `GetVariablesInScope()`** when you need to know "what variables can this scope access/use?"
  - Variable analysis and IntelliSense
  - Checking for variable name conflicts
  - Understanding what's available to use in a scope

- **Use `GetVariablesDeclaredInScope()`** when you need to know "what variables are owned by this scope?"
  - Scope-specific statistics and reporting
  - Analyzing scope complexity
  - Finding variables that belong to a specific scope

### Example with Your Sample Program

```csharp
class A
   method A(&quacker As string);
   property string ABC;
private
   instance boolean &f;
end-class;

method A
   Local string &y;
   &f = True;      // Can access instance variable
   &ABC = "asdf";  // Can access property  
end-method;
```

For the method A scope:

```csharp
var methodScope = GetMethodAScope();

// GetVariablesInScope() returns 4 variables:
var accessible = visitor.GetVariablesInScope(methodScope);
// - &quacker (Parameter, from Method A scope)  
// - &y (Local, from Method A scope)
// - &f (Instance, from Class A scope)
// - ABC (Property, from Class A scope)

// GetVariablesDeclaredInScope() returns 2 variables:
var declared = visitor.GetVariablesDeclaredInScope(methodScope);
// - &quacker (Parameter, declared in Method A)
// - &y (Local, declared in Method A)
```

## Variable Safety Classification

The system classifies variables as **safe** or **unsafe** for refactoring:

### ✅ Safe to Refactor
- **Local** variables - Only accessible within method/function
- **Instance** variables - Only accessible within class (internal to program)  
- **Parameter** variables - Only accessible within method/function

### ❌ Unsafe to Refactor  
- **Global** variables - May be accessed from other programs
- **Component** variables - Shared across component sessions
- **Constant** variables - May be referenced externally

```csharp
// Check if variable is safe to rename
var variable = visitor.VariableRegistry.FindVariableInScope("myVar", currentScope);
if (variable?.IsSafeToRefactor == true)
{
    // Safe to rename - will only affect current program
    var allReferences = variable.References; // All locations to update
}
```

## Scope Hierarchy and Access Rules

The system follows PeopleCode scoping rules:

```
Global Scope
├── Class Scope (if class program)  
│   ├── Method Scope
│   │   └── Local variables, parameters
│   └── Property Scope
├── Function Scope  
│   └── Local variables, parameters
└── Global variables, constants
```

### Variable Accessibility:
- Variables in **current scope** are accessible
- Variables in **parent scopes** are accessible (Global → Class → Method)
- Variables in **sibling or child scopes** are NOT accessible

```csharp
// Check variable accessibility
var methodScope = GetCurrentScope();
var classScope = methodScope.Parent; 
var globalScope = classScope.Parent;

// Method can access its own variables + class variables + global variables  
var accessibleVars = visitor.GetAccessibleVariables(methodScope);

// Check if specific scope can access another scope's variables
bool canAccess = methodScope.CanAccessScope(globalScope); // true
bool canAccess2 = globalScope.CanAccessScope(methodScope); // false
```

## Critical Implementation Details

### ⚠️ Proper Scope Lifecycle

**CRITICAL**: OnExit methods are called **BEFORE** the scope is popped from the stack. This ensures `GetCurrentScope()` returns the correct scope during OnExit event handlers.

```csharp
protected void ExitScope()
{
    var exitingScope = scopeStack.Peek();
    
    // 1. FIRST: Call OnExit while scope is still current
    CallOnExitScopeMethod(exitingScope, ...); // GetCurrentScope() works correctly here
    
    // 2. THEN: Pop scope from stack  
    scopeStack.Pop();
    
    // 3. FINALLY: Update current scope reference
    currentScope = scopeStack.Count > 0 ? scopeStack.Peek() : null;
}
```

### GetCurrentScope() is Never Null

During AST traversal, `GetCurrentScope()` is guaranteed to return a valid scope (never null). There is always at least the global scope active during program analysis. The method will throw an exception if called before or after AST traversal:

```csharp
// During AST traversal - always works
var currentScope = visitor.GetCurrentScope(); // Never null

// Before/after traversal - throws exception  
var visitor = new MyVisitor();
var scope = visitor.GetCurrentScope(); // Throws InvalidOperationException
```

### Parameter Annotations as References

Method parameter annotations are tracked as references for renaming purposes, but they do **NOT** count as actual variable usage:

```csharp
// For a method like: method DoSomething(&param1 as string, &param2 as number)
// The &param1 and &param2 in annotations are tracked as ParameterAnnotation references
// but they don't count as "usage" for unused variable detection

protected override void OnVariableReferenced(string variableName, VariableReference reference)
{
    if (reference.ReferenceType == ReferenceType.ParameterAnnotation)
    {
        // This is a parameter annotation reference - important for renaming
        // but does NOT count as actual usage of the parameter
        Console.WriteLine($"Parameter annotation reference: {variableName}");
    }
}
```

### Variable Usage vs References

There's an important distinction between **references** and **usage**:

- **References**: All occurrences of a variable name (Declaration, Read, Write, ParameterAnnotation)
- **Usage**: Only meaningful code usage (Read and Write only)

```csharp
var variable = visitor.VariableRegistry.FindVariableInScope("myParam", scope);

// All references (for renaming)
var allReferences = variable.References; // Includes parameter annotations

// Only actual usage (for unused variable detection)  
var isUsed = variable.IsUsed; // Only Read/Write count, NOT parameter annotations
var usageCount = variable.UsageCount; // Only Read/Write references

// Parameter annotations are tracked but don't count as usage
var paramAnnotations = variable.GetParameterAnnotationReferences();
```

## Complete Example: Variable Renaming Analysis

```csharp
public class RenameAnalyzer : EnhancedScopedAstVisitor<object>
{
    public RenameAnalysis CanRenameVariable(string oldName, string newName, ScopeContext scope)
    {
        // Find the variable
        var variable = VariableRegistry.FindVariableInScope(oldName, scope);
        if (variable == null)
        {
            return new RenameAnalysis { CanRename = false, Reason = "Variable not found" };
        }
        
        // Check if it's safe to refactor
        if (!variable.IsSafeToRefactor)
        {
            return new RenameAnalysis 
            { 
                CanRename = false, 
                Reason = $"Variable is {variable.Kind} and may be accessed externally" 
            };
        }
        
        // Check for naming conflicts in accessible scopes
        var accessibleVars = GetAccessibleVariables(scope);
        var conflict = accessibleVars.FirstOrDefault(v => 
            v.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) && v != variable);
            
        if (conflict != null)
        {
            return new RenameAnalysis 
            { 
                CanRename = false, 
                Reason = $"Name conflict with {conflict.Kind} variable '{newName}' in {conflict.DeclarationScope.Name}" 
            };
        }
        
        // Safe to rename!
        return new RenameAnalysis
        {
            CanRename = true,
            Reason = "Variable is safe to rename",
            References = variable.References.ToList(),
            AffectedLocations = variable.References.Select(r => new Location(r.Line, r.Column)).ToList()
        };
    }
}
```

## Migration from Old ScopedAstVisitor

### Key Differences:
1. **EnhancedScopeType** instead of ScopeType (avoids naming conflict)
2. **EnhancedVariableInfo** instead of VariableInfo (comprehensive tracking)
3. **VariableRegistry** for centralized management
4. **OnEnter/OnExit events** with proper parameters and lifecycle
5. **Complete reference tracking** including parameter annotations
6. **Safety classification** for refactoring decisions

### Migration Steps:
1. Replace `ScopedAstVisitor<T>` with `EnhancedScopedAstVisitor<T>`
2. Update OnEnter/OnExit method signatures to include proper parameters
3. Use `VariableRegistry` for variable queries instead of manual tracking
4. Update variable safety checks to use `IsSafeToRefactor` property
5. Use the comprehensive query API for variable and scope analysis

The new system provides everything needed for robust variable analysis, scope-aware refactoring, and comprehensive PeopleCode program understanding.