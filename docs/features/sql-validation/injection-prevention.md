# SQL Injection Prevention

SQL injection is one of the most common and dangerous security vulnerabilities in applications. AppRefiner provides robust tools to detect and prevent SQL injection vulnerabilities in your PeopleCode.

## What is SQL Injection?

SQL injection occurs when untrusted data is included in a SQL query in an unsafe manner, allowing attackers to manipulate the query's structure and potentially:

- Access unauthorized data
- Modify database contents
- Execute administrative operations
- Compromise the entire system

## How AppRefiner Prevents SQL Injection

AppRefiner employs multiple strategies to identify and prevent SQL injection vulnerabilities:

### 1. Static Analysis

AppRefiner's static analysis engine examines your PeopleCode to identify potential SQL injection vulnerabilities:

- **String concatenation detection**: Identifies SQL strings built through concatenation
- **Input source tracking**: Traces the flow of external inputs into SQL statements
- **Unsafe pattern recognition**: Flags known dangerous patterns in SQL construction

### 2. Binding Variable Enforcement

AppRefiner strongly encourages the use of binding variables (placeholders) instead of direct string concatenation:

```peoplecode
/* Unsafe - vulnerable to SQL injection */
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = '" | &userInput | "'");

/* Safe - uses binding variables */
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
```

### 3. Input Validation Checking

AppRefiner checks if inputs are properly validated before being used in SQL:

```peoplecode
/* Unsafe - no validation */
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = :1", &userInput);

/* Safe - with validation */
If IsValidInput(&userInput) Then
   &sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
End-If;
```

### 4. Dynamic SQL Analysis

AppRefiner analyzes dynamic SQL construction patterns to identify potential vulnerabilities:

```peoplecode
/* Potentially unsafe - dynamic WHERE clause */
&whereClause = "";
If &includeActive Then
   &whereClause = &whereClause | " AND ACTIVE_FLAG = 'Y'";
End-If;
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE 1=1" | &whereClause);

/* Safer approach with binding */
&sql = CreateSQL("SELECT FIELD1 FROM PS_RECORD WHERE 1=1" | &whereClause);
```

## Common SQL Injection Vulnerabilities

AppRefiner identifies several common SQL injection patterns:

### 1. Direct String Concatenation

```peoplecode
/* Vulnerable */
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = '" | &userInput | "'");
```

### 2. Unvalidated Input in LIKE Clauses

```peoplecode
/* Vulnerable */
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 LIKE '%" | &searchTerm | "%'");
```

### 3. Dynamic Table or Column Names

```peoplecode
/* Vulnerable */
&sql = CreateSQL("SELECT * FROM " | &tableName | " WHERE BUSINESS_UNIT = :1", &businessUnit);
```

### 4. Multiple Statement Execution

```peoplecode
/* Vulnerable */
&sql = CreateSQL(&userProvidedQuery);
```

### 5. Comment Injection

```peoplecode
/* Vulnerable */
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = '" | &userInput | "' /* Additional filters */");
```

## Best Practices for SQL Injection Prevention

AppRefiner recommends these best practices:

### 1. Always Use Binding Variables

```peoplecode
/* Good practice */
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
```

### 2. Validate All Inputs

```peoplecode
/* Good practice */
If Not IsValidInput(&userInput) Then
   Error("Invalid input provided");
   Return;
End-If;
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
```

### 3. Use Parameterized Queries for LIKE

```peoplecode
/* Good practice */
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 LIKE :1", "%" | &searchTerm | "%");
```

### 4. Use Whitelisting for Dynamic Table/Column Names

```peoplecode
/* Good practice */
&allowedTables = CreateArray("PS_RECORD1", "PS_RECORD2", "PS_RECORD3");
If &allowedTables.Find(&tableName) > 0 Then
   &sql = CreateSQL("SELECT * FROM " | &tableName | " WHERE BUSINESS_UNIT = :1", &businessUnit);
End-If;
```

### 5. Implement Proper Error Handling

```peoplecode
/* Good practice */
try
   &sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
   &sql.Execute();
catch Exception &e
   LogError(&e);
   Error("An error occurred processing your request");
end-try;
```

## Configuring SQL Injection Prevention

You can customize SQL injection prevention settings in AppRefiner:

1. Go to **Tools > Options > AppRefiner > SQL Validation**
2. Adjust the following settings:
   - **SQL injection detection level**: Basic, Standard, or Strict
   - **Report severity**: Error, Warning, or Information
   - **Allow dynamic table names**: Whether to allow dynamic table names (with warnings)
   - **Check input validation**: Whether to verify input validation before SQL usage

## Quick Fixes for SQL Injection Issues

When AppRefiner identifies a potential SQL injection vulnerability, you can:

1. **View the issue details**: Hover over the highlighted SQL statement
2. **Apply Quick Fix**: Click the lightbulb icon or press Ctrl+. to see available fixes
3. **Fix manually**: Update your code to use proper binding variables and validation

### Example Quick Fix

Before:
```peoplecode
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = '" | &userInput | "'");
```

After applying Quick Fix:
```peoplecode
&sql = CreateSQL("SELECT * FROM PS_RECORD WHERE FIELD1 = :1", &userInput);
```

## Related Features

- [SQL Binding Validation](binding-validation.md)
- [SQL Definition References](definition-references.md)
- [SQL Formatting](../editor-tweaks/sql-formatting.md)
