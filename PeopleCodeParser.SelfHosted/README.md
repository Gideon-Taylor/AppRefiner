# PeopleCode Self-Hosted Parser

A comprehensive, self-hosted recursive descent parser for PeopleCode with advanced error recovery capabilities, designed to handle incomplete and malformed code during live editing scenarios.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Core Components](#core-components)
- [Key Design Decisions](#key-design-decisions)
- [Quick Start](#quick-start)
- [Usage Examples](#usage-examples)
- [Documentation](#documentation)
- [Contributing](#contributing)

## Architecture Overview

The PeopleCode Self-Hosted Parser implements a **multi-phase compilation pipeline** that transforms source code into a rich Abstract Syntax Tree (AST) through the following stages:

```
Source Code → Lexical Analysis → Directive Preprocessing → Parsing → AST Generation
```

### Design Philosophy

This parser is built with **live editing support** as a primary concern, meaning it can gracefully handle:
- Incomplete code during typing
- Syntax errors and malformed constructs  
- Partial parsing for incremental analysis
- Rich error diagnostics for development tools

The parser uses a **recursive descent** approach with **sophisticated error recovery** mechanisms to provide meaningful results even when the input contains syntax errors.

## Core Components

### 🔤 PeopleCodeLexer
**Tokenization with comprehensive language support**

- **Complete token coverage**: All PeopleCode keywords, operators, literals, and punctuation
- **Trivia support**: Preserves whitespace, comments, and formatting information
- **UTF-8 compatible**: Proper handling of international characters and encoding
- **Position tracking**: Line/column information for precise error reporting

```csharp
var lexer = new PeopleCodeLexer(sourceCode);
var tokens = lexer.TokenizeAll();
```

### 🔧 DirectivePreprocessor  
**Compiler directive resolution with version awareness**

- **Conditional compilation**: Handles `#If`, `#Then`, `#Else`, `#End-If` constructs
- **ToolsVersion evaluation**: Supports version-based conditional compilation
- **Token-level processing**: Directives can appear anywhere in the token stream
- **Span tracking**: Maintains information about skipped code sections

```csharp
var preprocessor = new DirectivePreprocessor(tokens, toolsVersion);
var processedTokens = preprocessor.ProcessDirectives();
```

### 🌳 PeopleCodeParser
**Recursive descent parser with advanced error recovery**

- **Robust error handling**: Sophisticated synchronization and recovery strategies
- **Context-aware parsing**: Maintains parsing context for better error messages
- **Flexible input handling**: Can parse complete programs, fragments, or expressions
- **Diagnostic generation**: Rich error and warning information with source locations

```csharp  
var parser = new PeopleCodeParser(tokens, toolsVersion);
var program = parser.ParseProgram();
var errors = parser.GetErrors();
```

### 🏗️ AST Node System
**56 specialized node types organized in 6 categories**

| Category | Count | Purpose |
|----------|-------|---------|
| **Program Structure** | 4 | Top-level constructs (Program, Class, Interface, Import) |
| **Declarations** | 7 | Named entities (Methods, Properties, Variables, Functions) |
| **Statements** | 18 | Control flow and executable constructs |
| **Expressions** | 16 | Values, operations, and computations |
| **Types** | 4 | Type references and specifications |
| **Base Classes** | 5 | Abstract foundations and utilities |

Each node provides:
- **Source tracking**: Precise location information via `FirstToken`/`LastToken`
- **Tree navigation**: Parent/child relationships with helper methods
- **Visitor support**: Integration with visitor pattern for analysis
- **Semantic hooks**: Extensible attributes for analysis passes

### 🚶 Visitor Framework
**Extensible traversal and analysis system**

- **Base visitors**: `AstVisitorBase` with sensible default traversal order
- **Generic visitors**: `IAstVisitor<TResult>` for computations and transformations
- **Scoped visitors**: `ScopedAstVisitor<T>` with variable tracking and scope management
- **Custom data**: Attach arbitrary data to scopes and contexts

```csharp
public class MyAnalyzer : AstVisitorBase
{
    public override void VisitMethod(MethodNode node)
    {
        // Analyze method declarations
        Console.WriteLine($"Found method: {node.Name}");
        base.VisitMethod(node);
    }
}
```

## Key Design Decisions

### Token-Based Source Tracking
Every AST node maintains references to its `FirstToken` and `LastToken`, allowing for precise source location tracking without storing redundant position data. The `SourceSpan` is calculated on-demand from these token boundaries.

**Benefits:**
- Memory efficient (no duplicate position storage)
- Always accurate (derived from actual token positions)  
- Detailed tracking (can access leading/trailing comments)

### Automatic Parent-Child Management
AST nodes automatically maintain bidirectional parent-child relationships through helper methods like `AddChild()` and `RemoveChild()`, ensuring tree consistency.

**Benefits:**
- Prevents orphaned nodes and broken references
- Enables tree navigation utilities (`FindAncestor<T>`, `FindDescendants<T>`)
- Simplifies AST construction and modification

### Comprehensive Visitor Pattern
The parser provides both void (`IAstVisitor`) and generic return value (`IAstVisitor<TResult>`) visitor interfaces, with rich base classes that handle common traversal patterns.

**Benefits:**
- Separation of concerns (traversal vs. analysis logic)
- Extensible analysis framework
- Consistent traversal order across different analyses

### Advanced Error Recovery
The parser uses **synchronization tokens** and **contextual recovery strategies** to continue parsing after encountering syntax errors, providing partial results and multiple diagnostics in a single pass.

**Benefits:**
- Better development experience (multiple errors reported at once)
- Partial parsing results for incomplete code
- Meaningful error messages with context

## Quick Start

### 1. Parse Complete Program

```csharp
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;

// Tokenize source code
var lexer = new PeopleCodeLexer(sourceCode);
var tokens = lexer.TokenizeAll();

// Parse into AST
var parser = new PeopleCodeParser(tokens);
var program = parser.ParseProgram();

// Check for errors
if (parser.GetErrors().Any())
{
    foreach (var error in parser.GetErrors())
    {
        Console.WriteLine($"{error.Severity}: {error.Message} at {error.Location}");
    }
}
```

### 2. Analyze AST with Custom Visitor

```csharp
public class MethodCountAnalyzer : AstVisitorBase
{
    public int MethodCount { get; private set; }
    
    public override void VisitMethod(MethodNode node)
    {
        MethodCount++;
        base.VisitMethod(node); // Continue traversal
    }
}

// Usage
var analyzer = new MethodCountAnalyzer();
program.Accept(analyzer);
Console.WriteLine($"Found {analyzer.MethodCount} methods");
```

### 3. Handle Parse Errors Gracefully

```csharp
var parser = new PeopleCodeParser(tokens);
var program = parser.ParseProgram();

// Parser always returns a program, even with errors
Console.WriteLine($"Parsed program with {program.Functions.Count} functions");

// Examine specific errors
foreach (var error in parser.GetErrors())
{
    switch (error.Severity)
    {
        case ParseErrorSeverity.Error:
            Console.WriteLine($"Syntax Error: {error.Message}");
            break;
        case ParseErrorSeverity.Warning:
            Console.WriteLine($"Warning: {error.Message}");
            break;
    }
}
```

## Usage Examples

### Finding All Variable Declarations

```csharp
public class VariableFinder : AstVisitorBase
{
    public List<VariableNode> Variables { get; } = new();
    
    public override void VisitVariable(VariableNode node)
    {
        Variables.Add(node);
        base.VisitVariable(node);
    }
}

var finder = new VariableFinder();
program.Accept(finder);

foreach (var variable in finder.Variables)
{
    Console.WriteLine($"{variable.Scope} {variable.Type} {variable.Name}");
}
```

### Analyzing Method Complexity

```csharp
public class CyclomaticComplexityCalculator : AstVisitorBase
{
    public int Complexity { get; private set; } = 1; // Base complexity
    
    public override void VisitIf(IfStatementNode node)
    {
        Complexity++; // Each decision point adds complexity
        base.VisitIf(node);
    }
    
    public override void VisitFor(ForStatementNode node)
    {
        Complexity++;
        base.VisitFor(node);
    }
    
    public override void VisitWhile(WhileStatementNode node)
    {
        Complexity++;
        base.VisitWhile(node);
    }
    
    // ... other control flow statements
}

// Calculate complexity for each method
public class MethodComplexityAnalyzer : AstVisitorBase
{
    public Dictionary<string, int> MethodComplexities { get; } = new();
    
    public override void VisitMethod(MethodNode node)
    {
        var calculator = new CyclomaticComplexityCalculator();
        if (node.Body != null)
        {
            node.Body.Accept(calculator);
            MethodComplexities[node.Name] = calculator.Complexity;
        }
        
        base.VisitMethod(node);
    }
}
```

### Building AST Programmatically

```csharp
// Create a simple method declaration
var returnType = new BuiltInTypeNode(BuiltInType.String);
var methodNode = new MethodNode("GetMessage", nameToken, returnType, VariableScope.Instance);

// Add parameters
var param = new ParameterNode("prefix", paramToken, new BuiltInTypeNode(BuiltInType.String));
methodNode.AddParameter(param);

// Create method body
var block = new BlockNode();
var returnStmt = new ReturnStatementNode(
    new BinaryOperationNode(
        new IdentifierNode("prefix", IdentifierType.UserVariable),
        BinaryOperator.Concatenate,
        false,
        new LiteralNode("Hello World", LiteralType.String)
    )
);
block.AddStatement(returnStmt);

// Complete the method
var methodImpl = new MethodImplNode("GetMessage", nameToken, block);
methodNode.SetImplementation(methodImpl);
```

## Documentation

Comprehensive documentation is available in the [`docs/`](docs/) directory:

- **[Architecture Guide](docs/architecture/)** - Deep dive into parser design and components
- **[API Reference](docs/api-reference/)** - Detailed documentation for all classes and methods  
- **[Examples](docs/examples/)** - Common usage patterns and advanced techniques
- **[Performance](docs/performance/)** - Benchmarks and optimization guidelines

### Quick Links
- [Lexing Process](docs/architecture/lexing.md)
- [Parsing Strategy](docs/architecture/parsing.md)  
- [AST Node Reference](docs/api-reference/nodes/)
- [Visitor Patterns](docs/api-reference/visitors/)
- [Error Recovery](docs/examples/error-recovery.md)

## Contributing

We welcome contributions to the PeopleCode Self-Hosted Parser! Please see our [contribution guidelines](CONTRIBUTING.md) for:

- Code style and conventions
- Testing requirements
- Pull request process
- Issue reporting

### Development Setup

1. Clone the repository
2. Ensure you have .NET 8 SDK installed
3. Restore dependencies: `dotnet restore`
4. Build the parser: `dotnet build`
5. Run tests: `dotnet test`

---

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.