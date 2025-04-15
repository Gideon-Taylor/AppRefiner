# Command Palette

The Command Palette is the central feature of AppRefiner that provides seamless access to all functionality directly within PeopleSoft Application Designer.

## Overview

Inspired by modern IDEs like Visual Studio Code, the Command Palette offers a quick and efficient way to access AppRefiner's features without leaving your PeopleCode editor in Application Designer. It appears as a modal popup that overlays the Application Designer interface, providing immediate access to all available commands.

## Accessing the Command Palette

Press `Ctrl+Shift+P` while working in PeopleSoft Application Designer to open the Command Palette.

## Command Palette Features

### Command Search and Execution

- **Filtering**: Start typing to filter the command list in real-time. Only commands containing your typed text will be shown.
- **Execution**: Use the Up/Down arrow keys to navigate the filtered list and press `Enter` to execute the selected command.
- **Dismiss**: Press `Escape` to close the Command Palette without executing a command.
- **Categorization**: Commands are generally grouped by prefixes (e.g., `Editor:`, `Lint:`, `Refactor:`).

### Available Commands (Examples)

The Command Palette provides access to most AppRefiner functionality. Here are common examples grouped by prefix:

- **`Editor:`** (General editor actions and setting toggles)
    - `Editor: Lint Current Code (Ctrl+Alt+L)`: Runs all active linters.
    - `Editor: Collapse All (Ctrl+Alt+Left)` / `Editor: Expand All (Ctrl+Alt+Right)`: Manages code folding.
    - `Editor: Dark Mode`: Applies dark styling to the current editor instance.
    - `Editor: Toggle Auto Collapse` / `Toggle Only PeopleCode Editors` / `Toggle Auto Dark Mode` / `Toggle Auto Format SQL`: Toggles corresponding settings found on the Settings tab.
- **`Lint:`** (Linter-specific actions)
    - `Lint: [Linter Description]` (e.g., `Lint: Detects empty catch blocks`): Runs a single, specific linter rule.
    - `Lint: Clear Annotations`: Removes visible linter issue annotations from the editor.
    - `Lint: Lint Project`: Runs active linters on the entire open project (requires DB connection).
- **`Refactor:`** (Code transformation tools)
    - `Refactor: Rename (Ctrl+Shift+R)`: Renames variables, parameters, etc.
    - `Refactor: Resolve Imports (Ctrl+Shift+I)`: Adds missing import statements.
    - `Refactor: Add Import`: Manually adds a specific import.
    - `Refactor: Sort Methods`: Reorders methods/functions alphabetically.
    - `Refactor: Add Flower Box`: Inserts a standard header comment.
    - `Refactor: Create AutoComplete`: Generates AutoComplete event method stubs.
    - `Refactor: Suppress Report`: Adds comments to suppress linter warnings.
- **`Styler:`** (Syntax highlighting and visual aid toggles)
    - `Styler: Toggle [Styler Description]` (e.g., `Styler: Toggle Highlights TODO/FIXME comments`): Enables or disables specific stylers.
- **`Snapshot:`** (Editor history management)
    - `Snapshot: Revert File`: Allows restoring the editor to a previous saved state.
- **`Template:`** (Code snippet insertion)
    - `Template: Apply Template` (or similar): Allows selecting and inserting predefined code templates.
- **`DB:`** (Database connection management)
    - `DB: Connect to database`: Initiates the database connection process.

*Note: This list is illustrative; the exact commands and names may vary slightly.* 

### Keyboard Navigation

Within the Command Palette:
- Use `Up Arrow` / `Down Arrow` keys to navigate between commands.
- Press `Enter` to execute the selected command.
- Press `Escape` to dismiss the Command Palette.

## Examples of Command Palette Usage

### Example 1: Renaming a Variable

1. Place your cursor on a variable in Application Designer.
2. Press `Ctrl+Shift+P` to open the Command Palette.
3. Type "rename" (the `Refactor: Rename (...)` command appears).
4. Press `Enter` to select the command.
5. Type the new variable name in the dialog.
6. Press `Enter` or click OK to apply the rename.

### Example 2: Running a Specific Linter

1. Press `Ctrl+Shift+P` to open the Command Palette.
2. Type the description of the linter (e.g., "empty catch").
3. Select the desired `Lint: [Linter Description]` command (e.g., `Lint: Detects empty catch blocks`) and press `Enter`.
4. Any issues found by that specific linter appear as annotations in the editor.

### Example 3: Applying a Code Template

1. Press `Ctrl+Shift+P` to open the Command Palette.
2. Type "template".
3. Select the `Template: Apply Template` command (or similar) and press `Enter`.
4. Choose from the available templates in the subsequent list or dialog.
5. The template is inserted at the cursor position in Application Designer.

## Benefits of Using the Command Palette

- **Efficiency**: Access any feature with just a few keystrokes
- **Discoverability**: Easily find features you might not know exist
- **Keyboard-centric workflow**: Minimize mouse usage for faster development
- **Seamless integration**: Work within Application Designer without context switching
- **Reduced learning curve**: Similar interface to popular modern IDEs

## Related Features

- [Keyboard Shortcuts](keyboard-shortcuts.md)
- [UI Overview](ui-overview.md)
