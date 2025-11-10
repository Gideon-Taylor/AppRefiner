# Type Checking and Function Signatures

AppRefiner includes a comprehensive type checking system that powers intelligent code completion, error detection, and developer productivity features. This document explains how type checking works and how to read the standardized function signature notation used throughout AppRefiner.

## Overview

This document covers two foundational concepts:

1. **Function Signature Format** - The standardized notation used to display function parameters, types, and return values
2. **Type Checking and Inference** - How AppRefiner validates your code for type correctness

Understanding these concepts helps you:
- Read function tooltips and understand parameter requirements
- Interpret type information in auto-suggest lists
- Understand type errors and warnings
- Write more type-safe code

## Understanding Function Signature Format

AppRefiner uses a standardized notation to display function signatures in tooltips and documentation. This format is novel in the PeopleCode world and uses regex-style notation for clarity and precision. Understanding this notation helps you quickly interpret function parameters and make fewer type errors.

### Why This Format?

Traditional PeopleCode function signatures are vague, only providing the expected type or in some cases just saying `any` and letting the code error at runtime if you pass the wrong type. 

AppRefiner's signature format provides:
- **Parameter Names** - Clarifies the purpose of each parameter
- **Precision** - No ambiguity about what parameters are required or optional
- **Consistency** - The same notation is used everywhere
- **Composability** - Simple patterns combine to express complex signatures

### Basic Parameter Types

The most common parameter types you'll encounter:

| Notation | Meaning | Example |
|----------|---------|---------|
| `string` | Required string parameter | `Substring(source_str: string, ...)` |
| `number` | Required numeric parameter | `Round(dec: number, precision: number)` |
| `boolean` | Required boolean value | `SetDefault(flag: boolean)` |
| `any` | Accepts any type | `Max(values: any+)` |

**Object Types:**
- `Record`, `Field`, `Rowset`, `Row` - PeopleCode object instances
- `AnalyticGrid`, `Session` - Other builtin object types

**Example:**
```
GetRecord(recname: @RECORD) -> record
```

### Reference Types vs Runtime Instances

AppRefiner distinguishes between **definition references** and **runtime instances**:

**Reference Types** (use `@` prefix):
- `@RECORD` - Reference to a record definition (e.g., Record.EMPLOYEE)
- `@FIELD` - Reference to a field definition (e.g., Field.EMPLID)
- `@SQL` - Reference to a SQL definition
- `@PAGE`, `@COMPONENT`, `@MENU` - Other PeopleSoft definition types

**Runtime Instances** (no prefix):
- `record` - A record variable (an instance)
- `field` - A field variable (an instance)
- `rowset`, `row` - Runtime objects

**Example:**
```peoplecode
/* @RECORD reference - expects Record.EMPLOYEE */
&myRecord = GetRecord(@RECORD.EMPLOYEE);

/* record instance - expects a record variable */
&success = &myRecord.SelectByKey();
```

In signatures:
```
GetRecord(rec_name: @RECORD?) -> record               // Takes optional reference, returns instance
&myRecord.SelectByKey(UseChangedBuffers: boolean?) -> boolean?  // Method on record instance
```

### Optional Parameters

Optional parameters are marked with a question mark (`?`):

| Notation | Meaning |
|----------|---------|
| `string?` | Optional string (0 or 1) |
| `number?` | Optional number (0 or 1) |
| `@FIELD?` | Optional field reference |

**Example:**
```
Substring(source_str: string, start_pos: number, length: number) -> string
```

This means:
- All three parameters (`source_str`, `start_pos`, and `length`) are **required**

**Usage:**
```peoplecode
&result = Substring("Hello World", 1, 5);    /* All parameters required */
```

### Variable Arguments (Varargs)

Functions that accept a variable number of parameters use `*` or `+`:

| Notation | Meaning |
|----------|---------|
| `string*` | 0 or more strings |
| `string+` | 1 or more strings (at least one required) |
| `any*` | 0 or more parameters of any type |
| `any+` | 1 or more parameters of any type |

**Example:**
```
Max(values: any+) -> $same_as_first
```

This means:
- `values` accepts 1 or more parameters of any type
- The return type matches the type of the first parameter

