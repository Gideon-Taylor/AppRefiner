# Interpolated String Support for AppRefiner

## Overview

Add f-string style interpolated strings to AppRefiner as a language extension. These are parsed, type-checked, and support full editor features (autocomplete, function tips, etc.), then transpiled to native PeopleCode string concatenation using `|`.

### Syntax

```peoplecode
$"Hello, {&name}! You have {&count} messages."
```

Expands to:

```peoplecode
"Hello, " | &name | "! You have " | &count | " messages."
```

### Constraints

- **Single-line only**: Interpolated strings must be contained on a single line. This simplifies parsing and provides natural error recovery during live editing.

---

## 1. AST Modeling

### New Node Types

```
InterpolatedStringNode : ExpressionNode
  ├── parts: List<InterpolatedStringPart>
  ├── hasErrors: boolean                   // True if recovery occurred
  └── (inherits source position from ExpressionNode)

InterpolatedStringPart (abstract)
  ├── StringFragment : InterpolatedStringPart
  │     ├── text: string
  │     └── sourcePosition
  └── Interpolation : InterpolatedStringPart
        ├── expression: ExpressionNode?    // Nullable for empty/error cases
        ├── hasErrors: boolean             // True if incomplete/malformed
        └── sourcePosition                 // Includes the braces (if present)
```

### Design Notes

- `Interpolation.expression` is nullable to handle empty or incomplete interpolations during live editing.
- `hasErrors` flags allow downstream code to know this node was synthesized during recovery.
- Source positions are critical for editor features—each part needs accurate start/end positions.
- The `InterpolatedStringNode` itself is an expression with result type `String`.

---

## 2. Lexer Changes

### Approach: Lexer Modes with EOL Recovery

Introduce modes to handle transitions between string content and expressions, with end-of-line as a hard recovery boundary.

| Token | Description | Example |
|-------|-------------|---------|
| `INTERP_STRING_START` | Opening `$"` up to first `{` or closing `"` | `$"Hello, ` |
| `INTERP_STRING_MID` | Content between `}` and next `{` | `! You have ` |
| `INTERP_STRING_END` | Content after last `}` through closing `"` | ` messages."` |
| `INTERP_EXPR_START` | Opening brace | `{` |
| `INTERP_EXPR_END` | Closing brace | `}` |
| `INTERP_STRING_UNTERMINATED` | Recovery token when EOL hit | (see below) |

### Lexer Logic

1. When encountering `$"`, switch to **interpolated string mode**.

2. In **interpolated string mode**:
   - Accumulate characters as string content.
   - On `{`, emit the accumulated string fragment token, then emit `INTERP_EXPR_START` and switch to **interpolation expression mode**.
   - On `"`, emit the accumulated string fragment as `INTERP_STRING_END` and return to normal mode.
   - On `""`, treat as escaped quote—add single `"` to accumulated content and continue.
   - On `{{`, treat as escaped brace—add single `{` to accumulated content and continue.
   - On `}}`, treat as escaped brace—add single `}` to accumulated content and continue.
   - **On EOL/EOF**: Emit accumulated content as `INTERP_STRING_UNTERMINATED`, return to normal mode.

3. In **interpolation expression mode**:
   - Lex normally (including nested strings with `"..."` and their `""` escapes).
   - Track brace depth to handle nested braces (e.g., `{&arr[&i]}`).
   - On `}` at brace depth 0, emit `INTERP_EXPR_END` and return to interpolated string mode.
   - On `"` at brace depth 0: This is ambiguous—could be start of nested string or user error. **Treat as start of nested string literal** and lex it normally.
   - **On EOL/EOF**: Emit `INTERP_STRING_UNTERMINATED`, return to normal mode. Parser will handle recovery.

### EOL Recovery Behavior

The key insight: **end-of-line resets lexer state unconditionally**. This prevents a malformed interpolated string from corrupting the parse of subsequent lines.

```
Input: $"Hello {
        ~~~~~~~~^ EOL here

Tokens emitted:
  INTERP_STRING_START  "$"Hello "
  INTERP_EXPR_START    "{"
  INTERP_STRING_UNTERMINATED  ""   (signals incomplete)
```

```
Input: $"Hello {&name
        ~~~~~~~~~~~~~^ EOL here

Tokens emitted:
  INTERP_STRING_START  "$"Hello "
  INTERP_EXPR_START    "{"
  IDENTIFIER           "&name"
  INTERP_STRING_UNTERMINATED  ""
```

