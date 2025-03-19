# Variable Renaming

Variable renaming is one of the most common and important refactoring operations. AppRefiner enhances PeopleSoft Application Designer by providing intelligent variable renaming capabilities specifically designed for PeopleCode.

## Overview

Renaming variables is essential for maintaining clean, readable code. As code evolves, variable names may become outdated or no longer reflect their purpose. AppRefiner adds variable renaming functionality to Application Designer, allowing you to safely rename variables throughout your codebase while respecting PeopleCode's scoping rules.

## Features of Variable Renaming

### 1. Context-Aware Renaming

AppRefiner's variable renaming is context-aware:

- **Scope recognition**: Understands local, global, and class-level variable scopes
- **Type awareness**: Recognizes variable types (string, number, object, etc.)
- **Name collision detection**: Prevents renaming that would cause naming conflicts
- **Reference tracking**: Identifies all references to the variable being renamed

### 2. Multi-File Renaming

When a variable is used across multiple files, AppRefiner can:

- **Find all references**: Locate all occurrences across the entire project
- **Update consistently**: Apply the rename operation to all references
- **Respect file boundaries**: Only modify files where the variable is in scope

### 3. Preview and Control

Before applying changes, AppRefiner provides:

- **Change preview**: See all occurrences that will be renamed
- **Selective application**: Choose which occurrences to rename
- **Conflict warnings**: Get notified about potential naming conflicts
- **Syntax validation**: Verify that the new name is valid in PeopleCode

## How to Rename Variables

### Using the Command Palette

1. Place your cursor on the variable you want to rename in Application Designer
2. Press **Ctrl+Shift+P** to open the Command Palette
3. Type "Rename Variable" and select the command
4. Enter the new variable name
5. Review the preview of changes
6. Confirm to apply the renaming

### Using Keyboard Shortcuts

1. Place your cursor on the variable you want to rename in Application Designer
2. Press **Ctrl+Shift+R**
3. Enter the new variable name
4. Review the preview of changes
5. Press **Enter** to apply or **Escape** to cancel

## Variable Renaming Options

When renaming variables, AppRefiner provides several options:

### 1. Search Scope

- **Current file only**: Rename only in the current file
- **Current function/method**: Rename only within the current function or method
- **Current class**: Rename within the current class
- **Entire project**: Rename across all project files

### 2. Reference Types

- **All references**: Rename both read and write references
- **Write references only**: Rename only places where the variable is assigned
- **Read references only**: Rename only places where the variable is read

### 3. Additional Options

- **Preview changes**: Show all occurrences before applying
- **Comments and strings**: Whether to search within comments and string literals
- **Case sensitivity**: Whether to match case when finding references

## Examples of Variable Renaming

### Local Variable Renaming

Before:
```peoplecode
Function CalculateTotal(&amt As number) Returns number
   Local number &tax = &amt * 0.08;
   Local number &total = &amt + &tax;
   Return &total;
End-Function;
```

After renaming `&amt` to `&amount`:
```peoplecode
Function CalculateTotal(&amount As number) Returns number
   Local number &tax = &amount * 0.08;
   Local number &total = &amount + &tax;
   Return &total;
End-Function;
```

### Class Member Renaming

Before:
```peoplecode
class MyClass
   property number &val;
   
   method MyClass();
   method Calculate() Returns number;
private
   instance number &multiplier;
end-class;

method MyClass
   &val = 0;
   &multiplier = 2;
end-method;

method Calculate
   Return &val * &multiplier;
end-method;
```

After renaming `&val` to `&value`:
```peoplecode
class MyClass
   property number &value;
   
   method MyClass();
   method Calculate() Returns number;
private
   instance number &multiplier;
end-class;

method MyClass
   &value = 0;
   &multiplier = 2;
end-method;

method Calculate
   Return &value * &multiplier;
end-method;
```

## Best Practices for Variable Renaming

1. **Choose meaningful names**: Select names that clearly indicate the variable's purpose
2. **Follow naming conventions**: Adhere to your team's naming standards
3. **Be consistent**: Use consistent naming patterns throughout your code
4. **Avoid reserved words**: Don't use PeopleCode reserved words or function names
5. **Check all occurrences**: Review all references before applying the rename
6. **Test after renaming**: Verify that your code still works as expected

## Common Variable Naming Conventions in PeopleCode

AppRefiner supports various naming conventions:

### 1. Hungarian Notation

```peoplecode
Local string &strName;
Local number &numCount;
Local array &arrValues;
```

### 2. Camel Case

```peoplecode
Local string &userName;
Local number &itemCount;
Local array &dataValues;
```

### 3. Prefix Notation

```peoplecode
Local string &g_Name;  /* Global */
Local number &l_Count;  /* Local */
Local array &m_Values;  /* Member */
```

## Handling Special Cases

### 1. Renaming in SQL Strings

AppRefiner can detect and update variable references in SQL strings:

```peoplecode
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &oldName);
```

After renaming `&oldName` to `&newName`:

```peoplecode
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &newName);
```

### 2. Renaming in Dynamic Expressions

AppRefiner handles variables used in dynamic expressions:

```peoplecode
&fieldName = "FIELD1";
&rec = CreateRecord(Record.RECORD1);
&fieldValue = &rec.GetField(@("Field." | &fieldName)).Value;
```

## Related Features

- [Refactoring Overview](overview.md)
- [Import Optimization](import-optimization.md)
- [FlowerBox Headers](flowerbox-headers.md)
