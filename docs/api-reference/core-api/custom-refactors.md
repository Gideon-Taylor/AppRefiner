# Custom Refactors

The Refactor API provides a framework for implementing automated code transformation plugins in AppRefiner. This document describes how to create custom refactoring plugins.

## Overview

Custom refactors allow you to define specific, automated code changes that can be invoked by users via the Command Palette or keyboard shortcuts. Examples include renaming variables, adding standard comments, sorting methods, or any other rule-based code modification.

The Refactor API uses the ANTLR4 parser to analyze the code structure and provides base classes and helper methods to define and apply changes safely.

## Creating a Custom Refactor Plugin

### 1. Project Setup

-   Create a new .NET Class Library project (targeting .NET Framework 4.8 or compatible).
-   Add references to necessary AppRefiner assemblies:
    -   `AppRefiner.exe` (or core assembly)
    -   `Antlr4.Runtime.Standard.dll`
    -   `PeopleCodeParser.dll`
    -   `System.Windows.Forms` (if defining shortcuts or using dialogs)

### 2. Implement the Refactor Class

Custom refactors are created by extending the abstract `AppRefiner.Refactors.ScopedRefactor` class. You typically override AST visitor methods to find the code elements you want to modify and then call helper methods (`EditText`, `InsertText`, `DeleteText`) to stage the changes.

**Example: Simple Comment Replacer**

```csharp
using AppRefiner.Refactors;
using AppRefiner.PeopleCode;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Windows.Forms; // For Keys enum

namespace MyCompany.AppRefiner.CustomRefactors
{
    public class StandardizeCommentRefactor : ScopedRefactor
    {
        // --- Static Properties for Discovery ---
        
        public new static string RefactorName => "Standardize Comment"; 
        public new static string RefactorDescription => "Replaces /* TODO */ comments with standard format.";

        // Optional Shortcut:
        // public static new bool RegisterKeyboardShortcut => true; 
        // public static new ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Shift;
        // public static new Keys ShortcutKey => Keys.C;

        // --- Constructor ---
        public StandardizeCommentRefactor(ScintillaEditor editor) : base(editor) { }

        // --- ANTLR Listener Implementation ---
        
        public override void VisitTerminal(ITerminalNode node)
        {
            // Check if it's a comment token and matches the target text
            if (node.Symbol.Channel == PeopleCodeLexer.COMMENTS && 
                node.GetText().Trim() == "/* TODO */")
            {
                // Find the context (needed for ReplaceNode)
                var context = FindContext(node);
                if (context != null)
                {   
                    // Stage a replacement change
                    ReplaceNode(context, 
                                "/* TODO: [Description] - [Your Name] - [Date] */", 
                                "Standardize TODO comment");
                }
            }
        }

        // Helper to find context (may not always be reliable)
        private ParserRuleContext? FindContext(ITerminalNode node)
        {
             return node.Parent as ParserRuleContext;
        }
    }
}
```

### 3. Implement Analysis & Changes

-   Override ANTLR listener methods to find relevant code constructs.
-   Use helper methods like `ReplaceNode`, `InsertText`, `DeleteNode` to stage modifications.
-   These helpers add `CodeChange` objects to an internal list.

### 4. (Optional) Add User Input

-   If your refactor needs user input (e.g., a new name for a rename refactor):
    -   Set `public override bool RequiresUserInputDialog => true;`
    -   Optionally set `public override bool DeferDialogUntilAfterVisitor => true;` if you need analysis results *before* showing the dialog.
    -   Override `public override bool ShowRefactorDialog()` to display a custom `System.Windows.Forms.Form`. Capture input within the form and return `true` if the user clicks OK/confirms, `false` if they cancel.

### 5. Compile the Plugin

-   Build your Class Library project to produce a DLL.

## Key API Components Reference

### `ScopedRefactor` Class

Abstract base class for refactorings. Inherits from `ScopedAstVisitor<object>` and provides automatic scope and variable tracking.

