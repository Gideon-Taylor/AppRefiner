# Styler API

The Styler API provides interfaces and classes for implementing code styling and visual analysis in AppRefiner. This document describes the core components of the Styler API and how to use them effectively.

## Overview

The Styler API is built on top of the ANTLR4 parsing framework and provides mechanisms for:

1. Adding visual annotations to code
2. Highlighting specific code sections
3. Applying custom colors to code elements
4. Tracking variable usage and scope

The main components of this API are:

- `BaseStyler`: The abstract base class for all stylers
- `ScopedStyler<T>`: A specialized styler that tracks variable scopes
- Supporting structures: `CodeAnnotation`, `CodeHighlight`, and `CodeColor`

## BaseStyler

`BaseStyler` is the foundation of all styling operations in AppRefiner:

```csharp
public abstract class BaseStyler : PeopleCodeParserBaseListener
{
    public List<CodeAnnotation>? Annotations;
    public List<CodeHighlight>? Highlights;
    public List<CodeColor>? Colors;
    public List<Antlr4.Runtime.IToken>? Comments;
    public abstract void Reset();

    public bool Active = false;
    public string Description = "Description not set";
}
```

### Key Properties and Methods

- `Annotations`: List of text annotations to display alongside the code
- `Highlights`: List of background highlights to apply to code regions
- `Colors`: List of text color changes to apply to code regions
- `Comments`: Collection of comment tokens from the lexer
- `Reset()`: Clears the styler's state between analyses
- `Active`: Whether the styler is currently enabled
- `Description`: Human-readable description of what the styler does

## Supporting Structures

### CodeAnnotation

Represents a text annotation to be displayed alongside the code:

```csharp
public struct CodeAnnotation
{
    public string Message;
    public int LineNumber;
}
```

### CodeHighlight

Represents a background highlight to be applied to a region of code:

```csharp
public struct CodeHighlight
{
    public int Start { get; set; }
    public int Length { get; set; }
    public HighlightColor Color { get; set; }
}
```

### CodeColor

Represents a text color change to be applied to a region of code:

```csharp
public struct CodeColor
{
    public int Start { get; set; }
    public int Length { get; set; }
    public FontColor Color { get; set; }
}
```

## ScopedStyler<T>

`ScopedStyler<T>` extends `BaseStyler` with variable scope tracking capabilities:

```csharp
public abstract class ScopedStyler<T> : BaseStyler
{
    protected readonly Stack<Dictionary<string, T>> scopeStack = new();
    protected readonly Stack<Dictionary<string, VariableInfo>> variableScopeStack = new();

    protected ScopedStyler()
    {
        // Start with a global scope
        scopeStack.Push(new Dictionary<string, T>());
        variableScopeStack.Push(new Dictionary<string, VariableInfo>());
    }

    // Variable tracking methods
    protected void AddLocalVariable(string name, string type, int line, int start, int stop);
    protected bool TryGetVariableInfo(string name, out VariableInfo? info);
    protected IEnumerable<VariableInfo> GetVariablesInCurrentScope();
    protected void MarkVariableAsUsed(string name);
    protected abstract void OnExitScope(Dictionary<string, T> scope, Dictionary<string, VariableInfo> variableScope);

    // ANTLR listener methods for scope management
    public override void EnterFunctionDeclaration(FunctionDeclarationContext context);
    public override void ExitFunctionDeclaration(FunctionDeclarationContext context);
    public override void EnterMethodDeclaration(MethodDeclarationContext context);
    public override void ExitMethodDeclaration(MethodDeclarationContext context);
    // ... other scope-related methods
}
```

### Key Methods

- `AddLocalVariable`: Registers a new variable in the current scope
- `TryGetVariableInfo`: Attempts to find variable information across all scopes
- `GetVariablesInCurrentScope`: Returns all variables in the current scope
- `MarkVariableAsUsed`: Marks a variable as used
- `OnExitScope`: Abstract method called when exiting a scope, to be implemented by subclasses

## VariableInfo

The `VariableInfo` class tracks information about variables:

```csharp
public class VariableInfo
{
    public string Name { get; }
    public string Type { get; }
    public int Line { get; }
    public (int Start, int Stop) Span { get; }
    public bool Used { get; private set; }

    public VariableInfo(string name, string type, int line, (int Start, int Stop) span)
    {
        Name = name;
        Type = type;
        Line = line;
        Span = span;
        Used = false;
    }

    public void MarkAsUsed()
    {
        Used = true;
    }
}
```

## Creating a Custom Styler

To create a basic custom styler, extend the `BaseStyler` class:

```csharp
public class MyCustomStyler : BaseStyler
{
    public MyCustomStyler()
    {
        Description = "Highlights specific patterns in the code";
        Active = true;
    }

    public override void EnterSomeGrammarRule(SomeGrammarRuleContext context)
    {
        // Add a highlight for this rule
        Highlights?.Add(new CodeHighlight
        {
            Start = context.Start.StartIndex,
            Length = context.Stop.StopIndex - context.Start.StartIndex + 1,
            Color = HighlightColor.Yellow
        });

        // Add an annotation
        Annotations?.Add(new CodeAnnotation
        {
            Message = "This is a special pattern",
            LineNumber = context.Start.Line - 1
        });
    }

    public override void Reset()
    {
        Highlights?.Clear();
        Annotations?.Clear();
        Colors?.Clear();
    }
}
```

To create a scoped styler that tracks variables, extend the `ScopedStyler<T>` class:

```csharp
public class UnusedLocalVariableStyler : ScopedStyler<object>
{
    public UnusedLocalVariableStyler()
    {
        Description = "Grays out unused local variables.";
        Active = true;
    }

    protected override void OnExitScope(Dictionary<string, object> scope, Dictionary<string, VariableInfo> variableScope)
    {
        // Check for unused variables in the current scope
        foreach (var variable in variableScope.Values)
        {
            if (!variable.Used)
            {
                // Add highlight for unused variables
                Highlights?.Add(new CodeHighlight()
                {
                    Color = HighlightColor.Gray,
                    Start = variable.Span.Start,
                    Length = variable.Span.Stop - variable.Span.Start + 1
                });
            }
        }
    }

    public override void Reset()
    {
        base.Reset();
        Highlights?.Clear();
    }
}
```

## Best Practices

1. **Performance**: Keep stylers efficient, as they run frequently during editing
2. **Visual Clarity**: Use consistent highlighting and coloring patterns
3. **Complementary Styling**: Ensure your stylers work well with syntax highlighting
4. **Configurability**: Allow users to enable/disable stylers individually
5. **Documentation**: Document what visual cues your styler provides

## See Also

- [Linter API](linter-api.md)
- [Refactor API](refactor-api.md)
- [Creating Custom Stylers](../extension-api/custom-stylers.md)
