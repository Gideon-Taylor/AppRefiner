# Auto-Suggest and IntelliSense

AppRefiner provides an intelligent code completion system that enhances PeopleCode development with context-aware suggestions and real-time information. This feature set includes variable suggestions, object member completion, system variable assistance, app package navigation, and function call tooltips.

## Overview

The auto-suggest system leverages AppRefiner's type inference engine to provide smart, context-aware code completions. As you type, AppRefiner automatically detects trigger characters and displays relevant suggestions based on your current context, accessible variables, object types, and expected parameter types.

Key benefits:
- Reduces typing and minimizes typos
- Discovers available methods and properties
- Provides immediate access to function signatures
- Enforces type compatibility with smart filtering
- Navigates complex app package hierarchies

## Configuration

Auto-suggest features can be individually enabled or disabled in the AppRefiner Settings dialog under the **Auto-Suggest** section:

- **Variable Suggestions** - Show variable completions when typing `&`
- **Function Signatures** - Show parameter information when typing `(`
- **Object Members** - Show methods and properties when typing `.`
- **System Variables** - Show system variables when typing `%`

All features are enabled by default. You can toggle them based on your preferences and workflow.

## Variable Suggestions

### When It Appears

Variable suggestions appear automatically when you type the ampersand (`&`) character followed by a space, symbol, or at the end of the document. This prevents false triggers when prefixing existing identifiers.

### What Variables Are Shown

AppRefiner displays all variables accessible from your current position in the code:

- **Local variables** - Variables declared in the current method or function
- **Parameters** - Method and function parameters
- **Instance variables** - Class properties (displayed as `%This.PropertyName`)
- **Global variables** - Program-level variables
- **Component variables** - Component-scoped variables
- **Constants** - Declared constant values
- **Exception variables** - Variables from catch blocks

### Scope Awareness

Variable suggestions are scope-aware and only show variables that are:
- Declared before the current cursor position
- Accessible from the current scope (respecting method/function boundaries)
- Not inside string literals

Variables declared after your cursor position or in inaccessible scopes are excluded.

### Type-Aware Filtering

When typing inside a function call argument, AppRefiner intelligently sorts variables by type compatibility:

1. **Compatible types** appear first - Variables matching the expected parameter type
2. **Other types** appear second - All other accessible variables

This helps you quickly find the right variable for the context.

### Visual Indicators

Each variable is displayed with an icon indicating its kind:

| Icon | Variable Kind |
|------|---------------|
| Local Variable icon | Local variables |
| Parameter icon | Method/function parameters |
| Instance Variable icon | Instance variables (properties) |
| Global Variable icon | Global variables |
| Component Variable icon | Component variables |
| Constant icon | Constants |
| Property icon | Properties |

### Format

Variables are displayed in this format:
```
variableName -> TypeName (Kind)
```

**Examples:**
- `recordCount -> number (Local)`
- `customerName -> string (Parameter)`
- `isActive -> boolean (Instance)`

### Using Variable Suggestions

1. Type `&` followed by a space or symbol
2. Browse the list using arrow keys
3. Press Enter or Tab to select a variable
4. AppRefiner replaces from `&` to the cursor with the full variable name

**Notes:**
- Properties are automatically prefixed with `%This.`
- Other variables are prefixed with `&`
- Partial matches work - typing `&cust` filters to variables starting with "cust"
- Press Escape to dismiss the list

### Database Requirements

Variable suggestions do **not** require a database connection. They work entirely from the parsed code in your current editor.

## Object Member Suggestions

### When It Appears

Object member suggestions appear when you type a dot (`.`) after an object reference, followed by a space, symbol, or at the end of the document.

### What Members Are Shown

AppRefiner uses type inference to determine the object type preceding the dot, then displays:

- **Methods** - Callable functions on the object
- **Properties** - Accessible properties
- **Fields** - For Record objects, all fields from the database

### Supported Object Types

