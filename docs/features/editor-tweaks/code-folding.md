# Code Folding

Code folding is a feature in AppRefiner that allows you to collapse and expand sections of code, making it easier to navigate and read large code files.

## Overview

PeopleCode files can become lengthy and complex, especially when they contain multiple functions, methods, or class definitions. Code folding helps manage this complexity by allowing you to collapse sections of code that aren't currently relevant to your work.

## How to Use Code Folding

### Visual Indicators

- **Click the `-` symbol** in the gutter margin (left side of the editor) next to a code block (like a function, method, loop, or comment) to collapse it.
- **Click the `+` symbol** next to a collapsed block to expand it.

### Keyboard Shortcuts

- **Collapse Current Block**: `Alt+Left`
- **Expand Current Block**: `Alt+Right`
- **Collapse All Blocks**: `Ctrl+Alt+Left`
- **Expand All Blocks**: `Ctrl+Alt+Right`

### Command Palette

- Run `Editor: Collapse All` from the Command Palette (`Ctrl+Shift+P`).
- Run `Editor: Expand All` from the Command Palette (`Ctrl+Shift+P`).

### Initial Folding State (Auto Collapse)

- You can control whether code blocks are automatically collapsed when an editor is first opened.
- Use the `Editor: Toggle Auto Collapse` command in the Command Palette to turn this setting on or off.
- This setting corresponds to the "Auto Collapse" checkbox on the Settings tab in the main AppRefiner window.

## Foldable Sections

### PeopleCode
AppRefiner automatically identifies the following sections as foldable:

- **Function definitions**
- **Method definitions**
- **Class definitions**
- **If/Then/Else blocks**
- **For loops**
- **While loops**
- **Try/Catch blocks**
- **Evaluate/When blocks**
- **Multi-line comments**

**Note** Folding is actually done by indentation level, but since Application Designer enforces indentation, it effectively works as mentioned above.

**Note** Folding is most performant in 8.61 where the foldable sections are defined by the language grammar. In earlier versions AppRefiner has to set the folding levels each line of the program. Excessively large programs may see significant delays while folding is being processed.

### HTML
AppRefiner sets folding levels by indentation level like it does for PeopleCode.

### SQL
AppRefiner sets folding levels by indentation level like it does for PeopleCode. Due to the Application Designer formatting rules this isn't always great, but with AppRefiner's [SQL formatting](sql-formatting.md) feature you can improve the formatting and thereby improve the effectiveness of the folding.

## Benefits
- **Improved readability**: Focus on the code sections that matter most
- **Easier navigation**: Quickly scroll through large files by collapsing irrelevant sections
- **Better code organization**: Visualize the structure of your code through the folding hierarchy

## Related Features

- [Annotations](annotations.md) - Add notes and markers to your code
- [Dark Mode](dark-mode.md) - Reduce eye strain with dark mode theme
