# Declaration Nodes

Declaration nodes represent named entities in PeopleCode programs, including methods, properties, variables, constants, and functions. All declaration nodes inherit from `DeclarationNode` and share common properties for names, visibility, and syntax elements.

## DeclarationNode (Abstract Base)

**Inherits:** `AstNode`

Base class for all declaration nodes providing common naming and visibility functionality.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Name of the declared item |
| `NameToken` | `Token` | Token representing the name in source |
| `HasSemicolon` | `bool` | True if declaration had a semicolon in source |
| `Visibility` | `VisibilityModifier` | Visibility level (Public, Protected, Private) |

### Enumerations

**`VisibilityModifier`**
- `Public` - Accessible from anywhere
- `Protected` - Accessible within class hierarchy  
- `Private` - Accessible only within defining class

---

## MethodNode

**Inherits:** `DeclarationNode`

Represents a method declaration and/or implementation in a class or interface.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `HeaderSpan` | `SourceSpan` | Source span for the method header (separate from body) |
| `Parameters` | `List<ParameterNode>` | Method parameters |
| `ParameterAnnotations` | `List<ParameterNode>` | Parameter annotations from method implementation |
| `ReturnType` | `TypeNode?` | Return type (null for constructors and procedures) |
| `Implementation` | `MethodImplNode?` | Method implementation (null for declarations only) |
| `Body` | `BlockNode?` | Method body (compatibility property - returns Implementation?.Body) |
| `IsAbstract` | `bool` | True if this method is abstract |
| `IsConstructor` | `bool` | True if this is a constructor |
| `IsImplementation` | `bool` | True if this method has an implementation |
| `IsDeclaration` | `bool` | True if this is a declaration only (no implementation) |
| `ClassName` | `string?` | Class this method belongs to (for implementations outside class) |
| `Documentation` | `string?` | Documentation string (from DOC annotation) |
| `ImplementedInterfaces` | `List<TypeNode>` | Interfaces implemented by this method |
| `ImplementedMethodName` | `string?` | Name of implemented interface method |

### Methods

**`AddParameter(ParameterNode parameter)`**
- Adds a parameter to the method signature

**`SetReturnType(TypeNode returnType)`**
- Sets the method return type
- Removes old return type if present

**`SetImplementation(MethodImplNode implementation)`**
- Sets the method implementation
- Establishes bidirectional reference between declaration and implementation

**`SetBody(BlockNode body)`** *(Compatibility method)*
- Creates a MethodImplNode with the provided body
- Use `SetImplementation()` for new code

**`AddImplementedInterface(TypeNode interfaceType)`**
- Adds an interface that this method implements

### Usage Examples

```csharp
// Create method declaration
var method = new MethodNode("CalculateTotal", nameToken);
method.SetReturnType(new BuiltInTypeNode(BuiltInType.Number));

// Add parameters
var param1 = new ParameterNode("amount", paramToken, numberType);
var param2 = new ParameterNode("taxRate", param2Token, numberType);
method.AddParameter(param1);
method.AddParameter(param2);

// Create implementation
var body = new BlockNode();
var returnStmt = new ReturnStatementNode(
    new BinaryOperationNode(
        new IdentifierNode("amount", IdentifierType.UserVariable),
        BinaryOperator.Multiply,
        false,
        new BinaryOperationNode(
            new LiteralNode(1, LiteralType.Integer),
            BinaryOperator.Add,
            false,
            new IdentifierNode("taxRate", IdentifierType.UserVariable)
        )
    )
);
body.AddStatement(returnStmt);

var implementation = new MethodImplNode("CalculateTotal", nameToken, body);
method.SetImplementation(implementation);
```

### Visitor Integration

```csharp
public override void VisitMethod(MethodNode node)
{
    Console.WriteLine($"Method: {node.Name}");
    
    // Visit return type
    if (node.ReturnType != null)
    {
        Console.Write($"  Returns: ");
        node.ReturnType.Accept(this);
    }
    
    // Visit parameters
    Console.WriteLine($"  Parameters: {node.Parameters.Count}");
    foreach (var param in node.Parameters)
    {
        Console.Write($"    {param.Name}: ");
        param.Type.Accept(this);
    }
    
    // Visit implementation
    if (node.IsImplementation)
    {
        Console.WriteLine("  Implementation:");
        node.Implementation?.Accept(this);
    }
}
```

