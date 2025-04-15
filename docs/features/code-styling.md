# Code Styling

AppRefiner provides powerful code styling features to help you maintain clean, readable, and consistent PeopleCode. This guide explains how to use these features effectively.

## Understanding Code Styling

Code styling in AppRefiner refers to:

1. **Visual formatting** - How code appears in the editor through syntax highlighting.
2. **Code analysis** - Identifying stylistic issues using Code Stylers.

## Syntax Highlighting

AppRefiner provides rich syntax highlighting for PeopleCode, applying distinct, fixed colors to different code elements like keywords, strings, comments, variables, method calls, and constants to improve readability. These colors are currently not user-configurable.

## Code Stylers

AppRefiner includes several code stylers that analyze your code for specific stylistic issues or provide visual cues. These run automatically as you work.

Available stylers include:

- **Unused Imports**: Identifies import statements that are not referenced in your code.
- **Unused Variables**: Highlights variables that are declared but never used.
- **Undefined Variables**: Highlights variables that are used but not declared.
- **Meaningless Variable Names**: Flags variables with overly generic or non-descriptive names (e.g., `a`, `b`, `x`).
- **Property As Variable**: Detects when properties are accessed directly instead of through getter/setter methods where appropriate.
- **Invalid App Class**: Checks if imported Application Classes actually exist in the database (requires DB connection).
- **Todo/Fixme Comments**: Highlights `TODO`, `FIXME`, `NOTE`, `BUG`, `HACK`, `TBD` comment markers with distinct colors and provides a summary annotation.
- **Linter Suppression**: Highlights areas where linter warnings have been suppressed using `/* #AppRefiner suppress(...) */` comments.

## Enabling and Configuring Stylers

Stylers can be enabled or disabled individually:

- **Via UI**: Navigate to the **Stylers Tab** in the main AppRefiner window and check/uncheck the box next to each styler.
- **Via Command Palette**: Use the `Styler: Toggle [Styler Description]` commands (e.g., `Styler: Toggle Highlights TODO/FIXME comments`) found in the Command Palette (`Ctrl+Shift+P`).

## Next Steps

To learn about database integration features in AppRefiner, proceed to the [Database Integration](../user-guide/database-integration.md) section.
