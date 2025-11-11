# Plugin System

## Overview

AppRefiner provides a powerful plugin system that allows developers to extend its functionality without modifying the core application. Plugins are standard .NET assemblies (DLL files) that contain custom linters, stylers, refactors, and tooltip providers. The plugin system uses reflection-based discovery to automatically detect and load extensions at runtime.

**Key Features:**
- **Zero Configuration**: Drop DLL files in Plugins directory - they're automatically discovered
- **Hot Reload**: Reload plugins without restarting Application Designer
- **Four Extension Points**: Linters, Stylers, Refactors, Tooltip Providers
- **Type-Safe**: All plugins inherit from strongly-typed base classes
- **AST Access**: Full access to PeopleCode AST via self-hosted parser
- **Visual Feedback**: Plugins integrate seamlessly with AppRefiner's UI
- **Error Isolation**: Plugin load failures don't crash AppRefiner

---

## Plugin Architecture

### Extension Points

AppRefiner supports four types of plugins:

| Plugin Type | Base Class | Purpose | Examples |
|-------------|-----------|---------|----------|
| **Linter** | `BaseLintRule` | Code quality analysis and issue detection | Detect unused variables, check naming conventions, find code smells |
| **Styler** | `BaseStyler` | Visual indicators and syntax highlighting | Highlight errors, mark deprecated code, show semantic info |
| **Refactor** | `BaseRefactor` | Code transformations and generation | Extract method, rename variables, generate boilerplate |
| **Tooltip Provider** | `BaseTooltipProvider` | Contextual information display | Show variable info, method signatures, documentation |

### Discovery Mechanism

1. **Load Phase**: AppRefiner scans `Plugins` directory for `*.dll` files
2. **Reflection**: Uses `Assembly.LoadFrom()` to load each DLL
3. **Type Discovery**: Searches assemblies for types inheriting from base classes
4. **Instantiation**: Creates instances using parameterless constructors (linters/stylers/tooltips) or single-parameter constructors (refactors)
5. **Registration**: Adds instances to respective manager collections
6. **Activation**: Plugins appear in Settings UI with enable/disable checkboxes

---

## Creating Your First Plugin

### Prerequisites

- **Visual Studio 2022** or later
- **.NET 8.0 SDK**
- **AppRefiner** source code or binaries for reference
- **C#** programming knowledge

### Step 1: Create Plugin Project

Create a new .NET 8 Class Library project:

```bash
dotnet new classlib -n MyAppRefinerPlugin -f net8.0-windows7.0
```

### Step 2: Add AppRefiner Reference

Edit your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference AppRefiner project or DLL -->
    <ProjectReference Include="..\AppRefiner\AppRefiner.csproj" Private="false" />
    <!-- OR -->
    <Reference Include="AppRefiner">
      <HintPath>C:\Path\To\AppRefiner.exe</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

**Important**: Set `Private="false"` to avoid copying AppRefiner.exe into your plugin output.

### Step 3: Choose Plugin Type

Decide which extension point(s) to implement. You can implement multiple types in one plugin assembly.

---

## Creating a Custom Linter

Linters analyze code for issues and report findings to the user.

### Basic Linter Template

```csharp
using AppRefiner.Linters;
using PeopleCodeParser.SelfHosted.Nodes;

namespace MyPlugin
{
    public class MyCustomLinter : BaseLintRule
    {
        // Unique identifier for this linter
        public override string LINTER_ID => "MY_CUSTOM_LINTER";

        public MyCustomLinter()
        {
            Description = "Checks for custom code patterns";
            Type = ReportType.Warning; // Or Error, Info, Style
            Active = true; // Default enabled state
        }

        // Override visitor methods for AST nodes you want to check
        public override void VisitMethod(MethodNode node)
        {
            // Example: Check method name length
            if (node.Name.Length > 30)
            {
                AddReport(
                    reportNumber: 1,
                    message: $"Method name '{node.Name}' is too long (>{node.Name.Length} characters)",
                    type: ReportType.Warning,
                    line: node.SourceSpan.Start.Line,
                    span: node.NameToken.SourceSpan
                );
            }

            base.VisitMethod(node);
        }

        public override void Reset()
        {
            base.Reset();
            // Clear any linter-specific state
        }
    }
}
```