#### Record Objects
When accessing a Record object, AppRefiner shows all fields from the database:

```peoplecode
Local Record &myRecord;
&myRecord = GetRecord();
&myRecord. /* Shows all fields: EMPLID, NAME, etc. */
```

**Database Required**: Yes - Field information is retrieved from your connected database

#### Builtin Objects
For PeopleCode builtin types (String, Number, Array, etc.), AppRefiner shows all available methods and properties from its type database:

```peoplecode
Local string &text = "Hello";
&text. /* Shows: Length, Substring(), Upper(), Lower(), etc. */
```

**Database Required**: No - Builtin type information is included with AppRefiner

#### Application Classes
For Application Class instances, AppRefiner shows methods and properties defined in the class, including inherited members:

```peoplecode
Local MY_PACKAGE:MY_CLASS &obj;
&obj = create MY_PACKAGE:MY_CLASS();
&obj. /* Shows: public/protected methods and properties */
```

**Database Required**: Yes - For full inheritance chain resolution

### Visibility Handling

AppRefiner respects member visibility when showing suggestions:

- **Public context** (outside the class): Shows only public members
- **Protected context** (within derived classes): Shows public and protected members
- **Private context** (within the same class): Shows all members including private

### Inheritance Support

For Application Classes, AppRefiner traverses the inheritance chain and includes:
- Methods and properties from the current class
- Inherited protected and public members from base classes
- Members from implemented interfaces

### Visual Indicators

| Icon | Member Type |
|------|-------------|
| Method icon | Methods (shown with parentheses) |
| Property icon | Properties |
| Field icon | Record fields |

### Format

Members are displayed in this format:
```
MemberName() -> ReturnType (Method)
PropertyName -> Type (Property)
FieldName -> FieldType
```

**Examples:**
- `GetName() -> string (Method)`
- `IsActive -> boolean (Property)`
- `EMPLID -> string`

### Using Object Member Suggestions

1. Type `.` after an object reference
2. Browse methods and properties with arrow keys
3. Press Enter or Tab to select a member
4. AppRefiner replaces from `.` to cursor with the member name
5. For methods, `(` is automatically inserted and function tooltips appear

**Special Behavior:**
- The `Value` property appears at the top for Field objects
- Members are sorted alphabetically within each category
- Typing filters the list (e.g., typing `.Get` shows only members starting with "Get")

### Database Requirements

- **Record fields**: Requires database connection
- **Builtin objects**: No database required
- **App Classes with metadata**: Database connection recommended for full inheritance resolution

## System Variables

### When It Appears

System variable suggestions appear when you type the percent sign (`%`) followed by a space, symbol, or at the end of the document.

### What System Variables Are Shown

AppRefiner shows all properties from the PeopleCode System object, plus context-aware synthetic variables:

**Standard System Variables:**
- All properties from the System builtin object (e.g., `Date`, `Time`, `Language`, etc.)

**Context-Aware Variables:**
- **%This** - Available inside Application Classes and Interfaces
- **%Super** - Available inside derived classes (references the base class or interface)

### Type-Based Filtering

System variables can be filtered based on expected types. Currently, all system variables are shown, but future enhancements may filter by the type expected in the current context (such as function parameters).

### Format

System variables are displayed in this format:
```
VariableName -> TypeName
```

**Examples:**
- `Date -> Date`
- `Language -> string`
- `This -> MY_PACKAGE:MY_CLASS`
- `Super -> BASE_PACKAGE:BASE_CLASS`

### Using System Variables

1. Type `%` followed by a space or symbol
2. Browse system variables with arrow keys
3. Press Enter or Tab to select a variable
4. AppRefiner replaces from `%` to cursor with the full variable name

**Notes:**
- Variable names are shown without the `%` prefix in the list
- After selection, the full name includes `%` (e.g., `%Date`, `%This`)
- Type the first few letters to filter the list

### Database Requirements

System variables do **not** require a database connection. Type information is included with AppRefiner.

