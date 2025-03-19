# Code Styling

AppRefiner provides powerful code styling features to help you maintain clean, readable, and consistent PeopleCode. This guide explains how to use these features effectively.

## Understanding Code Styling

Code styling in AppRefiner refers to:

1. **Visual formatting** - How code appears in the editor
2. **Code analysis** - Identifying stylistic issues
3. **Automatic formatting** - Applying consistent formatting rules

## Syntax Highlighting

AppRefiner provides rich syntax highlighting for PeopleCode:

- **Keywords** are displayed in blue
- **Strings** are displayed in red
- **Comments** are displayed in green
- **Variables** are displayed in black
- **Method calls** are displayed in purple
- **Constants** are displayed in teal

You can customize the colors in **Tools > Options > Editor > Colors**.

## Code Stylers

AppRefiner includes several code stylers that analyze your code for stylistic issues:

### Unused Imports

Identifies import statements that are not used in your code.

### Unused Variables

Highlights variables that are declared but never used.

### Meaningless Variable Names

Identifies variables with non-descriptive names (like a, b, x, etc.).

### Property As Variable

Detects when properties are accessed directly instead of through getter/setter methods.

### Linter Suppression

Highlights areas where linter warnings have been suppressed.

## Enabling and Configuring Stylers

To enable or configure code stylers:

1. Go to **Tools > Options** in the menu
2. Select the **Stylers** tab
3. Check the box next to each styler you want to enable
4. Click **Apply** to save your changes

## Code Formatting

AppRefiner can automatically format your code according to configurable rules:

### Indentation

Control indentation settings:

1. Go to **Tools > Options > Editor > Formatting**
2. Set your preferred indentation style (Tabs or Spaces)
3. Set the tab size (usually 2 or 4 spaces)

### Automatic Formatting

To format code automatically:

1. Select the code you want to format (or the entire file)
2. Press `Ctrl+K, Ctrl+F` or select **Edit > Format Selection** from the menu

### Format on Save

Enable automatic formatting whenever you save a file:

1. Go to **Tools > Options > Editor > Formatting**
2. Check "Format on Save"

## Code Folding

AppRefiner supports code folding to help you focus on specific parts of your code:

- Click the "-" icon in the margin to collapse a code block
- Click the "+" icon to expand it
- Use `Alt+Left` to collapse the current level
- Use `Alt+Right` to expand the current level
- Use `Ctrl+Alt+Left` to collapse all blocks
- Use `Ctrl+Alt+Right` to expand all blocks

## Code Style Configuration

You can customize code style rules to match your team's standards:

1. Go to **Tools > Options > Editor > Code Style**
2. Configure rules for:
   - Brace placement
   - Spacing
   - Line wrapping
   - Naming conventions
   - And more

## Exporting and Importing Style Settings

Share your code style settings with your team:

1. Go to **Tools > Options > Editor > Code Style**
2. Click **Export** to save your settings to a file
3. Share this file with your team members
4. They can click **Import** to apply the same settings

## Next Steps

To learn about database integration features in AppRefiner, proceed to the [Database Integration](database-integration.md) section.
