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
- Java 17+ (for ANTLR parser generation)
- PowerShell 5.1+

### Development Workflow
```powershell
# Restore dependencies
dotnet restore

# Build main project
dotnet build AppRefiner/AppRefiner.csproj

# Build C++ hook DLL (requires Visual Studio)
msbuild AppRefinerHook/AppRefinerHook.vcxproj /p:Configuration=Release /p:Platform=x64

# Generate ANTLR parsers (automatic during build)
# Located in PeopleCodeParser project
```

**IMPORTANT FOR CLAUDE CODE USERS**: Do not attempt to build the project directly when working in WSL environments. AppRefiner is a Windows-specific application that relies on Windows Forms, Win32 APIs, and Visual Studio C++ build tools. Building should only be done on Windows with proper development tools installed.

### Project Structure
- **AppRefiner/**: Main Windows Forms application (.NET 8)
- **PeopleCodeParser/**: ANTLR-based PeopleCode parser (.NET 8)
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
The core extensibility model is based on abstract base classes with ANTLR visitor pattern:

**Base Classes:**
- `BaseLintRule`: For code analysis and issue detection
- `BaseStyler`: For visual indicators and highlighting
- `BaseRefactor`: For code transformations
- `BaseTooltipProvider`: For contextual information display

**Scoped Variants:**
- `ScopedLintRule<T>`: Adds variable/scope tracking
- `ScopedStyler<T>`: Scope-aware styling
- `ScopedRefactor<T>`: Scope-aware refactoring

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

### AST Model
Simplified AST representation using Composite pattern:
- `Program`: Root AST node with dependency resolution
- `AppClass`, `Interface`, `Method`, `Property`: Core language constructs
- ANTLR-generated parser with custom AST transformation
- Caching with invalidation support

**IMPORTANT**: When working with ANTLR parse trees, always reference the grammar file at `PeopleCodeParser/PeopleCodeParser.g4` to ensure correct navigation of parser rule contexts. The grammar defines the exact structure and naming of parser rules that must be used for proper tree traversal.

## Development Guidelines

### Adding New Linters
1. Inherit from `BaseLintRule` or `ScopedLintRule<T>`
2. Override `GetName()`, `GetDescription()`, and ANTLR visitor methods
3. Use `AddReport()` to generate findings
4. Configure through `GetConfiguration()` and `ApplyConfiguration()`
5. **Always reference `PeopleCodeParser/PeopleCodeParser.g4` when implementing ANTLR visitor methods**

### Adding New Stylers
1. Inherit from `BaseStyler` or `ScopedStyler<T>`
2. Override required methods and ANTLR visitors
3. Use `AddIndicator()` for visual feedback
4. Consider adding Quick Fixes through `GetQuickFixes()`
5. **Always reference `PeopleCodeParser/PeopleCodeParser.g4` when implementing ANTLR visitor methods**

### Adding New Refactors
1. Inherit from `BaseRefactor` or `ScopedRefactor<T>`
2. Override `GetName()`, `GetDescription()`, and `CanExecute()`
3. Use `InsertText()`, `ReplaceNode()`, etc. to track modifications
4. Handle user input through dialogs if needed
5. **Always reference `PeopleCodeParser/PeopleCodeParser.g4` when implementing ANTLR visitor methods**

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

## Key Files and Locations

- **MainForm.cs**: Central coordination and UI management
- **Services/**: Core service implementations
- **Linters/**, **Stylers/**, **Refactors/**: Extension implementations
- **Database/**: Data access layer
- **Ast/**: AST model definitions
- **Templates/**: Code generation templates
- **Dialogs/**: UI dialog implementations