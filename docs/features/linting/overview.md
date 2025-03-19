# Linting Overview

Linting in AppRefiner is a powerful code analysis feature that helps identify potential errors, bugs, and stylistic issues in your PeopleCode directly within PeopleSoft Application Designer.

## What is Linting?

Linting is the process of analyzing source code to flag programming errors, bugs, stylistic errors, and suspicious constructs. AppRefiner's linting system examines your PeopleCode as you type in Application Designer, identifying potential issues before they cause problems at runtime.

## How Linting Works in AppRefiner

AppRefiner's linting system runs alongside Application Designer and is built on a robust parsing engine that understands PeopleCode syntax and semantics. The linting process involves:

1. **Parsing**: Converting your code into an Abstract Syntax Tree (AST)
2. **Analysis**: Applying lint rules to the AST to identify issues
3. **Reporting**: Displaying warnings and errors in the Application Designer editor through visual indicators

## Types of Lint Rules

AppRefiner includes several categories of lint rules:

### Syntax Rules

These rules check for basic syntax errors and issues:
- **Missing semicolons**
- **Unmatched parentheses or brackets**
- **Invalid operators or expressions**
- **Incorrect function call syntax**

### Semantic Rules

These rules analyze the meaning and logic of your code:
- **Unused variables**
- **Unreachable code**
- **Assignment in conditionals**
- **Duplicate variable declarations**
- **Missing return statements**

### Best Practice Rules

These rules enforce coding standards and best practices:
- **Consistent naming conventions**
- **Proper error handling**
- **Function complexity limits**
- **Comment requirements for functions**
- **Deprecated API usage**

### PeopleSoft-Specific Rules

These rules are specific to PeopleSoft development:
- **Proper SQL binding variable usage**
- **Component buffer access patterns**
- **SavePreChange/SavePostChange usage**
- **RowInit/RowSelect event handling**
- **PeopleCode function restrictions**

## Linting Configuration

AppRefiner allows you to customize the linting experience:

### Rule Configuration

1. Open AppRefiner settings
2. Navigate to the Linting section
3. Enable or disable specific lint rules
4. Adjust severity levels (Error, Warning, Information, Hint)
5. Configure rule-specific parameters

### Project-Level Configuration

You can create project-specific linting configurations:

1. Create a `.apprefiner` file in your project root
2. Define which rules to enable, disable, or customize
3. Share this configuration with your team for consistent standards

## Working with Lint Results

### Viewing Lint Issues

- **In-editor indicators**: Squiggly underlines and margin icons in Application Designer
- **Problems panel**: List of all issues with filtering options in AppRefiner
- **Quick Fix suggestions**: Available for many common issues

### Fixing Lint Issues

- **Hover over an issue** to see a detailed description
- **Use the Command Palette** (Ctrl+Shift+P) to access quick fixes
- **Apply Quick Fixes** to automatically resolve common issues
- **Suppress warnings** with special comments when appropriate

### Suppressing Lint Warnings

Sometimes you may need to suppress specific lint warnings:

```peoplecode
/* lint:disable UnusedVariable */
Local string &tempVar;
/* lint:enable UnusedVariable */
```

## Benefits of Linting

- **Catch errors early**: Identify issues before they cause runtime problems
- **Improve code quality**: Enforce consistent coding standards
- **Enhance readability**: Promote clear, maintainable code
- **Facilitate learning**: Learn best practices through lint suggestions
- **Increase productivity**: Spend less time debugging common issues

## Related Features

- [Available Lint Rules](available-rules.md)
- [Custom Lint Rules](custom-rules.md)
- [Suppressing Lint Warnings](suppressing-warnings.md)
