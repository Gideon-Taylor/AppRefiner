# Lexical Analysis

The `PeopleCodeLexer` is responsible for converting raw source text into a stream of tokens that the parser can understand. This document covers the tokenization process, token types, and trivia handling.

## Overview

The lexer operates as a **single-pass scanner** that reads through the source text character by character, identifying and classifying language constructs into tokens. It maintains position information and preserves formatting details for tooling support.

```csharp
var lexer = new PeopleCodeLexer(sourceCode);
var tokens = lexer.TokenizeAll();
```

## Token Classification

### Keywords
The lexer recognizes all PeopleCode keywords with **case-insensitive** matching:

**Control Flow Keywords:**
```
IF, THEN, ELSE, END-IF
FOR, TO, STEP, END-FOR  
WHILE, END-WHILE
REPEAT, UNTIL
EVALUATE, WHEN, WHEN-OTHER, END-EVALUATE
TRY, CATCH, END-TRY
BREAK, CONTINUE, RETURN, EXIT, ERROR, WARNING
```

**Declaration Keywords:**
```
CLASS, END-CLASS
INTERFACE, END-INTERFACE  
METHOD, END-METHOD
PROPERTY, GET, SET, END-GET, END-SET
FUNCTION, END-FUNCTION
CONSTANT, LOCAL, GLOBAL, COMPONENT, INSTANCE
```

**Type Keywords:**
```
ANY, BOOLEAN, DATE, DATETIME, EXCEPTION
FLOAT, INTEGER, NUMBER, STRING, TIME
ARRAY, ARRAY2, ARRAY3... (up to ARRAY9)
```

**Modifier Keywords:**
```
ABSTRACT, PRIVATE, PROTECTED, READONLY
EXTENDS, IMPLEMENTS, IMPORT
CREATE, AS, OF, OUT, REF
```

### Operators

**Arithmetic Operators:**
- `+` (Addition)
- `-` (Subtraction) 
- `*` (Multiplication)
- `/` (Division)
- `**` (Exponentiation)

**Comparison Operators:**
- `=` (Equality)
- `<>` (Inequality)
- `<` (Less than)
- `<=` (Less than or equal)
- `>` (Greater than)
- `>=` (Greater than or equal)

**Logical Operators:**  
- `AND` (Logical and)
- `OR` (Logical or)
- `NOT` (Logical not)

**Assignment Operators:**
- `=` (Assignment)
- `+=` (Add and assign)
- `-=` (Subtract and assign) 
- `|=` (Concatenate and assign)

**Other Operators:**
- `|` (String concatenation)
- `@` (Reference operator)

### Literals

**String Literals:**
- Delimited by double quotes: `"Hello World"`
- Support escape sequences: `"Line 1\nLine 2"`
- Can span multiple lines with continuation

**Numeric Literals:**
- Integers: `123`, `0`, `-456`
- Decimals: `3.14159`, `0.5`, `-2.718`
- Scientific notation: `1.23E+10`, `5.67e-8`

**Boolean Literals:**
- `TRUE` and `FALSE` (case insensitive)

**Special Literals:**
- `NULL` (represents null/empty values)

### Identifiers

**User Variables:**
- Start with `&`: `&MyVariable`, `&count`
- Case insensitive: `&MyVar` equals `&myvar`

**System Variables:**
- Start with `%`: `%USERID`, `%DATETIME`, `%COMPONENT`
- Predefined by the system

**Generic Identifiers:**
- Function names, class names, method names
- Must start with letter or underscore
- Can contain letters, digits, underscores
- Case insensitive: `MyFunction` equals `myfunction`

### Comments

**Line Comments:**
- Start with `REM` or `//`
- Continue until end of line
- Example: `REM This is a comment`
- Example: `// Another comment style`

**Block Comments:**
- Delimited by `/*` and `*/`
- Can span multiple lines
- Can be nested in some contexts
- Example: `/* Multi-line comment */`

**Documentation Comments:**
- Special block comments starting with `/+`
- Used for method annotations and documentation
- Example: `/+ DOC: This method calculates totals +/`

### Punctuation

**Delimiters:**
- `(` `)` - Parentheses for grouping and parameters
- `[` `]` - Brackets for array access
- `{` `}` - Braces (limited use in PeopleCode)

