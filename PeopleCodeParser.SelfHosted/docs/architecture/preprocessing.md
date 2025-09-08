# Directive Preprocessing

The `DirectivePreprocessor` handles PeopleCode compiler directives in a first pass before main parsing begins. This document explains how conditional compilation directives are processed and evaluated.

## Overview

PeopleCode supports **conditional compilation** through preprocessor directives that can include or exclude code sections based on version conditions. The preprocessor resolves these directives against a target `ToolsVersion` before the main parser processes the token stream.

```csharp
var preprocessor = new DirectivePreprocessor(tokens, toolsVersion);
var processedTokens = preprocessor.ProcessDirectives();
```

## Directive Syntax

### Conditional Compilation Directives

**#If Directive:**
```peoplecode
#If ToolsRelease >= "8.1"
    // Code for PeopleTools 8.1 and later
#Then
    // Alternative code for earlier versions  
#Else  
    // Code for versions before 8.1
#End-If
```

**Supported Comparison Operators:**
- `>=` - Greater than or equal
- `>` - Greater than
- `<=` - Less than or equal
- `<` - Less than  
- `=` - Equal to
- `<>` - Not equal to

**Version Format:**
- Standard format: `"8.54.03"`
- Major.Minor format: `"8.1"`  
- Major only: `"8"`

### Nested Directives

Directives can be nested to handle complex version logic:

```peoplecode
#If ToolsRelease >= "8.5"
    // PeopleTools 8.5+ code
    #If ToolsRelease >= "8.6"
        // Additional 8.6+ features
    #End-If
#Else
    // Legacy code for pre-8.5
#End-If
```

## ToolsVersion Evaluation

### Version Comparison Logic

The preprocessor uses semantic version comparison:

```csharp
public class ToolsVersion
{
    public int Major { get; }
    public int Minor { get; }  
    public int Patch { get; }
    
    public int CompareTo(ToolsVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        
        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;
        
        return Patch.CompareTo(other.Patch);
    }
}
```

### Default Version Policy

When no explicit ToolsVersion is provided, the preprocessor uses **"99.99.99"** representing the "newest version" policy:

```csharp
private ToolsVersion _toolsRelease = new("99.99.99");
```

This ensures that:
- Code without version constraints is always included
- Future version checks don't accidentally exclude code
- Development scenarios have reasonable defaults

### Version Parsing

Version strings are parsed flexibly:

- `"8.54.03"` → Major: 8, Minor: 54, Patch: 3
- `"8.1"` → Major: 8, Minor: 1, Patch: 0  
- `"8"` → Major: 8, Minor: 0, Patch: 0

## Processing Algorithm

### Directive Context Stack

The preprocessor maintains a stack to track nested directive contexts:

```csharp
private class DirectiveContext
{
    public bool IsActive { get; set; }     // Should this branch be included?
    public bool HasActiveBranch { get; set; } // Has any branch been active?
    public bool InElseBranch { get; set; }    // Currently in #Else section?
}
```

### Token Processing Loop

The main processing algorithm:

```csharp
public List<Token> ProcessDirectives()
{
    var result = new List<Token>();
    var directiveStack = new Stack<DirectiveContext>();
    var position = 0;
    
    while (position < _originalTokens.Count)
    {
        var token = _originalTokens[position];
        
        if (IsDirectiveToken(token))
        {
            position = ProcessDirective(token, position, directiveStack);
        }
        else if (IsActiveContext(directiveStack))
        {
            result.Add(token);
            position++;
        }
        else
        {
            // Skip token in inactive branch
            RecordSkippedSpan(token);
            position++;
        }
    }
    
    return result;
}
```

### Directive Recognition

Directives are identified by specific comment patterns:

```csharp
private bool IsDirectiveToken(Token token)
{
    if (!token.Type.IsCommentType()) return false;
    
    var text = token.Text.Trim();
    return text.StartsWith("#If", StringComparison.OrdinalIgnoreCase) ||
           text.StartsWith("#Then", StringComparison.OrdinalIgnoreCase) ||
           text.StartsWith("#Else", StringComparison.OrdinalIgnoreCase) ||
           text.StartsWith("#End-If", StringComparison.OrdinalIgnoreCase);
}
```

## Expression Evaluation

### Condition Parsing

The preprocessor parses version comparison expressions:

```csharp
private bool EvaluateCondition(string condition)
{
    // Parse: ToolsRelease >= "8.54"
    var match = Regex.Match(condition, 
        @"ToolsRelease\s*([><=]+)\s*""([^""]+)""", 
        RegexOptions.IgnoreCase);
    
    if (!match.Success)
    {
        ReportError($"Invalid directive condition: {condition}");
        return false; // Default to false for invalid conditions
    }
    
    var operatorStr = match.Groups[1].Value;
    var versionStr = match.Groups[2].Value;
    var targetVersion = new ToolsVersion(versionStr);
    
    return EvaluateComparison(_toolsVersion, operatorStr, targetVersion);
}
```

### Comparison Operations

Version comparisons are evaluated based on the operator:

```csharp
private bool EvaluateComparison(ToolsVersion current, string op, ToolsVersion target)
{
    var comparison = current.CompareTo(target);
    
    return op switch
    {
        ">=" => comparison >= 0,
        ">" => comparison > 0,
        "<=" => comparison <= 0,
        "<" => comparison < 0,
        "=" => comparison == 0,
        "<>" => comparison != 0,
        _ => throw new ArgumentException($"Unknown operator: {op}")
    };
}
```

## Error Handling

### Malformed Directives

When directives have syntax errors:

```csharp
private void ReportError(string message)
{
    _errors.Add($"Directive preprocessing error: {message}");
}
```

Common errors:
- `"Unmatched #If directive at line 15"`
- `"#Else without corresponding #If at line 23"`  
- `"Invalid version format '8.x.1' at line 8"`
- `"Missing #End-If for directive started at line 5"`

### Recovery Strategies

**Unmatched Directives:**
- Missing `#End-If`: Assume it occurs at end of file
- Extra `#Else`: Treat as start of new else branch
- Invalid conditions: Default to `false` (exclude code)

**Version Format Errors:**
- Invalid versions: Use `"0.0.0"` as fallback
- Missing quotes: Attempt to parse without quotes
- Empty versions: Default to `"0.0.0"`

## Span Tracking

### Skipped Code Regions

The preprocessor tracks which source regions were excluded:

```csharp
public List<SourceSpan> SkippedSpans { get; } = new();

private void RecordSkippedSpan(Token token)
{
    var span = new SourceSpan(token.SourceSpan.Start, token.SourceSpan.End);
    SkippedSpans.Add(span);
}
```

### Integration with AST

Skipped spans are attached to the root `ProgramNode`:

```csharp
public class ProgramNode : AstNode
{
    public List<SourceSpan> SkippedDirectiveSpans { get; set; } = new();
}
```

This enables tooling features:
- **Syntax highlighting** can gray out inactive code sections
- **Code folding** can collapse inactive directive branches
- **Refactoring tools** can understand which code is conditionally compiled

## Performance Characteristics

### Single-Pass Processing

The preprocessor processes tokens in a single forward pass, maintaining linear time complexity.

### Memory Efficiency

Only tokens in active branches are retained in the output, potentially reducing memory usage for large files with many inactive sections.

### Minimal Parsing Overhead

Directive evaluation uses simple regular expressions and string comparisons, adding minimal overhead to the tokenization process.

## Usage Patterns

### Development Workflow

**Version-Specific Development:**
```csharp
// Target specific PeopleTools version
var preprocessor = new DirectivePreprocessor(tokens, new ToolsVersion("8.54"));
```

**Latest Version Development:**
```csharp
// Use default "newest version" policy  
var preprocessor = new DirectivePreprocessor(tokens, null);
```

**Legacy Code Support:**
```csharp
// Support older PeopleTools versions
var preprocessor = new DirectivePreprocessor(tokens, new ToolsVersion("8.1"));
```

### Integration with Parser

The preprocessor integrates seamlessly with the parsing pipeline:

```csharp
// Complete parsing workflow
var lexer = new PeopleCodeLexer(sourceCode);
var tokens = lexer.TokenizeAll();

var preprocessor = new DirectivePreprocessor(tokens, toolsVersion);
var processedTokens = preprocessor.ProcessDirectives();

var parser = new PeopleCodeParser(processedTokens, toolsVersion);
var program = parser.ParseProgram();

// Access skipped regions
program.SkippedDirectiveSpans = preprocessor.SkippedSpans;
```

## Limitations and Considerations

### Directive Placement

Directives can appear **anywhere** in the token stream, not just at statement boundaries. This provides maximum flexibility but requires careful handling:

```peoplecode
LOCAL string &name #If ToolsRelease >= "8.5" = "default" #End-If ;
```

### Complex Expressions

Currently, only simple version comparisons are supported. Complex boolean expressions are not implemented:

```peoplecode
// NOT SUPPORTED:
#If ToolsRelease >= "8.5" AND ToolsRelease < "9.0"
```

### Runtime vs Compile-Time

Directives are resolved at **parse time**, not runtime. The target version must be known during parsing, not during code execution.