---

## PropertyNode

**Inherits:** `DeclarationNode`

Represents a property declaration and/or implementation with get/set accessors.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `TypeNode` | Property type |
| `HasGet` | `bool` | True if property has a getter (default: true) |
| `HasSet` | `bool` | True if property has a setter (default: true) |
| `HasSetter` | `bool` | Alias for HasSet (compatibility) |
| `IsReadOnly` | `bool` | True if property is read-only |
| `IsAbstract` | `bool` | True if property is abstract |
| `ImplementedInterface` | `TypeNode?` | Interface that this property implements |
| `ImplementedPropertyName` | `string?` | Name of implemented interface property |
| `GetterImplementation` | `MethodImplNode?` | Property getter implementation |
| `SetterImplementation` | `MethodImplNode?` | Property setter implementation |
| `GetterBody` | `BlockNode?` | Property getter body (compatibility property) |
| `SetterBody` | `BlockNode?` | Property setter body (compatibility property) |
| `IsGetter` | `bool` | True if this is a getter implementation only |
| `IsSetter` | `bool` | True if this is a setter implementation only |
| `IsImplementation` | `bool` | True if this has getter or setter implementation |
| `ClassName` | `string?` | Class this property belongs to |

### Methods

**`SetGetterImplementation(MethodImplNode getterImplementation)`**
- Sets the property getter implementation

**`SetSetterImplementation(MethodImplNode setterImplementation)`**
- Sets the property setter implementation

### Usage Examples

```csharp
// Create read-write property declaration
var property = new PropertyNode("Name", nameToken, stringType);
property.HasGet = true;
property.HasSet = true;

// Create read-only property
var readOnlyProp = new PropertyNode("Id", idToken, intType);
readOnlyProp.HasGet = true;
readOnlyProp.HasSet = false;
readOnlyProp.IsReadOnly = true;

// Create property with implementation
var propWithImpl = new PropertyNode("Counter", counterToken, intType);

// Add getter implementation
var getterBody = new BlockNode();
getterBody.AddStatement(new ReturnStatementNode(
    new IdentifierNode("_counter", IdentifierType.UserVariable)
));
var getterImpl = new MethodImplNode("get_Counter", getToken, getterBody);
propWithImpl.SetGetterImplementation(getterImpl);

// Add setter implementation  
var setterBody = new BlockNode();
setterBody.AddStatement(new ExpressionStatementNode(
    new AssignmentNode(
        new IdentifierNode("_counter", IdentifierType.UserVariable),
        AssignmentOperator.Assign,
        new IdentifierNode("value", IdentifierType.UserVariable)
    )
));
var setterImpl = new MethodImplNode("set_Counter", setToken, setterBody);
propWithImpl.SetSetterImplementation(setterImpl);
```

### Visitor Integration

```csharp
public override void VisitProperty(PropertyNode node)
{
    Console.WriteLine($"Property: {node.Name}");
    
    // Visit property type
    Console.Write($"  Type: ");
    node.Type.Accept(this);
    
    // Show access pattern
    var access = (node.HasGet, node.HasSet) switch
    {
        (true, true) => "get set",
        (true, false) => "get (read-only)",
        (false, true) => "set (write-only)",
        (false, false) => "no accessors"
    };
    Console.WriteLine($"  Access: {access}");
    
    // Visit implementations
    if (node.GetterBody != null)
    {
        Console.WriteLine("  Getter implementation:");
        node.GetterBody.Accept(this);
    }
    
    if (node.SetterBody != null)
    {
        Console.WriteLine("  Setter implementation:");
        node.SetterBody.Accept(this);
    }
}
```

---

## VariableNode

**Inherits:** `DeclarationNode`

Represents a variable declaration with type, scope, and optional initialization.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `TypeNode` | Variable type |
| `Scope` | `VariableScope` | Variable scope (Local, Global, Component, Instance) |
| `InitialValue` | `ExpressionNode?` | Initial value expression (optional) |
| `AdditionalNames` | `List<string>` | Additional variable names (for multi-variable declarations) |
| `NameInfos` | `List<VariableNameInfo>` | Variable name information including tokens |
| `AllNames` | `IEnumerable<string>` | All variable names (main name + additional names) |