### Linter Key Concepts

#### LINTER_ID
- **Required**: Unique identifier for your linter
- **Format**: Use UPPER_SNAKE_CASE
- **Purpose**: Used for settings persistence and error reporting

#### ReportType
- **Error**: Red squiggly underline - serious issues
- **Warning**: Yellow squiggly underline - potential problems
- **Info**: Blue squiggly underline - informational
- **Style**: Gray squiggly underline - style suggestions

#### AddReport Method
```csharp
AddReport(
    int reportNumber,        // Unique number per linter run
    string message,          // User-facing description
    ReportType type,         // Severity level
    int line,                // Line number (1-based)
    SourceSpan span         // Character range for squiggly
)
```

#### Visitor Pattern
Override visitor methods for AST nodes you want to analyze:
- `VisitMethod(MethodNode node)` - Method declarations
- `VisitFunction(FunctionNode node)` - Function declarations
- `VisitIf(IfStatementNode node)` - If statements
- `VisitFor(ForStatementNode node)` - For loops
- `VisitIdentifier(IdentifierNode node)` - Variable references
- `VisitFunctionCall(FunctionCallNode node)` - Function calls
- And 40+ other node types

### Advanced Linter Example

```csharp
public class UnusedParameterLinter : BaseLintRule
{
    public override string LINTER_ID => "UNUSED_PARAMETER";

    private Dictionary<string, int> parameterReferences = new();

    public UnusedParameterLinter()
    {
        Description = "Detects unused method parameters";
        Type = ReportType.Info;
        Active = true;
    }

    public override void VisitMethod(MethodNode node)
    {
        // Reset tracking for this method
        parameterReferences.Clear();

        // Initialize all parameters as unreferenced
        foreach (var param in node.Parameters)
        {
            parameterReferences[param.Name] = 0;
        }

        // Visit method body to count references
        base.VisitMethod(node);

        // Report unused parameters
        foreach (var param in node.Parameters)
        {
            if (parameterReferences[param.Name] == 0)
            {
                AddReport(
                    1,
                    $"Parameter '{param.Name}' is never used",
                    ReportType.Info,
                    param.SourceSpan.Start.Line,
                    param.SourceSpan
                );
            }
        }
    }

    public override void VisitIdentifier(IdentifierNode node)
    {
        // Count parameter references
        if (parameterReferences.ContainsKey(node.Name))
        {
            parameterReferences[node.Name]++;
        }

        base.VisitIdentifier(node);
    }

    public override void Reset()
    {
        base.Reset();
        parameterReferences.Clear();
    }
}
```

---

## Creating a Custom Styler

Stylers add visual indicators (highlights, underlines) without reporting issues.

### Basic Styler Template

```csharp
using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes;

namespace MyPlugin
{
    public class MyCustomStyler : BaseStyler
    {
        // Define colors (0xBBGGRR format - Windows BGR)
        private const uint HIGHLIGHT_COLOR = 0xFFFF00; // Cyan

        public MyCustomStyler()
        {
            Description = "Highlights specific code patterns";
            Active = true;
        }

        public override string Description { get; }

        public override void VisitProgram(ProgramNode node)
        {
            Reset(); // Clear previous indicators
            base.VisitProgram(node);
        }

        public override void VisitMethodImpl(MethodImplNode node)
        {
            if (node.Declaration != null)
            {
                // Highlight method names
                AddIndicator(
                    node.Declaration.NameToken.SourceSpan,
                    IndicatorType.HIGHLIGHTER,
                    HIGHLIGHT_COLOR,
                    "Custom Styler: Method Name"
                );
            }

            base.VisitMethodImpl(node);
        }

        protected override void OnReset()
        {
            base.OnReset(); // Clears indicators automatically
        }
    }
}
```

### Styler Key Concepts

