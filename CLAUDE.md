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
- `BaseCommand`: For custom commands in the command palette with keyboard shortcuts (built-in or plugin)
- `BaseLanguageExtension`: For adding properties/methods to PeopleCode types via code transformations

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

### Adding New Commands
Commands can be built-in (located in `Commands/BuiltIn/`) or provided by plugins. All commands register in the command palette with optional keyboard shortcuts.

#### Base Class: `BaseCommand`
Located in `AppRefiner.Commands` namespace. All commands (built-in and plugin) must inherit from this abstract class.

**Required Properties:**
- `CommandName`: The display name shown in the command palette
- `CommandDescription`: Description of what the command does

**Optional Properties:**
- `RequiresActiveEditor`: Whether command needs an active editor (default: true)
- `DynamicEnabledCheck`: Func<bool> to control when command is enabled

**Key Methods:**
- `InitializeShortcuts(IShortcutRegistrar registrar, string commandId)`: Override to register keyboard shortcuts
- `Execute(CommandContext context)`: Main execution method - called from palette or shortcut
- `GetDisplayName()`: Returns name with shortcut text if registered
- `SetRegisteredShortcut(string shortcutText)`: Helper to store shortcut display text

#### CommandContext Structure
Passed to `Execute()` method, providing access to AppRefiner services:
- `ActiveEditor`: The currently active ScintillaEditor (may be null)
- `LinterManager`: Access to linting functionality
- `StylerManager`: Access to styling functionality
- `RefactorManager`: Access to refactoring functionality
- `SettingsService`: Access to application settings
- `AutoCompleteService`: Access to autocomplete functionality
- `FunctionCacheManager`: Access to cached function metadata
- `AutoSuggestSettings`: Current auto-suggest configuration

#### Keyboard Shortcut Registration
The `InitializeShortcuts()` method provides full control over shortcut registration:

**IShortcutRegistrar Interface:**
- `IsShortcutAvailable(ModifierKeys, Keys)`: Check if a shortcut is available
- `TryRegisterShortcut(commandId, ModifierKeys, Keys, BaseCommand)`: Register the command for this shortcut
- `GetShortcutDisplayText(ModifierKeys, Keys)`: Get formatted display text

**How It Works:**
- Commands register themselves by passing `this` to `TryRegisterShortcut`
- When the shortcut is pressed, ApplicationKeyboardService creates a fresh `CommandContext` with current application state
- The command's `Execute(context)` method is called with the populated context
- This ensures commands always have access to the active editor and all services

**Best Practices:**
- Always check availability before attempting registration
- Provide fallback shortcut options if preferred combination is taken
- Use `TryRegisterShortcut()` return value to determine success
- Call `SetRegisteredShortcut()` on success to update display name
- Pass `this` as the command parameter (not a lambda or action)

#### Simple Example
```csharp
using AppRefiner.Commands;
using AppRefiner.Services;

public class SimpleCommand : BaseCommand
{
    public override string CommandName => "My Command";
    public override string CommandDescription => "Does something useful";
    public override bool RequiresActiveEditor => false;

    public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
    {
        // Try to register Ctrl+Alt+M - pass 'this' so keyboard service can execute with proper context
        if (registrar.TryRegisterShortcut(commandId,
            ModifierKeys.Control | ModifierKeys.Alt,
            Keys.M,
            this))
        {
            SetRegisteredShortcut(registrar.GetShortcutDisplayText(
                ModifierKeys.Control | ModifierKeys.Alt, Keys.M));
        }
    }

    public override void Execute(CommandContext context)
    {
        MessageBox.Show("Command executed!");
    }
}
```

