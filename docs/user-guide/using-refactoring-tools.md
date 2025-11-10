# Using Refactoring Tools

AppRefiner provides powerful refactoring tools that help you improve your PeopleCode without manually editing every occurrence. This guide explains how to use these tools effectively.

## What is Refactoring?

Refactoring is the process of restructuring existing code without changing its external behavior. The goal is to improve the code's internal structure, making it more readable, maintainable, and less prone to bugs.

## Types of Refactoring Tools

AppRefiner provides two categories of refactoring tools:

1. **User-Triggered Refactors** (documented in this guide): Can be invoked manually through the Command Palette or keyboard shortcuts
2. **Quick Fixes** (see [Quick Fixes documentation](../features/quick-fixes.md)): Auto-suggested refactors that appear only when the cursor is on code with a styler issue and are invoked with `Ctrl+.`

## Invoking Refactoring Tools

User-triggered refactoring tools are accessed through:

- **Command Palette**: Press `Ctrl+Shift+P` and type "Refactor:" to see available refactoring commands
- **Keyboard Shortcuts**: Some refactorings have dedicated shortcuts (listed below)

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

A shorthand to make create APP:CLASS() easier. Just type `create()` when you would normally type `create APP:CLASS(&params)`, and this refactor will expand it for you.
Variables are automatically placed in the constructor that match the parameter names.

- **Usage**: This automatically expands when present and valid in the code.
- **Example**:
  ```peoplecode
  /* Before */
  Local MY_PACKAGE:MY_CLASS &obj = create()

  /* After */
  Local MY_PACKAGE:MY_CLASS &obj = create MY_PACKAGE:MY_CLASS(&param1, &param2)
  ```

- **Usage**: Run the `Refactor: Create AutoComplete` command from the palette.

### Suppress Linter Reports

Adds a suppression comment to disable specific linter reports for a given scope.

- **Usage**:
    1. Position your cursor on the line that has the linter report(s) you want to suppress.
    2. Run the `Refactor: Suppress Report` command from the palette.
    3. In the dialog, select the desired suppression scope (Line, Nearest Block, Method/Function, Global).
    4. Click OK to insert the `/* #AppRefiner suppress (...) */` comment.
- **Note**: This suppresses all reports on the target line for the chosen scope.

### Concat Auto Complete

Expands the `+=` operator for string concatenation into explicit concatenation with the `|` operator.

- **Usage**: Run the `Refactor: Concat Auto Complete` command from the palette. The refactor expands shorthand string concatenation assignments into their explicit form.
- **Example**:
  ```peoplecode
  /* Before */
  &result += "more text";

  /* After */
  &result = &result | "more text";
  ```

### MsgBox Auto Complete

Expands the shorthand `MsgBox()` function call to the full `MessageBox()` function with all required parameters.

- **Usage**: Run the `Refactor: MsgBox Auto Complete` command from the palette. This converts simple message box calls to the proper MessageBox function format.
- **Example**:
  ```peoplecode
  /* Before */
  MsgBox("Hello");

  /* After */
  MessageBox(0, "", 0, 0, "Hello");
  ```

### Declare Function

Opens a dialog to declare external functions (DLLs or system functions) for use in PeopleCode.

- **Usage**: Run the `Refactor: Declare Function` command from the palette. A dialog will appear where you can specify the function name, library, return type, and parameters.
- **Note**: This is useful for declaring Windows API functions or custom DLL functions that need to be called from PeopleCode.

### Collect Local Variables

Groups and consolidates local variable declarations within a method or function.

- **Usage**: Run the `Refactor: Collect Local Variables` command from the palette. The refactor analyzes the current scope and organizes variable declarations together.
- **Note**: This helps improve code organization by moving scattered variable declarations to a common location.

### Mark Statement Numbers

Adds statement number markers to code for debugging purposes.

- **Usage**: Run the `Refactor: Mark Statement Numbers` command from the palette. This adds sequential markers to statements, making it easier to reference specific lines during debugging or code reviews.
- **Note**: Particularly useful when working with complex methods that need detailed debugging or when reporting issues to support.

## Undo Refactoring

If you're not satisfied with a refactoring result, you have a couple of options:

1.  **Application Designer Undo**: Press `Ctrl+Z` immediately after the refactoring. This uses Application Designer's built-in undo stack.
2.  **AppRefiner Snapshots**: Use the `Snapshot: Revert File` command from the Command Palette to restore the editor contents to a previously saved state.
    - **Important Note**: Currently, AppRefiner does *not* automatically create a snapshot just before performing a refactoring operation. You may want to save manually before complex refactorings if you anticipate needing to revert using snapshots.

## Quick Summary

AppRefiner provides 12 user-triggered refactoring tools:

| Refactor | Shortcut | Description |
|----------|----------|-------------|
| Rename | `Ctrl+Shift+R` | Rename variables, parameters, and private methods |
| Resolve Imports | `Ctrl+Shift+I` | Add missing import statements |
| Add Import | - | Manually add a specific import |
| Sort Methods | - | Organize methods alphabetically |
| Add Flower Box | - | Add documentation header |
| Create AutoComplete | - | Generate AutoComplete event stubs |
| Suppress Linter Reports | - | Add linter suppression comments |
| Concat Auto Complete | - | Expand += to explicit concatenation |
| MsgBox Auto Complete | - | Expand MsgBox() to MessageBox() |
| Declare Function | - | Declare external DLL functions |
| Collect Local Variables | - | Group variable declarations |
| Mark Statement Numbers | - | Add statement number markers |

## Related Features

- **[Quick Fixes](../features/quick-fixes.md)**: Automatic code corrections triggered by `Ctrl+.` when the cursor is on a styler issue
- **[Code Styling](../features/code-styling.md)**: Visual indicators and code analysis that can trigger quick fixes
- **[Keyboard Shortcuts](keyboard-shortcuts.md)**: Complete list of all AppRefiner shortcuts

## Next Steps

- Learn about [Quick Fixes](../features/quick-fixes.md) for automatic code corrections
- Explore [Code Styling](../features/code-styling.md) features for code analysis
- See [Keyboard Shortcuts](keyboard-shortcuts.md) for a complete shortcut reference