#### IndicatorType
- **HIGHLIGHTER**: Background color highlight
- **SQUIGGLY**: Wavy underline
- **PLAIN**: Solid underline
- **DIAGONAL**: Diagonal stripes
- **STRIKE**: Strikethrough
- **BOX**: Box around text
- **ROUNDBOX**: Rounded box
- **STRAIGHTBOX**: Straight box

#### Color Format
Colors use **BGR format** (not RGB!):
```csharp
0xBBGGRR
0xFF0000 = Blue
0x00FF00 = Green
0x0000FF = Red
0xFFFF00 = Cyan
0x00FFFF = Yellow
0xFF00FF = Magenta
```

#### AddIndicator Method
```csharp
AddIndicator(
    SourceSpan span,         // Character range to style
    IndicatorType type,      // Visual style
    uint color,              // BGR color code
    string tooltip          // Hover text (optional)
)
```

### Advanced Styler Example

```csharp
public class DeprecatedAPIStyler : BaseStyler
{
    private readonly HashSet<string> deprecatedFunctions = new()
    {
        "OldFunction",
        "LegacyMethod",
        "DeprecatedAPI"
    };

    private const uint DEPRECATED_COLOR = 0xC0C0C0; // Gray

    public DeprecatedAPIStyler()
    {
        Description = "Highlights deprecated API calls";
        Active = true;
    }

    public override string Description { get; }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        if (node.Function is IdentifierNode funcName &&
            deprecatedFunctions.Contains(funcName.Name))
        {
            AddIndicator(
                funcName.SourceSpan,
                IndicatorType.STRIKE,
                DEPRECATED_COLOR,
                $"Deprecated: {funcName.Name} is deprecated. Use new API instead."
            );
        }

        base.VisitFunctionCall(node);
    }
}
```

---

## Creating a Custom Refactor

Refactors transform code through edits, insertions, and deletions.

### Basic Refactor Template

```csharp
using AppRefiner;
using AppRefiner.Refactors;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Windows.Forms;

namespace MyPlugin
{
    public class MyCustomRefactor : BaseRefactor
    {
        public MyCustomRefactor(ScintillaEditor editor) : base(editor) { }

        // Static metadata
        public new static string RefactorName => "My Custom Refactor";
        public new static string RefactorDescription => "Performs a custom code transformation";
        public new static bool RegisterKeyboardShortcut => true;
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Alt;
        public new static Keys ShortcutKey => Keys.R;

        // Override visitor methods to find refactoring targets
        public override void VisitMethod(MethodNode node)
        {
            // Example: Add logging to method entry
            var methodStart = ScintillaManager.GetLineStartIndex(
                Editor,
                node.SourceSpan.Start.Line
            );

            string logStatement = $"    Debug.Log(\"Entering {node.Name}\");\r\n";
            InsertText(methodStart, logStatement, "Add entry logging");

            base.VisitMethod(node);
        }
    }
}
```

### Refactor Key Concepts

#### Text Manipulation Methods

```csharp
// Insert text at position
InsertText(int position, string text, string description);

// Replace text range
EditText(int start, int length, string newText, string description);

// Delete text range
DeleteText(int start, int length, string description);
```

#### Keyboard Shortcut Registration

```csharp
public new static bool RegisterKeyboardShortcut => true;
public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;
public new static Keys ShortcutKey => Keys.E;
// Results in: Ctrl+Shift+E
```

#### User Input Dialogs

```csharp
public override bool RequiresUserInputDialog => true;

public override bool ShowRefactorDialog()
{
    // Show custom dialog to gather user input
    using (var dialog = new MyRefactorDialog())
    {
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            // Store user choices
            this.userInput = dialog.UserInput;
            return true; // Proceed with refactoring
        }
    }
    return false; // Cancel refactoring
}
```

### Advanced Refactor Example

