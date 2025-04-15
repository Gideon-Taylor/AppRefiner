# Annotations

Annotations in AppRefiner provide visual indicators for potential issues in your code that are identified by the linting system.

## Overview

Annotations are visual markers that appear directly in your code editor, highlighting potential issues or areas that need attention. They help you identify problems without having to run your code, improving development efficiency and code quality.

## How Annotations Work

AppRefiner supports in-line annotations that appear underneath the offending line of code. These annotations are color-coded based on the report type:

- **Red**: Error - Critical issues that need immediate attention
- **Yellow**: Warning - Potential problems that may cause issues
- **Black**: Information - Suggestions or informational notes

## When Annotations Appear

Annotations appear in the editor after you explicitly run one or more linters against the current code. To see annotations:

1.  Open a PeopleCode file in Application Designer.
2.  Run linters using one of the following methods:
    *   Press the shortcut `Ctrl+Alt+L` (runs all active linters).
    *   Use the Command Palette (`Ctrl+Shift+P`) to run `Lint: Run All Active Linters` or a specific linter command like `Lint: [Linter Description]`.
3.  Annotations corresponding to any issues found will appear inline below the relevant code lines.
4.  You can remove existing annotations using the `Lint: Clear Annotations` command from the Command Palette.

For more details on running linters, see the [Working with Linters](../../user-guide/working-with-linters.md) section in the User Guide.

## Benefits of Annotations

- **Immediate feedback**: Quickly identify issues in your code
- **Visual clarity**: Color-coding helps prioritize which issues to address first
- **Contextual information**: See issues directly in the context of your code
- **Improved code quality**: Address potential problems before they cause runtime errors

## Related Features

- [Working with Linters](../../user-guide/working-with-linters.md)
- [Code Folding](code-folding.md)