### Handling `"` Inside Expressions

A `"` inside an interpolation expression starts a **nested string literal**, lexed normally:

```peoplecode
$"Result: {SomeFunc("arg")}"
```

Tokens:
```
INTERP_STRING_START   "$"Result: "
INTERP_EXPR_START     "{"
IDENTIFIER            "SomeFunc"
LPAREN                "("
STRING_LITERAL        ""arg""
RPAREN                ")"
INTERP_EXPR_END       "}"
INTERP_STRING_END     """
```

This works because in expression mode, `"` triggers normal string lexing. The brace depth tracking ignores braces inside nested strings.

### Edge Cases

| Input | Behavior |
|-------|----------|
| `$""` | Empty interpolated string → valid |
| `$"{}"` | Empty interpolation → parse as error or empty expression |
| `$"{&a}{&b}"` | Adjacent interpolations → empty string fragment between |
| `$"{"` + EOL | Recovery: emit unterminated token |
| `$"{&name"` + EOL | Recovery: emit expression tokens then unterminated |
| `$"Test: {Func("}")}"`| Nested string containing `}` → works, brace tracking ignores string content |

---

## 3. Parser Changes

### Grammar (Conceptual)

```
interpolated_string
    : INTERP_STRING_START interpolated_parts* string_end
    ;

interpolated_parts
    : INTERP_STRING_MID
    | INTERP_EXPR_START expression? INTERP_EXPR_END
    ;

string_end
    : INTERP_STRING_END
    | INTERP_STRING_UNTERMINATED   // Error recovery
    ;
```

### Implementation Steps

1. When the lexer produces `INTERP_STRING_START`, begin parsing an `InterpolatedStringNode`.
2. Capture the leading string content as a `StringFragment` (may be empty).
3. Loop:
   - If `INTERP_EXPR_START`:
     - If immediately followed by `INTERP_EXPR_END`, create `Interpolation` with null expression and `hasErrors = true`.
     - Otherwise, call `ParseExpression()`, then expect `INTERP_EXPR_END` or handle recovery.
     - Wrap result in an `Interpolation` node.
   - If `INTERP_STRING_MID`, capture as `StringFragment`.
   - If `INTERP_STRING_END`, capture final fragment and break.
   - If `INTERP_STRING_UNTERMINATED`, mark node with `hasErrors = true` and break.
4. Return the completed `InterpolatedStringNode`.

### Error Recovery Strategies

**Scenario 1: Unclosed brace at EOL**
```peoplecode
Local string &s = $"Hello {
```

Recovery:
- Lexer emits: `INTERP_STRING_START`, `INTERP_EXPR_START`, `INTERP_STRING_UNTERMINATED`
- Parser creates `InterpolatedStringNode` with:
  - `StringFragment("Hello ")`
  - `Interpolation(expression: null, hasErrors: true)`
  - `hasErrors: true`
- Next line parses normally.

**Scenario 2: Partial expression at EOL**
```peoplecode
Local string &s = $"Hello {&name
```

Recovery:
- Lexer emits: `INTERP_STRING_START`, `INTERP_EXPR_START`, `IDENTIFIER(&name)`, `INTERP_STRING_UNTERMINATED`
- Parser creates `InterpolatedStringNode` with:
  - `StringFragment("Hello ")`
  - `Interpolation(expression: IdentifierExpr(&name), hasErrors: true)` — expression is valid but interpolation unclosed
  - `hasErrors: true`

**Scenario 3: Unclosed string (no closing quote)**
```peoplecode
Local string &s = $"Hello
```

Recovery:
- Lexer emits: `INTERP_STRING_START`, `INTERP_STRING_UNTERMINATED`
- Parser creates `InterpolatedStringNode` with:
  - `StringFragment("Hello")`
  - `hasErrors: true`

### Error Reporting

Report errors but continue parsing:
- "Unterminated interpolated string" — point to the `$"` or EOL
- "Unclosed interpolation brace" — point to the `{`
- "Empty interpolation" — warning, point to `{}`

---

## 4. Semantic Analysis

### Type Checking

- The `InterpolatedStringNode` has result type `String`.
- Each `Interpolation.expression` (if non-null) is visited by the type checker as a normal expression.
- All expression types are valid since `|` coerces automatically.
- Skip type checking for interpolations where `hasErrors == true` or `expression == null`.

### Symbol Resolution