```csharp
public class ExtractVariableRefactor : BaseRefactor
{
    private string variableName = "&extracted";

    public ExtractVariableRefactor(ScintillaEditor editor) : base(editor) { }

    public new static string RefactorName => "Extract Variable";
    public new static string RefactorDescription => "Extracts selected expression into a new variable";
    public new static bool RegisterKeyboardShortcut => true;
    public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Alt;
    public new static Keys ShortcutKey => Keys.V;

    public override bool RequiresUserInputDialog => true;

    public override bool ShowRefactorDialog()
    {
        // Simple input dialog for variable name
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter variable name:",
            "Extract Variable",
            variableName
        );

        if (!string.IsNullOrWhiteSpace(input))
        {
            variableName = input;
            return true;
        }
        return false;
    }

    protected override void Initialize(ProgramNode program, int currentPosition)
    {
        base.Initialize(program, currentPosition);

        // Get selected text
        var selectedText = ScintillaManager.GetSelectedText(Editor);
        if (string.IsNullOrWhiteSpace(selectedText))
            return;

        // Find containing method
        var method = GetContainingMethod(program, currentPosition);
        if (method == null)
            return;

        // Insert variable declaration at method start
        var methodBodyStart = ScintillaManager.GetLineStartIndex(
            Editor,
            method.SourceSpan.Start.Line + 1
        );

        string declaration = $"    Local string {variableName} = {selectedText};\r\n";
        InsertText(methodBodyStart, declaration, "Declare extracted variable");

        // Replace selection with variable name
        var selectionStart = ScintillaManager.GetSelectionStart(Editor);
        var selectionLength = selectedText.Length;
        EditText(selectionStart, selectionLength, variableName, "Use extracted variable");
    }

    private MethodNode? GetContainingMethod(ProgramNode program, int position)
    {
        return program.FindNodes<MethodNode>(n =>
            n.SourceSpan.ContainsPosition(position)
        ).FirstOrDefault();
    }
}
```

---

## Creating a Custom Tooltip Provider

Tooltip providers display contextual information when hovering over code.

### Basic Tooltip Provider Template

```csharp
using AppRefiner.TooltipProviders;
using PeopleCodeParser.SelfHosted.Nodes;

namespace MyPlugin
{
    public class MyCustomTooltipProvider : BaseTooltipProvider
    {
        public override string Name => "My Custom Tooltip";
        public override string Description => "Shows custom information on hover";
        public override int Priority => 50; // Higher = checked first

        public override void VisitFunction(FunctionNode node)
        {
            // Check if hovering over this function's name
            if (ContainsPosition(node.NameToken.SourceSpan))
            {
                string tooltip = $"Function: {node.Name}\n" +
                                $"Parameters: {node.Parameters.Count}\n" +
                                $"Location: Line {node.SourceSpan.Start.Line}";

                RegisterTooltip(node.NameToken.SourceSpan, tooltip);
            }

            base.VisitFunction(node);
        }
    }
}
```

### Tooltip Provider Key Concepts

#### Priority System
- Higher priority = checked first
- Built-in priorities:
  - 100: Active Indicators
  - 80: Scope Context
  - 70: PeopleSoft Object Info
  - 50: Method Parameters, Variable Info
  - 40: Inferred Type
  - 0: Application Class Details

#### Helper Methods

```csharp
// Check if current hover position is within span
bool ContainsPosition(SourceSpan span);

// Register tooltip for a span
void RegisterTooltip(SourceSpan span, string tooltipText);

// Access current position and line
int CurrentPosition { get; }
int CurrentLine { get; }
```

#### Database Access

```csharp
public override DataManagerRequirement DatabaseRequirement =>
    DataManagerRequirement.Required; // or Optional, or NotRequired

// In visitor methods:
if (DataManager != null && DataManager.IsConnected)
{
    var data = DataManager.GetSomeData();
    // Use database data in tooltip
}
```

---

## Building and Deploying Plugins

### Build Configuration

1. **Build in Release Mode**:
   ```bash
   dotnet build -c Release
   ```

