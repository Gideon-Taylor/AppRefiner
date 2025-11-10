# Working with Linters

AppRefiner includes a powerful linting system that helps you identify potential issues in your PeopleCode. This guide explains how to use and configure linters effectively.

## Understanding Linters

Linters are code analysis tools that flag programming errors, bugs, stylistic errors, and suspicious constructs. AppRefiner's linters are specifically designed for PeopleCode and can help you:

- Identify potential bugs before they cause problems
- Enforce coding standards and best practices
- Improve code readability and maintainability
- Detect performance issues

## Available Linters

AppRefiner includes the following linters:

| Linter ID | Description | Default Severity |
|-----------|-------------|------------------|
| CREATE_SQL_VAR | Checks variable usage in CreateSQL statements | Warning |
| EMPTY_CATCH | Detects empty catch blocks that silently swallow exceptions | Warning |
| FLOWER_BOX | Checks for proper documentation headers | Information |
| FUNC_PARAM_COUNT | Flags functions with too many parameters | Warning |
| GETHTML_VAR | Validates variable usage in GetHTMLText functions | Warning |
| LONG_EXPR | Identifies overly complex expressions | Warning |
| MULTI_REM | Ensures multi-line comments use proper format | Information |
| NESTED_IF | Detects deeply nested if/else statements | Warning |
| OBJECT_TYPE | Detects inappropriate use of Object type | Warning |
| RECURSIVE_FUNC | Identifies potentially problematic recursive functions | Warning |
| REUSED_FOR_ITER | Detects the re-use of for loop iterators in nested for loops | Warning |
| SQL_EXEC_VAR | Checks for proper variable usage in SQLExec statements | Warning |
| SQL_LONG_STRING | Identifies long SQL strings that should be broken up | Warning |
| SQL_WILDCARD | Flags use of SELECT * in SQL statements | Warning |
| TODO_FIXME | Detects TODO and FIXME comments | Information |

## Enabling and Configuring Linters

To enable or configure linters:

1. Open the AppRefiner main window.
2. Navigate to the **Linters** tab.
3. Check the box next to each linter you want to enable.
4. For linters with configurable options, adjust their settings as needed (e.g., parameter counts, length limits).
5. Default severity levels (Error, Warning, Information) are not user-adjustable, but determine how issues are presented.

## Running Linters

You can run linters in several ways:

- **Run All Active Linters**: Press `Alt+L` to run all enabled linters on the current editor content.
- **Command Palette**: Open the command palette (`Ctrl+Shift+P`) and type "lint" to find commands such as:
    - `Lint: Run All Active Linters`: Same as `Alt+L`.
    - Individual Linter Commands (e.g., `Lint: Detects empty catch blocks`, `Lint: Checks for proper documentation headers`): Each enabled linter has its own command generated, allowing you to run just that specific rule against the current editor content.
    - `Lint: Clear Annotations`: Removes any inline linter annotations from the editor.
    - `Lint: Lint Project`: Runs all active linters on the currently open project in Application Designer (requires database connection).

## Understanding Linter Results

Linter results can appear in two main places:

- **Inline Annotations**: When run via shortcut or command palette, issues are often shown directly in the Application Designer editor window next to the relevant line. This is the quickest way to see results.
- **Linters Tab**: The bottom half of the Linters tab in the AppRefiner window contains a grid displaying all issues found by the linters. Each result includes:
    - An icon indicating severity (red for errors, yellow for warnings, blue for information)
    - The line number where the issue was found
    - A description of the issue
    - The linter ID that reported the issue
    - Double-clicking a result in this grid will navigate you to the corresponding line in the Application Designer editor.

## Suppressing Linter Warnings

If you need to suppress specific linter warnings for a section of code, you can add a special comment *before* the code block you want to ignore:

```peoplecode
/* #AppRefiner suppress (LINTER_ID1, LINTER_ID2, ...) */
// Code where LINTER_ID1 and LINTER_ID2 warnings will be ignored

// AppRefiner does not currently support enabling suppression
// or ignoring only a single line via comments.
```

Replace `LINTER_ID1`, `LINTER_ID2`, etc., with the specific IDs of the linters you wish to suppress (e.g., `EMPTY_CATCH`, `SQL_WILDCARD`). Suppression applies until the end of the current scope (e.g., method, block) or file.

## Linting an Entire Project

AppRefiner can analyze all PeopleCode objects within the currently open Application Designer project:

1. Ensure you have established a database connection within AppRefiner (see [Database Integration](database-integration.md)).
2. Open the Command Palette (`Ctrl+Shift+P`).
3. Run the `Lint: Lint Project` command.

AppRefiner will analyze the project components and display the results in the Linters tab grid.

## Next Steps

To learn about AppRefiner's powerful refactoring tools, proceed to the [Using Refactoring Tools](using-refactoring-tools.md) section.
