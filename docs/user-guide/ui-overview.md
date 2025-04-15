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

## AppRefiner Main Window

AppRefiner runs as a separate application window with a tabbed interface, providing access to configuration and features:

### Settings Tab

This tab allows you to control the core behavior of AppRefiner:

- **General Options**: Toggle features like Auto Collapse (code folding), Auto Dark Mode, Format SQL, Only Enhance PeopleCode Editors, Auto-Pairing (quotes/parentheses).
- **Database Connection**: Configure whether AppRefiner prompts for database connection details when the first editor is detected.
- **Plugins**: Set the directory for custom plugins.

### Stylers Tab

Manage the syntax highlighting applied to the Application Designer editor. You can enable or disable specific stylers.

### Tooltips Tab

Configure the information provided in tooltips when hovering over code elements. You can enable or disable specific tooltip providers.

### Linters Tab

Control the code analysis features:

- **Enable/Disable Linters**: Toggle individual linters on or off.
- **Configure Linters**: Adjust settings for linters that support configuration.
- **Database Connection**: Connect to PeopleSoft Oracle databases (requires bootstrap or read-only credentials) to improve linting accuracy by providing context about application objects.
- **Linting Results**: Issues found by active linters (Errors, Warnings, Information) along with descriptions and locations are typically displayed here or directly in the editor.

### Templates Tab

Browse available code templates. While templates can be applied from this tab, using the Command Palette is often more efficient.

## Command Palette

Access the command palette with `Ctrl+Shift+P` within Application Designer to quickly:

- Execute commands (e.g., linting, applying templates, reverting snapshots)
- Apply refactorings

## Next Steps

To learn about keyboard shortcuts that can help you work more efficiently with AppRefiner, proceed to the [Keyboard Shortcuts](keyboard-shortcuts.md) section.
