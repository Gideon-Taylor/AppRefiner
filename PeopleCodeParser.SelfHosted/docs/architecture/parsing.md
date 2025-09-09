# Parsing Strategy

The `PeopleCodeParser` implements a **recursive descent parser** with sophisticated error recovery mechanisms. This document explains the parsing approach, error handling strategies, and recovery techniques used to handle malformed code.

## Overview

The parser transforms a stream of tokens into an Abstract Syntax Tree (AST) using **recursive descent parsing**. This approach directly mirrors the grammar structure with each grammatical construct implemented as a parsing method.

```csharp
var parser = new PeopleCodeParser(tokens, toolsVersion);
var program = parser.ParseProgram();
```

## Recursive Descent Architecture

### Grammar-to-Method Mapping

Each grammar rule corresponds to a parsing method:

```csharp
// Grammar rule: statement := ifStatement | whileStatement | expressionStatement
private StatementNode ParseStatement()
{
    return CurrentToken.Type switch
    {
        TokenType.If => ParseIfStatement(),
        TokenType.While => ParseWhileStatement(), 
        _ => ParseExpressionStatement()
    };
}
```

### Predictive Parsing

The parser uses **lookahead** to make parsing decisions:

```csharp
private ExpressionNode ParsePrimaryExpression()
{
    switch (CurrentToken.Type)
    {
        case TokenType.Number:
            return ParseNumericLiteral();
        case TokenType.String:
            return ParseStringLiteral();
        case TokenType.UserVariable:
            return ParseIdentifier();
        case TokenType.LeftParen:
            return ParseParenthesizedExpression();
        default:
            return HandleUnexpectedToken();
    }
}
```

### Left-Recursion Elimination  

PeopleCode has left-recursive constructs like method calls and member access. The parser handles these using **precedence climbing**:

```csharp
private ExpressionNode ParseExpression()
{
    return ParseBinaryExpression(0); // Start with minimum precedence
}

private ExpressionNode ParseBinaryExpression(int minPrec)
{
    var left = ParseUnaryExpression();
    
    while (IsBinaryOperator(CurrentToken.Type))
    {
        var op = GetBinaryOperator(CurrentToken.Type);
        var prec = GetPrecedence(op);
        
        if (prec < minPrec) break;
        
        Consume(); // Consume operator
        var right = ParseBinaryExpression(prec + (IsLeftAssociative(op) ? 1 : 0));
        left = new BinaryOperationNode(left, op, false, right);
    }
    
    return left;
}
```

## Token Management

### Current Token Tracking

The parser maintains a **current position** in the token stream:

```csharp
private int _position;
private Token CurrentToken => _position < _tokens.Count ? _tokens[_position] : Token.EndOfFile;
private Token NextToken => _position + 1 < _tokens.Count ? _tokens[_position + 1] : Token.EndOfFile;
```

### Token Consumption

The `Consume()` method advances the position and provides optional validation:

```csharp
private Token Consume(TokenType? expected = null)
{
    var token = CurrentToken;
    
    if (expected.HasValue && token.Type != expected.Value)
    {
        ReportError($"Expected {expected.Value}, found {token.Type}", token.SourceSpan);
        return token; // Continue parsing despite error
    }
    
    _position++;
    return token;
}
```

### Lookahead Utilities

Helper methods provide easy lookahead access:

```csharp
private bool IsCurrentToken(TokenType type) => CurrentToken.Type == type;
private bool IsCurrentToken(params TokenType[] types) => types.Contains(CurrentToken.Type);
private bool IsNextToken(TokenType type) => NextToken.Type == type;
```

## Error Recovery Strategies

### Synchronization Points

When the parser encounters an error, it uses **synchronization tokens** to find safe points to resume parsing:

```csharp
private static readonly HashSet<TokenType> StatementSyncTokens = new()
{
    TokenType.Semicolon,
    TokenType.If, TokenType.For, TokenType.While,
    TokenType.Local, TokenType.Global,
    TokenType.EndIf, TokenType.EndFor, TokenType.EndWhile,
    // ... other safe recovery points
};
```

### Recovery Methods

**Skip to Synchronization Point:**
```csharp
private void SkipToSynchronizationPoint(HashSet<TokenType> syncTokens)
{
    while (!IsEndOfTokens() && !syncTokens.Contains(CurrentToken.Type))
    {
        Consume();
    }
}
```

**Recover from Statement Errors:**
```csharp
private StatementNode ParseStatementWithRecovery()
{
    try
    {
        return ParseStatement();
    }
    catch (ParseException)
    {
        SkipToSynchronizationPoint(StatementSyncTokens);
        return new ErrorStatementNode(CurrentToken);
    }
}
```

### Error Context Preservation

The parser maintains a **rule stack** for context-aware error messages:

```csharp
private readonly Stack<string> _ruleStack = new();

private T ParseWithContext<T>(string ruleName, Func<T> parser)
{
    _ruleStack.Push(ruleName);
    try
    {
        return parser();
    }
    finally
    {
        _ruleStack.Pop();
    }
}
```

Error messages include parsing context:
```
"Expected ';' after expression statement in method body at line 15"
```

## Specific Parsing Strategies

### Program Structure

The parser starts with the top-level `ParseProgram()` method:

