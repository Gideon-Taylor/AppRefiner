# AppRefiner Architecture

## Overview

AppRefiner is a productivity tool designed to enhance the PeopleSoft Application Designer experience by adding modern code editing features. It integrates with Application Designer's code windows through Scintilla Editor's message-based interface, providing features like syntax highlighting, code folding, linting, and refactoring capabilities without modifying the Application Designer itself.

## Core Architecture Components

### Scintilla Editor Integration & Hooking

AppRefiner integrates with the Scintilla editor component used by Application Designer's code windows through a combination of standard Windows techniques and deeper integration:

1.  **Window Detection (WinEvents)**: AppRefiner uses WinEvents to efficiently detect when an Application Designer code window gains focus.
2.  **Initial Enhancement (Window Messages)**: Upon detecting a relevant editor window, AppRefiner sends standard Scintilla messages (`SCI_...`) to apply initial enhancements like code folding margins and basic styling.
3.  **Deep Integration (DLL Hook)**: To enable more responsive and advanced features, AppRefiner injects a small DLL into the Application Designer process. This hook allows AppRefiner to directly receive Scintilla notifications (`SCN_...`) from the editor control. Key notifications used include:
    *   `SCN_DWELLSTART` / `SCN_DWELLEND`: Used to trigger hover tooltips.
    *   `SCN_MODIFIED`: Used, often in conjunction with a short timer, to detect pauses in typing.
4.  **Event-Driven Updates**: The hook enables features like Tooltips and dynamic Styler updates to react directly to user actions (hovering, finished typing) rather than relying on periodic polling.

### ANTLR-Based PeopleCode Parser

At the heart of AppRefiner's code analysis capabilities is an ANTLR-based parser for PeopleCode:

- Based on the grammar released by [lbaca/PeopleCodeParser](https://github.com/lbaca/PeopleCodeParser)
- Provides a robust abstract syntax tree (AST) for PeopleCode analysis
- Powers all intelligent features including:
  - Linters: Identify code issues and suggest improvements
  - Stylers: Apply consistent formatting and syntax highlighting
  - Refactors: Enable code transformations and refactoring operations

The parser ensures accurate code analysis while maintaining compatibility with PeopleCode's unique syntax and features.

### Update Mechanism

AppRefiner updates the enhanced editor view using an event-driven approach combined with initial processing:

1.  **Initial Processing (on Focus)**: When an editor window gains focus (detected via WinEvents), AppRefiner:
    *   Retrieves the current editor content.
    *   Calculates a hash of the content and compares it to a stored hash (if available) to avoid reprocessing identical content.
    *   If content is new or changed, applies initial setup like code folding and runs active Stylers.
2.  **Subsequent Updates (Hook-Driven)**: After initial processing, the DLL hook monitors editor events:
    *   **Typing Pause**: When a pause in typing is detected (via `SCN_MODIFIED` events and a timer), AppRefiner re-parses the content and reapplies active Stylers and potentially other visual cues.
    *   **Hover**: When the mouse hovers over text (`SCN_DWELLSTART`), relevant Tooltip Providers are queried.
3.  **Manual Refresh**: If the display seems out of sync (e.g., due to an error during a previous update), the `Editor: Force Refresh Current Editor` command (available in the Command Palette) can be used. This clears existing annotations/styles/tooltips for the active editor and forces a full reprocessing, similar to the initial processing step.

This approach balances responsiveness to user actions with optimizations to minimize resource usage.

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
  - **[Custom Linters](../api-reference/core-api/custom-linters.md)**: Define organization-specific code standards
  - **Custom Stylers**: Create specialized syntax highlighting rules
  - **Custom Refactors**: Implement additional code transformations

## Performance Considerations

AppRefiner is designed to be lightweight and responsive:
- **Content Hashing**: Avoids redundant processing during initial editor focus when content hasn't changed.
- **On-Demand Heavy Operations**: Resource-intensive operations (Linting, Refactoring) only run when explicitly requested.
- **Hook-Based Updates**: Reacting to editor events via the hook is generally more efficient than constant polling.
- A "Force Refresh" command (`Editor: Force Refresh Current Editor`) is available to manually reprocess the editor if needed.
