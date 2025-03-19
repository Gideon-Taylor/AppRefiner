# Available Lint Rules

AppRefiner includes a comprehensive set of lint rules designed specifically for PeopleCode development. This document provides a detailed reference of all available lint rules.

## Syntax Rules

### Missing Semicolon (SYNTAX-001)

**Severity**: Warning

Detects missing semicolons at the end of statements.

```peoplecode
/* Incorrect */
Local string &name = "John"

/* Correct */
Local string &name = "John";
```

### Unmatched Brackets (SYNTAX-002)

**Severity**: Error

Identifies unmatched parentheses, brackets, or braces.

```peoplecode
/* Incorrect */
If (&value > 10 {
   &result = "High";
}

/* Correct */
If (&value > 10) {
   &result = "High";
}
```

### Invalid Operator Usage (SYNTAX-003)

**Severity**: Error

Detects invalid operator combinations or usage.

```peoplecode
/* Incorrect */
&result = &value1 +* &value2;

/* Correct */
&result = &value1 + &value2;
```

## Variable Rules

### Unused Variable (VAR-001)

**Severity**: Warning

Identifies variables that are declared but never used.

```peoplecode
/* Incorrect */
Local string &name = "John";
Local number &age = 30;
Return &name;  /* &age is never used */

/* Correct */
Local string &name = "John";
Return &name;
```

### Undeclared Variable (VAR-002)

**Severity**: Error

Detects variables that are used without being declared.

```peoplecode
/* Incorrect */
&result = &value * 2;  /* &value not declared */

/* Correct */
Local number &value = 10;
&result = &value * 2;
```

### Shadowed Variable (VAR-003)

**Severity**: Warning

Identifies variables that shadow (redeclare) variables from an outer scope.

```peoplecode
/* Incorrect */
Local string &name = "Global";

Function ProcessName()
   Local string &name = "Local";  /* Shadows outer &name */
   /* ... */
End-Function;

/* Correct */
Local string &globalName = "Global";

Function ProcessName()
   Local string &localName = "Local";
   /* ... */
End-Function;
```

### Unused Parameter (VAR-004)

**Severity**: Warning

Detects function parameters that are never used within the function.

```peoplecode
/* Incorrect */
Function Calculate(&value1 As number, &value2 As number) Returns number
   Return &value1 * 2;  /* &value2 is never used */
End-Function;

/* Correct */
Function Calculate(&value1 As number) Returns number
   Return &value1 * 2;
End-Function;
```

## Control Flow Rules

### Unreachable Code (FLOW-001)

**Severity**: Warning

Identifies code that will never be executed due to preceding return statements, breaks, or other control flow issues.

```peoplecode
/* Incorrect */
Function ProcessValue(&value As number)
   If &value < 0 Then
      Return "Negative";
   End-If;
   Return "Positive";
   &value = &value * 2;  /* Unreachable code */
End-Function;

/* Correct */
Function ProcessValue(&value As number)
   If &value < 0 Then
      Return "Negative";
   End-If;
   &value = &value * 2;
   Return "Positive";
End-Function;
```

### Empty Block (FLOW-002)

**Severity**: Information

Detects empty code blocks that may indicate incomplete implementation.

```peoplecode
/* Incorrect */
If &value < 0 Then
   /* Empty block */
End-If;

/* Correct */
If &value < 0 Then
   &result = "Negative";
End-If;
```

### Assignment in Condition (FLOW-003)

**Severity**: Warning

Identifies assignments within conditional expressions, which may be a typo for a comparison.

```peoplecode
/* Incorrect */
If (&value = 10) Then  /* Assignment instead of comparison */
   /* ... */
End-If;

/* Correct */
If (&value = 10) Then  /* Comparison */
   /* ... */
End-If;
```

## Function Rules

### Missing Return (FUNC-001)

**Severity**: Error

Detects functions with a return type that don't have a return statement on all code paths.

```peoplecode
/* Incorrect */
Function GetValue(&id As number) Returns string
   If &id > 0 Then
      Return "Positive";
   End-If;
   /* Missing return for &id <= 0 */
End-Function;

/* Correct */
Function GetValue(&id As number) Returns string
   If &id > 0 Then
      Return "Positive";
   Else
      Return "Non-positive";
   End-If;
End-Function;
```

### Function Complexity (FUNC-002)

**Severity**: Warning

Identifies functions that exceed a complexity threshold (cyclomatic complexity).

```peoplecode
/* Incorrect - Too complex */
Function ProcessData(&value As number) Returns string
   /* Function with many nested if statements and loops */
End-Function;

/* Correct */
Function ProcessData(&value As number) Returns string
   /* Break down into smaller, more focused functions */
   If &value < 0 Then
      Return ProcessNegative(&value);
   Else
      Return ProcessPositive(&value);
   End-If;
End-Function;
```

### Missing Documentation (FUNC-003)

**Severity**: Information

Detects functions without proper documentation comments.

```peoplecode
/* Incorrect */
Function CalculateTotal(&values As array of number) Returns number
   /* ... */
End-Function;

/* Correct */
/* 
 * Calculates the total sum of all values in the array
 * @param &values Array of numbers to sum
 * @returns The total sum
 */
Function CalculateTotal(&values As array of number) Returns number
   /* ... */
End-Function;
```

## SQL Rules

### Unbounded SQL Query (SQL-001)

**Severity**: Warning

Identifies SQL queries without a WHERE clause or with a potentially unbounded WHERE clause.

```peoplecode
/* Incorrect */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD");

/* Correct */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1", &value);
```

### SQL Injection Risk (SQL-002)

**Severity**: Error

Detects potential SQL injection vulnerabilities where user input is directly concatenated into SQL strings.

```peoplecode
/* Incorrect */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = '" | &userInput | "'");

/* Correct */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
```

### Incorrect Binding Usage (SQL-003)

**Severity**: Error

Identifies incorrect usage of SQL binding variables.

```peoplecode
/* Incorrect */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :1", &value1, &value2);

/* Correct */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :2", &value1, &value2);
```

## PeopleSoft-Specific Rules

### Improper Component Buffer Access (PS-001)

**Severity**: Warning

Detects improper access patterns for the component buffer.

```peoplecode
/* Incorrect */
&rec = GetRecord(Record.RECORD_NAME);
&rec.FIELD.Value = "New Value";
/* Missing call to RowInit or other appropriate event */

/* Correct */
&rec = GetRecord(Record.RECORD_NAME);
&rec.RowInit();
&rec.FIELD.Value = "New Value";
```

### Deprecated PeopleCode Function (PS-002)

**Severity**: Warning

Identifies usage of deprecated PeopleCode functions or methods.

```peoplecode
/* Incorrect */
WinMessage("Debug message");  /* Deprecated */

/* Correct */
MessageBox(0, "", 0, 0, "Debug message");
```

### Missing SaveEdit Validation (PS-003)

**Severity**: Warning

Detects field changes without proper SaveEdit validation.

```peoplecode
/* Incorrect */
&rec.FIELD.Value = &newValue;
/* Missing validation */

/* Correct */
If ValidateField(&newValue) Then
   &rec.FIELD.Value = &newValue;
End-If;
```

## Configuring Lint Rules

You can configure the severity and behavior of lint rules in AppRefiner:

1. Go to **Tools > Options > AppRefiner > Linting**
2. Select a rule from the list
3. Adjust its severity level (Error, Warning, Information, Hint, or Disabled)
4. Configure any rule-specific parameters

## Related Features

- [Linting Overview](overview.md)
- [Custom Lint Rules](custom-rules.md)
- [Suppressing Lint Warnings](suppressing-warnings.md)
