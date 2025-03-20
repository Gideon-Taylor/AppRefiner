# Code Styling

AppRefiner includes several code styling features that help you identify potential issues in your code by highlighting specific patterns directly in the editor. These visual cues make it easier to spot common problems without interrupting your workflow.

## Available Stylers

AppRefiner includes the following stylers that highlight different aspects of your code:

### Linter Suppression Styler

**File:** `LinterSuppressionStyler.cs`

This styler highlights linter suppression comments in your code with a light red background. It specifically looks for comments in the format:

```
/* #AppRefiner suppress (rule_name) */
```

These comments are used to suppress specific linter warnings in your code.

### Meaningless Variable Name Styler

**File:** `MeaninglessVariableNameStyler.cs`

This styler highlights variable names that are considered too generic or meaningless with a yellow background. It checks variable names against a predefined list that includes:

- Single letters (a-z)
- Double letters (aa-zz)
- Generic names like "var", "temp", "obj", "str", etc.

Using descriptive variable names improves code readability and maintainability.

### Property As Variable Styler

**File:** `PropertyAsVariable.cs`

This styler highlights instances where class properties are used as variables outside of constructors with a light green background. This helps identify potential issues where properties might be used incorrectly as regular variables.

The styler tracks public and protected properties and highlights them when they're used outside of a constructor context.

### Unused Imports Styler

**File:** `UnusedImportsListener.cs`

This styler grays out import statements that aren't used anywhere in the code. Removing unused imports helps keep your code clean and can improve compilation times.

The styler tracks all import declarations and checks if they're referenced elsewhere in the code.

### Unused Local Variables Styler

**File:** `UnusedLocalVariableStyler.cs`

This styler grays out local variables that are declared but never used in your code. Unused variables can indicate potential bugs or unnecessary code.

The styler tracks variable declarations and usages within their respective scopes and highlights any variables that are never referenced.

## Scoped Styler Base Class

Many of the stylers inherit from the `ScopedStyler<T>` base class, which provides functionality for tracking variables and their usage across different scopes in the code. This base class handles the complexities of scope management, making it easier to implement stylers that need to track variables across different code blocks.

## Enabling/Disabling Stylers

Each styler can be individually enabled or disabled through the AppRefiner settings. By default, all stylers are enabled, but you can customize which ones are active based on your preferences.
