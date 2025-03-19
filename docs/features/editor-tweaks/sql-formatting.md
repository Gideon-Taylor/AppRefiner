# SQL Formatting

SQL formatting in AppRefiner helps you write clean, readable, and consistent SQL code within your PeopleCode files.

## Overview

When working with SQL in PeopleCode, proper formatting is essential for readability and maintainability. AppRefiner provides automatic SQL formatting capabilities that help standardize your SQL statements according to best practices.

## Features

### Automatic SQL Detection

AppRefiner automatically detects SQL statements in your PeopleCode, including:

- **SQLExec statements**
- **CreateSQL calls**
- **SQL string literals assigned to variables**
- **SQL statements in App Engine PeopleCode**

### Formatting Options

The SQL formatter provides several formatting options:

- **Keyword capitalization**: Automatically capitalize SQL keywords (SELECT, FROM, WHERE, etc.)
- **Clause alignment**: Align clauses on new lines with proper indentation
- **Comma placement**: Configure whether commas should appear at the beginning or end of lines
- **Indentation style**: Choose between different indentation styles for nested queries
- **Whitespace control**: Add appropriate spacing around operators and parentheses

## How to Use SQL Formatting

### Manual Formatting

1. Select the SQL statement you want to format
2. Right-click and select **Format SQL** from the context menu
3. Alternatively, use the keyboard shortcut **Ctrl+K, Ctrl+F** when your cursor is within an SQL statement

### Automatic Formatting

You can configure AppRefiner to automatically format SQL statements:

1. Go to **Tools > Options > AppRefiner > SQL Formatting**
2. Enable the **Format SQL on paste** option
3. Enable the **Format SQL on save** option if desired

## Customizing SQL Formatting

AppRefiner allows you to customize SQL formatting rules to match your team's coding standards:

1. Go to **Tools > Options > AppRefiner > SQL Formatting**
2. Adjust the following settings:
   - **Keyword case**: UPPERCASE, lowercase, or Capitalize
   - **Indent size**: Number of spaces for each indentation level
   - **Comma style**: End of line or beginning of line
   - **Alignment style**: Simple, stacked, or compact

## Example

### Before Formatting

```sql
select a.field1,a.field2,b.field1 from ps_record a,ps_record2 b where a.field1=b.field1 and a.field3='Value' order by a.field1
```

### After Formatting

```sql
SELECT a.field1, a.field2, b.field1
FROM   ps_record a, ps_record2 b
WHERE  a.field1 = b.field1
AND    a.field3 = 'Value'
ORDER BY a.field1
```

## Benefits

- **Improved readability**: Consistently formatted SQL is easier to read and understand
- **Error reduction**: Proper formatting helps identify syntax errors more easily
- **Maintainability**: Standardized formatting makes code maintenance simpler
- **Team consistency**: Ensures all team members follow the same SQL formatting standards

## Related Features

- [SQL Binding Validation](../sql-validation/binding-validation.md)
- [SQL Injection Prevention](../sql-validation/injection-prevention.md)
