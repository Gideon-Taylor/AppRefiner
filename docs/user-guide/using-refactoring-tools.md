# Using Refactoring Tools

AppRefiner provides powerful refactoring tools that help you improve your PeopleCode without manually editing every occurrence. This guide explains how to use these tools effectively.

## What is Refactoring?

Refactoring is the process of restructuring existing code without changing its external behavior. The goal is to improve the code's internal structure, making it more readable, maintainable, and less prone to bugs.

## Invoking Refactoring Tools

Refactoring tools are primarily accessed through:

- **Command Palette**: Press `Ctrl+Shift+P` and type "Refactor:" to see available refactoring commands.
- **Keyboard Shortcuts**: Some refactorings have dedicated shortcuts (listed below).

## Available Refactoring Tools

AppRefiner includes the following refactoring tools:

### Rename

Renames various code elements, ensuring all references within the correct scope are updated.

- **Elements**: Local variables, instance variables, method/function parameters, private methods.
  *Note: Due to the complexities of analyzing external references without a full project view, renaming is generally limited to elements defined and used within the current file (e.g., local variables, parameters, private instance members/methods). Renaming public class members or elements with potential external usage may not update all references correctly.*
- **Usage**: Place your cursor on the element you want to rename, then use the shortcut `Ctrl+Shift+R` or run the `Refactor: Rename` command from the palette. Enter the new name in the dialog that appears.

### Resolve Imports

Automatically adds missing `import` statements at the top of your code for referenced Application Classes.

- **Usage**: Press `Ctrl+Shift+I` or run the `Refactor: Resolve Imports` command from the palette. AppRefiner analyzes the code and adds necessary imports.

### Add Import

Allows you to manually add a specific import statement.

- **Usage**: Run the `Refactor: Add Import` command from the palette. You will likely be prompted to enter the class or package path to import.

### Sort Methods

Organizes the methods and functions within a PeopleCode program or class, typically alphabetically.

- **Usage**: Run the `Refactor: Sort Methods` command from the palette.

### Add Flower Box

Adds a standardized documentation header (flower box) comment to the beginning of your PeopleCode file.

- **Usage**: Run the `Refactor: Add Flower Box` command from the palette. A template header is inserted automatically (no user input needed).

### Create AutoComplete

Generates method stubs for `AutoComplete` PeopleSoft events, if applicable to the current context.

- **Usage**: Run the `Refactor: Create AutoComplete` command from the palette.

### Suppress Linter Reports

Adds a suppression comment to disable specific linter reports for a given scope.

- **Usage**:
    1. Position your cursor on the line that has the linter report(s) you want to suppress.
    2. Run the `Refactor: Suppress Report` command from the palette.
    3. In the dialog, select the desired suppression scope (Line, Nearest Block, Method/Function, Global).
    4. Click OK to insert the `/* #AppRefiner suppress (...) */` comment.
- **Note**: This suppresses all reports on the target line for the chosen scope.

## Undo Refactoring

If you're not satisfied with a refactoring result, you have a couple of options:

1.  **Application Designer Undo**: Press `Ctrl+Z` immediately after the refactoring. This uses Application Designer's built-in undo stack.
2.  **AppRefiner Snapshots**: Use the `Snapshot: Revert File` command from the Command Palette to restore the editor contents to a previously saved state.
    - **Important Note**: Currently, AppRefiner does *not* automatically create a snapshot just before performing a refactoring operation. You may want to save manually before complex refactorings if you anticipate needing to revert using snapshots.

## Next Steps

To learn about code styling features in AppRefiner, proceed to the [Code Styling](../features/code-styling.md) section.