2. **Output**: Plugin DLL appears in `bin\Release\net8.0-windows7.0\`

3. **Dependencies**: Only copy your plugin DLL, not AppRefiner.exe or system DLLs

### Deployment Steps

1. **Locate Plugins Directory**:
   - Default: `C:\Path\To\AppRefiner\Plugins\`
   - Configurable via Settings

2. **Copy Plugin DLL**:
   ```
   Copy MyAppRefinerPlugin.dll → Plugins\
   ```

3. **Reload Plugins**:
   - Option A: Open AppRefiner Settings → Click "Plugins..." button → Click "Reload"
   - Option B: Restart Application Designer

4. **Verify Load**:
   - Check Plugin Manager dialog for your plugin
   - Verify counts: "X linters, Y stylers, Z refactors, W tooltip providers"

5. **Enable Plugin Components**:
   - Navigate to appropriate Settings tabs (Linters, Stylers, Tooltips)
   - Check boxes to enable your components
   - Test in an editor

---

## Plugin Manager Dialog

Access via Settings → General → "Plugins..." button

### Features

**Plugin List**:
- Shows all loaded plugin assemblies
- Displays:
  - Assembly name
  - Version number
  - File path
  - Component counts (linters, stylers, refactors, tooltips)

**Actions**:
- **Reload Plugins**: Rescans Plugins directory and reloads all DLLs
- **Change Directory**: Set custom plugin directory path
- **View Details**: Inspect plugin metadata

**Error Handling**:
- Failed plugins shown with error message
- Other plugins continue loading
- Errors logged to debug log

---

## Debugging Plugins

### Attach Debugger

1. Build plugin in Debug mode
2. Copy DLL to Plugins directory
3. In Visual Studio: Debug → Attach to Process
4. Select `pside.exe` (Application Designer)
5. Set breakpoints in your plugin code
6. Trigger plugin functionality in Application Designer
7. Debugger hits breakpoints

### Debug Output

Use AppRefiner's Debug class:

```csharp
Debug.Log("Plugin initialized");
Debug.Log($"Processing node: {node.GetType().Name}");
Debug.LogException(ex, "Error in plugin");
```

View logs: Settings → General → "Debug Log..." button

### Common Issues

**Plugin Not Loading**:
- Check DLL is in correct directory
- Verify .NET 8.0 target framework
- Ensure no conflicting assembly versions
- Review debug log for load errors

**Plugin Loads But Doesn't Appear**:
- Verify class is public and non-abstract
- Check inheritance from correct base class
- Ensure parameterless constructor (or single-parameter for refactors)
- Reload plugins via Plugin Manager

**Runtime Errors**:
- Check for null reference exceptions
- Validate AST node properties before accessing
- Use try-catch for defensive coding
- Log errors to debug output

---

## Best Practices

### Performance

1. **Minimize Work in Visitors**: Only process nodes relevant to your plugin
2. **Cache Expensive Operations**: Don't repeat database queries or calculations
3. **Use Lazy Evaluation**: Defer work until actually needed
4. **Clean Up**: Always implement Reset() to clear state between runs

### Code Quality

1. **Null Checks**: AST nodes may have null properties
2. **Defensive Coding**: Validate assumptions before acting
3. **Error Handling**: Catch and log exceptions gracefully
4. **Thread Safety**: Assume plugins run on background threads

### User Experience

1. **Clear Messages**: Write descriptive error/warning messages
2. **Appropriate Severity**: Use correct ReportType for findings
3. **Helpful Tooltips**: Provide actionable information
4. **Sensible Defaults**: Default to enabled if generally useful

### Documentation

1. **Document Plugin Purpose**: Add XML comments
2. **Usage Examples**: Provide sample code in README
3. **Configuration**: Document any settings or customization
4. **Changelog**: Track versions and changes

---

## Advanced Topics

### Accessing Scope Information

Use `ScopedAstVisitor<T>` features for variable tracking:

```csharp
public class MyScopedLinter : BaseLintRule
{
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Get current scope
        var scope = GetCurrentScope();

        // Find variable declaration
        var variable = FindVariable(node.Name);

