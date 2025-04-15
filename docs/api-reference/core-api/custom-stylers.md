# Custom Stylers

The Styler API provides interfaces and classes for implementing code styling and visual analysis plugins in AppRefiner. This document describes the core components of the Styler API and how to create custom styler plugins.

## Overview

Custom Stylers allow you to extend AppRefiner's visual rendering of code in Application Designer. You can use them to:

-   Highlight specific code patterns or keywords with background colors.
-   Change the text color of certain code elements.
-   Analyze code structure (optionally with scope awareness) to apply styling (e.g., graying out unused variables).

Stylers run automatically in the background when AppRefiner detects a pause in typing, and their effects are applied visually to the editor.

## Creating a Custom Styler Plugin

### 1. Project Setup

-   Create a new .NET Class Library project (targeting .NET Framework 4.8 or compatible).
-   Add references to necessary AppRefiner assemblies:
    -   `AppRefiner.exe` (or the core assembly containing the API classes)
    -   `Antlr4.Runtime.Standard.dll` (Bundled with AppRefiner or via NuGet)
    -   `PeopleCodeParser.dll` (Bundled with AppRefiner)

### 2. Implement the Styler Class

Choose the appropriate base class depending on whether you need scope awareness:

-   **`AppRefiner.Stylers.BaseStyler`**: For stylers performing context-free analysis (e.g., highlighting specific keywords or comment types). Inherits from `PeopleCodeParserBaseListener`.
-   **`AppRefiner.Stylers.ScopedStyler<T>`**: For stylers needing to track variable declarations and usage across scopes (e.g., identifying unused variables). Inherits from `BaseStyler` and adds scope management.

**Example: Basic Keyword Styler (using `BaseStyler`)**

```csharp
using AppRefiner.Stylers;
using AppRefiner.PeopleCode;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser; // For context types if needed

namespace MyCompany.AppRefiner.CustomStylers
{
    public class HighlightTodoStyler : BaseStyler
    {
        public HighlightTodoStyler()
        {
            Description = "Highlights TODO Keywords";
            Active = true; // Enabled by default
        }

        // VisitTerminal is often useful for keyword/token-based styling
        public override void VisitTerminal(ITerminalNode node)
        {
            // Check if the token type matches the TODO keyword from the lexer
            if (node.Symbol.Type == PeopleCodeLexer.TODO) 
            {
                // Add an Indicator to the list to apply styling
                Indicators?.Add(new Indicator
                {
                    Start = node.Symbol.StartIndex, // 0-based start index
                    Length = node.Symbol.StopIndex - node.Symbol.StartIndex + 1, // Length of the token
                    Color = 0xFFFF8C00, // Orange background (BGRA format: 0xAABBGGRR)
                    Type = IndicatorType.HIGHLIGHTER, // Apply background highlight
                    Tooltip = "TODO item detected" // Optional tooltip on hover
                });
            }
        }
        
        // Reset state before processing a new file
        public override void Reset()
        {
            // Crucial to call base.Reset() to clear the Indicators list
            base.Reset(); 
        }
    }
}
```

**Example: Unused Variable Styler (using `ScopedStyler<T>`)**

```csharp
using AppRefiner.Stylers;
using AppRefiner.Linters; // For VariableInfo (might be in a shared namespace)
using AppRefiner.PeopleCode;
using System.Collections.Generic;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace MyCompany.AppRefiner.CustomStylers
{
    // T can be 'object' if no extra data per scope is needed
    public class UnusedVariableStyler : ScopedStyler<object> 
    {
        public UnusedVariableStyler()
        {
            Description = "Grays out unused local/instance variables";
            Active = true;
        }

        // Need to override listener methods to track variable usage
        public override void EnterPrimaryExpr(PrimaryExprContext context)
        { 
            // If a variable name is used in an expression, mark it as used
            if (context.variableName() != null)
            {
                MarkVariableAsUsed(context.variableName().GetText());
            }
            // Important to call base method if the base class implements logic here
            base.EnterPrimaryExpr(context);
        }

        // This method is called automatically when exiting a scope (function, loop, etc.)
        protected override void OnExitScope(Dictionary<string, object> scopeData, Dictionary<string, VariableInfo> variableScope)
        { 
            // variableScope contains only variables declared *within* the scope being exited
            foreach (var variable in variableScope.Values)
            {
                if (!variable.Used)
                {
                    // Add an indicator to change text color for unused variables
                    Indicators?.Add(new Indicator()
                    {
                        Start = variable.Span.Start, // Span comes from VariableInfo captured during declaration
                        Length = variable.Span.Stop - variable.Span.Start + 1,
                        Color = 0x80808080, // Gray with 50% Alpha (BGRA: 0xAABBGGRR)
                        Type = IndicatorType.TEXTCOLOR, // Change text color
                        Tooltip = $"Variable '{variable.Name}' is declared but not used in this scope."
                    });
                }
            }
        }

        // Reset is needed to clear state between files
        public override void Reset()
        {
            // base.Reset() clears Indicators and also resets the scope stacks
            base.Reset(); 
        }
    }
}
```

