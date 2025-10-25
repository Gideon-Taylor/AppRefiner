# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AppRefiner is a Windows Forms application that enhances PeopleSoft's Application Designer with modern development features including code folding, linting, syntax highlighting, and refactoring tools. It integrates seamlessly with Application Designer through Win32 API hooks and provides a comprehensive plugin architecture for extensibility.

## Build and Development Commands

### Building the Project
```powershell
# Framework-dependent build (requires .NET 8 runtime on target machine)
.\build.ps1

# Self-contained build (includes .NET runtime)
.\build.ps1 -SelfContained
```

### Prerequisites
- Windows with Visual Studio 2022 (C++ development tools)
- .NET 8 SDK
- PowerShell 5.1+

### Development Workflow
```powershell
# Restore dependencies
dotnet restore

# Build main project
dotnet build AppRefiner/AppRefiner.csproj

# Build C++ hook DLL (requires Visual Studio)
msbuild AppRefinerHook/AppRefinerHook.vcxproj /p:Configuration=Release /p:Platform=x64

# Parser is self-hosted in PeopleCodeParser.SelfHosted project
# No code generation required - pure C# implementation
```

**IMPORTANT FOR CLAUDE CODE USERS**: Do not attempt to build the project directly when working in WSL environments. AppRefiner is a Windows-specific application that relies on Windows Forms, Win32 APIs, and Visual Studio C++ build tools. Building should only be done on Windows with proper development tools installed.