## App Package Suggestions

### When It Appears

App package suggestions appear when you type a colon (`:`) in an Application Class path context.

### What Is Shown

AppRefiner queries your database and displays:

- **Subpackages** - Nested package paths (labeled with "(Package)")
- **Classes** - Application Classes in the current package (labeled with "(Class)")

Both are sorted alphabetically, with packages appearing before classes.

### Recursive Navigation

When you select a package (not a class), AppRefiner automatically inserts the package name with a trailing colon and re-triggers suggestions for the next level:

```peoplecode
/* Type: PT_ */
PT_AB:     /* Suggestions: PT_AB:PT_BC (Package), PT_AB:MY_CLASS (Class) */
PT_AB:PT_BC:   /* Suggestions for next level appear automatically */
```

### Automatic Import Generation

When you select a class from the suggestions, AppRefiner:
1. Inserts the class name
2. Automatically triggers the **Add Import** refactor
3. Adds the import statement at the top of your code (if not already present)

This ensures Application Classes are properly imported without manual intervention.

### Using App Package Suggestions

1. Start typing an app package path (e.g., `PT_`)
2. Type `:` to trigger suggestions
3. Browse packages and classes with arrow keys
4. Press Enter or Tab to select an item
5. For packages, suggestions automatically appear for the next level
6. For classes, the class is inserted and import is added

**Example Workflow:**
```peoplecode
/* Start typing */
Local PT_:

/* After selecting PT_AB package */
Local PT_AB:

/* After selecting PT_BC package */
Local PT_AB:PT_BC:

/* After selecting MY_CLASS */
Local PT_AB:PT_BC:MY_CLASS &obj;
/* Import automatically added: import PT_AB:PT_BC:MY_CLASS; */
```

### Database Requirements

App package suggestions **require a database connection**. AppRefiner queries the database for available packages and classes in real-time.

## Function Call Tooltips

### When They Appear

Function call tooltips appear automatically when you:
- Type an opening parenthesis `(` after a function name
- Type a comma `,` to move to the next parameter
- Have your cursor inside a function call

Tooltips hide when you type the closing parenthesis `)` or move outside the function call.

### What Information Is Displayed

Function tooltips show:

1. **Function signature** - Function name with all parameters using standardized notation
2. **Return type** - What the function returns
3. **Next allowed types** - Expected types for the current parameter position

**Format:**
```
FunctionName(param1: Type1, param2: Type2) -> ReturnType

Next allowed type(s):
paramName: TypeName
```

The signature uses AppRefiner's standardized notation with special symbols to indicate optional parameters (`?`), variable arguments (`*`, `+`), union types (`|`), reference types (`@`), and more. See **[Type Checking - Understanding Function Signature Format](type-checking.md#understanding-function-signature-format)** for a complete guide to reading these signatures.

### Type Validation

AppRefiner uses the Function Call Validator to determine which parameter types are acceptable at your current position. This helps prevent type errors before they occur.

For functions with multiple overloads or union types, all valid types are shown.

### Parameter Navigation

As you type parameters and commas, the tooltip updates to highlight the next expected parameter:

```peoplecode
MyFunction(  /* Tooltip shows: param1 expected */
MyFunction(&value1,  /* Tooltip updates: param2 expected */
```

### Supported Function Types

Function tooltips work for:
- **Builtin functions** - PeopleCode standard functions (e.g., `MessageBox`, `SQLExec`)
- **User-defined functions** - Functions declared in your code
- **Application Class methods** - Methods on Application Class instances
- **Constructors** - `create APP:CLASS()` calls

### Using Function Tooltips

Function tooltips appear automatically - no action needed. They provide real-time guidance as you write function calls.

**Tips:**
- Pay attention to "Next allowed type(s)" to avoid type errors
- Use the tooltip to remember parameter order without checking documentation
- For complex functions with many parameters, the tooltip helps track your position

### Database Requirements

