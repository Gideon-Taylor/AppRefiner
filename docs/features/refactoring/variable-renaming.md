# Variable Renaming

Variable renaming is one of the most common and important refactoring operations. AppRefiner enhances PeopleSoft Application Designer by providing intelligent variable renaming capabilities specifically designed for PeopleCode.

## Overview

Renaming variables is essential for maintaining clean, readable code. As code evolves, variable names may become outdated or no longer reflect their purpose. AppRefiner adds variable renaming functionality to Application Designer, allowing you to safely rename variables throughout your codebase while respecting PeopleCode's scoping rules.

AppRefiner supports renaming both local variables and function/method parameters, ensuring all references are consistently updated.

## Features of Variable Renaming

The renaming operation will only rename local variables in the current file and will take into account the scope of the variable. (e.g. local variables renamed in a function/method will not be renamed in a different function/method)

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

### Function Parameter Renaming

Before:
```peoplecode
Function ProcessRecord(&recField As Record) Returns boolean
   Local string &fieldName = &recField.Name;
   If &recField.IsChanged Then
      &recField.Update();
      Return True;
   End-If;
   Return False;
End-Function;
```

After renaming `&recField` to `&record`:
```peoplecode
Function ProcessRecord(&record As Record) Returns boolean
   Local string &fieldName = &record.Name;
   If &record.IsChanged Then
      &record.Update();
      Return True;
   End-If;
   Return False;
End-Function;
```

### Class Member Renaming
**Note**: Due to not being able to access external references we can only rename private members of the current class.

Before:
```peoplecode
class MyClass
   method MyMethod();
private
   instance number &myVar;
end-class;

method MyMethod
   &myVar = 1;
end-method;
```

After renaming `&myVar` to `&myVariable`:
```peoplecode
class MyClass
   method MyMethod();
private
   instance number &myVariable;
end-class;

method MyMethod
   &myVariable = 1;
end-method;
```

### Method Parameter Renaming

Before:
```peoplecode
class Logger
private
   instance string &logPrefix;
   method LogMessage(&msg As string);
end-class;

method LogMessage
   &this.WriteToLog(&logPrefix | ": " | &msg);
end-method;
```

After renaming `&msg` to `&message`:
```peoplecode
class Logger
private
   instance string &logPrefix;
   method LogMessage(&message As string);
end-class;

method LogMessage
   &this.WriteToLog(&logPrefix | ": " | &message);
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
