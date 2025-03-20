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

Annotations are only produced when a user runs linters on the code. To see annotations in your code:

1. Open a code file in the editor
2. Run the linting process (manually or automatically, depending on your settings)
3. View the annotations that appear under problematic code lines

For more information about the linting system, see the [Linting Overview](../linting/overview.md).

## Benefits of Annotations

- **Immediate feedback**: Quickly identify issues in your code
- **Visual clarity**: Color-coding helps prioritize which issues to address first
- **Contextual information**: See issues directly in the context of your code
- **Improved code quality**: Address potential problems before they cause runtime errors

## Related Features

- [Linting Overview](../linting/overview.md)
- [Code Folding](code-folding.md)