#### Advanced Example with Fallback Shortcuts
```csharp
public class AdvancedCommand : BaseCommand
{
    public override string CommandName => "Advanced Command";
    public override string CommandDescription => "Command with fallback shortcuts";
    public override bool RequiresActiveEditor => true;

    // Only enable when database is connected
    public override Func<bool>? DynamicEnabledCheck => () =>
    {
        // Add runtime logic here
        return true;
    };

    public override void InitializeShortcuts(IShortcutRegistrar registrar, string commandId)
    {
        // Try multiple shortcuts with fallback
        var shortcuts = new[]
        {
            (ModifierKeys.Control | ModifierKeys.Alt, Keys.D),
            (ModifierKeys.Control | ModifierKeys.Shift, Keys.D),
            (ModifierKeys.Alt | ModifierKeys.Shift, Keys.D)
        };

        foreach (var (modifiers, key) in shortcuts)
        {
            if (registrar.IsShortcutAvailable(modifiers, key))
            {
                if (registrar.TryRegisterShortcut(commandId, modifiers, key, this))
                {
                    SetRegisteredShortcut(registrar.GetShortcutDisplayText(modifiers, key));
                    Debug.Log($"{CommandName}: Registered {RegisteredShortcutText}");
                    return; // Success
                }
            }
        }

        Debug.Log($"{CommandName}: Could not register any shortcuts");
    }

    public override void Execute(CommandContext context)
    {
        if (context.ActiveEditor == null)
        {
            MessageBox.Show("No active editor");
            return;
        }

        // Access AppRefiner services through context
        context.LinterManager?.ProcessLintersForActiveEditor(
            context.ActiveEditor,
            context.ActiveEditor.DataManager);
    }
}
```

#### Accessing Services and Data
```csharp
public override void Execute(CommandContext context)
{
    // Check for active editor
    if (context.ActiveEditor == null)
        return;

    // Access database through editor
    var dataManager = context.ActiveEditor.DataManager;
    if (dataManager != null)
    {
        // Perform database operations
    }

    // Use linter manager
    if (context.LinterManager != null)
    {
        context.LinterManager.ProcessLintersForActiveEditor(
            context.ActiveEditor, dataManager);
    }

    // Access settings
    if (context.SettingsService != null)
    {
        var settings = context.SettingsService.LoadGeneralSettings();
        // Use settings
    }

    // Execute refactors
    if (context.RefactorManager != null)
    {
        // Create and execute refactor instances
    }
}
```

#### Discovery and Registration
Commands are automatically discovered at startup:
1. `CommandManager.DiscoverAndCacheCommands()` finds all `BaseCommand` subclasses from both the main assembly and loaded plugins
2. Each command's `InitializeShortcuts()` is called with `IShortcutRegistrar`
3. Commands are registered in the command palette with their display names
4. Shortcuts are globally active when registered

#### Testing Commands
**For Plugin Commands:**
1. Build your plugin project (targeting .NET 8)
2. Copy the compiled DLL to AppRefiner's Plugins directory
3. Restart AppRefiner
4. Open command palette (Ctrl+Shift+P) to see your commands
5. Check Debug Console for registration messages

**For Built-in Commands:**
1. Create command class in `Commands/BuiltIn/` folder
2. Inherit from `BaseCommand` and implement required members
3. Build and run AppRefiner
4. Command is auto-discovered and appears in command palette

