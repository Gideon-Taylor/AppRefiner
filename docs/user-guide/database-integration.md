# Database Integration

AppRefiner can connect to your PeopleSoft Oracle database in a **read-only** capacity to enhance certain features by providing necessary context about PeopleCode objects, classes, and SQL definitions. This guide explains how to configure and leverage this connection.

## Purpose of Database Connection

The database connection is used exclusively to:

- **Improve Linter Accuracy**: Some linters can provide more accurate results when they have access to database context (e.g., verifying object references in SQLExec, checking parameters in function calls).
- **Enable Project Linting**: The `Lint: Lint Project` command requires a database connection to analyze all objects within the current Application Designer project.
- **Enhance Tooltips**: Certain tooltip providers use the connection to display more detailed information (e.g., details about PeopleSoft objects, method parameters).
- **Enable Specific Stylers**: Some stylers require database access (e.g., verifying Application Class imports).
- **Support Certain Refactors**: Refactoring tools that interact with class definitions or imports need database context (e.g., Resolve Imports, Add Import).

**Important**: AppRefiner **does not** modify the database. It does not provide features for browsing, searching, editing, or saving PeopleCode or other objects directly back to the database. All interactions are read-only to gather context.

## Configuring and Connecting

1.  **Open Command Palette**: Press `Ctrl+Shift+P`.
2.  **Run Connect Command**: Type and select `DB: Connect to database`.
3.  **Enter Credentials**: Provide the following information in the connection dialog:
    *   **TNS Name**: The TNS entry for your PeopleSoft database (from your `tnsnames.ora` file).
    *   **Username**: Your database username.
    *   **Password**: Your database password.
4.  **Choose Authentication Method**:
    *   **Bootstrap User**: Typically your standard PeopleSoft login (e.g., PS). While this user often has write access, AppRefiner will only perform read operations.
    *   **Read-Only User**: A user with restricted, read-only database access. If using this method, you may need to explicitly specify the PeopleSoft **Schema Name** (usually `SYSADM` or similar) so AppRefiner knows where to find the objects.
5.  **Password Saving (Optional)**: You can choose to save your password. If saved, it is encrypted using Windows Data Protection API (DPAPI), specific to your Windows user profile.
6.  **Connect**: Click the connect button.

Alternatively, a "Connect DB..." button may be available on the **Linters Tab** of the main AppRefiner window.

Your Username and selected Authentication Method are saved automatically for future connections.

## Features Utilizing Database Connection

While not always strictly required (some features may degrade gracefully), a database connection enhances or enables the following:

- **Commands**:
    - `Lint: Lint Project` (Required)
- **Linters** (Benefit from context):
    - Linters checking SQLExec/CreateSQL parameters (`SQL_EXEC_VAR`, `CREATE_SQL_VAR`)
    - Linters validating object types or method calls
    - (Others may benefit implicitly)
- **Tooltip Providers**:
    - `PeopleSoftObjectTooltipProvider` (Required)
    - `MethodParametersTooltipProvider` (Optional - provides richer info)
- **Stylers**:
    - `InvalidAppClass` (Required)
- **Refactors** (Inferred requirement):
    - `Refactor: Resolve Imports`
    - `Refactor: Add Import`
    - `Refactor: Create AutoComplete`

## Security Considerations

- AppRefiner only performs read operations on the database.
- Your database Username and selected Authentication Method are stored for convenience.
- Password storage is optional. If you choose to save the password, it is encrypted using Windows DPAPI, tied to your user account.
- Ensure the credentials provided have the necessary (read) permissions for AppRefiner to query PeopleSoft metadata tables (like `PSRECDEFN`, `PSPCMPROG`, etc.).

## Next Steps

To learn about using code templates in AppRefiner, proceed to the [Templates](templates.md) section.
