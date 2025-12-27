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