- Expressions inside interpolations participate in normal symbol resolution.
- This enables "go to definition" and "find references" for variables/methods used inside interpolations.
- For incomplete expressions, resolve what you can—partial expressions may still have valid prefixes.

### Flow Analysis

- If your existing analysis tracks definite assignment, null checks, etc., these should flow through interpolation expressions naturally.
- Nodes with `hasErrors` might need special handling to avoid spurious warnings.

---

## 5. Editor Features

### Autocomplete

When the cursor is inside an interpolation (between `{` and `}` or after `{` at EOL):

1. Determine cursor position relative to the `Interpolation` node.
2. Even if the interpolation is incomplete, identify the partial expression being typed.
3. Use existing autocomplete logic—context is a normal expression context.

**Example scenarios:**

| Code | Cursor | Autocomplete behavior |
|------|--------|----------------------|
| `$"Hello, {\|}"` | After `{` | Offer all in-scope symbols |
| `$"Hello, {&emp.\|}"` | After `.` | Offer members of `&emp`'s type |
| `$"Hello, {&emp.GetN\|}"` | After `GetN` | Filter to methods starting with "GetN" |
| `$"Hello, {\|` + EOL | After `{` | Still offer all in-scope symbols |
| `$"Hello, {&emp.\|` + EOL | After `.` | Still offer members |

The key: **autocomplete should work even when the string is unterminated**.

### Function Signature Help

When inside a method call within an interpolation:

1. Detect that cursor is within a `CallExpression` inside an `Interpolation`.
2. Trigger signature help as usual.
3. Works even in unterminated strings as long as the expression parse got far enough.

Example: `$"Result: {Calculate(&a, |)}"` — show parameter hints for `Calculate`.

### Syntax Highlighting

- `$"` and closing `"` — string delimiter color
- String fragments — string color
- `{` and `}` — interpolation bracket color (distinct from regular braces)
- Expression content — normal syntax highlighting
- Unterminated string — error styling (red underline or similar on the line)

### Error Squiggles

- Unterminated interpolated string: squiggle from `$"` to end of line
- Unclosed brace: squiggle on the `{`
- Errors within expressions: accurate positions within the interpolation

### Cursor Context Detection

Add logic to determine if cursor is in an interpolation context:

```
function GetCursorContext(position):
    node = FindNodeAtPosition(position)
    
    if node is InterpolatedStringNode:
        for part in node.parts:
            if part.sourcePosition.contains(position):
                if part is Interpolation:
                    return ExpressionContext  // Normal autocomplete
                else:
                    return StringContext      // No autocomplete, or snippet suggestions
    
    // ... other contexts
```

---

## 6. Transpilation (Lowering)

### Transformation

Convert `InterpolatedStringNode` to a chain of `|` binary expressions.

**Input AST:**
```
InterpolatedStringNode
  ├── StringFragment("Hello, ")
  ├── Interpolation(IdentifierExpr(&name))
  ├── StringFragment("! Count: ")
  └── Interpolation(BinaryExpr(&count + 1))
```

**Output AST:**
```
BinaryExpr(|)
  ├── BinaryExpr(|)
  │     ├── BinaryExpr(|)
  │     │     ├── LiteralExpr("Hello, ")
  │     │     └── IdentifierExpr(&name)
  │     └── LiteralExpr("! Count: ")
  └── BinaryExpr(&count + 1)
```

### Implementation

```
function LowerInterpolatedString(node: InterpolatedStringNode): ExpressionNode
    if node.hasErrors:
        // Don't lower malformed nodes—keep as-is or emit error placeholder
        return ErrorExpr(node.sourcePosition)
    
    if node.parts is empty:
        return LiteralExpr("")
    
    result = LowerPart(node.parts[0])
    
    for i = 1 to node.parts.length - 1:
        part = LowerPart(node.parts[i])
        result = BinaryExpr(result, "|", part)
    
    return result

function LowerPart(part: InterpolatedStringPart): ExpressionNode
    if part is StringFragment:
        return LiteralExpr(part.text)
    else if part is Interpolation:
        if part.expression is null:
            return LiteralExpr("")  // Or error
        return part.expression
```

### Edge Cases

| Input | Output |
|-------|--------|
| `$""` | `""` |
| `$"{&x}"` | `"" \| &x` or just `&x` |
| `$"{&a}{&b}"` | `&a \| &b` |
| `$"Hi"` (no interpolations) | `"Hi"` |
| Error nodes | Don't lower, or emit placeholder |

