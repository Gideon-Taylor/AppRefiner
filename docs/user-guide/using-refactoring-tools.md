# Using Refactoring Tools

AppRefiner provides powerful refactoring tools that help you improve your PeopleCode without manually editing every occurrence. This guide explains how to use these tools effectively.

## What is Refactoring?

Refactoring is the process of restructuring existing code without changing its external behavior. The goal is to improve the code's internal structure, making it more readable, maintainable, and less prone to bugs.

## Available Refactoring Tools

AppRefiner includes the following refactoring tools:

### Rename Local Variable

This tool allows you to rename a local variable throughout its scope, ensuring all references are updated correctly.

**Usage:**
1. Place your cursor on the variable you want to rename
2. Press `Ctrl+Shift+R` or select **Refactor > Rename Variable** from the menu
3. Enter the new variable name in the dialog
4. Review the changes and click **Apply**

### Resolve Imports

Automatically adds missing import statements for referenced classes and packages.

**Usage:**
1. Press `Ctrl+Shift+I` or select **Refactor > Resolve Imports** from the menu
2. AppRefiner will analyze your code and add any necessary import statements

### Optimize Imports

Removes unused imports and organizes import statements in a consistent order.

**Usage:**
1. Select **Refactor > Optimize Imports** from the menu
2. AppRefiner will remove any unused imports and sort the remaining ones

### Add Flower Box

Adds a standardized documentation header (flower box) to your PeopleCode.

**Usage:**
1. Place your cursor at the beginning of the file
2. Select **Refactor > Add Flower Box** from the menu
3. Fill in the required information in the dialog
4. Click **Apply** to insert the header

### Suppress Linter Reports

Adds a suppression comment to disable specific linter reports.

**Usage:**
1. Position your cursor on the line that has the linter report(s) you want to suppress
2. Open the Command Palette (Ctrl+Shift+P)
3. Type "Suppress Report" and select the command
4. In the dialog that appears, select the scope of the suppression:
   - **Line**: Suppresses the warning for just this line
   - **Nearest Block**: Suppresses the warning for the current code block (If, While, For, etc.)
   - **Method/Function**: Suppresses the warning for the entire method or function (getters and setters included)
   - **Global**: Suppresses the warning for the entire file
5. Click **OK** to add the suppression comment

**Note:** If a line has multiple linter reports, the suppression comment will suppress all of them. You can edit the suppression comment manually afterward to remove specific suppressions if needed.

## Undo Refactoring

If you're not satisfied with a refactoring result:

1. Press `Ctrl+Z` immediately after applying the refactoring
2. Or execute the `Restore Snapshot` command from the Command Palette

## Next Steps

To learn about code styling features in AppRefiner, proceed to the [Code Styling](code-styling.md) section.
