# Program Structure Nodes

Program structure nodes represent the top-level constructs that organize PeopleCode programs. These nodes define the overall program structure including imports, classes, interfaces, and the main program.

## ProgramNode

**Inherits:** `AstNode`

The root node representing a complete PeopleCode program. Every parsed source file results in a `ProgramNode` at the root of the AST.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Imports` | `List<ImportNode>` | Import declarations at the top of the program |
| `AppClass` | `AppClassNode?` | Application class definition (if this is a class program) |
| `Interface` | `InterfaceNode?` | Interface definition (if this is an interface program) |
| `Functions` | `List<FunctionNode>` | Function declarations and implementations |
| `Variables` | `List<VariableNode>` | Global and component variable declarations |
| `Constants` | `List<ConstantNode>` | Constant declarations |
| `MainBlock` | `BlockNode?` | Main program statements (for non-class programs) |
| `Comments` | `List<Token>` | All comments found in the program |
| `IsClassProgram` | `bool` | True if this program defines an application class |
| `IsInterfaceProgram` | `bool` | True if this program defines an interface |
| `SkippedDirectiveSpans` | `List<SourceSpan>` | Source regions excluded by preprocessor directives |

### Methods

**`AddImport(ImportNode import)`**
- Adds an import declaration to the program
- Automatically establishes parent-child relationship

**`AddFunction(FunctionNode function)`**
- Adds a function declaration or implementation
- Functions are sorted by declaration vs implementation

**`AddVariable(VariableNode variable)`**
- Adds a global or component variable declaration

**`AddConstant(ConstantNode constant)`**
- Adds a constant declaration

**`SetAppClass(AppClassNode appClass)`**
- Sets the application class definition
- Throws exception if interface is already set

**`SetInterface(InterfaceNode interfaceNode)`**
- Sets the interface definition  
- Throws exception if app class is already set

**`SetMainBlock(BlockNode mainBlock)`**
- Sets the main program block (for non-class programs)

### Usage Examples

```csharp
// Create a simple program with variables and functions
var program = new ProgramNode();

// Add global variable
var globalVar = new VariableNode("gCounter", token, intType, VariableScope.Global);
program.AddVariable(globalVar);

// Add function declaration
var function = new FunctionNode("DoSomething", token, FunctionType.UserDefined);
program.AddFunction(function);

// Check program type
if (program.IsClassProgram)
{
    Console.WriteLine($"Class program: {program.AppClass.Name}");
}
else
{
    Console.WriteLine($"Regular program with {program.Functions.Count} functions");
}
```

### Visitor Integration

```csharp
public override void VisitProgram(ProgramNode node)
{
    // Visit imports first
    foreach (var import in node.Imports)
        import.Accept(this);
        
    // Visit global variables
    foreach (var variable in node.Variables) 
        variable.Accept(this);
        
    // Visit class or interface
    node.AppClass?.Accept(this);
    node.Interface?.Accept(this);
    
    // Visit functions
    foreach (var function in node.Functions)
        function.Accept(this);
        
    // Visit main block
    node.MainBlock?.Accept(this);
}
```

---

## ImportNode

**Inherits:** `AstNode`

Represents an import declaration that brings external classes or packages into scope.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `PackagePath` | `IReadOnlyList<string>` | Package path components (e.g., ["MyPackage", "Utilities"]) |
| `ClassName` | `string?` | Specific class name being imported, or null for wildcard imports |
| `IsWildcard` | `bool` | True if this is a wildcard import (package:*) |
| `FullPath` | `string` | Full import path as it appears in source |
| `ImportedType` | `TypeNode` | The imported type node (class or wildcard) |

### Constructors

**`ImportNode(IEnumerable<string> packagePath, string? className = null)`**
- Creates import from package path components
- ClassName null indicates wildcard import

**`ImportNode(string fullPath)`**
- Creates import from full path string
- Parses path to extract components

### Usage Examples

```csharp
// Wildcard import: MyPackage:Utilities:*
var wildcardImport = new ImportNode(new[] { "MyPackage", "Utilities" });
Console.WriteLine(wildcardImport.IsWildcard); // True
Console.WriteLine(wildcardImport.FullPath); // "MyPackage:Utilities:*"

// Specific class import: MyPackage:Utilities:Helper
var classImport = new ImportNode("MyPackage:Utilities:Helper");  
Console.WriteLine(classImport.ClassName); // "Helper"
Console.WriteLine(classImport.IsWildcard); // False

