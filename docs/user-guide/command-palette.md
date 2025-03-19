# Command Palette

The Command Palette is the central feature of AppRefiner that provides seamless access to all functionality directly within PeopleSoft Application Designer.

## Overview

Inspired by modern IDEs like Visual Studio Code, the Command Palette offers a quick and efficient way to access AppRefiner's features without leaving your PeopleCode editor in Application Designer. It appears as a modal popup that overlays the Application Designer interface, providing immediate access to all available commands.

## Accessing the Command Palette

Press `Ctrl+Shift+P` while working in PeopleSoft Application Designer to open the Command Palette.

## Command Palette Features

### Command Search

- Type to search for any command
- Results filter in real-time as you type
- Commands are grouped by category
- Recently used commands appear at the top

### Available Command Categories

The Command Palette provides access to all AppRefiner functionality, including:

#### Refactoring Commands
- Rename Variable
- Optimize Imports
- Extract Method
- Inline Variable
- Generate FlowerBox Headers

#### Linting Commands
- Run Linting
- Fix All Issues
- Suppress Warning
- Configure Linting Rules

#### Editor Enhancement Commands
- Toggle Code Folding
- Format SQL
- Toggle Dark Mode
- Show/Hide Annotations

#### Code Navigation
- Go to Definition
- Find All References
- Show Call Hierarchy
- Navigate to Related Files

#### Templates
- Insert Code Template
- Create New Template
- Manage Templates

#### Database Integration
- Connect to Database
- Validate SQL
- Check Record Field References

### Keyboard Navigation

Within the Command Palette:
- Use arrow keys to navigate between commands
- Press `Enter` to execute the selected command
- Press `Escape` to dismiss the Command Palette
- Use `Tab` to auto-complete search terms

## Examples of Command Palette Usage

### Example 1: Renaming a Variable

1. Place your cursor on a variable in Application Designer
2. Press `Ctrl+Shift+P` to open the Command Palette
3. Type "rename" (the "Rename Variable" command appears)
4. Press `Enter` to select the command
5. Type the new variable name
6. Press `Enter` to apply the rename

### Example 2: Running Linting

1. Press `Ctrl+Shift+P` to open the Command Palette
2. Type "lint" (linting-related commands appear)
3. Select "Run Linting" and press `Enter`
4. Linting results appear as annotations in the Application Designer editor

### Example 3: Applying a Code Template

1. Press `Ctrl+Shift+P` to open the Command Palette
2. Type "template" (template-related commands appear)
3. Select "Insert Code Template" and press `Enter`
4. Choose from the available templates
5. The template is inserted at the cursor position in Application Designer

## Customizing the Command Palette

You can customize which commands appear in the Command Palette through AppRefiner's settings:

1. Open AppRefiner's settings panel
2. Navigate to the "Command Palette" section
3. Enable or disable specific commands
4. Reorder commands by priority

## Benefits of Using the Command Palette

- **Efficiency**: Access any feature with just a few keystrokes
- **Discoverability**: Easily find features you might not know exist
- **Keyboard-centric workflow**: Minimize mouse usage for faster development
- **Seamless integration**: Work within Application Designer without context switching
- **Reduced learning curve**: Similar interface to popular modern IDEs

## Related Features

- [Keyboard Shortcuts](keyboard-shortcuts.md)
- [UI Overview](ui-overview.md)