```csharp
public ProgramNode ParseProgram()
{
    var program = new ProgramNode();
    
    // Parse imports first
    while (IsCurrentToken(TokenType.Import))
    {
        program.AddImport(ParseImport());
    }
    
    // Parse global declarations
    while (IsGlobalDeclaration())
    {
        ParseGlobalDeclaration(program);
    }
    
    // Parse class or interface definition
    if (IsCurrentToken(TokenType.Class))
    {
        program.SetAppClass(ParseAppClass());
    }
    else if (IsCurrentToken(TokenType.Interface))
    {
        program.SetInterface(ParseInterface());
    }
    
    // Parse remaining content (functions, main statements)
    ParseProgramBody(program);
    
    return program;
}
```

### Expression Parsing

Expressions use **precedence climbing** to handle operator precedence correctly:

| Precedence | Operators | Associativity |
|------------|-----------|---------------|
| 1 | `OR` | Left |
| 2 | `AND` | Left |
| 3 | `=`, `<>` | Left |
| 4 | `<`, `<=`, `>`, `>=` | Left |
| 5 | `\|` (concatenation) | Left |
| 6 | `+`, `-` | Left |  
| 7 | `*`, `/` | Left |
| 8 | `**` (exponentiation) | Right |
| 9 | Unary `-`, `NOT`, `@` | Right |

### Statement Block Parsing

Blocks handle variable statement types and optional semicolons:

```csharp
private BlockNode ParseBlock()
{
    var block = new BlockNode();
    
    while (!IsBlockTerminator() && !IsEndOfTokens())
    {
        var statement = ParseStatement();
        block.AddStatement(statement);
        
        // Semicolon is optional in many contexts
        if (IsCurrentToken(TokenType.Semicolon))
        {
            statement.HasSemicolon = true;
            Consume(TokenType.Semicolon);
        }
    }
    
    return block;
}
```

### Type Parsing

Type references support built-in types, arrays, and user-defined classes:

```csharp
private TypeNode ParseType()
{
    switch (CurrentToken.Type)
    {
        case TokenType.Any:
        case TokenType.String:
        case TokenType.Integer:
        // ... other built-in types
            return ParseBuiltInType();
            
        case TokenType.Array:
        case TokenType.Array2:
        // ... other array types
            return ParseArrayType();
            
        default:
            return ParseUserDefinedType();
    }
}
```

## Error Reporting

### ParseError Structure

Errors contain comprehensive diagnostic information:

```csharp
public class ParseError
{
    public string Message { get; }
    public SourceSpan Location { get; }
    public ParseErrorSeverity Severity { get; }
    public string Context { get; }
    public IReadOnlyList<string> RuleStack { get; }
}
```

### Error Severity Levels

- **Error**: Syntax violations that prevent correct parsing
- **Warning**: Questionable constructs that parse but may be problematic
- **Information**: Style suggestions and minor issues

### Contextual Error Messages

The parser generates specific error messages based on context:

```csharp
private void ReportError(string message, SourceSpan location)
{
    var context = string.Join(" -> ", _ruleStack);
    var error = new ParseError(message, location, ParseErrorSeverity.Error, context);
    _errors.Add(error);
}
```

Examples:
- `"Expected 'END-IF' to close IF statement at line 10"`
- `"Missing semicolon after variable declaration at line 15"`
- `"Unexpected token ')' in expression at line 8"`

## Recovery Quality

### Partial Results

Even with syntax errors, the parser provides useful partial results:

- **Method signatures** are parsed even if bodies contain errors
- **Variable declarations** are recognized even with initialization errors
- **Class structure** is maintained even with member parsing failures

### Multiple Error Reporting

The parser continues after errors to find additional issues:

```csharp
private void ParseStatements(BlockNode block)
{
    while (!IsBlockEnd())
    {
        try
        {
            var stmt = ParseStatement();
            block.AddStatement(stmt);
        }
        catch (ParseException ex)
        {
            _errors.Add(ex.Error);
            RecoverFromStatementError();
            // Continue parsing next statement
        }
    }
}
```

### Error Recovery Limits

To prevent infinite loops in pathological cases:

```csharp
private const int MaxErrorRecoveryAttempts = 10;
private int _errorRecoveryCount = 0;

private void RecoverFromError()
{
    if (++_errorRecoveryCount > MaxErrorRecoveryAttempts)
    {
        throw new ParseException("Too many consecutive errors, stopping parse");
    }
    
    // Perform recovery...
    
    // Reset counter on successful parse
    if (ParseNextStatement() != null)
    {
        _errorRecoveryCount = 0;
    }
}
```

## Performance Considerations

### Single-Pass Parsing

The parser processes tokens in a single forward pass without backtracking, ensuring linear time complexity for valid input.

### Memory Efficiency

AST nodes are created on-demand and parent-child relationships are established immediately, avoiding temporary data structures.

### Error Recovery Overhead

Error recovery mechanisms add minimal overhead to successful parses but provide substantial benefits when handling malformed code.

[PLACEHOLDER - Parse time benchmarks can be measured by adding timing to PeopleCodeParser.ParseProgram()]

## Integration Points

### Directive Preprocessing

The parser expects preprocessed tokens from `DirectivePreprocessor`:

```csharp
var preprocessor = new DirectivePreprocessor(rawTokens, toolsVersion);
var filteredTokens = preprocessor.ProcessDirectives();
var parser = new PeopleCodeParser(filteredTokens, toolsVersion);
```

### AST Consumer Interface

The parser produces a complete AST that can be consumed by:
- **Semantic analyzers** for type checking and symbol resolution
- **Code generators** for compilation or transformation  
- **Refactoring tools** for code manipulation
- **Analysis tools** for metrics and quality assessment