**Usage:**
```peoplecode
&result = Max(5, 10);              /* Valid: 2 numbers, returns number */
&result = Max(5, 10, 3, 8);        /* Valid: 4 numbers, returns number */
&result = Max("apple", "zebra");   /* Valid: 2 strings, returns string */
```

### Union Types

Some parameters accept multiple types, indicated with the pipe (`|`) symbol:

| Notation | Meaning |
|----------|---------|
| `string\|number` | Accepts string OR number |
| `@SQL\|string` | Accepts SQL definition OR string |
| `@FIELD\|string` | Accepts field reference OR string |

**Example:**
```
SQLExec(sqlcmd: @SQL|string, bindexprs: any*, outputvars: any*) -> boolean?
```

This means `SQLExec` accepts either:
- A SQL definition reference (e.g., `SQL.MY_SQL_OBJ`)
- A string containing SQL text (e.g., `"SELECT * FROM PS_TABLE"`)

It also accepts optional bind expressions and output variables, and returns an optional boolean.

**Usage:**
```peoplecode
/* Using SQL definition with output variable */
SQLExec(SQL.MY_QUERY, &param1, &outputValue);

/* Using string with output variable */
SQLExec("SELECT COUNT(*) FROM PS_EMPLOYEE WHERE STATUS = :1", "A", &count);
```

### Array Types

Array parameters use the `array_` prefix:

| Notation | Meaning |
|----------|---------|
| `array_string` | Array of strings |
| `array_number` | Array of numbers |
| `array_array_number` | 2D array of numbers |

**Example:**
```
SetLayout(row_fields: array_string, column_fields: array_string) -> void
```

**Usage:**
```peoplecode
Local array of string &rows, &cols;
&rows = CreateArray("DEPT", "LOCATION");
&cols = CreateArray("YEAR", "QUARTER");
&grid.SetLayout(&rows, &cols);
```

### Parameter Groups

Parentheses group related parameters that must be used together:

| Notation | Meaning |
|----------|---------|
| `(type1, type2)` | Required group - both parameters needed |
| `(type1, type2)?` | Optional group - either both or neither |
| `(type1, type2)*` | 0 or more groups |
| `(type1, type2)+` | 1 or more groups |

**Example:**
```
Sort(scrolls: @SCROLL*, keys: (@FIELD, SortOrder)+) -> void
```

This means:
- `scrolls` accepts 0 or more scroll references
- `keys` requires 1 or more groups, each group containing a field and a sort order

**Usage:**
```peoplecode
Sort(@SCROLL.SCROLL1, @FIELD.EMPLID, "A", @FIELD.NAME, "D");
/* Group 1: EMPLID, "A" (ascending) */
/* Group 2: NAME, "D" (descending) */
```

### Exact Count Constraints

Braces specify exact parameter counts:

| Notation | Meaning |
|----------|---------|
| `{3}` | Exactly 3 |
| `{2-5}` | Between 2 and 5 |
| `{2-}` | 2 or more |
| `{0-2}` | Between 0 and 2 |

**Example:**
```
Fill(value: any, positions: number{3}) -> array
```

This requires exactly 3 numbers for the `positions` parameter.

**Usage:**
```peoplecode
&array = Fill("X", 10, 5, 3);     /* Valid: exactly 3 numbers */
&array = Fill("X", 10, 5);        /* Error: need 3 numbers */
```

### Parameter Names

Parameters are named using `name: type` syntax:

```
AddStep(ae_step_name: string, new_step_name: string?) -> void
SetField(field: @FIELD, value: string) -> void
Transform(record: @RECORD, options: (mode: string, flags: number)) -> boolean
```

Parameter names:
- Clarify the purpose of each parameter
- Make it easier to remember argument order
- Appear in the "Next allowed type(s)" section of tooltips

### Return Types

The arrow (`->`) shows what the function returns:

| Notation | Meaning |
|----------|---------|
| `-> string` | Returns a string |
| `-> number` | Returns a number |
| `-> string` | Returns a string |
| `-> number` | Returns a number |
| `-> boolean` | Returns true/false |
| `-> void` | Returns nothing |
| `-> record` | Returns a record instance |
| `-> @RECORD` | Returns a record definition reference |

**Example:**
```
GetRecord(rec_name: @RECORD?) -> record
IsUserInRole(role: string) -> boolean
MessageBox(style: number, title: string, message_set: number, message_num: number, default_msg_txt: string, paramlist: any*) -> number?
```

