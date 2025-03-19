# Database Integration

AppRefiner provides powerful database integration features that allow you to work directly with PeopleSoft databases. This guide explains how to configure and use these features effectively.

## Supported Database Systems

AppRefiner currently supports the following database systems:

- Oracle Database (for PeopleSoft implementations)

## Configuring Database Connections

To set up a database connection:

1. Go to **Tools > Database Connections** in the menu
2. Click **Add New Connection**
3. Enter the following information:
   - Connection Name: A descriptive name for this connection
   - Database Type: Oracle
   - Server: The database server hostname or IP address
   - Port: The database server port (default: 1521)
   - Service Name/SID: The Oracle service name or SID
   - Username: Your database username
   - Password: Your database password
   - Schema: The PeopleSoft schema name (usually SYSADM)
4. Click **Test Connection** to verify the settings
5. Click **Save** to store the connection

## Connecting to a Database

To connect to a configured database:

1. Go to **Database > Connect** in the menu
2. Select the connection you want to use from the dropdown
3. Click **Connect**

Once connected, the status bar will display the current connection name.

## Working with PeopleCode Objects

When connected to a database, you can:

### Browse PeopleCode Objects

1. Go to **Database > Browse Objects** in the menu
2. Use the tree view to navigate through:
   - Records
   - Fields
   - Pages
   - Components
   - App Packages
   - And more

### Search for Objects

1. Go to **Database > Search Objects** in the menu
2. Enter a search term
3. Select the object types to include in the search
4. Click **Search**

### View and Edit PeopleCode

1. Find the object in the browser or search results
2. Double-click to open the associated PeopleCode
3. Make your changes in the editor
4. Save changes back to the database by clicking **Save** or pressing `Ctrl+S`

## Working with SQL Objects

AppRefiner allows you to work with SQL definitions:

### View SQL Definitions

1. Go to **Database > SQL Objects** in the menu
2. Select the SQL object you want to view
3. The SQL definition will open in the editor

### Analyze SQL Statements

AppRefiner can analyze SQL statements in your PeopleCode:

1. Open a PeopleCode file containing SQL statements
2. Right-click on a SQL statement
3. Select **Analyze SQL**
4. AppRefiner will check for:
   - Syntax errors
   - Performance issues
   - Best practices violations

## Working with HTML Definitions

AppRefiner allows you to work with HTML definitions:

1. Go to **Database > HTML Objects** in the menu
2. Select the HTML object you want to view
3. The HTML definition will open in the editor

## Project Integration

AppRefiner integrates with PeopleSoft projects:

### Load Project PeopleCode

1. Go to **Database > Load Project** in the menu
2. Select the project you want to load
3. Choose which object types to include
4. Click **Load**

All PeopleCode objects in the project will be loaded into AppRefiner.

### Compare with Database

Compare your local changes with the database version:

1. Right-click on an open PeopleCode file
2. Select **Compare with Database**
3. A diff view will show the differences between your local version and the database version

## Security Considerations

- Database credentials are stored encrypted in your user profile
- You can choose to not save passwords, requiring manual entry each time
- All database operations are performed with your provided credentials, so ensure you have appropriate permissions

## Next Steps

To learn about code templates in AppRefiner, proceed to the [Templates](templates.md) section.