### Enumerations

**`VariableScope`**
- `Local` - Local to method or block
- `Global` - Global across the application
- `Component` - Component-level scope
- `Instance` - Instance variable in class

### Methods

**`SetInitialValue(ExpressionNode initialValue)`**
- Sets the variable initialization expression

**`AddName(string name)`**
- Adds an additional variable name for multi-variable declarations

### Usage Examples

```csharp
// Simple variable declaration
var simpleVar = new VariableNode("counter", nameToken, intType, VariableScope.Local);

// Variable with initialization
var initializedVar = new VariableNode("message", msgToken, stringType, VariableScope.Global);
initializedVar.SetInitialValue(new LiteralNode("Hello World", LiteralType.String));

// Multi-variable declaration: LOCAL string &name1, &name2, &name3;
var multiVar = new VariableNode("name1", firstToken, stringType, VariableScope.Local);
multiVar.AddName("name2");
multiVar.AddName("name3");

// Instance variable in class
var instanceVar = new VariableNode("data", dataToken, stringType, VariableScope.Instance);
appClass.AddMember(instanceVar, VisibilityModifier.Private);
```

### Visitor Integration

```csharp
public override void VisitVariable(VariableNode node)
{
    Console.WriteLine($"Variable: {node.Scope} {node.Type} {string.Join(", ", node.AllNames)}");
    
    // Visit type
    node.Type.Accept(this);
    
    // Visit initialization if present
    if (node.InitialValue != null)
    {
        Console.Write($"  Initial value: ");
        node.InitialValue.Accept(this);
    }
    
    // Show additional names
    if (node.AdditionalNames.Any())
    {
        Console.WriteLine($"  Additional names: {string.Join(", ", node.AdditionalNames)}");
    }
}
```

---

## ConstantNode

**Inherits:** `DeclarationNode`

Represents a named constant declaration with its value.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `ExpressionNode` | Constant value expression |

### Usage Examples

```csharp
// Numeric constant
var numConstant = new ConstantNode("MAX_SIZE", nameToken, 
    new LiteralNode(100, LiteralType.Integer));

// String constant
var strConstant = new ConstantNode("APP_NAME", appToken,
    new LiteralNode("My Application", LiteralType.String));

// Expression constant
var exprConstant = new ConstantNode("TAX_MULTIPLIER", taxToken,
    new BinaryOperationNode(
        new LiteralNode(1, LiteralType.Integer),
        BinaryOperator.Add,
        false,
        new LiteralNode(0.0825, LiteralType.Decimal)
    ));

program.AddConstant(numConstant);
```

### Visitor Integration

```csharp
public override void VisitConstant(ConstantNode node)
{
    Console.WriteLine($"Constant: &{node.Name}");
    Console.Write($"  Value: ");
    node.Value.Accept(this);
}
```

---

## FunctionNode

**Inherits:** `DeclarationNode`

Represents a function declaration or implementation.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Parameters` | `List<ParameterNode>` | Function parameters |
| `ReturnType` | `TypeNode?` | Return type (null for procedures) |
| `Body` | `BlockNode?` | Function body (null for declarations) |
| `FunctionType` | `FunctionType` | Function type (PeopleCode, Library, or UserDefined) |
| `Documentation` | `string?` | Documentation string (from DOC annotation) |
| `IsImplementation` | `bool` | True if this function has a body |
| `IsDeclaration` | `bool` | True if this is a declaration only |
| `RecordName` | `string?` | Record name (for PeopleCode function declarations) |
| `FieldName` | `string?` | Field name (for PeopleCode function declarations) |
| `RecordEvent` | `string?` | Record event (for PeopleCode function declarations) |
| `LibraryName` | `string?` | Library name (for DLL function declarations) |
| `AliasName` | `string?` | Alias name (for DLL function declarations) |

### Enumerations

**`FunctionType`**
- `UserDefined` - User-defined function
- `PeopleCode` - PeopleCode function declaration
- `Library` - External library (DLL) function

### Methods

**`AddParameter(ParameterNode parameter)`**
- Adds a parameter to the function signature

**`SetReturnType(TypeNode returnType)`**
- Sets the function return type

**`SetBody(BlockNode body)`**
- Sets the function implementation body

### Usage Examples

```csharp
// User-defined function
var userFunc = new FunctionNode("CalculateInterest", nameToken, FunctionType.UserDefined);
userFunc.SetReturnType(new BuiltInTypeNode(BuiltInType.Number));
userFunc.AddParameter(new ParameterNode("principal", p1Token, numberType));
userFunc.AddParameter(new ParameterNode("rate", p2Token, numberType));

