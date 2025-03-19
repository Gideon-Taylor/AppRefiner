# Refactor API

The Refactor API provides a framework for implementing automated code transformations in AppRefiner. This document describes the core components of the Refactor API and how to use them effectively.

## Overview

The Refactor API is built on top of the ANTLR4 parsing framework and provides a structured way to modify PeopleCode source files. The main components of this API are:

- `BaseRefactor`: The abstract base class for all refactoring operations
- `RefactorResult`: Represents the outcome of a refactoring operation
- `CodeChange`: Abstract base class for all types of code changes
- Concrete change classes: `InsertChange`, `DeleteChange`, and `ReplaceChange`

## RefactorResult

The `RefactorResult` class represents the outcome of a refactoring operation:

```csharp
public class RefactorResult
{
    public bool Success { get; }
    public string? Message { get; }

    public RefactorResult(bool success, string? message = null)
    {
        Success = success;
        Message = message;
    }

    public static RefactorResult Successful => new(true);
    public static RefactorResult Failed(string message) => new(false, message);
}
```

### Key Properties and Methods

- `Success`: Indicates whether the refactoring was successful
- `Message`: Optional message providing details about the result
- `Successful`: Static factory method for creating a successful result
- `Failed`: Static factory method for creating a failed result with an error message

## CodeChange

The `CodeChange` abstract class is the base for all types of source code modifications:

```csharp
public abstract class CodeChange
{
    public int StartIndex { get; }
    public string Description { get; }

    protected CodeChange(int startIndex, string description)
    {
        StartIndex = startIndex;
        Description = description;
    }

    public abstract void Apply(StringBuilder source);
    public abstract int UpdateCursorPosition(int cursorPosition);
}
```

### Key Properties and Methods

- `StartIndex`: The starting position in the source code where the change begins
- `Description`: A human-readable description of what the change does
- `Apply`: Abstract method that applies the change to a StringBuilder
- `UpdateCursorPosition`: Abstract method that calculates how this change affects cursor position

## Concrete Change Classes

### InsertChange

Inserts new text at a specific position:

```csharp
public class InsertChange : CodeChange
{
    public string TextToInsert { get; }

    public InsertChange(int startIndex, string textToInsert, string description)
        : base(startIndex, description)
    {
        TextToInsert = textToInsert;
    }

    public override void Apply(StringBuilder source)
    {
        source.Insert(StartIndex, TextToInsert);
    }

    public override int UpdateCursorPosition(int cursorPosition)
    {
        if (cursorPosition < StartIndex)
            return cursorPosition;
        else
            return cursorPosition + TextToInsert.Length;
    }
}
```

### DeleteChange

Removes text from the source code:

```csharp
public class DeleteChange : CodeChange
{
    public int EndIndex { get; }
    public int DeleteLength => EndIndex - StartIndex + 1;

    public DeleteChange(int startIndex, int endIndex, string description)
        : base(startIndex, description)
    {
        EndIndex = endIndex;
    }

    public override void Apply(StringBuilder source)
    {
        source.Remove(StartIndex, DeleteLength);
    }

    public override int UpdateCursorPosition(int cursorPosition)
    {
        if (cursorPosition <= StartIndex)
            return cursorPosition;
        else if (cursorPosition <= EndIndex)
            return StartIndex;
        else
            return cursorPosition - DeleteLength;
    }
}
```

### ReplaceChange

Replaces a section of text with new content:

```csharp
public class ReplaceChange : CodeChange
{
    public int EndIndex { get; }
    public string NewText { get; }
    public int ReplaceLength => EndIndex - StartIndex + 1;

    public ReplaceChange(int startIndex, int endIndex, string newText, string description)
        : base(startIndex, description)
    {
        EndIndex = endIndex;
        NewText = newText;
    }

    public override void Apply(StringBuilder source)
    {
        source.Remove(StartIndex, ReplaceLength);
        source.Insert(StartIndex, NewText);
    }

    public override int UpdateCursorPosition(int cursorPosition)
    {
        if (cursorPosition <= StartIndex)
            return cursorPosition;
        else if (cursorPosition <= EndIndex)
            return StartIndex + NewText.Length;
        else
            return cursorPosition - ReplaceLength + NewText.Length;
    }
}
```

## BaseRefactor

The `BaseRefactor` class is the foundation for all refactoring operations in AppRefiner:

```csharp
public abstract class BaseRefactor
{
    /// <summary>
    /// Gets the display name for this refactor
    /// </summary>
    public static string RefactorName => "Base Refactor";

    /// <summary>
    /// Gets the description for this refactor
    /// </summary>
    public static string RefactorDescription => "Base refactoring operation";

    /// <summary>
    /// Gets whether this refactor requires a user input dialog
    /// </summary>
    public virtual bool RequiresUserInputDialog => false;

    /// <summary>
    /// Gets whether this refactor should have a keyboard shortcut registered
    /// </summary>
    public static bool RegisterKeyboardShortcut => false;

    /// <summary>
    /// Gets the keyboard shortcut modifier keys for this refactor
    /// </summary>
    public static ModifierKeys ShortcutModifiers => ModifierKeys.Control;

    /// <summary>
    /// Gets the keyboard shortcut key for this refactor
    /// </summary>
    public static Keys ShortcutKey => Keys.None;

    /// <summary>
    /// Shows the dialog for this refactor
    /// </summary>
    /// <returns>True if the user confirmed, false if canceled</returns>
    public virtual bool ShowRefactorDialog()
    {
        // Base implementation just returns true (no dialog needed)
        return true;
    }

    // Initialize the refactor with source code and token stream
    public virtual void Initialize(string sourceCode, CommonTokenStream tokens, int? cursorPosition = null);

    // Set a failure status with an error message
    protected void SetFailure(string message);

    // Get the result of the refactoring operation
    public RefactorResult GetResult();

    // Get the refactored source code with all changes applied
    public string GetRefactoredCode();

    // Get the updated cursor position after refactoring
    public int GetUpdatedCursorPosition();

    // Get the list of changes that will be applied
    public IReadOnlyList<CodeChange> GetChanges();

    // Add a new replacement change using parser context
    protected void ReplaceNode(ParserRuleContext context, string newText, string description);

    // Add a replacement change with explicit start and end positions
    protected void ReplaceText(int startIndex, int endIndex, string newText, string description);

    // Add a new insertion change
    protected void InsertText(int position, string textToInsert, string description);

    // Add a new insertion change after a parser rule context
    protected void InsertAfter(ParserRuleContext context, string textToInsert, string description);

    // Add a new insertion change before a parser rule context
    protected void InsertBefore(ParserRuleContext context, string textToInsert, string description);

    // Add a new deletion change
    protected void DeleteText(int startIndex, int endIndex, string description);

    // Add a new deletion change to remove a parser rule context
    protected void DeleteNode(ParserRuleContext context, string description);

    // Get the original text for a parser rule context
    protected string GetOriginalText(ParserRuleContext context);
}
```

### Key Methods

- `Initialize`: Sets up the refactor with source code and token stream
- `GetResult`: Returns the result of the refactoring operation
- `GetRefactoredCode`: Returns the modified source code
- `GetUpdatedCursorPosition`: Returns the new cursor position after refactoring
- `RequiresUserInputDialog`: Indicates whether this refactor needs user input via a dialog
- `ShowRefactorDialog`: Shows a dialog to gather user input for the refactoring operation
- Helper methods for adding different types of changes:
  - `ReplaceNode`/`ReplaceText`: Replace text
  - `InsertText`/`InsertAfter`/`InsertBefore`: Insert text
  - `DeleteText`/`DeleteNode`: Delete text
- `GetOriginalText`: Extracts the original text from a parser rule context

## Dialog-Driven Refactoring

AppRefiner supports a "bring your own UI" pattern for refactoring operations that require user input:

1. Override the `RequiresUserInputDialog` property to return `true` in your refactor class
2. Implement the `ShowRefactorDialog` method to display your custom dialog
3. Use the dialog result to configure your refactoring operation

Example:

```csharp
public class MyCustomRefactorWithDialog : BaseRefactor
{
    private string newName;
    
    // Indicate that this refactor needs a dialog
    public override bool RequiresUserInputDialog => true;
    
    // Implement the dialog display method
    public override bool ShowRefactorDialog()
    {
        using var dialog = new MyCustomDialog();
        
        // Show dialog with the editor's main window as owner
        var wrapper = new WindowWrapper(GetEditorMainWindowHandle());
        DialogResult result = dialog.ShowDialog(wrapper);
        
        // If user confirmed, update the refactor parameters
        if (result == DialogResult.OK)
        {
            newName = dialog.EnteredName;
            return true;
        }
        
        return false;
    }
    
    // Rest of the refactor implementation...
}
```

## Creating a Custom Refactoring

To create a custom refactoring, extend the `BaseRefactor` class:

```csharp
public class MyCustomRefactor : BaseRefactor
{
    public override void Initialize(string sourceCode, CommonTokenStream tokens, int? cursorPosition = null)
    {
        // Call the base implementation first
        base.Initialize(sourceCode, tokens, cursorPosition);

        try
        {
            // Parse the source code
            var lexer = new PeopleCodeLexer(new AntlrInputStream(sourceCode));
            var parser = new PeopleCodeParser(new CommonTokenStream(lexer));
            var tree = parser.program();

            // Create a visitor to find the elements to refactor
            var visitor = new MyCustomVisitor(this);
            visitor.Visit(tree);

            // If no changes were made, set a failure message
            if (GetChanges().Count == 0)
            {
                SetFailure("No applicable code found to refactor");
            }
        }
        catch (Exception ex)
        {
            SetFailure($"Error during refactoring: {ex.Message}");
        }
    }

    // Custom visitor class to find elements to refactor
    private class MyCustomVisitor : PeopleCodeParserBaseVisitor<object?>
    {
        private readonly MyCustomRefactor _refactor;

        public MyCustomVisitor(MyCustomRefactor refactor)
        {
            _refactor = refactor;
        }

        // Override visitor methods to find and modify code
        public override object? VisitSomeRule(PeopleCodeParser.SomeRuleContext context)
        {
            // Add changes using the refactor's helper methods
            _refactor.ReplaceNode(context, "new code", "Replaced old code with new code");
            return null;
        }
    }
}
```

## Best Practices

1. **Atomic Changes**: Make each refactoring operation perform a single, well-defined change
2. **Preview**: Always provide a preview of changes before applying them
3. **Undo Support**: Ensure refactorings can be undone
4. **Error Handling**: Handle parsing errors gracefully
5. **Documentation**: Document what your refactoring does and when it should be used
6. **User Input**: For refactorings that require parameters, use the dialog-driven approach

## See Also

- [Linter API](linter-api.md)
- [Styler API](styler-api.md)
- [Creating Custom Refactors](../extension-api/custom-refactors.md)
