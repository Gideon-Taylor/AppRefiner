# Annotations

Annotations in AppRefiner provide a way to add notes, reminders, and contextual information directly within your code without affecting its execution.

## Overview

Code annotations are visual markers and notes that appear alongside your code in the editor. They help document code behavior, highlight potential issues, and provide additional context without modifying the actual code.

## Types of Annotations

AppRefiner supports several types of annotations:

### 1. Margin Annotations

These appear in the margin area next to your code and can indicate:
- **Warnings**: Potential issues that don't prevent code execution
- **Errors**: Issues that would cause runtime errors
- **Information**: General notes and contextual information
- **Suggestions**: Recommended improvements

### 2. Inline Annotations

These appear directly within the code as:
- **Squiggly underlines**: Highlight potential issues with different colors (red for errors, green for warnings)
- **Tooltips**: Appear when hovering over annotated code
- **Inline hints**: Small text indicators that appear next to specific code elements

### 3. Code Lens Annotations

These appear above code blocks and provide:
- **Reference counts**: Number of references to a function or variable
- **Implementation links**: Quick access to implementations of interfaces
- **Test status**: Information about test coverage and results

## How Annotations Are Generated

Annotations in AppRefiner come from multiple sources:

- **Linters**: Code analysis tools that check for potential issues
- **Stylers**: Tools that analyze code style and formatting
- **Database validation**: Checks that validate database references
- **User-defined annotations**: Notes added manually by developers

## Working with Annotations

### Viewing Annotations

- **Hover** over squiggly underlines or margin icons to see detailed information
- **Click** on margin icons to see full annotation details and available actions
- Use the **Problems panel** to see a list of all annotations in the current file

### Adding Manual Annotations

1. Right-click on a line of code
2. Select **Add Annotation** from the context menu
3. Choose the annotation type and enter your note
4. Click **OK** to add the annotation

### Managing Annotations

- **Filter annotations** by type in the Problems panel
- **Navigate between annotations** using F8 (next) and Shift+F8 (previous)
- **Disable specific annotation types** in the Options menu
- **Export annotations** to share with team members

## Customizing Annotations

You can customize how annotations appear in AppRefiner:

1. Go to **Tools > Options > AppRefiner > Annotations**
2. Adjust settings for:
   - **Colors**: Change the colors used for different annotation types
   - **Icons**: Select different icons for margin annotations
   - **Visibility**: Choose which annotation types to display
   - **Severity levels**: Adjust the threshold for warnings vs. errors

## Benefits

- **Improved code quality**: Quickly identify and fix potential issues
- **Better documentation**: Add context and explanations without cluttering code
- **Enhanced collaboration**: Share knowledge with team members through annotations
- **Faster debugging**: Annotations highlight potential problem areas

## Related Features

- [Code Folding](code-folding.md)
- [Linting Overview](../linting/overview.md)
