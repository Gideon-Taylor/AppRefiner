# SQL Definition References

AppRefiner's SQL Definition References feature helps you work with SQL objects in your PeopleCode by providing validation, auto-completion, and navigation for SQL definitions stored in your PeopleSoft database.

## Overview

When developing PeopleCode that interacts with the database, it's essential to reference SQL definitions correctly. AppRefiner's SQL Definition References feature connects to your PeopleSoft database to provide real-time validation and assistance with SQL objects.

## Key Features

### 1. SQL Definition Validation

AppRefiner validates references to SQL definitions in your PeopleCode:

- **Existence checking**: Verifies that referenced SQL objects exist in the database
- **Parameter validation**: Ensures that the correct number and types of parameters are provided
- **Return type checking**: Validates that the SQL result is used appropriately

### 2. SQL Definition Auto-Completion

When working with SQL definitions, AppRefiner provides intelligent auto-completion:

- **SQL object name completion**: Suggests available SQL object names as you type
- **Parameter completion**: Provides hints about required parameters
- **Context-aware suggestions**: Offers relevant SQL objects based on the current context

### 3. SQL Definition Navigation

AppRefiner allows you to navigate between PeopleCode and SQL definitions:

- **Go to Definition**: Jump directly to the SQL definition from its reference in PeopleCode
- **Find All References**: Locate all places where a SQL definition is used
- **Preview SQL**: View the SQL definition content without leaving your PeopleCode

## Working with SQL Definition References

### Connecting to the Database

To use SQL Definition References, you need to connect to your PeopleSoft database:

1. Go to **Tools > Database Connection**
2. Enter your database connection details:
   - Database type (Oracle, SQL Server, etc.)
   - Server name
   - Database name
   - Authentication credentials
3. Click **Connect**

Once connected, AppRefiner will automatically load SQL definitions and provide validation and assistance.

### Validating SQL References

AppRefiner automatically validates SQL references in your PeopleCode:

```peoplecode
/* AppRefiner will validate that SQL.MY_SQL_DEFINITION exists */
&sql = CreateSQL(SQL.MY_SQL_DEFINITION, &param1, &param2);
```

If a SQL definition doesn't exist or is referenced incorrectly, AppRefiner will highlight the issue:

- **Red underline**: SQL definition doesn't exist
- **Yellow underline**: Parameter count or usage issue
- **Green underline**: SQL definition exists but might have other issues

### Using Auto-Completion

When typing SQL definition references, AppRefiner provides auto-completion:

1. Type `SQL.` in your PeopleCode
2. A dropdown list of available SQL definitions will appear
3. Continue typing to filter the list or use arrow keys to select
4. Press Tab or Enter to insert the selected SQL definition

### Navigating to SQL Definitions

To view or edit a SQL definition:

1. Right-click on a SQL definition reference in your PeopleCode
2. Select **Go to Definition**
3. The SQL definition will open in the editor

Alternatively, you can:

1. Place your cursor on a SQL definition reference
2. Press F12 (default keyboard shortcut for Go to Definition)

### Finding SQL Definition References

To find all places where a SQL definition is used:

1. Right-click on a SQL definition reference
2. Select **Find All References**
3. The search results will show all locations where the SQL definition is used

### Previewing SQL Definitions

To quickly view a SQL definition without navigating away:

1. Hover over a SQL definition reference
2. A tooltip will show the SQL definition content
3. Click **Preview** in the tooltip to open a temporary preview window

## SQL Definition Management

AppRefiner also provides tools for managing SQL definitions:

### Viewing All SQL Definitions

To see all available SQL definitions:

1. Go to **View > SQL Definitions**
2. A panel will open showing all SQL definitions in the database
3. You can filter, sort, and search this list

### Creating New SQL Definitions

To create a new SQL definition:

1. Go to **File > New > SQL Definition**
2. Enter a name for the new SQL definition
3. Write the SQL statement
4. Click **Save**

### Editing SQL Definitions

To edit an existing SQL definition:

1. Navigate to the SQL definition (using Go to Definition or the SQL Definitions panel)
2. Make your changes
3. Click **Save**

## Best Practices

1. **Use SQL definitions for complex queries**: Store complex SQL in SQL definitions rather than embedding in PeopleCode
2. **Properly parameterize SQL definitions**: Use binding variables (:1, :2, etc.) for all parameters
3. **Document SQL definitions**: Add comments explaining the purpose and parameters
4. **Use consistent naming**: Follow a consistent naming convention for SQL definitions
5. **Validate SQL definitions**: Test SQL definitions with various parameter values

## Configuring SQL Definition References

You can customize SQL Definition References settings in AppRefiner:

1. Go to **Tools > Options > AppRefiner > SQL Validation**
2. Adjust the following settings:
   - **Enable SQL definition validation**: Turn the feature on or off
   - **Validation severity**: Set to Error, Warning, or Information
   - **Auto-refresh SQL definitions**: Whether to automatically refresh when database changes
   - **Show SQL previews**: Enable or disable preview tooltips

## Related Features

- [SQL Binding Validation](binding-validation.md)
- [SQL Injection Prevention](injection-prevention.md)
- [Database Connection](../database/connection.md)
