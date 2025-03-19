# Refactoring Overview

Code refactoring is the process of restructuring existing code without changing its external behavior. AppRefiner provides powerful refactoring tools specifically designed for PeopleCode to help you improve code quality, maintainability, and readability.

## What is Refactoring?

Refactoring is a disciplined technique for restructuring an existing body of code, altering its internal structure without changing its external behavior. The goal of refactoring is to improve non-functional attributes of the software, such as:

- **Readability**: Making code easier to understand
- **Maintainability**: Making code easier to modify and extend
- **Performance**: Improving execution efficiency
- **Reusability**: Making code more modular and reusable
- **Testability**: Making code easier to test

## AppRefiner's Refactoring Tools

AppRefiner provides a comprehensive set of refactoring tools specifically designed for PeopleCode:

### 1. Rename Refactoring

Safely rename variables, functions, methods, and classes throughout your codebase:

- **Context-aware**: Understands PeopleCode scope rules
- **Preview changes**: See all occurrences before applying
- **Reference tracking**: Updates all references automatically

### 2. Extract Method/Function

Convert a code fragment into a method or function:

- **Parameter detection**: Automatically identifies required parameters
- **Return value analysis**: Determines appropriate return type
- **Scope management**: Handles variable scope transitions

### 3. Inline Method/Function

Replace a method or function call with its body:

- **Usage analysis**: Ensures inlining won't cause issues
- **Parameter substitution**: Properly substitutes parameters
- **Format preservation**: Maintains code formatting

### 4. Import Optimization

Manage and optimize import statements:

- **Remove unused imports**: Eliminate unnecessary imports
- **Organize imports**: Sort and group import statements
- **Add missing imports**: Identify and add required imports

### 5. Code Cleanup

Apply various code cleanup operations:

- **Format code**: Apply consistent formatting
- **Remove unused variables**: Eliminate dead code
- **Simplify expressions**: Convert complex expressions to simpler forms
- **Standardize syntax**: Use consistent syntax patterns

### 6. FlowerBox Headers

Add or update documentation headers:

- **Template-based**: Use customizable templates
- **Auto-population**: Fill in function parameters and return types
- **Batch processing**: Apply to multiple files at once

## How to Use Refactoring Tools

AppRefiner's refactoring tools can be accessed in several ways:

### Context Menu

1. Right-click on the code element you want to refactor
2. Select **Refactor** from the context menu
3. Choose the specific refactoring operation

### Keyboard Shortcuts

- **Rename**: F2 or Ctrl+R, R
- **Extract Method**: Ctrl+R, M
- **Inline Method**: Ctrl+R, I
- **Optimize Imports**: Ctrl+R, O

### Refactor Menu

1. Select the code you want to refactor
2. Go to **Edit > Refactor**
3. Choose the specific refactoring operation

## Refactoring Workflow

A typical refactoring workflow in AppRefiner:

1. **Identify code to refactor**: Locate code that needs improvement
2. **Select refactoring operation**: Choose the appropriate refactoring tool
3. **Configure options**: Set any specific options for the refactoring
4. **Preview changes**: Review the changes before applying
5. **Apply refactoring**: Implement the changes
6. **Verify behavior**: Ensure the code still works as expected

## Safe Refactoring

AppRefiner ensures safe refactoring through several mechanisms:

### 1. Syntax Verification

Before applying changes, AppRefiner verifies that the refactored code will be syntactically valid.

### 2. Preview Changes

AppRefiner shows you exactly what changes will be made before applying them:

- **Side-by-side comparison**: See before and after code
- **File list**: See all files that will be modified
- **Change summary**: Get a summary of the types of changes

### 3. Undo Support

If you're not satisfied with a refactoring, you can undo it:

- **Single-step undo**: Ctrl+Z to undo the entire refactoring
- **Change history**: View and revert specific changes

### 4. Conflict Detection

AppRefiner detects potential conflicts that might arise from refactoring:

- **Name conflicts**: Identifies potential naming collisions
- **Scope issues**: Detects scope-related problems
- **Reference ambiguities**: Highlights ambiguous references

## Best Practices for Refactoring

1. **Refactor incrementally**: Make small, focused changes rather than large-scale refactorings
2. **Test after each refactoring**: Verify that behavior is preserved
3. **Commit refactorings separately**: Keep refactoring commits separate from feature changes
4. **Document your refactorings**: Explain why you made certain refactoring decisions
5. **Use automated refactoring tools**: Avoid manual search-and-replace operations

## Related Features

- [Variable Renaming](variable-renaming.md)
- [Import Optimization](import-optimization.md)
- [FlowerBox Headers](flowerbox-headers.md)
- [Linting Overview](../linting/overview.md)