#### Return optionability
Generally, you cannot ignore return values from functions but there are exceptions to this rule.

Some functions may have optional return types, indicated with `?`:

**Example:**
```
&array.Pop() -> any?
```

This indicates that Pop() does return a value but you can ignore the return value if you want.

### Complete Examples

#### Simple Functions
```
Abs(x: number) -> number
Upper(str: string) -> string
Lower(string: string) -> string
```

#### Functions with Variable Arguments
```
Max(values: any+) -> $same_as_first
Product(numbers: number+) -> number
IsUserInPermissionList(permission_list: string+) -> boolean
```

#### Functions with Union Types
```
SQLExec(sqlcmd: @SQL|string, bindexprs: any*, outputvars: any*) -> boolean?
SetDefault(field: field|string, value: any*) -> boolean?
```

#### Complex Functions
```
CreateRecord(recname: @RECORD) -> record

SortScroll(
  level: number,
  scrollpath: @RECORD+,
  sort_fields: (field|string, order: string)+
) -> boolean?
```

### Reading Tooltips

When you see a function tooltip like:
```
MessageBox(style: number, title: string, message_set: number, message_num: number, default_msg_txt: string, paramlist: any*) -> number?

Next allowed type(s):
style: number
```

This tells you:
- The function signature with all parameters and their types
- The function returns `number?` (optional return - you can ignore the return value)
- You're currently at the first parameter `style`, which expects a `number`

As you type each parameter and comma, the "Next allowed type(s)" section updates to show what's expected next.

## Type Checking and Inference

AppRefiner includes a comprehensive type checking system that validates your code for type correctness and powers the intelligent auto-suggest features.

### What Type Checking Does

The type checking system:

1. **Infers types** - Automatically determines the type of variables, expressions, and function returns
2. **Validates assignments** - Ensures variables receive values of compatible types
3. **Checks function calls** - Verifies arguments match parameter types
4. **Detects type errors** - Flags incompatible type usage before runtime
5. **Powers auto-suggest** - Enables smart filtering and sorting of suggestions

### How Type Inference Works

Type inference analyzes your code to determine types without requiring explicit type annotations:

**Variable Declarations:**
```peoplecode
Local string &name;                           /* Explicit type: string */
Local Record &rec;                            /* Explicit type: record */
```

**Assignment and Initialization:**
```peoplecode
Local number &count = 0;                      /* Inferred from literal: number */
&name = "John";                               /* Inferred: string */
&rec = GetRecord();                           /* Inferred from function return: record */
```

**Expression Types:**
```peoplecode
Local number &x = 5;
Local number &y = 10;
Local number &sum = &x + &y;                  /* Inferred from operands: number */
Local boolean &isValid = &sum > 100;          /* Inferred from comparison: boolean */
```

**Function Returns:**
```peoplecode
Function GetEmployeeName(&emplid as string) Returns string
   /* Implementation */
End-Function;

Local string &name = GetEmployeeName("12345"); /* Inferred from function signature */
```

**Object Members:**
```peoplecode
Local Record &rec = GetRecord();
Local Field &field = &rec.GetField(@FIELD:EMPLID);  /* Inferred from GetField return */
Local string &value = &field.Value;                  /* Inferred from property type */
```

### Type Compatibility Rules

AppRefiner understands PeopleCode's type hierarchy and compatibility:

**Assignment Compatibility:**
- Any type can be assigned to `any` or `object`
- Specific types can only be assigned to compatible variables
- Application Classes respect inheritance (derived class → base class is valid)

**Function Parameter Compatibility:**
- Arguments must match or be assignable to parameter types
- `any` accepts all types
- Union types accept any type in the union

### Benefits of Type Checking

**Catch Errors Early:**
Type errors are detected while you write code, not at runtime:
```peoplecode
Local number &count;
&count = "hello";    /* Type error: string not assignable to number */
```

**Smarter Auto-Suggest:**
Variable suggestions are sorted by type compatibility when inside function calls:
```peoplecode
Function ProcessEmployee(&id as string)
   /* ... */
End-Function;

Local string &emplId = "12345";
Local number &deptId = 100;

ProcessEmployee(   /* Auto-suggest shows &emplId first (string matches), then &deptId */
```