### 3. Implement Analysis Logic

-   Override ANTLR listener methods (`Enter*`, `Exit*`, `VisitTerminal`) to traverse the parse tree created from the editor content.
-   Use the `context` or `node` parameter to inspect code elements.
-   If using `ScopedStyler`, call `AddLocalVariable` when detecting declarations and `MarkVariableAsUsed` when detecting usage. Implement the `OnExitScope` method for end-of-scope analysis.
-   Add `Indicator` structs to the `Indicators` list to apply visual changes.

### 4. Compile the Plugin

-   Build your Class Library project, generating a DLL (e.g., `MyCompany.AppRefiner.CustomStylers.dll`).

## Key API Components Reference

### `BaseStyler` Class

Inherits `PeopleCodeParserBaseListener`.

-   **`Description` (string)**: REQUIRED (set in constructor). User-friendly name for UI.
-   **`Active` (bool)**: REQUIRED (set in constructor). Default enabled state.
-   **`DatabaseRequirement` (DataManagerRequirement, virtual override)**: OPTIONAL. Specify if DB access is needed (default `NotRequired`). See [Database API](database-api.md).
-   **`DataManager` (IDataManager?)**: Instance provided if DB is required/optional and connected.
-   **`Indicators` (List<Indicator>?)**: Add `Indicator` structs here.
-   **`Comments` (IList<IToken>?)**: Access comment tokens.
-   **`Reset()` (virtual)**: Override to clear custom state. **Must call `base.Reset()`** to clear `Indicators` list.

### `ScopedStyler<T>` Class

Inherits `BaseStyler`. Adds scope tracking.

-   **`AddLocalVariable(...)`**: Protected. Call this when a variable is declared.
-   **`TryGetVariableInfo(...)`**: Protected. Check if variable exists.
-   **`MarkVariableAsUsed(...)`**: Protected. Call when a variable is referenced.
-   **`OnExitScope(..., Dictionary<string, VariableInfo> variableScope)` (abstract override)**: REQUIRED. Implement logic here using `variableScope` (variables declared *in* the exited scope).
-   **(Scope Enter/Exit Methods)**: Automatically handles pushing/popping scope stacks for common blocks (functions, methods, loops, etc.).

### `Indicator` Struct

Defines a visual change to apply.

-   **`Start` (int)**: 0-based start index.
-   **`Length` (int)**: Span length.
-   **`Color` (uint)**: BGRA color (e.g., `0xAABBGGRR`).
-   **`Type` (IndicatorType)**: `HIGHLIGHTER` or `TEXTCOLOR`.
-   **`Tooltip` (string?)**: Optional hover text.

### `IndicatorType` Enum

-   `HIGHLIGHTER`: Background color change.
-   `SQUIGGLE`: Underline (usually for Linters).
-   `TEXTCOLOR`: Foreground text color change.

### `VariableInfo` Class (Used by `ScopedStyler`)

Stores info about a variable declaration.

-   `Name`, `Type`, `Line`, `Span` (declaration location), `Used` (bool).

## Installing the Plugin

1.  Compile your custom styler(s) into a DLL.
2.  Place the DLL in the AppRefiner **plugins directory**.
3.  Restart AppRefiner.

The plugins directory location is configured on the **Settings Tab** in AppRefiner.

## Managing Custom Stylers

Installed stylers appear in the **Stylers Tab**. Enable/disable them via the checkbox or the `Styler: Toggle [Your Styler Description]` command in the Command Palette.

## Best Practices

-   **Performance**: Stylers run often during typing pauses; keep them fast.
-   **Clarity**: Ensure visual changes are understandable and don't make code *harder* to read.
-   **Subtlety**: Often less is more; avoid overly aggressive styling.
-   **Choose Base Class Wisely**: Use `ScopedStyler` only if scope/variable tracking is truly needed.
-   **Reset State**: Always implement `Reset()` and call `base.Reset()`. 