        if (variable != null)
        {
            Debug.Log($"Variable {variable.Name} declared at line {variable.DeclarationLine}");
            Debug.Log($"Kind: {variable.Kind}, Type: {variable.Type}");
            Debug.Log($"References: {variable.References.Count}");
        }

        base.VisitIdentifier(node);
    }
}
```

### Multi-File Analysis

For cross-file analysis, access database:

```csharp
public override DataManagerRequirement DatabaseRequirement =>
    DataManagerRequirement.Required;

public override void VisitFunctionCall(FunctionCallNode node)
{
    if (DataManager == null) return;

    // Query function definition from database
    var funcInfo = DataManager.GetFunctionInfo(node.FunctionName);

    if (funcInfo != null)
    {
        // Analyze cross-file usage
    }

    base.VisitFunctionCall(node);
}
```

### Custom Settings

Store plugin settings using SettingsService:

```csharp
public class MyConfigurableLinter : BaseLintRule
{
    public int MaxComplexity { get; set; } = 10;

    public MyConfigurableLinter()
    {
        // Load from settings
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        // Access app settings or use custom config file
        var config = ConfigurationManager.AppSettings["MyLinter.MaxComplexity"];
        if (int.TryParse(config, out int value))
        {
            MaxComplexity = value;
        }
    }
}
```

### Quick Fixes

Stylers can offer quick fixes:

```csharp
public class MyStylerWithFixes : BaseStyler
{
    public override List<QuickFix> GetQuickFixes(ScintillaEditor editor, int position)
    {
        var fixes = new List<QuickFix>();

        // Find indicators at position
        var indicator = GetIndicatorAt(position);
        if (indicator != null)
        {
            fixes.Add(new QuickFix
            {
                Title = "Fix Issue",
                Description = "Applies automatic fix",
                Action = () => ApplyFix(editor, indicator)
            });
        }

        return fixes;
    }

    private void ApplyFix(ScintillaEditor editor, IndicatorInfo indicator)
    {
        // Implement fix logic
    }
}
```

---

## Example: Complete Plugin

Here's a complete, real-world plugin that detects magic numbers:

```csharp
using AppRefiner.Linters;
using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes;

namespace MagicNumberPlugin
{
    /// <summary>
    /// Linter that detects magic numbers (hard-coded numeric literals)
    /// </summary>
    public class MagicNumberLinter : BaseLintRule
    {
        public override string LINTER_ID => "MAGIC_NUMBER";

        private readonly HashSet<string> allowedNumbers = new()
        {
            "0", "1", "-1", "100", "1000"
        };

        public MagicNumberLinter()
        {
            Description = "Detects magic numbers that should be constants";
            Type = ReportType.Warning;
            Active = true;
        }

        public override void VisitLiteral(LiteralNode node)
        {
            if (node.LiteralType == LiteralType.Number)
            {
                var value = node.Value?.ToString() ?? "";

                if (!allowedNumbers.Contains(value) && !IsInDeclaration(node))
                {
                    AddReport(
                        1,
                        $"Magic number '{value}' should be defined as a constant",
                        ReportType.Warning,
                        node.SourceSpan.Start.Line,
                        node.SourceSpan
                    );
                }
            }

            base.VisitLiteral(node);
        }

        private bool IsInDeclaration(AstNode node)
        {
            // Check if this literal is part of a constant declaration
            return node.FindAncestor<ConstantNode>() != null;
        }

        public override void Reset()
        {
            base.Reset();
        }
    }

    /// <summary>
    /// Styler that highlights magic numbers
    /// </summary>
    public class MagicNumberStyler : BaseStyler
    {
        private const uint MAGIC_NUMBER_COLOR = 0xFFE0B0; // Light orange

        private readonly HashSet<string> allowedNumbers = new()
        {
            "0", "1", "-1", "100", "1000"
        };

        public MagicNumberStyler()
        {
            Description = "Highlights magic numbers";
            Active = true;
        }

        public override string Description { get; }