Function tooltips do **not** require a database connection for builtin functions and functions in the current file. Database connection enhances tooltips for Application Class methods by resolving the full inheritance chain.

## Keyboard Navigation

All auto-suggest lists support consistent keyboard navigation:

| Key | Action |
|-----|--------|
| **Arrow keys** | Navigate up/down through suggestions |
| **Enter** | Select highlighted suggestion |
| **Tab** | Select highlighted suggestion |
| **Escape** | Dismiss the suggestion list |
| **Any letter** | Filter suggestions by typing |

## Integration with Type Checking

The auto-suggest system is deeply integrated with AppRefiner's type inference and checking engine, creating a seamless development experience where type information flows throughout all features.

**Note**: For foundational concepts about type checking, type inference, and the function signature format, see **[Type Checking and Function Signatures](type-checking.md)**. This section focuses on how those concepts integrate specifically with auto-suggest features.

### The Type Information Pipeline

When you trigger any auto-suggest feature, AppRefiner follows this pipeline:

1. **Parse** - Your code is parsed into an Abstract Syntax Tree (AST)
2. **Infer Types** - The type inference engine analyzes the AST and determines types for all expressions, variables, and function calls
3. **Analyze Context** - Your cursor position is analyzed to determine:
   - Current scope (global, method, function, block)
   - Accessible variables
   - Expected types (if inside a function call or assignment)
4. **Filter & Sort** - Suggestions are filtered by accessibility and sorted by type compatibility
5. **Validate** - Selected suggestions are validated for type correctness

This pipeline runs in milliseconds, providing real-time intelligent assistance.

### Type-Aware Variable Suggestions

When showing variable suggestions, AppRefiner uses type information to prioritize compatible variables:

**Example:**
```peoplecode
Function ProcessRecord(&rec as Record)
   /* Implementation */
End-Function;

Local Record &empRecord = GetRecord();
Local string &name = "Test";
Local number &count = 100;

ProcessRecord(   /* Variable suggestions appear */
```

**Suggestion Order:**
1. `&empRecord` (Record) - **Highlighted** - Matches expected type
2. `&name` (string) - Available but not matching
3. `&count` (number) - Available but not matching

Variables matching the expected type appear first, helping you make the right choice quickly.

### Type-Aware Object Members

When you type `.` after an object, type inference determines the object's type and shows appropriate members:

**Example:**
```peoplecode
Local Record &rec = GetRecord();
&rec.   /* Shows Record methods: SelectByKey(), GetField(), etc. */

Local string &text = "Hello";
&text.  /* Shows String methods: Length, Substring(), Upper(), etc. */

Local MY_PACKAGE:MY_CLASS &obj;
&obj.   /* Shows MY_CLASS methods and properties, including inherited members */
```

Without type inference, this would be impossible - AppRefiner wouldn't know what members to show.

### Function Call Validation

Function tooltips use type information to validate your arguments in real-time:

**Example:**
```peoplecode
Local string &name = "John";
Local number &style = 0;

/* Typing a function call */
MessageBox(&style, "Title", 1, 1, "Employee: " | &name);
/*         ^^^^^^  ^^^^^^^  ^  ^  ^^^^^^^^^^^^^^^^^^^^^^^^
           OK      OK       OK OK  OK - all types valid */

/* Type error example */
MessageBox(&name, "Title", 1, 1, "Text");
/*         ^^^^^
           ERROR: First parameter expects number, got string */
```

The tooltip shows expected types at each position, and type checking validates your arguments as you type.

### Scope-Aware Filtering

Type inference tracks variable scopes, ensuring suggestions only show accessible variables:

**Example:**
```peoplecode
Local string &globalVar = "Global";

Function MyFunction()
   Local string &localVar = "Local";

   /* Inside function: both &globalVar and &localVar appear */
   &   /* Suggestions: &globalVar, &localVar, parameters, etc. */
End-Function;

/* Outside function: only &globalVar appears */
&   /* Suggestions: &globalVar only */
```