// Add to program
program.AddImport(wildcardImport);
program.AddImport(classImport);
```

### Visitor Integration

```csharp
public override void VisitImport(ImportNode node)
{
    // Visit the imported type node
    node.ImportedType.Accept(this);
    
    // Analyze import
    Console.WriteLine($"Importing: {node.FullPath}");
    if (node.IsWildcard)
        Console.WriteLine("  Wildcard import");
    else
        Console.WriteLine($"  Specific class: {node.ClassName}");
}
```

---

## AppClassNode

**Inherits:** `AstNode`

Represents an application class definition with its members and inheritance relationships.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Class name |
| `NameToken` | `Token` | Token for the class name |
| `ProtectedToken` | `Token?` | Token for 'protected' keyword, if present |
| `PrivateToken` | `Token?` | Token for 'private' keyword, if present |
| `BaseClass` | `TypeNode?` | Base class type (for EXTENDS clause) |
| `ImplementedInterface` | `TypeNode?` | Implemented interface type (for IMPLEMENTS clause) |
| `Methods` | `List<MethodNode>` | Method declarations in the class header |
| `Properties` | `List<PropertyNode>` | Property declarations |
| `InstanceVariables` | `List<VariableNode>` | Instance variable declarations |
| `Constants` | `List<ConstantNode>` | Constant declarations |
| `MethodImplementations` | `List<MethodNode>` | Method implementations (outside class declaration) |
| `PropertyGetters` | `List<PropertyNode>` | Property getter implementations |
| `PropertySetters` | `List<PropertyNode>` | Property setter implementations |
| `VisibilitySections` | `Dictionary<VisibilityModifier, List<AstNode>>` | Members organized by visibility |

### Methods

**`SetBaseClass(TypeNode baseClass)`**
- Sets the base class for inheritance
- Removes old base class if present

**`SetImplementedInterface(TypeNode implementedInterface)`**
- Sets the implemented interface
- Removes old interface if present

**`AddMember(AstNode member, VisibilityModifier visibility = VisibilityModifier.Public)`**
- Adds a member to the appropriate collection based on type
- Organizes members by visibility level

### Usage Examples

```csharp
// Create class with inheritance
var appClass = new AppClassNode("MyClass", nameToken);
appClass.SetBaseClass(new AppClassTypeNode("BasePackage:BaseClass"));
appClass.SetImplementedInterface(new AppClassTypeNode("Interfaces:IMyInterface"));

// Add instance variable
var instanceVar = new VariableNode("counter", varToken, intType, VariableScope.Instance);
appClass.AddMember(instanceVar, VisibilityModifier.Private);

// Add method declaration  
var method = new MethodNode("DoWork", methodToken);
method.SetReturnType(new BuiltInTypeNode(BuiltInType.Boolean));
appClass.AddMember(method, VisibilityModifier.Public);

// Add to program
program.SetAppClass(appClass);
```

### Visitor Integration

```csharp
public override void VisitAppClass(AppClassNode node)
{
    Console.WriteLine($"Class: {node.Name}");
    
    // Visit inheritance relationships
    node.BaseClass?.Accept(this);
    node.ImplementedInterface?.Accept(this);
    
    // Visit members by visibility
    foreach (var section in node.VisibilitySections)
    {
        Console.WriteLine($"  {section.Key} members:");
        foreach (var member in section.Value)
        {
            member.Accept(this);
        }
    }
}
```

---

## InterfaceNode

**Inherits:** `AstNode`

Represents an interface definition with method and property signatures.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Interface name |
| `BaseInterface` | `TypeNode?` | Base interface type (for EXTENDS clause) |
| `Methods` | `List<MethodNode>` | Method signatures in the interface |
| `Properties` | `List<PropertyNode>` | Property signatures in the interface |

### Methods

**`SetBaseInterface(TypeNode baseInterface)`**
- Sets the base interface for inheritance
- Removes old base interface if present

**`AddMethod(MethodNode method)`**
- Adds a method signature to the interface

**`AddProperty(PropertyNode property)`**
- Adds a property signature to the interface

### Usage Examples

```csharp
// Create interface with base interface
var interface = new InterfaceNode("IWorker");
interface.SetBaseInterface(new AppClassTypeNode("Base:IEntity"));

// Add method signature
var methodSig = new MethodNode("ProcessData", token);
methodSig.SetReturnType(new BuiltInTypeNode(BuiltInType.Boolean));
methodSig.AddParameter(new ParameterNode("data", paramToken, stringType));
interface.AddMethod(methodSig);

// Add property signature
var propSig = new PropertyNode("Status", propToken, stringType);
propSig.HasGet = true;
propSig.HasSet = false; // Read-only property
interface.AddProperty(propSig);

// Add to program
program.SetInterface(interface);
```

### Visitor Integration

```csharp
public override void VisitInterface(InterfaceNode node)
{
    Console.WriteLine($"Interface: {node.Name}");
    
    // Visit base interface
    node.BaseInterface?.Accept(this);
    
    // Visit method signatures
    foreach (var method in node.Methods)
    {
        Console.WriteLine($"  Method: {method.Name}");
        method.Accept(this);
    }
    
    // Visit property signatures  
    foreach (var property in node.Properties)
    {
        Console.WriteLine($"  Property: {property.Name}");
        property.Accept(this);
    }
}
```

## Common Patterns

### Program Type Detection

```csharp
public ProgramType GetProgramType(ProgramNode program)
{
    if (program.IsClassProgram)
        return ProgramType.Class;
    if (program.IsInterfaceProgram) 
        return ProgramType.Interface;
    if (program.Functions.Any(f => f.FunctionType == FunctionType.PeopleCode))
        return ProgramType.PeopleCodeFunction;
    return ProgramType.Application;
}
```

### Collecting All Declarations

```csharp
public List<DeclarationNode> GetAllDeclarations(ProgramNode program)
{
    var declarations = new List<DeclarationNode>();
    
    declarations.AddRange(program.Variables);
    declarations.AddRange(program.Constants);
    declarations.AddRange(program.Functions);
    
    if (program.AppClass != null)
    {
        declarations.AddRange(program.AppClass.Methods);
        declarations.AddRange(program.AppClass.Properties);
        declarations.AddRange(program.AppClass.InstanceVariables);
        declarations.AddRange(program.AppClass.Constants);
    }
    
    return declarations;
}
```

### Import Analysis

```csharp
public class ImportAnalyzer : AstVisitorBase
{
    public HashSet<string> WildcardPackages { get; } = new();
    public HashSet<string> SpecificClasses { get; } = new();
    
    public override void VisitImport(ImportNode node)
    {
        if (node.IsWildcard)
            WildcardPackages.Add(string.Join(":", node.PackagePath));
        else
            SpecificClasses.Add(node.FullPath);
    }
}
```