        public override void VisitLiteral(LiteralNode node)
        {
            if (node.LiteralType == LiteralType.Number)
            {
                var value = node.Value?.ToString() ?? "";

                if (!allowedNumbers.Contains(value) && !IsInDeclaration(node))
                {
                    AddIndicator(
                        node.SourceSpan,
                        IndicatorType.HIGHLIGHTER,
                        MAGIC_NUMBER_COLOR,
                        $"Magic number: {value}"
                    );
                }
            }

            base.VisitLiteral(node);
        }

        private bool IsInDeclaration(AstNode node)
        {
            return node.FindAncestor<ConstantNode>() != null;
        }
    }
}
```

---

## Troubleshooting

### Plugin Doesn't Load

**Problem**: Plugin DLL not appearing in Plugin Manager.

**Solutions**:
1. Check Plugins directory path is correct
2. Verify DLL is .NET 8.0 assembly (not .NET Framework)
3. Review debug log for load errors
4. Ensure no missing dependencies
5. Try copying to default Plugins directory

---

### Components Don't Appear in Settings

**Problem**: Plugin loads but linters/stylers don't show in Settings.

**Solutions**:
1. Verify class is public: `public class MyLinter`
2. Verify class is not abstract
3. Check inheritance: must inherit from base class
4. Ensure parameterless constructor exists
5. Reload plugins via Plugin Manager
6. Check debug log for discovery errors

---

### Runtime Exceptions

**Problem**: Plugin crashes or throws exceptions during use.

**Solutions**:
1. Add null checks for all AST node properties
2. Validate SourceSpan before accessing
3. Use try-catch for defensive programming
4. Test with various code samples
5. Review debug log for stack traces
6. Attach debugger and reproduce

---

### Performance Issues

**Problem**: Editor becomes slow with plugin enabled.

**Solutions**:
1. Profile your visitor methods - are you doing too much work?
2. Cache expensive operations
3. Minimize database queries
4. Use lazy evaluation
5. Only process relevant node types
6. Consider async operations for heavy work

---

## Frequently Asked Questions

### Q: Can plugins access the file system?
**A**: Yes, plugins are standard .NET assemblies with full file system access. Use `System.IO` namespaces.

### Q: Can plugins make network requests?
**A**: Yes, use standard .NET HTTP clients. Be mindful of performance and user privacy.

### Q: Can plugins create UI dialogs?
**A**: Yes, use Windows Forms or WPF. Reference appropriate assemblies in your plugin project.

### Q: How do I distribute my plugin?
**A**: Distribute the compiled DLL file. Users copy it to their Plugins directory. Consider publishing on GitHub or NuGet.

### Q: Can multiple plugins conflict?
**A**: Generally no. Each plugin operates independently. Avoid global state and static mutable fields.

### Q: Can I sell commercial plugins?
**A**: Check AppRefiner's license. Generally, plugins are your intellectual property.

### Q: How do I update an existing plugin?
**A**: Replace the DLL file and reload plugins. Consider version numbers for tracking.

### Q: Can plugins extend other plugins?
**A**: Not directly, but you can reference other plugin DLLs if needed.

---

## Related Documentation

- **[BaseLintRule API](../api/base-lint-rule.md)**: Linter base class reference
- **[BaseStyler API](../api/base-styler.md)**: Styler base class reference
- **[BaseRefactor API](../api/base-refactor.md)**: Refactor base class reference
- **[BaseTooltipProvider API](../api/base-tooltip-provider.md)**: Tooltip provider base class reference
- **[AST Node Reference](../api/ast-nodes.md)**: Complete AST node documentation
- **[CLAUDE.md](../../CLAUDE.md)**: Architecture and development guide

---

## Next Steps

1. **Study PluginSample**: Review the sample plugin project in the AppRefiner repository
2. **Start Small**: Create a simple linter or styler to learn the basics
3. **Test Thoroughly**: Use various PeopleCode samples to test your plugin
4. **Share**: Publish useful plugins to benefit the PeopleSoft community
5. **Contribute**: Submit plugins to the AppRefiner repository

---

*For more information on AppRefiner architecture and development, see [CLAUDE.md](../../CLAUDE.md) and the [Developer Guide](../developer-guide/).*