This prevents suggesting variables that aren't in scope, reducing errors.

### Application Class Type Resolution

For Application Classes, type inference resolves the full inheritance chain:

**Example:**
```peoplecode
/* BASE_CLASS defines: Method1(), Property1 */
/* DERIVED_CLASS extends BASE_CLASS, adds: Method2(), Property2 */

Local MY_PACKAGE:DERIVED_CLASS &obj;
&obj.   /* Shows: Method1(), Method2(), Property1, Property2 */
```

Type information flows from the database through the type resolver to auto-suggest, providing complete member information.

### Cross-Feature Type Consistency

Type information is consistent across all AppRefiner features:

- **Auto-Suggest** uses types to filter and sort suggestions
- **Tooltips** display inferred types when hovering
- **Stylers** use types to detect errors (Type Errors styler)
- **Quick Fixes** respect type constraints when generating code
- **Type Error Reports** (`Ctrl+Alt+E`) show comprehensive type analysis

This consistency means you see the same type information everywhere, creating a cohesive development experience.

### Performance Optimization

Type inference is optimized for interactive use:

- **Incremental parsing** - Only re-parses changed regions when possible
- **Caching** - Type information is cached and reused
- **Background processing** - Heavy operations run on background threads
- **Lazy evaluation** - Types are inferred only when needed

Even for large files, type inference and auto-suggest remain responsive.

## Performance Considerations

### Parsing and Type Inference

Auto-suggest features trigger parsing and type inference on demand. For most code files, this happens nearly instantaneously. For very large files (>5000 lines), there may be a slight delay.

### Database Queries

Features requiring database connections (Record fields, App Package suggestions) execute queries in real-time. Query performance depends on your database connection speed and server load.

**Tips for best performance:**
- Maintain a stable database connection
- Use wired network connections when possible
- Consider disabling database-dependent features when working offline

## Troubleshooting

### Suggestions Not Appearing

**Problem**: Auto-suggest lists don't appear when typing trigger characters

**Solutions**:
1. Check that the feature is enabled in Settings > Auto-Suggest
2. Ensure you're typing the trigger character followed by space/symbol (not in the middle of a word)
3. Verify you're not inside a string literal (suggestions are suppressed there)
4. Check if the editor has focus

### Incorrect Object Members

**Problem**: Object member suggestions show wrong methods/properties

**Solutions**:
1. Verify type inference is working correctly (check variable declarations)
2. For App Classes, ensure database connection is active
3. Check that the object variable has a specific type (not `any` or `object`)
4. Verify imports are present for Application Classes

### App Package Suggestions Empty

**Problem**: No packages or classes appear after typing `:`

**Solutions**:
1. Ensure database connection is active (required for app package suggestions)
2. Verify the package path is valid in your database
3. Check that you have access permissions to view Application Classes
4. Try disconnecting and reconnecting to the database

### Function Tooltips Not Updating

**Problem**: Tooltip shows wrong parameter or doesn't update when typing commas

**Solutions**:
1. Ensure cursor is inside the function call (between `(` and `)`)
2. Check that the function is recognized (builtin or defined in current file)
3. For App Class methods, verify database connection for full resolution
4. Move cursor outside and back inside the function call to refresh

## Related Features

- **[Type Checking and Function Signatures](type-checking.md)** - Understanding function signature notation and how type inference works
- **[Quick Fixes](quick-fixes.md)** - Automatic code corrections triggered by `Ctrl+.`
- **[Code Styling](code-styling.md)** - Visual indicators for code issues
- **[Tooltips](tooltips.md)** - Hover information for variables, methods, and objects

## Next Steps

- Read [Type Checking and Function Signatures](type-checking.md) to understand function signature notation and type inference
- Learn about [Database Integration](../user-guide/database-integration.md) to enable all features
- Review [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for efficient navigation
- Configure [Settings](../user-guide/settings-reference.md) to customize auto-suggest behavior