**Better Tooltips:**
Function tooltips show expected types at each parameter position, preventing mistakes:
```peoplecode
SQLExec(   /* Tooltip: sql: @SQL|string expected */
```

**Documentation:**
Types serve as inline documentation, making code more understandable:
```peoplecode
Local MY_PACKAGE:MY_CLASS &processor;  /* Clear: this is an app class instance */
```

### Type Error Detection

AppRefiner detects various type errors:

**Assignment Errors:**
```peoplecode
Local number &value;
&value = "text";              /* Error: Cannot assign string to number */
```

**Function Call Errors:**
```peoplecode
Local string &text = "hello";
&result = Round(&text, 2);    /* Error: Round expects number for first parameter, got string */
```

**Property Access Errors:**
```peoplecode
Local string &text = "hello";
&len = &text.SelectByKey();   /* Error: string has no SelectByKey method */
```

**Incompatible Operations:**
```peoplecode
Local boolean &flag = True;
Local number &value = &flag + 10;  /* Error: Cannot add boolean and number */
```

### Viewing Type Information

**In Tooltips:**
Hover over variables, functions, and expressions to see inferred types.

**In Function Call Tips:**
Function tooltips show parameter types and expected types for the current position.

**In Type Error Reports:**
Generate a comprehensive type error report with `Ctrl+Alt+E` to see all type issues in your code.

### Type Checking Configuration

Type checking runs automatically as you write code. The visual indicators appear in real-time:

- **Red squiggles** - Type errors that must be fixed
- **Yellow squiggles** - Type warnings or potential issues
- **Tooltip information** - Hover to see detailed type information

You can enable/disable the **Type Errors** styler in AppRefiner Settings > Stylers tab to control visual indicators.

### Generating Type Error Reports

For a comprehensive analysis of type issues in your code:

1. Press `Ctrl+Alt+E` to generate a type error report
2. The report shows all detected type errors with:
   - Error location (line and column)
   - Description of the type mismatch
   - Expected vs actual types
   - Context information

This is useful for reviewing type issues across your entire file at once.

### Database Requirements

Type checking works in two modes:

**Without Database Connection:**
- Infers types for builtin objects (String, Number, Date, etc.)
- Infers types for literals and expressions
- Resolves types within the current file
- Handles local variables, parameters, and functions

**With Database Connection:**
- Full Application Class type resolution
- Inheritance chain traversal
- Record and Field type information
- Cross-file type references

Most type checking features work offline, but database connection provides complete type information for Application Classes and database objects.

## Where Type Information Appears

Type information from the checking and inference system appears throughout AppRefiner:

### Function Tooltips
When you type `(` after a function name, tooltips display the signature using the notation described above. The "Next allowed type(s)" section updates as you type parameters.

See: [Auto-Suggest - Function Call Tooltips](auto-suggest.md#function-call-tooltips)

### Variable Suggestions
When you type `&`, variable suggestions include type information and are sorted by type compatibility when inside function calls.

See: [Auto-Suggest - Variable Suggestions](auto-suggest.md#variable-suggestions)

### Object Member Suggestions
When you type `.` after an object, type inference determines what members to show based on the object's type.

See: [Auto-Suggest - Object Member Suggestions](auto-suggest.md#object-member-suggestions)

### Code Styling and Visual Indicators
The **Type Errors** styler marks type incompatibilities with red squiggles. Other stylers use type information to provide context-aware analysis.

See: [Code Styling - Type Errors](code-styling.md#type-errors)

### Quick Fixes
Some quick fixes use type information to generate correct code. For example, when implementing abstract members, types are automatically filled in.

See: [Quick Fixes](quick-fixes.md)

## Related Features

- **[Auto-Suggest](auto-suggest.md)** - Intelligent code completion powered by type inference
- **[Code Styling](code-styling.md)** - Visual indicators for type errors and other issues
- **[Quick Fixes](quick-fixes.md)** - Automatic code corrections
- **[Tooltips](tooltips.md)** - Hover information showing type details

## Next Steps

- Try [Auto-Suggest](auto-suggest.md) to experience type-aware code completion
- Learn about [Code Styling](code-styling.md) to see type errors highlighted in real-time
- Generate a Type Error Report (`Ctrl+Alt+E`) to analyze type issues in your code
- Explore [Database Integration](../user-guide/database-integration.md) to enable full type resolution