**Static Properties (for discovery by AppRefiner):**
-   `RefactorName` (string): REQUIRED. Name shown after "Refactor:" in Command Palette.
-   `RefactorDescription` (string): REQUIRED. Description shown in Command Palette.
-   `IsHidden` (bool): OPTIONAL. Set to `true` to hide from Command Palette (useful for internal refactors).
-   `RegisterKeyboardShortcut` (bool): OPTIONAL. Set to `true` to register a shortcut.
-   `ShortcutModifiers` (ModifierKeys): OPTIONAL. Modifier key(s) for shortcut.
-   `ShortcutKey` (Keys): OPTIONAL. Main key for shortcut.

**Instance Properties/Methods (for execution flow):**
-   `RequiresUserInputDialog` (bool, virtual): OPTIONAL. Set `true` if you override `ShowRefactorDialog`.
-   `DeferDialogUntilAfterVisitor` (bool, virtual): OPTIONAL. Controls *when* `ShowRefactorDialog` is called relative to tree traversal.
-   `ShowRefactorDialog()` (bool, virtual): OPTIONAL. Override to display a custom input Form.
-   `Initialize(...)`: Called by AppRefiner internally.
-   `SetFailure(string message)`: Protected. Call this if the refactor cannot proceed (e.g., invalid context, user cancellation).
-   `GetResult()` (RefactorResult): Returns success/failure status after execution.
-   `GetRefactoredCode()` (string): Returns the fully modified code string.
-   `GetUpdatedCursorPosition()` (int): Returns the calculated cursor position after changes.
-   `GetChanges()` (IReadOnlyList<CodeChange>): Returns the list of staged `CodeChange` objects.

**Protected Helper Methods (Call these during tree traversal to stage changes):**
-   `EditText(int startIndex, int endIndex, string newText, string description)`
-   `EditText(SourceSpan span, string newText, string description)`
-   `InsertText(int position, string text, string description)`
-   `InsertText(SourcePosition position, string text, string description)`
-   `DeleteText(int startIndex, int endIndex, string description)`
-   `DeleteText(SourceSpan span, string description)`
-   `SetFailure(string message)` - Mark the refactor as failed

### `RefactorResult` Class

Represents the outcome of the refactoring attempt.
-   `Success` (bool): `true` if no failure was set and (if applicable) dialog was confirmed.
-   `Message` (string?): Optional message, usually set with `SetFailure`.
-   `RefactorResult.Successful` / `RefactorResult.Failed(string message)`: Static helpers.

### `CodeChange` Class Hierarchy

Represent individual staged modifications.
-   `CodeChange` (abstract): Base for all changes.
-   `InsertChange`: Holds `TextToInsert`.
-   `DeleteChange`: Holds `StartIndex`, `EndIndex`.
-   `ReplaceChange`: Holds `StartIndex`, `EndIndex`, `NewText`.
*Each change class knows how to apply itself to a `StringBuilder` and update cursor positions.* 

## Installing the Plugin

1.  Compile your custom refactor(s) into a DLL.
2.  Place the DLL in the AppRefiner **plugins directory**.
3.  Restart AppRefiner.

The plugins directory location is configured on the **Settings Tab** in AppRefiner.

## Managing Custom Refactors

Installed refactors appear in the **Command Palette** (`Ctrl+Shift+P`) prefixed with "Refactor:". If a shortcut was registered, it will also be active. Refactors do not appear in the main AppRefiner window tabs.

## Best Practices

-   **Focus**: Make refactors perform a single, well-defined task.
-   **Safety**: Prioritize correctness. Ensure generated code is valid.
-   **Clarity**: Use clear names and descriptions for the Command Palette.
-   **User Experience**: Avoid unnecessary dialogs. If input is needed, make the dialog clear and simple.
-   **Idempotency (Optional but nice)**: If possible, running the refactor multiple times should ideally produce the same result as running it once.