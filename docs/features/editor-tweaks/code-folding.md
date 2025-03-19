# Code Folding

Code folding is a feature in AppRefiner that allows you to collapse and expand sections of code, making it easier to navigate and read large code files.

## Overview

PeopleCode files can become lengthy and complex, especially when they contain multiple functions, methods, or class definitions. Code folding helps manage this complexity by allowing you to collapse sections of code that aren't currently relevant to your work.

## How to Use Code Folding

### Collapsing Code

- **Click the `-` symbol** in the gutter margin next to a foldable section of code to collapse it
- **Use keyboard shortcut Ctrl+M, Ctrl+M** to toggle folding at the current cursor position
- **Use keyboard shortcut Ctrl+M, Ctrl+L** to collapse all foldable sections in the file

### Expanding Code

- **Click the `+` symbol** in the gutter margin next to a collapsed section to expand it
- **Use keyboard shortcut Ctrl+M, Ctrl+M** to toggle folding at the current cursor position
- **Use keyboard shortcut Ctrl+M, Ctrl+O** to expand all collapsed sections in the file

## Foldable Sections

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

## Customizing Code Folding

You can customize code folding behavior in AppRefiner through the Settings menu:

1. Go to **Tools > Options > Text Editor > PeopleCode > Advanced**
2. Under the **Outlining** section, you can:
   - Enable or disable automatic outlining
   - Choose which code elements should be foldable
   - Set the default state (expanded or collapsed) for newly opened files

## Benefits

- **Improved readability**: Focus on the code sections that matter most
- **Easier navigation**: Quickly scroll through large files by collapsing irrelevant sections
- **Better code organization**: Visualize the structure of your code through the folding hierarchy

## Related Features

- [Annotations](annotations.md) - Add notes and markers to your code
- [Dark Mode](dark-mode.md) - Reduce eye strain with dark mode theme
