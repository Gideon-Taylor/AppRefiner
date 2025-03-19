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
| EMPTY_CATCH | Detects empty catch blocks that silently swallow exceptions | Warning |
| FLOWER_BOX | Checks for proper documentation headers | Information |
| FUNC_PARAM_COUNT | Flags functions with too many parameters | Warning |
| LONG_EXPR | Identifies overly complex expressions | Warning |
| NESTED_IF | Detects deeply nested if/else statements | Warning |
| RECURSIVE_FUNC | Identifies potentially problematic recursive functions | Warning |
| SQL_EXEC_VAR | Checks for proper variable usage in SQLExec statements | Warning |
| SQL_LONG_STRING | Identifies long SQL strings that should be broken up | Warning |
| SQL_WILDCARD | Flags use of SELECT * in SQL statements | Warning |
| OBJECT_TYPE | Detects inappropriate use of Object type | Warning |
| CREATE_SQL_VAR | Checks variable usage in CreateSQL statements | Warning |
| GETHTML_VAR | Validates variable usage in GetHTMLText functions | Warning |
| MULTI_REM | Ensures multi-line comments use proper format | Information |

## Enabling and Configuring Linters

To enable or configure linters:

1. Go to **Tools > Options** in the menu
2. Select the **Linters** tab
3. Check the box next to each linter you want to enable
4. Adjust severity levels if needed (Error, Warning, Information)
5. Click **Apply** to save your changes

## Running Linters

You can run linters in several ways:

- **Automatically**: Linters run automatically when code is loaded or modified
- **Manually**: Press `Ctrl+Alt+L` to run linters on demand
- **Command Palette**: Open the command palette (`Ctrl+Shift+P`) and type "lint"

## Understanding Linter Results

Linter results appear in the Linting Results panel at the bottom of the window. Each result includes:

- An icon indicating severity (red for errors, yellow for warnings, blue for information)
- The line number where the issue was found
- A description of the issue
- The linter ID that reported the issue

## Suppressing Linter Warnings

You can suppress specific linter warnings in your code using special comments:

```
/* @lint-disable LINTER_ID */
// Code with suppressed linting
/* @lint-enable LINTER_ID */
```

To suppress a specific warning on a single line:

```
someCode(); // @lint-ignore LINTER_ID
```

## Linter Reports

AppRefiner can generate comprehensive linting reports for your codebase:

1. Go to **Tools > Generate Linting Report**
2. Select the output format (HTML, CSV, or JSON)
3. Choose the destination folder
4. Click **Generate**

Reports include all linting issues found, organized by file and severity.

## Next Steps

To learn about AppRefiner's powerful refactoring tools, proceed to the [Using Refactoring Tools](using-refactoring-tools.md) section.
