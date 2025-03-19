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

### Suppress Linter Warning

Adds a suppression comment to disable specific linter warnings.

**Usage:**
1. Right-click on a linter warning in the Linting Results panel
2. Select **Suppress Warning**
3. Choose whether to suppress just this instance or all warnings of this type

## Batch Refactoring

AppRefiner supports applying refactorings to multiple files at once:

1. Select **Refactor > Batch Refactor** from the menu
2. Choose the refactoring operation to apply
3. Select the files or project to refactor
4. Review the changes and click **Apply**

## Refactoring Preview

Before applying any refactoring, AppRefiner shows you a preview of the changes:

- Changed lines are highlighted
- You can toggle individual changes on/off
- A summary of all changes is provided
- You can save the changes to a different file if desired

## Undo Refactoring

If you're not satisfied with a refactoring result:

1. Press `Ctrl+Z` immediately after applying the refactoring
2. Or select **Edit > Undo** from the menu

## Best Practices

- Always save your work before performing major refactorings
- Use the preview feature to verify changes before applying them
- Consider running linters after refactoring to ensure code quality
- For complex refactorings, break them down into smaller, manageable steps

## Next Steps

To learn about code styling features in AppRefiner, proceed to the [Code Styling](code-styling.md) section.