### Project Structure
- **AppRefiner/**: Main Windows Forms application (.NET 8)
- **PeopleCodeParser.SelfHosted/**: Self-hosted C# recursive descent parser (.NET 8)
- **AppRefinerHook/**: Win32 API hook DLL (C++)
- **PluginSample/**: Example plugin implementation

## Core Architecture

### Service Layer Architecture
The application uses a service-oriented architecture with dependency injection:

- **AstService**: Manages AST parsing and caching with dependency resolution
- **SettingsService**: Centralized configuration management with JSON serialization
- **KeyboardShortcutService**: Global hotkey management
- **WinEventService**: Windows event handling and Application Designer integration

Services are constructor-injected into managers and use concurrent collections for thread safety.

### Plugin/Extension System
The core extensibility model is based on abstract base classes with the visitor pattern:

**Base Classes:**
- `BaseLintRule`: For code analysis and issue detection
- `BaseStyler`: For visual indicators and highlighting
- `ScopedRefactor`: For code transformations (unified base class)
- `BaseTooltipProvider`: For contextual information display

**Scoped Variants:**
- `ScopedLintRule<T>`: Adds variable/scope tracking
- `ScopedStyler<T>`: Scope-aware styling
- All refactors now use `ScopedRefactor` (unified base class with automatic scope tracking)

**Key Patterns:**
- Reflection-based discovery from assemblies and plugins
- Template Method pattern for extensibility
- Manager classes handle registration and lifecycle
- Priority-based execution ordering

### Database Integration
Uses Repository pattern with interface abstraction:
- `IDataManager`: Defines data access contract
- `OraclePeopleSoftDataManager`: PeopleSoft-specific implementation
- `DataManagerRequirement`: Dependency declaration system
- Connection pooling and caching for performance

### Parser and AST Architecture

AppRefiner uses a self-hosted recursive descent parser written entirely in C# with no external dependencies:

**Parser Components:**
- **PeopleCodeLexer**: Tokenizes source text with UTF-8 byte index tracking for Scintilla integration
  - Case-insensitive keyword recognition
  - System variable identification (%, &)
  - Comment and whitespace handling (trivia)
  - Comprehensive error reporting

- **PeopleCodeParser**: Recursive descent parser with advanced error recovery
  - Synchronization tokens for resilient parsing during live editing
  - Directive preprocessing support (#If, #Else, #End-If)
  - Produces strongly-typed AST nodes
  - Detailed parse error reporting

**AST Node Hierarchy:**
- **AstNode**: Base class for all AST nodes
  - Token-based source location tracking (`FirstToken`, `LastToken`, `SourceSpan`)
  - Parent-child relationships built-in
  - Visitor pattern support (`Accept(IAstVisitor)`)
  - Attributes dictionary for semantic analysis (types, errors, etc.)
  - Helper methods: `FindAncestor<T>()`, `FindDescendants<T>()`, `GetRoot()`

**Core Node Types** (in `PeopleCodeParser.SelfHosted.Nodes` namespace):
- **Program Structure**: `ProgramNode`, `AppClassNode`, `InterfaceNode`, `ImportNode`
- **Declarations**: `MethodNode`, `FunctionNode`, `PropertyNode`, `ProgramVariableNode`, `ConstantNode`
- **Statements**: `IfStatementNode`, `ForStatementNode`, `WhileStatementNode`, `TryStatementNode`, `BlockNode`, etc.
- **Expressions**: `BinaryOperationNode`, `FunctionCallNode`, `IdentifierNode`, `LiteralNode`, `AssignmentNode`, etc.
- **Types**: `BuiltInTypeNode`, `ArrayTypeNode`, `AppClassTypeNode`

**Visitor Pattern:**
- **IAstVisitor**: Interface for traversing AST without return values
- **IAstVisitor\<TResult\>**: Interface for traversing AST with return values
- **AstVisitorBase**: Base implementation with default depth-first traversal
- **ScopedAstVisitor\<T\>**: Enhanced visitor with comprehensive scope and variable tracking
  - Automatic scope management (global, class, method, function, property getter/setter)
  - Variable declaration and reference tracking
  - Variable registry with accessibility queries
  - Custom scope data support

**Scope and Variable Tracking:**
- **ScopeContext**: Represents a code scope with type (Global, Class, Method, Function, PropertyGetter, PropertySetter)
- **VariableRegistry**: Central registry for all variables and scopes in the program
- **VariableInfo**: Tracks variable declarations with type, kind (Local, Global, Instance, Parameter, etc.), and all references
- **VariableReference**: Tracks individual variable usages with Read/Write classification

**Type System:**
- **TypeInferenceVisitor**: Infers types for expressions and attaches type information to AST nodes
- **TypeCheckerVisitor**: Validates type correctness and reports type errors
- Type information stored in node Attributes dictionary using `AstNode.TypeInfoAttributeKey`

**Key Differences from ANTLR Approach:**
- No grammar file to reference - work directly with strongly-typed C# AST nodes
- Visitor methods are named after node types: `VisitMethod(MethodNode node)`, `VisitIf(IfStatementNode node)`, etc.
- IntelliSense provides full API discovery for AST nodes and their properties
- Source location tracking uses tokens, not parse tree contexts
- Built-in parent-child navigation without tree walking

## Development Guidelines

### Adding New Linters
1. Inherit from `BaseLintRule` (which extends `ScopedAstVisitor<object>`)
2. Implement required properties: `LINTER_ID`, `Description`, `Type`
3. Override visitor methods for AST nodes of interest (e.g., `VisitMethod(MethodNode node)`)
4. Use `AddReport()` to generate findings with line numbers and source spans
5. Access scope and variable information via inherited `ScopedAstVisitor` methods:
   - `GetCurrentScope()`: Get current scope context
   - `FindVariable(name)`: Find variable in accessible scopes
   - `GetAllVariables()`: Query all variables in the program
   - `GetUnusedVariables()`: Find unused variables
6. Use IntelliSense to discover available AST node types and properties

### Adding New Stylers
1. Inherit from `BaseStyler` (which extends `ScopedAstVisitor<object>`)
2. Override visitor methods for AST nodes of interest
3. Use `AddIndicator()` for visual feedback with source spans
4. Consider adding Quick Fixes through `GetQuickFixes()`
5. Access AST node properties directly (e.g., `node.Name`, `node.SourceSpan`, `node.Parameters`)
6. Use node navigation methods: `node.FindAncestor<T>()`, `node.FindDescendants<T>()`, `node.Children`

### Adding New Refactors
1. Inherit from `ScopedRefactor` (unified base class with automatic scope tracking)
2. Override static properties for metadata (`RefactorName`, `RefactorDescription`, etc.)
3. Use `EditText()`, `InsertText()`, `DeleteText()` methods to track modifications
4. Handle user input through dialogs if needed via `ShowRefactorDialog()`
5. Override visitor methods to identify refactoring opportunities
6. Access source locations via `node.SourceSpan` for precise text editing

### Plugin Development
1. Create new .NET 8 class library project
2. Reference AppRefiner project
3. Implement desired base classes
4. Copy compiled DLL to AppRefiner's Plugins directory
5. Restart AppRefiner to load plugins

### Database Integration
Components requiring database access should declare `DataManagerRequirement` and check `HasDatabaseConnection()` before executing database-dependent logic.

### Threading Considerations
- Use `Invoke()` for UI thread operations
- Services use concurrent collections for thread safety
- AST parsing is performed on background threads
- Always dispose of resources properly

### MessageBox Dialog Pattern
When displaying MessageBox dialogs, **always use this pattern** instead of `MessageBox.Show()`:

```csharp
Task.Delay(100).ContinueWith(_ =>
{
    // Show message box with specific error
    var mainHandle = Process.GetProcessById((int)activeEditor.ProcessId).MainWindowHandle;
    var handleWrapper = new WindowWrapper(mainHandle);
    new MessageBoxDialog("Your message here", "Dialog Title", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
});
```

### AppRefiner Debug Pattern
AppRefiner uses a custom Debug class and not the .NET delivered one. The Debug class has a Log() method that should be used instead of the standard Debug.WriteLine().

**Key Requirements:**
- Use `Task.Delay(100).ContinueWith()` for proper threading
- Get the main window handle from the editor's process ID
- Use `WindowWrapper` for proper dialog ownership
- Use `MessageBoxDialog` instead of `MessageBox.Show()`
- Pass the main window handle to both the dialog constructor and `ShowDialog()`

### Performance Optimization
- Leverage caching at AST, database, and settings levels
- Use lazy evaluation where possible
- Implement proper disposal patterns
- Consider scoped variants for performance-critical operations

## Testing

The project uses the existing test files and validation through:
- Manual testing with PeopleSoft Application Designer
- Plugin validation through PluginSample project
- Build validation through CI/CD pipeline

## Better Find Dialog Implementation

### Architecture Overview
The Better Find dialog (`BetterFindDialog.cs`) is a comprehensive replacement for Application Designer's basic search functionality, leveraging Scintilla's advanced search capabilities including regex support, replacement with captured groups, and various search options.

### Key Components

#### Search State Management
- **SearchState Class**: Encapsulates all search-related state per editor
- **Per-Editor State**: Each ScintillaEditor has its own SearchState instance
- **Persistent History**: Maintains search and replace term history
- **Option Persistence**: Remembers user preferences across sessions

```csharp
public class SearchState
{
    public string LastSearchTerm { get; set; } = string.Empty;
    public string LastReplaceText { get; set; } = string.Empty;
    public List<string> SearchHistory { get; set; } = new();
    public List<string> ReplaceHistory { get; set; } = new();
    // ... search options (MatchCase, WholeWord, UseRegex, etc.)
}
```

#### Dialog Implementation
- **Modal Dialog**: Uses DialogHelper.ModalDialogMouseHandler for proper modal behavior
- **AppRefiner Styling**: Consistent with other dialogs (dark header, light content)
- **Keyboard Navigation**: Full keyboard support with standard shortcuts
- **Real-time Validation**: Regex syntax validation with error display

#### Search Operations
- **Cross-Process Memory**: Uses VirtualAllocEx/WriteProcessMemory for string marshalling
- **Scintilla Integration**: Direct use of Scintilla search API (SCI_SEARCHINTARGET, etc.)
- **Search Highlighting**: Visual feedback using Scintilla indicators
- **Direction Support**: Forward/backward search with proper wrapping

### Keyboard Shortcuts
- **Ctrl+Alt+F**: Open Better Find dialog
- **F3**: Find next (works in dialog and globally)
- **Shift+F3**: Find previous (works in dialog and globally)
- **Escape**: Close dialog
- **Enter**: Execute search/replace
- **Ctrl+Enter**: Replace current/Replace all (with Shift)

### Command Integration
The Better Find functionality is integrated into the command palette system:
- "Better Find" - Opens the dialog
- "Find Next" - Finds next occurrence using current search state
- "Find Previous" - Finds previous occurrence using current search state

### Implementation Files
- **BetterFindDialog.cs**: Main dialog implementation with UI and event handling
- **ScintillaEditor.cs**: Added SearchState property for per-editor state management
- **ScintillaManager.cs**: Added search constants and core search methods
- **MainForm.cs**: Added keyboard shortcut handlers and command registration

### Key Methods in ScintillaManager
- `FindNext(ScintillaEditor editor)`: Execute forward search
- `FindPrevious(ScintillaEditor editor)`: Execute backward search
- `ReplaceSelection(ScintillaEditor editor, string replaceText)`: Replace current selection
- `ReplaceAll(ScintillaEditor editor, string searchTerm, string replaceText)`: Replace all occurrences
- `HighlightAllMatches(ScintillaEditor editor, string searchTerm, int searchFlags)`: Visual highlighting
- `ClearSearchHighlights(ScintillaEditor editor)`: Remove search highlights

### Usage Pattern
1. User opens Better Find dialog (Ctrl+Alt+F)
2. Dialog loads previous search state from ScintillaEditor.SearchState
3. User enters search criteria and options
4. Dialog validates input (especially regex patterns)
5. User performs search/replace operations
6. Dialog saves state back to ScintillaEditor.SearchState
7. Search highlights are applied for visual feedback
8. Dialog cleanup removes highlights when closed

### Extension Points
- **Custom Search Flags**: Easy to add new Scintilla search options
- **Search History**: Expandable history management
- **UI Customization**: Modular UI creation methods for easy modification
- **Command Integration**: Seamless integration with command palette system

## Key Files and Locations

- **MainForm.cs**: Central coordination and UI management
- **Services/**: Core service implementations
- **Linters/**, **Stylers/**, **Refactors/**: Extension implementations
- **Database/**: Data access layer
- **PeopleCodeParser.SelfHosted/**: Self-hosted parser implementation
  - **PeopleCodeLexer.cs**: Lexer/tokenizer
  - **PeopleCodeParser.cs**: Recursive descent parser
  - **AstNode.cs**: Base AST node class
  - **Nodes/**: AST node type definitions (ExpressionNodes, StatementNodes, DeclarationNodes, etc.)
  - **Visitors/**: Visitor interfaces and base implementations
    - **IAstVisitor.cs**: Visitor pattern interfaces
    - **ScopedAstVisitor.cs**: Base visitor with scope/variable tracking
    - **TypeInferenceVisitor.cs**: Type inference implementation
    - **TypeCheckerVisitor.cs**: Type checking/validation
- **Templates/**: Code generation templates
- **Dialogs/**: UI dialog implementations
- **Dialogs/BetterFindDialog.cs**: Advanced search and replace dialog

## Parser Usage Examples

### Basic Parsing
```csharp
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

// Tokenize source code
var lexer = new PeopleCodeLexer(sourceCode);
var tokens = lexer.Tokenize();

// Parse tokens into AST
var parser = new PeopleCodeParser(tokens);
var program = parser.ParseProgram();

// Check for parse errors
if (parser.Errors.Any())
{
    foreach (var error in parser.Errors)
    {
        Console.WriteLine($"Line {error.Line}: {error.Message}");
    }
}
```

### Implementing a Custom Visitor
```csharp
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Nodes;

public class MyAnalyzer : ScopedAstVisitor<object>
{
    public override void VisitMethod(MethodNode node)
    {
        // Access method properties
        var methodName = node.Name;
        var paramCount = node.Parameters.Count;
        var returnType = node.ReturnType?.TypeName;

        // Get current scope information
        var scope = GetCurrentScope();
        var localVars = GetVariablesInScope(scope)
            .Where(v => v.Kind == VariableKind.Local);

        // Access source location
        var span = node.SourceSpan;
        Console.WriteLine($"Method {methodName} at line {span.Start.Line}");

        // Continue traversal
        base.VisitMethod(node);
    }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        // Find what function is being called
        if (node.Function is IdentifierNode funcName)
        {
            Console.WriteLine($"Calling function: {funcName.Name}");
        }

        base.VisitFunctionCall(node);
    }
}

// Use the visitor
var analyzer = new MyAnalyzer();
program.Accept(analyzer);
```