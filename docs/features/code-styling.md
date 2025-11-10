# Code Styling

AppRefiner provides powerful code styling features to help you maintain clean, readable, and consistent PeopleCode. This guide explains how to use these features effectively.

## Understanding Code Styling

Code styling in AppRefiner refers to:

1. **Visual formatting** - How code appears in the editor through syntax highlighting
2. **Code analysis** - Identifying stylistic issues using Code Stylers
3. **Quick fixes** - Automatic corrections for common styling issues

## Syntax Highlighting

AppRefiner provides rich syntax highlighting for PeopleCode, applying distinct colors to different code elements like keywords, strings, comments, variables, method calls, and constants to improve readability. Colors are currently not user-configurable.

## Code Stylers

AppRefiner includes 20 code stylers that analyze your code for specific issues and provide visual indicators. These run automatically as you work, highlighting potential problems with colored underlines, text colors, or highlights.

### Variable and Declaration Issues

#### Unused Variables
**What It Detects**: Variables (local, instance, or parameters) that are declared but never used in the code.

**Visual Indicator**: Grayed out text color

**Quick Fix**: [Delete Unused Variable](quick-fixes.md#2-delete-unused-variable) - Press `Ctrl+.` on the unused variable to automatically remove it

**Why It Matters**: Unused variables clutter code, reduce readability, and may indicate incomplete refactoring or logic errors.

---

#### Undefined Variables
**What It Detects**: Variables that are referenced in code but never declared, particularly for loop iterators.

**Visual Indicator**: Yellow highlight

**Quick Fix**: [Declare For Loop Iterator](quick-fixes.md#1-declare-for-loop-iterator) - Press `Ctrl+.` on an undefined for loop iterator to add a declaration

**Why It Matters**: Undefined variables will cause runtime errors. PeopleCode allows some implicit declarations, but explicit declarations improve code clarity.

---

#### Redeclared Variables
**What It Detects**: Variables that are declared multiple times within the same scope.

**Visual Indicator**: Warning squiggle

**Why It Matters**: Redeclarations can cause confusion and unexpected behavior, especially when variables have different types or initial values.

---

#### Meaningless Variable Names
**What It Detects**: Variables with overly generic or non-descriptive names (e.g., `&a`, `&b`, `&x`, `&temp`, `&var`).

**Visual Indicator**: Text color highlighting

**Why It Matters**: Descriptive variable names improve code readability and maintainability.

**Example**:
```peoplecode
/* Bad */
Local number &a, &b;
&a = GetRecord(...);

/* Good */
Local number &recordCount, &totalAmount;
&recordCount = GetRecord(...);
```

---

#### Reused For Iterators
**What It Detects**: For loop iterator variables that are reused in nested loops, which can cause logic errors.

**Visual Indicator**: Warning squiggle

**Why It Matters**: Reusing the same iterator variable in nested loops is a common source of bugs, as the inner loop modifies the outer loop's iterator.

**Example**:
```peoplecode
/* Problem - &i is reused */
For &i = 1 To 10
   For &i = 1 To 5  /* Reuses &i from outer loop */
      /* code */
   End-For;
End-For;

/* Fixed - use different iterators */
For &i = 1 To 10
   For &j = 1 To 5
      /* code */
   End-For;
End-For;
```

---

### Class and Method Issues

#### Class Name Mismatch
**What It Detects**: Class names that don't match the expected name from the file's class path in Application Designer.

**Visual Indicator**: Red squiggle on class name

**Why It Matters**: Class name mismatches can cause confusion when navigating code and may indicate the class was copied from another location without proper renaming.

**Note**: A quick fix for this issue exists in the code but is not yet connected to this styler.

---

#### Missing Constructors
**What It Detects**: Classes that extend another class requiring constructor parameters but don't have a constructor defined.

**Visual Indicator**: Yellow squiggle on class name

**Quick Fix**: [Generate Base Constructor](quick-fixes.md#4-generate-base-constructor) - Press `Ctrl+.` to automatically generate the required constructor

**Prerequisites**: Requires database connection to analyze parent class

**Why It Matters**: Classes extending other classes with parameterized constructors must provide their own constructor to properly initialize the parent.

---

#### Missing Method Implementation
**What It Detects**: Method declarations in a class header that don't have corresponding implementations after the class definition.

**Visual Indicator**: Yellow squiggle on method name

**Quick Fix**: [Implement Missing Method](quick-fixes.md#6-implement-missing-method) - Press `Ctrl+.` to generate a default implementation

**Why It Matters**: Every declared method must have an implementation, or it will cause runtime errors when called.

---

#### Unimplemented Abstract Members
**What It Detects**: Classes that extend abstract classes or implement interfaces but don't provide implementations for all abstract methods and properties.

**Visual Indicator**: Yellow squiggle on class name

**Quick Fix**: [Implement Abstract Members](quick-fixes.md#5-implement-abstract-members) - Press `Ctrl+.` to generate all missing implementations

**Prerequisites**: Requires database connection to analyze parent class/interface hierarchy

**Why It Matters**: Abstract members must be implemented in concrete classes. Missing implementations will cause compile-time or runtime errors.

---

#### Property As Variable
**What It Detects**: Cases where properties are accessed directly as variables instead of through their getter/setter methods.

**Visual Indicator**: Warning indicator

**Why It Matters**: Direct property access bypasses encapsulation and may not respect property access modifiers or logic.

---

### Exception Handling Issues

#### Wrong Exception Variable
**What It Detects**: References to exception variables from different catch blocks within nested try-catch statements.

**Visual Indicator**: Yellow squiggle

**Quick Fix**: [Fix Exception Variable](quick-fixes.md#3-fix-exception-variable) - Press `Ctrl+.` to rename to the correct exception variable

**Why It Matters**: Using the wrong exception variable can access the wrong exception context, leading to incorrect error handling.

**Example**:
```peoplecode
Try
   /* code */
Catch &ex1
   Try
      /* code */
   Catch &ex2
      /* Problem - using &ex1 in &ex2's scope */
      MessageBox(0, "", 0, 0, &ex1.ToString());
   End-Try;
End-Try;
```

---

### Code Quality Issues

#### Dead Code
**What It Detects**: Unreachable code that appears after return, exit, throw, or break statements.

**Visual Indicator**: Grayed out text

**Why It Matters**: Dead code will never execute and should be removed to reduce confusion and improve maintainability.

**Example**:
```peoplecode
method Calculate()
   Return 42;
   /* Everything after this point is dead code */
   MessageBox(0, "", 0, 0, "This will never show");
end-method;
```

---

#### Missing Semicolons
**What It Detects**: Statements that are missing required semicolons at the end.

**Visual Indicator**: Error squiggle

**Why It Matters**: Missing semicolons cause syntax errors and prevent code from compiling.

---

#### Syntax Errors
**What It Detects**: Parse errors and syntax errors detected during code analysis.

**Visual Indicator**: Red squiggle

**Why It Matters**: Syntax errors prevent code from compiling and must be fixed before the code can run.

---

### Type System Issues

#### Type Errors
**What It Detects**: Type inference and type checking errors, such as type mismatches in assignments, incompatible parameter types, and invalid operations.

**Visual Indicator**: Error squiggle

**Why It Matters**: Type errors can cause runtime failures or unexpected behavior. The type checking system helps catch these issues during development.

**Related Feature**: Use `Ctrl+Alt+E` to generate a comprehensive [Type Error Report](type-checking.md)

---

### Import and Dependency Issues

#### Unused Imports
**What It Detects**: Import statements for Application Classes that are never referenced in the code.

**Visual Indicator**: Grayed out text

**Why It Matters**: Unused imports clutter code and can slow down class loading.

**Tip**: Use `Ctrl+Shift+I` to automatically [Resolve Imports](../user-guide/using-refactoring-tools.md) and remove unused ones.

---

#### Invalid App Class
**What It Detects**: Imported Application Classes that don't exist in the database.

**Visual Indicator**: Error squiggle on import path

**Prerequisites**: Requires database connection

**Why It Matters**: Invalid imports will cause runtime errors when the code attempts to use the missing class.

---

### SQL and Data Issues

#### Find() Parameter Order
**What It Detects**: Incorrect parameter ordering in Find() function calls, which is a common source of bugs.

**Visual Indicator**: Warning squiggle

**Why It Matters**: The Find() function has a specific parameter order that must be followed for correct behavior.

---

#### SQL Variable Count
**What It Detects**: Mismatches between the number of bind variables in SQL statements and the number of values provided.

**Visual Indicator**: Warning squiggle

**Why It Matters**: SQL variable count mismatches will cause runtime errors when executing the SQL statement.

**Example**:
```peoplecode
/* Problem - 2 bind variables but only 1 value */
SQLExec("SELECT * FROM PS_TABLE WHERE FIELD1 = :1 AND FIELD2 = :2", &val1);

/* Fixed */
SQLExec("SELECT * FROM PS_TABLE WHERE FIELD1 = :1 AND FIELD2 = :2", &val1, &val2);
```

---

### Comment and Documentation Issues

#### TODO/FIXME Comments
**What It Detects**: Special comment markers including `TODO`, `FIXME`, `NOTE`, `BUG`, `HACK`, and `TBD`.

**Visual Indicator**: Distinct colors for each marker type, with summary annotation

**Why It Matters**: Helps track areas of code that need attention, improvement, or completion.

**Tip**: Use these markers to leave reminders for yourself and your team about code that needs work.

---

#### Linter Suppression
**What It Detects**: Areas where linter warnings have been suppressed using `/* #AppRefiner suppress(...) */` comments.

**Visual Indicator**: Distinct highlighting

**Why It Matters**: Makes it visible where code quality checks have been intentionally disabled, which should be used sparingly.

---

## Enabling and Configuring Stylers

### Via User Interface

1. Open the AppRefiner main window
2. Navigate to the **Stylers** tab
3. Check or uncheck the box next to each styler to enable/disable it
4. Changes take effect immediately

### Via Command Palette

1. Press `Ctrl+Shift+P` to open the Command Palette
2. Type "Styler: Toggle" to see all available styler toggle commands
3. Select the styler you want to toggle (e.g., `Styler: Toggle Highlights TODO/FIXME comments`)

## Working with Styler Indicators

### Understanding Visual Indicators

- **Red Squiggle**: Error that must be fixed (syntax errors, type errors)
- **Yellow Squiggle**: Warning about potential issues (missing methods, wrong exception variable)
- **Text Color Changes**: Informational highlights (unused variables appear grayed out)
- **Highlights**: Background color changes (undefined variables, TODO comments)

### Applying Quick Fixes

Many stylers provide quick fixes that can automatically resolve the detected issue:

1. Position your cursor on the highlighted code
2. Press `Ctrl+.` (period) to apply the quick fix
3. The code will be automatically corrected

See the [Quick Fixes](quick-fixes.md) documentation for detailed information about each available fix.

## Performance Considerations

### Database-Dependent Stylers

Some stylers require a database connection to function:
- Invalid App Class
- Missing Constructors
- Unimplemented Abstract Members

These stylers will not show results if no database connection is established. See [Database Integration](../user-guide/database-integration.md) for setup instructions.

### Disabling Stylers

If AppRefiner performance is impacted, consider:
- Disabling stylers you don't frequently use
- Temporarily disabling database-dependent stylers when working offline
- Focusing on error-level stylers (syntax, type errors) rather than informational ones

## Next Steps

- Learn about [Quick Fixes](quick-fixes.md) for automatic code corrections
- Explore [Type Checking](type-checking.md) for detailed type analysis
- See [Working with Linters](../user-guide/working-with-linters.md) for code quality analysis
- Read about [Database Integration](../user-guide/database-integration.md) to enable database-dependent features
