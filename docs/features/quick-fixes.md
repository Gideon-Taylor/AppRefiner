# Quick Fixes

## Overview

Quick fixes are a special category of refactoring tools that provide automatic code corrections for issues detected by stylers. Unlike regular refactors that can be invoked manually through the command palette or keyboard shortcuts, quick fixes are context-aware and only appear when the cursor is positioned on code with a styler issue.

## How to Use Quick Fixes

1. Position your cursor on code that has a styler indicator (squiggle, highlight, or text color change)
2. Press **Ctrl+.** (period) to invoke the Apply Quick Fix command
3. If a quick fix is available for the issue at the cursor position, it will be automatically applied
4. The code will be modified to resolve the detected issue

## Key Characteristics

- **Automatic Suggestion**: Quick fixes cannot be triggered manually from menus or command palette
- **Context-Aware**: Only appear when the cursor is on code marked by a specific styler
- **Instant Application**: Apply immediately when Ctrl+. is pressed
- **Styler Integration**: Each quick fix is associated with one or more stylers that detect the issues it can resolve

## Available Quick Fixes

### 1. Declare For Loop Iterator

**Triggered By**: [Undefined Variables](code-styling.md#undefined-variables) styler

**What It Fixes**: Undefined for loop iterator variables that are used in a for statement without being declared.

**What It Does**: Inserts a local variable declaration (`Local number &varname;`) above the for loop statement with proper indentation.

**Example**:

Before:
```peoplecode
For &i = 1 To 10
   /* code */
End-For;
```

After:
```peoplecode
Local number &i;
For &i = 1 To 10
   /* code */
End-For;
```

---

### 2. Delete Unused Variable

**Triggered By**: [Unused Variables](code-styling.md#unused-variables) styler

**What It Fixes**: Local variables, instance variables, or parameters that are declared but never used in the code.

**What It Does**:
- For local/instance variables declared alone on a line: Removes the entire declaration line
- For variables in a comma-separated declaration list: Removes only that variable from the list
- For parameters: Removes the parameter from the function/method signature, handling commas appropriately

**Examples**:

Before (standalone declaration):
```peoplecode
Local string &unusedVar;
Local number &count;
```

After:
```peoplecode
Local number &count;
```

Before (multi-variable declaration):
```peoplecode
Local string &name, &unusedVar, &address;
```

After:
```peoplecode
Local string &name, &address;
```

---

### 3. Fix Exception Variable

**Triggered By**: [Wrong Exception Variable](code-styling.md#wrong-exception-variable) styler

**What It Fixes**: References to exception variables from different catch blocks within nested try-catch statements.

**What It Does**: Renames all incorrect exception variable references within a catch block to use the correctly scoped exception variable.

**Example**:

Before:
```peoplecode
Try
   /* code */
Catch &ex1
   Try
      /* code */
   Catch &ex2
      /* Using &ex1 here is wrong - should use &ex2 */
      MessageBox(0, "", 0, 0, &ex1.ToString());
   End-Try;
End-Try;
```

After:
```peoplecode
Try
   /* code */
Catch &ex1
   Try
      /* code */
   Catch &ex2
      MessageBox(0, "", 0, 0, &ex2.ToString());
   End-Try;
End-Try;
```

---

### 4. Generate Base Constructor

**Triggered By**: [Missing Constructors](code-styling.md#missing-constructors) styler

**What It Fixes**: Classes that extend another class requiring constructor parameters but don't have a constructor defined.

**What It Does**:
- Generates both the constructor declaration in the class header
- Generates the constructor implementation after the class definition
- Includes parameter annotations
- Calls the parent class constructor with `%Super = create ParentClass(params);`
- Handles parameter name conflicts with existing class members

**Prerequisites**: Requires database connection to analyze the parent class constructor signature.

**Example**:

Before:
```peoplecode
class MyClass extends ParentClass
   /* No constructor defined */
end-class;
```

After:
```peoplecode
class MyClass extends ParentClass
   method MyClass(&param1 As string, &param2 As number);
end-class;

method MyClass
   /+ &param1 as string, +/
   /+ &param2 as number +/
   %Super = create ParentClass(&param1, &param2);

end-method;
```

---

### 5. Implement Abstract Members

**Triggered By**: [Unimplemented Abstract Members](code-styling.md#unimplemented-abstract-members) styler

**What It Fixes**: Classes that extend abstract classes or implement interfaces but don't provide implementations for abstract methods and properties.

**What It Does**:
- Analyzes the entire inheritance hierarchy to find all unimplemented abstract members
- Generates method/property declarations in appropriate visibility sections (public/protected/private)
- Creates section headers if they don't exist
- Generates method implementations with proper annotations and comments
- Includes "Implements" comments indicating the source class/interface

**Prerequisites**: Requires database connection to analyze the parent class/interface hierarchy.

**Example**:

Before:
```peoplecode
class MyClass extends AbstractParent
   /* Missing implementations */
end-class;
```

After:
```peoplecode
class MyClass extends AbstractParent
   method ProcessData(&input As string) Returns boolean; /* Implements AbstractParent.ProcessData */
   property string Status readonly; /* Implements AbstractParent.Status */
end-class;

method ProcessData
   /+ &input as string +/
   /+ Returns boolean +/
   /+ Implements AbstractParent.ProcessData +/

   /* TODO: Implement method */
   Return False;
end-method;
```

---

### 6. Implement Missing Method

**Triggered By**: [Missing Method Implementation](code-styling.md#missing-method-implementation) styler

**What It Fixes**: Method declarations in a class that don't have corresponding implementations.

**What It Does**:
- Generates a default implementation for the declared method
- Includes parameter annotations
- Adds "Implements" comment if the method overrides a parent class method
- Inserts implementation at the appropriate location (constructors first, then other methods)

**Prerequisites**: Database connection optional (used to detect if method is an override).

**Example**:

Before:
```peoplecode
class MyClass
   method Calculate(&value As number) Returns number;
end-class;
/* No implementation provided */
```

After:
```peoplecode
class MyClass
   method Calculate(&value As number) Returns number;
end-class;

method Calculate
   /+ &value as number +/
   /+ Returns number +/

   /* TODO: Implement method */
   Return 0;
end-method;
```

---

## Troubleshooting

### Quick Fix Not Available

If pressing Ctrl+. doesn't apply a quick fix:

1. **Check Cursor Position**: Ensure your cursor is directly on the highlighted/underlined code, not adjacent to it
2. **Verify Styler Active**: Check that the styler detecting the issue is enabled in Settings → Stylers tab
3. **Database Connection**: Some quick fixes (Generate Base Constructor, Implement Abstract Members, Implement Missing Method) may require a database connection
4. **Issue Not Supported**: Not all styler issues have quick fixes available

### Quick Fix Applied Incorrectly

If a quick fix produces unexpected results:

1. **Undo**: Press Ctrl+Z to undo the change
2. **Check Context**: Some quick fixes depend on the specific code context and may need manual adjustment
3. **Report Issue**: If the quick fix consistently produces incorrect code, please report it as a bug

## Related Features

- [Code Styling](code-styling.md) - Visual indicators that trigger quick fixes
- [Refactoring Tools](../user-guide/using-refactoring-tools.md) - Manual refactoring operations
- [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) - All available shortcuts including Ctrl+.