**Separators:**
- `;` - Statement terminator (optional in many contexts)
- `,` - Parameter and list separator
- `.` - Member access operator  
- `:` - Package separator and other contexts

## Tokenization Process

### Character-by-Character Scanning

The lexer maintains several state variables as it processes the source:

- **Position**: Current character index in source
- **Line/Column**: Current line and column numbers (1-based)
- **Byte Index**: UTF-8 byte position for editor integration

### Lookahead and Backtracking

The lexer uses **single-character lookahead** for most decisions:

```csharp
private char Peek() => _position < _source.Length ? _source[_position] : '\0';
private char PeekNext() => _position + 1 < _source.Length ? _source[_position + 1] : '\0';
```

For complex operators like `<=`, `>=`, `<>`, `**`, the lexer looks ahead to determine the complete operator.

### Keyword Recognition

Identifiers are first tokenized as generic identifiers, then checked against the keyword dictionary:

```csharp
private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
{
    { "IF", TokenType.If },
    { "THEN", TokenType.Then },
    // ... full keyword mapping
};
```

This approach ensures case-insensitive keyword matching while maintaining efficiency.

## Trivia Handling

**Trivia** refers to source text that doesn't affect program semantics but is important for formatting and tooling:

- Whitespace (spaces, tabs)
- Line endings (`\r\n`, `\n`, `\r`)
- Comments (line and block)

### Leading and Trailing Trivia

Each token can have associated **leading trivia** (trivia before the token) and **trailing trivia** (trivia after the token):

```csharp
public class Token
{
    public List<Token> LeadingTrivia { get; } = new();
    public List<Token> TrailingTrivia { get; } = new();
    // ... other properties
}
```

**Example:**
```peoplecode
    // This is a comment
    LOCAL string &name; // Another comment
```

The `LOCAL` token would have:
- **Leading trivia**: Whitespace and the first comment
- **Trailing trivia**: The second comment

### Trivia Preservation

Trivia preservation enables:
- **Code formatting tools** that preserve original formatting
- **Syntax highlighting** that can color comments appropriately
- **Documentation extraction** from comment trivia
- **Refactoring tools** that maintain code style

## Position Tracking

The lexer maintains comprehensive position information for each token:

```csharp
public struct SourcePosition
{
    public int Index { get; }      // Character index (0-based)
    public int ByteIndex { get; }  // UTF-8 byte index (0-based)
    public int Line { get; }       // Line number (1-based)
    public int Column { get; }     // Column number (1-based)
}
```

### UTF-8 Byte Indexing

PeopleCode can contain international characters, so the lexer tracks both character positions and UTF-8 byte positions:

- **Character positions**: Used for logical operations and Unicode handling
- **Byte positions**: Used for editor integration and binary operations

## Error Handling

### Invalid Characters

When the lexer encounters invalid characters, it:
1. Creates an `Error` token with the invalid character
2. Reports the error with precise location information
3. Continues scanning from the next character

### Unterminated Strings

For unterminated string literals:
1. The lexer continues until end of line or end of file
2. Creates a `String` token with the partial content
3. Reports an error about the unterminated string
4. Resumes normal scanning

### Malformed Numbers

For invalid numeric literals:
1. Collects all characters that could be part of a number
2. Creates a `Number` token with the malformed content
3. Reports the specific formatting error
4. Continues with normal tokenization

## Performance Characteristics

### Token Caching
Tokens are created once and cached during the tokenization pass. Subsequent access returns the cached tokens without re-parsing.

### String Interning
Keyword strings and common identifiers are interned to reduce memory usage and improve comparison performance.

### Memory Efficiency  
The lexer processes source text in a single pass without creating intermediate data structures, keeping memory usage proportional to the output token count.

## Integration with Parser

The lexer produces tokens that are consumed by the `DirectivePreprocessor` and then the `PeopleCodeParser`:

```
Source Text → PeopleCodeLexer → Raw Tokens → DirectivePreprocessor → Filtered Tokens → PeopleCodeParser → AST
```

The parser can request tokens on-demand or work with pre-tokenized input, allowing for flexible integration patterns.