### When to Lower

Lower as late as possible—after all semantic analysis is complete. This preserves the high-level structure for editor features.

**Only lower nodes where `hasErrors == false`**. Malformed interpolations should not be transpiled.

---

## 7. Testing Strategy

### Parser Tests — Valid Cases

- Basic: `$"Hello, {&name}"`
- Multiple: `$"{&a} and {&b}"`
- Complex expressions: `$"Value: {&obj.Method(&arg)}"`
- Nested braces: `$"Array: {&arr[&i]}"`
- Escaped braces: `$"Use {{braces}}"`
- Escaped quotes: `$"Say ""Hello"""`
- Adjacent interpolations: `$"{&a}{&b}"`
- Empty string: `$""`
- Nested strings: `$"Result: {Func("arg")}"`
- Nested string with brace: `$"Result: {Func("}")}"`

### Parser Tests — Error Recovery

- Unterminated string: `$"Hello` + EOL
- Unclosed brace: `$"Hello {` + EOL
- Unclosed brace with partial expr: `$"Hello {&name` + EOL
- Unclosed brace with complex expr: `$"Hello {&obj.Method(` + EOL
- Empty interpolation: `$"Hello {}"`

### Parser Tests — Multi-line Context

Verify that malformed interpolation doesn't break subsequent lines:

```peoplecode
Local string &s = $"Hello {
Local integer &x = 5;
```

Should parse as:
1. Variable declaration with malformed interpolated string (with errors)
2. Valid integer variable declaration

### Type Checking Tests

- Valid expressions produce no errors
- Invalid expressions (inside interpolation) produce correct diagnostics
- Error nodes are skipped gracefully

### Editor Tests

- Autocomplete triggers inside interpolations
- Autocomplete works in unterminated strings
- Signature help works for method calls inside interpolations
- Go to definition works for symbols inside interpolations
- Error positions are accurate
- Syntax highlighting handles all cases

### Transpilation Tests

- Only error-free nodes are lowered
- Output is valid PeopleCode
- Semantics preserved

---

## 8. Implementation Order

1. **AST nodes**: Define `InterpolatedStringNode`, `StringFragment`, `Interpolation` with `hasErrors` flags.
2. **Lexer**: Add lexer modes, new tokens, and EOL recovery logic.
3. **Parser**: Handle interpolated string parsing with error recovery.
4. **Recovery tests**: Verify multi-line scenarios don't break.
5. **Semantic analysis**: Ensure type checking and symbol resolution work, skip error nodes.
6. **Editor features**: Autocomplete, signature help, error reporting—including in incomplete strings.
7. **Transpilation**: Implement lowering to `|` chains (error-free nodes only).
8. **Integration**: Hook into save/export workflow.

---

## Appendix: State Machine Summary

```
                    ┌─────────────────────────────────────┐
                    │                                     │
                    ▼                                     │
    ┌───────────────────────────┐                        │
    │       NORMAL MODE         │                        │
    │                           │                        │
    │  On $" → emit START,      │                        │
    │         goto INTERP_STR   │                        │
    └───────────────────────────┘                        │
                    │                                     │
                    │ $"                                  │
                    ▼                                     │
    ┌───────────────────────────┐                        │
    │   INTERPOLATED STRING     │                        │
    │        MODE               │                        │
    │                           │                        │
    │  Accumulate string chars  │                        │
    │  On { → emit fragment,    │                        │
    │        emit EXPR_START,   │                        │
    │        goto INTERP_EXPR   │                        │
    │  On " → emit END,         │────────────────────────┘
    │        goto NORMAL        │
    │  On EOL → emit UNTERM,    │────────────────────────┘
    │          goto NORMAL      │
    │  On "" → add " to buffer  │
    │  On {{ → add { to buffer  │
    └───────────────────────────┘
                    │
                    │ {
                    ▼
    ┌───────────────────────────┐
    │  INTERPOLATION EXPR MODE  │
    │                           │
    │  Lex normally             │
    │  Track brace depth        │
    │  On " → lex nested string │
    │  On } (depth 0) →         │
    │        emit EXPR_END,     │────┐
    │        goto INTERP_STR    │    │
    │  On EOL → emit UNTERM,    │    │
    │          goto NORMAL      │────┼───────────────────┐
    └───────────────────────────┘    │                   │
                                     │                   │
                                     ▼                   ▼
                          back to INTERP_STR      back to NORMAL
```
