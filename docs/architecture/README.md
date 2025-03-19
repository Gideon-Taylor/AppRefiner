# AppRefiner Architecture

## Overview

AppRefiner is a productivity tool designed to enhance the PeopleSoft Application Designer experience by adding modern code editing features. It integrates with Application Designer's code windows through Scintilla Editor's message-based interface, providing features like syntax highlighting, code folding, linting, and refactoring capabilities without modifying the Application Designer itself.

## Core Architecture Components

### Scintilla Editor Integration

AppRefiner leverages the Scintilla Editor component that powers Application Designer's code windows. Communication happens through a message-based approach:

1. **Window Detection**: AppRefiner scans active windows to identify Application Designer code windows
2. **Message Passing**: Once identified, AppRefiner sends Scintilla-specific messages to the editor window
3. **Feature Activation**: These messages enable features like code folding, syntax highlighting, and other enhancements

The message-based approach allows AppRefiner to enhance the editor without requiring modifications to Application Designer's code.

### ANTLR-Based PeopleCode Parser

At the heart of AppRefiner's code analysis capabilities is an ANTLR-based parser for PeopleCode:

- Based on the grammar released by [lbaca/PeopleCodeParser](https://github.com/lbaca/PeopleCodeParser)
- Provides a robust abstract syntax tree (AST) for PeopleCode analysis
- Powers all intelligent features including:
  - Linters: Identify code issues and suggest improvements
  - Stylers: Apply consistent formatting and syntax highlighting
  - Refactors: Enable code transformations and refactoring operations

The parser ensures accurate code analysis while maintaining compatibility with PeopleCode's unique syntax and features.

### Scanning Mechanism

AppRefiner employs a periodic scanning mechanism to detect and enhance Application Designer code windows:

1. **Window Scanning**: Every 1 second, AppRefiner scans for active Application Designer code windows
2. **Change Detection**: For each detected window, AppRefiner:
   - Retrieves the current editor content
   - Calculates a hash of the content
   - Compares it with the previously stored hash (if any)
3. **Optimization**: If the content hash hasn't changed since the last scan, processing is skipped to prevent redundant work
   - Note: If the automatic detection fails, a "Force Refresh" command is available to manually reset the content hash and trigger a full refresh on the next scan
4. **Feature Application**: For changed content, AppRefiner:
   - Enables code folding
   - Initializes styles
   - Runs active Stylers to enhance the code display

This approach ensures that AppRefiner remains responsive while minimizing resource usage.

## On-Demand Features

While scanning provides automatic enhancements, certain features are triggered on-demand:

- **Linters**: Run when explicitly requested via commands or keyboard shortcuts
- **Refactors**: Activated through the refactoring menu or keyboard shortcuts

These on-demand features always work with a fresh copy of the editor content to ensure accuracy.

## Data Privacy and Security

AppRefiner is designed with privacy and security in mind:

- Editor contents **never** leave the AppRefiner ↔ Application Designer circuit
- Code is not transmitted to external services or used for any purpose beyond the immediate functionality
- All processing happens locally on the user's machine

## Extension Points

AppRefiner is designed to be extensible through:

- **Plugin System**: Allows third-party developers to add new features
  - **Custom Linters**: Define organization-specific code standards
  - **Custom Stylers**: Create specialized syntax highlighting rules
  - **Custom Refactors**: Implement additional code transformations

## Performance Considerations

AppRefiner is designed to be lightweight and responsive:
- **Content Hashing**: Avoids redundant processing when content hasn't changed
  - A "Force Refresh" command is available to manually reset the content hash if needed which will trigger a re-styling on the next scan
- **On-Demand Heavy Operations**: Resource-intensive operations only run when requested
- **Background Processing**: Ensures UI responsiveness during analysis operations
