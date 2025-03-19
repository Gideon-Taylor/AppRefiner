# User Interface Overview

AppRefiner provides a comprehensive interface designed to enhance PeopleSoft's Application Designer with modern development features. This guide will help you understand how AppRefiner integrates with Application Designer and the main components of its user interface.

## Integration with Application Designer

AppRefiner runs alongside PeopleSoft's Application Designer as a companion application:

- **Non-intrusive**: Doesn't modify Application Designer's core functionality
- **Context-aware**: Automatically detects the active PeopleCode editor window
- **Seamless enhancement**: Adds features to the current editor without disrupting workflow
- **Lightweight**: Minimal impact on Application Designer's performance

## Enhanced Editor Features

AppRefiner adds the following enhancements to the Application Designer PeopleCode editor:

- **Syntax highlighting**: Improved color coding for PeopleCode elements
- **Code folding**: Collapse and expand code blocks for better organization
- **Error and warning indicators**: Visual indicators for code issues
- **Auto-indentation**: Improved code formatting

## Panels and Views

### Linting Panel

The linting panel displays issues found in your code based on the active linting rules. Each issue includes:

- Severity level (Error, Warning, Information)
- Description of the issue
- Line and column number where the issue occurs

The linting panel also provides a way to connect to PeopleSoft Oracle databases which can improve linting results. 

**Note:** Bootstrap or readonly credentials to the database are required.


### Command Palette

Access the command palette with `Ctrl+Shift+P` to quickly:

- Execute commands
- Apply refactorings
- Run linting operations

The Command Palette is the most efficient way to access AppRefiner's functionality, similar to how it works in VS Code.

## Next Steps

To learn about keyboard shortcuts that can help you work more efficiently with AppRefiner, proceed to the [Keyboard Shortcuts](keyboard-shortcuts.md) section.
