# SQL Binding Validation

SQL Binding Validation in AppRefiner helps ensure that SQL statements in your PeopleCode properly use binding variables for parameters, improving both security and performance.

## Overview

When working with SQL in PeopleCode, it's important to use binding variables (placeholders) for parameter values rather than concatenating values directly into SQL strings. AppRefiner's SQL Binding Validation feature automatically analyzes your SQL statements to ensure proper binding variable usage.

## Why Use Binding Variables?

Binding variables offer several important benefits:

1. **Security**: Prevent SQL injection attacks by properly separating code from data
2. **Performance**: Enable the database to cache and reuse execution plans
3. **Readability**: Make SQL statements cleaner and easier to understand
4. **Maintainability**: Simplify parameter changes and reduce string manipulation errors

## How SQL Binding Validation Works

AppRefiner's SQL Binding Validation:

1. Automatically detects SQL statements in your PeopleCode
2. Analyzes the SQL syntax to identify parameter usage
3. Verifies that parameters are properly bound using binding variables
4. Checks that the number of binding variables matches the number of provided parameters
5. Ensures binding variables are used correctly in the SQL statement

## Binding Variable Formats

AppRefiner validates several binding variable formats:

### 1. Numbered Binding Variables

```peoplecode
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :2", &value1, &value2);
```

### 2. Named Binding Variables

```peoplecode
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :FIELD1 AND FIELD2 = :FIELD2", &value1, &value2);
```

### 3. Question Mark Placeholders

```peoplecode
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = ? AND FIELD2 = ?", &value1, &value2);
```

## Common Binding Validation Issues

AppRefiner detects several common binding issues:

### 1. Missing Binding Variables

```peoplecode
/* Issue: Direct string concatenation instead of binding */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = '" | &value1 | "'");

/* Correct approach */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1", &value1);
```

### 2. Binding Variable Count Mismatch

```peoplecode
/* Issue: More binding variables than parameters */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :2", &value1);

/* Issue: More parameters than binding variables */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1", &value1, &value2);

/* Correct approach */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :2", &value1, &value2);
```

### 3. Duplicate Binding Variable Numbers

```peoplecode
/* Issue: Same binding variable number used twice with different values */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :1", &value1, &value2);

/* Correct approach */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :2", &value1, &value2);

/* Alternative correct approach (when same value should be used twice) */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM PS_RECORD WHERE FIELD1 = :1 AND FIELD2 = :1", &value1);
```

### 4. Invalid Binding Variable Usage

```peoplecode
/* Issue: Binding variable used in incorrect context */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM :1 WHERE FIELD1 = :2", &tableName, &value);

/* Correct approach */
&sql = CreateSQL("SELECT FIELD1, FIELD2 FROM " | &tableName | " WHERE FIELD1 = :1", &value);
```

## Configuring SQL Binding Validation

You can customize SQL Binding Validation settings in AppRefiner:

1. Go to **Tools > Options > AppRefiner > SQL Validation**
2. Adjust the following settings:
   - **Enable SQL binding validation**: Turn the feature on or off
   - **Validation severity**: Set to Error, Warning, or Information
   - **Allow dynamic table names**: Whether to allow non-bound table names
   - **Require named parameters**: Whether to enforce named binding variables

## Fixing Binding Issues

When AppRefiner identifies a binding issue, you can:

1. **View the issue details**: Hover over the highlighted SQL statement
2. **Apply Quick Fix**: Click the lightbulb icon or press Ctrl+. to see available fixes
3. **Fix manually**: Update your code to use proper binding variables

### Example Quick Fix

Before:
```peoplecode
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = '" | &value | "'");
```

After applying Quick Fix:
```peoplecode
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = :1", &value);
```

## Best Practices

1. **Always use binding variables** for all parameter values
2. **Use numbered binding variables consistently** (e.g., :1, :2, :3)
3. **Match parameter count** with binding variable count
4. **Don't use binding variables for table names** or column names
5. **Test SQL statements** with various parameter values

## Related Features

- [SQL Injection Prevention](injection-prevention.md)
- [SQL Definition References](definition-references.md)
- [SQL Formatting](../editor-tweaks/sql-formatting.md)