### Adding New Language Extensions
Language extensions allow adding new properties and methods to existing PeopleCode types through code transformations (similar to C# extension methods).

#### Base Class: `BaseLanguageExtension`
Located in `AppRefiner.LanguageExtensions` namespace. Supports multi-transform architecture where a single extension class can provide multiple methods/properties.

**Required Properties:**
- `TargetTypes`: List of types this extension applies to (all transforms apply to all target types)
- `Transforms`: Collection of `ExtensionTransform` instances defining the methods/properties

**Key Concepts:**
- Single extension class can define multiple transforms
- All transforms automatically apply to all target types
- Active state and configuration shared across all transforms in the class
- Transforms shown individually in extension grid UI

#### Creating Simple Pattern-Based Transforms
For common string replacement cases, use the `ExtensionTransform.CreateSimple()` factory method:

**Pattern Syntax:**
- `%1` = target expression (the object before the dot)
- `%2`, `%3`, `%4`, etc. = function arguments (1st, 2nd, 3rd argument for method extensions)
- Optional arguments automatically handled (unreplaced placeholders removed)

**Examples:**

```csharp
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace AppRefiner.LanguageExtensions.BuiltIn
{
    public class StringExtensions : BaseLanguageExtension
    {
        public override List<TypeInfo> TargetTypes => new()
        {
            PrimitiveTypeInfo.String  // All transforms apply to String type
        };

        public override List<ExtensionTransform> Transforms => new()
        {
            // Property: &string.Len → Len(&string)
            ExtensionTransform.CreateSimple(
                name: "Len",
                description: "Get the length of a string (transforms to Len())",
                extensionType: LanguageExtensionType.Property,
                transformPattern: "Len(%1)",
                returnType: new TypeWithDimensionality(PeopleCodeType.Number)
            ),

            // Method: &string.IndexOf("foo") → Find("foo", &string)
            // Method: &string.IndexOf("foo", 3) → Find("foo", &string, 3)
            ExtensionTransform.CreateSimple(
                name: "IndexOf",
                description: "Find index of substring (transforms to Find())",
                extensionType: LanguageExtensionType.Method,
                transformPattern: "Find(%2, %1, %3)",  // %3 auto-removed if not provided
                returnType: new TypeWithDimensionality(PeopleCodeType.Number),
                functionInfo: new FunctionInfo()
                {
                    Parameters = new()
                    {
                        new SingleParameter(new TypeWithDimensionality(PeopleCodeType.String), "search_string"),
                        new VariableParameter(new SingleParameter(new TypeWithDimensionality(PeopleCodeType.Number)), 0, 1, "start_index")
                    },
                    ReturnType = new TypeWithDimensionality(PeopleCodeType.Number)
                }
            ),

            // Property: &string.ToUpper → Upper(&string)
            ExtensionTransform.CreateSimple(
                name: "ToUpper",
                description: "Convert string to uppercase (transforms to Upper())",
                extensionType: LanguageExtensionType.Property,
                transformPattern: "Upper(%1)",
                returnType: new TypeWithDimensionality(PeopleCodeType.String)
            )
        };
    }
}
```

#### Creating Custom Transform Logic
For complex transformations that require custom logic (like `ArrayExtensions.ForEach`), use the full `ExtensionTransform` constructor with a `TransformAction` delegate:

```csharp
new ExtensionTransform
{
    Name = "ForEach",
    Description = "Expands to a For loop that iterates the array",
    ExtensionType = LanguageExtensionType.Method,
    ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
    FunctionInfo = new FunctionInfo() { /* ... */ },
    TransformAction = (editor, node, matchedType, variableRegistry) =>
    {
        // Custom transformation logic
        // Access AST node, editor, variable registry
        // Use ScintillaManager.ReplaceTextRange() to modify code
    }
}
```

**Transform Execution Context:**
- `editor`: ScintillaEditor instance containing the code
- `node`: AST node where extension is used (MemberAccessNode for properties, FunctionCallNode for methods)
- `matchedType`: The actual TypeInfo that was matched (important for multi-type extensions)
- `variableRegistry`: Variable and scope information (may be null)

#### Multi-Type Extensions
A single extension class can target multiple types:

```csharp
public override List<TypeInfo> TargetTypes => new()
{
    PrimitiveTypeInfo.String,
    PrimitiveTypeInfo.Number,
    new ArrayTypeInfo()
};
```

All transforms in the `Transforms` collection will be available on all target types.

#### Discovery and Registration
Language extensions are:
- Automatically discovered from main assembly and plugins via reflection
- Flattened into individual transforms during initialization
- Cached by type name for O(1) lookup performance
- Displayed in extension grid UI (one row per transform)
- Active state managed at extension class level (affects all transforms)

**For Built-in Extensions:**
1. Create extension class in `LanguageExtensions/BuiltIn/` folder
2. Inherit from `BaseLanguageExtension`
3. Define `TargetTypes` and `Transforms` collections
4. Build and run AppRefiner
5. Extension auto-discovered and transforms appear in autocomplete

**For Plugin Extensions:**
1. Create .NET 8 class library
2. Reference AppRefiner project
3. Implement `BaseLanguageExtension` subclass
4. Copy compiled DLL to AppRefiner's Plugins directory
5. Restart AppRefiner

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
- **Commands/**: Command system (built-in and plugin)
  - **IShortcutRegistrar.cs**: Interface for keyboard shortcut registration
  - **CommandContext.cs**: Context passed to commands
  - **BaseCommand.cs**: Base class for all commands (built-in and plugin)
  - **CommandManager.cs**: Discovery and execution of commands
  - **BuiltIn/**: Built-in command implementations
- **Linters/**, **Stylers/**, **Refactors/**: Extension implementations
- **LanguageExtensions/**: Language extension system for type augmentation
  - **BaseLanguageExtension.cs**: Base class for language extensions
  - **ExtensionTransform.cs**: Transform descriptor with CreateSimple factory
  - **LanguageExtensionManager.cs**: Discovery, caching, and execution
  - **BuiltIn/**: Built-in language extensions (StringExtensions, ArrayExtensions)
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