// Function with body
var body = new BlockNode();
body.AddStatement(new ReturnStatementNode(
    new BinaryOperationNode(
        new IdentifierNode("principal", IdentifierType.UserVariable),
        BinaryOperator.Multiply,
        false,
        new IdentifierNode("rate", IdentifierType.UserVariable)
    )
));
userFunc.SetBody(body);

// PeopleCode function declaration
var pcodeFunc = new FunctionNode("FieldDefault", fieldToken, FunctionType.PeopleCode);
pcodeFunc.RecordName = "EMPLOYEE";
pcodeFunc.FieldName = "SALARY";
pcodeFunc.RecordEvent = "FieldDefault";

// Library function declaration
var libFunc = new FunctionNode("MessageBox", msgToken, FunctionType.Library);
libFunc.LibraryName = "USER32";
libFunc.AliasName = "MessageBoxA";
libFunc.AddParameter(new ParameterNode("hWnd", hToken, intType));
```

### Visitor Integration

```csharp
public override void VisitFunction(FunctionNode node)
{
    Console.WriteLine($"Function: {node.Name} ({node.FunctionType})");
    
    // Visit return type
    if (node.ReturnType != null)
    {
        Console.Write($"  Returns: ");
        node.ReturnType.Accept(this);
    }
    
    // Visit parameters
    foreach (var param in node.Parameters)
    {
        Console.Write($"  Parameter {param.Name}: ");
        param.Type.Accept(this);
    }
    
    // Show function-specific details
    switch (node.FunctionType)
    {
        case FunctionType.PeopleCode:
            Console.WriteLine($"  PeopleCode: {node.RecordName}.{node.FieldName} {node.RecordEvent}");
            break;
        case FunctionType.Library:
            Console.WriteLine($"  Library: {node.LibraryName} (alias: {node.AliasName})");
            break;
    }
    
    // Visit implementation
    if (node.IsImplementation)
    {
        Console.WriteLine("  Implementation:");
        node.Body?.Accept(this);
    }
}
```

---

## ParameterNode

**Inherits:** `AstNode` *(Note: Does not inherit from DeclarationNode)*

Represents a method or function parameter.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Parameter name |
| `NameToken` | `Token` | Token representing the parameter name |
| `Type` | `TypeNode` | Parameter type |
| `IsOut` | `bool` | True if parameter is passed by reference (OUT parameter) |
| `Mode` | `ParameterMode` | Parameter passing mode (for DLL functions) |

### Enumerations

**`ParameterMode`**
- `Value` - Pass by value (default)
- `Reference` - Pass by reference

### Usage Examples

```csharp
// Simple parameter
var param = new ParameterNode("name", nameToken, stringType);

// OUT parameter
var outParam = new ParameterNode("result", resultToken, intType);
outParam.IsOut = true;

// Reference parameter (for DLL functions)
var refParam = new ParameterNode("buffer", bufToken, stringType);
refParam.Mode = ParameterMode.Reference;

// Add to method
method.AddParameter(param);
method.AddParameter(outParam);
```

### Common Analysis Patterns

```csharp
// Find all methods with specific parameter patterns
public class ParameterAnalyzer : AstVisitorBase
{
    public List<MethodNode> MethodsWithOutParams { get; } = new();
    
    public override void VisitMethod(MethodNode node)
    {
        if (node.Parameters.Any(p => p.IsOut))
        {
            MethodsWithOutParams.Add(node);
        }
        base.VisitMethod(node);
    }
}

// Count parameters by type
public Dictionary<string, int> CountParameterTypes(ProgramNode program)
{
    var counts = new Dictionary<string, int>();
    var visitor = new ParameterTypeCounter(counts);
    program.Accept(visitor);
    return counts;
}
```