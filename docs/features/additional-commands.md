# Additional Commands Reference

## Overview

AppRefiner provides several utility commands for database management, editor control, debugging, and maintenance. These commands complement the main features and provide power-user functionality for advanced scenarios.

**Command Categories:**
- **Database Commands**: Connect/disconnect database functionality
- **Editor Maintenance**: Clear annotations, force refresh
- **SQL Formatting**: Format SQL Objects
- **Debug Commands**: Access debug logs and internal state
- **Declaration Commands**: Declare external functions

---

## Database Commands

### Database: Connect to DB

**Description**: Opens the database connection dialog to establish a connection to your PeopleTools database.

**When to Use**:
- When starting AppRefiner for the first time
- After disconnecting from database
- To switch to a different database environment
- When prompted for database-dependent features

**Access**:
- Command Palette: Search for "Database: Connect"
- Settings: Check "Prompt for DB Connection" for automatic prompt

**Dialog Fields**:
- **Database Type**: Oracle or SQL Server
- **Server/TNS Name**: Hostname or TNS entry name
- **Database Name**: Database/SID name
- **Username**: PeopleTools database user
- **Password**: User password
- **Test Connection**: Validates credentials before connecting

**Behavior**:
1. Opens database connection dialog
2. User enters credentials
3. Connection established and cached
4. Database-dependent features become available

**Related Settings**:
- "Prompt for DB Connection" - Auto-prompt on editor open
- "TNS_ADMIN Directory" - Oracle tnsnames.ora location

**Database-Dependent Features**:
- Smart Open (Ctrl+O)
- PeopleSoft Object Info tooltips
- Application Class Details tooltips
- Event Mapping Detection
- Project Linting (multi-file)
- Type Checking (external classes)
- Go To Definition (external classes)

---

### Database: Disconnect DB

**Description**: Closes the current database connection and disables database-dependent features.

**When to Use**:
- Switching to different database environment
- Working offline
- Troubleshooting connection issues
- Reducing resource usage when database not needed

**Access**:
- Command Palette: Search for "Database: Disconnect"
- Only available when database is connected

**Behavior**:
1. Closes active database connection
2. Releases database resources
3. Disables database-dependent features
4. Features gracefully degrade to local-only operation

**Effects After Disconnection**:
- Smart Open: Unavailable (falls back to Application Designer Open)
- PeopleSoft Object Info tooltips: Not shown
- Application Class Details tooltips: Not shown
- Event Mapping Detection: Disabled
- Type Checking: Limited to current file + builtins
- Go To Definition: Limited to current file

**Reconnecting**:
After disconnect, use "Database: Connect to DB" command to reconnect.

**Troubleshooting**:
If database connection is stuck or causing issues:
1. Run "Database: Disconnect DB"
2. Wait for confirmation
3. Reconnect with "Database: Connect to DB"
4. Or restart Application Designer for clean slate

---

## Editor Maintenance Commands

### Editor: Clear Annotations

**Description**: Removes all annotations from the current editor, providing a clean view of your code.

**When to Use**:
- Event mapping annotations too verbose
- Cleaing out linter results
- Annotations obscuring code view
- Testing code without annotation clutter
- Annotations contain outdated information

**Access**:
- Command Palette: Search for "Clear Annotations"
- Requires active editor

**What Remains**:
- **Styler Highlights**: Visual indicators from stylers remain
- **Code Content**: Actual code unchanged

---

### Editor: Force Refresh

**Description**: Forces the editor to re-parse code, re-run linters/stylers, and refresh all visual indicators. Useful when editors get out of sync.

**When to Use**:
- Linter highlights not appearing/disappearing correctly
- Styler indicators seem outdated
- Code folding markers incorrect
- Type information stale
- After changing linter/styler settings
- Troubleshooting visual display issues

**Access**:
- Command Palette: Search for "Force Refresh"
- Requires active editor

**What Gets Refreshed**:
- **AST**: Reparsed from current editor text
- **Linters**: All active linters re-run
- **Stylers**: All active stylers re-run
- **Type Inference**: Type information recalculated
- **Folding**: Fold points recalculated
- **Annotations**: Event mapping annotations regenerated
- **Tooltips**: Tooltip data refreshed

**When NOT Needed**:
Force refresh is usually not needed - AppRefiner automatically refreshes on:
- Text edits
- File saves
- Cursor moves / typing pauses

Only use Force Refresh when automatic refresh fails.

**Troubleshooting with Force Refresh**:
1. **Stale Highlights**: Run Force Refresh to clear/reapply
2. **Missing Indicators**: Verify settings, then Force Refresh
3. **Type Errors Won't Clear**: Edit code, save, then Force Refresh
4. **Fold Markers Wrong**: Force Refresh recalculates fold points

---

## SQL Formatting Command

### SQL: Format SQL

**Description**: Formats SQL objects PeopleCode for improved readability with proper indentation, keyword capitalization, and line breaks.

**Requirements**:
- Editor Tweaks → Settings → "Format SQL" must be enabled

**When to Use**:
- When writing or analyzing large SQL objects

**Access**:
- Command Palette: Search for "SQL: Format"
- Requires active editor to be SQL object editor

**Undo**:
If formatting produces undesired results disable the setting and close/reopen the SQL Object.

**Related Settings**:
- **Format SQL** checkbox: Must be enabled for command to appear
- Located in Editor Tweaks → General → Settings group

---

### Debug: Open Indicator Panel

**Description**: Opens a diagnostic panel showing all active styler indicators (highlights, underlines) applied to the current editor, useful for debugging styling issues and understanding what stylers are active.

**When to Use**:
- Debugging styler visual issues
- Understanding why code is highlighted
- Troubleshooting conflicting indicators
- Verifying styler is running
- Plugin development and testing

**Access**:
- Command Palette: Search for "Debug: Open Indicator Panel"
- Requires active editor

**Panel Features**:
- **Indicator List**: All active indicators with details
- **Indicator Type**: HIGHLIGHTER, SQUIGGLY, PLAIN, etc.
- **Color**: BGR color code displayed
- **Source Span**: Character range for indicator
- **Tooltip**: Associated hover text
- **Styler Name**: Which styler created this indicator
- **Real-Time Updates**: Refreshes as editor changes

**Indicator Information Displayed**:
```
Indicator #1:
  Type: SQUIGGLY
  Color: 0x0000FF (Red)
  Span: Line 42, Chars 15-28
  Tooltip: "Undefined variable: &invalidVar"
  Source: UndefinedVariableStyler
```

**Common Debugging Scenarios**:

**1. Indicator Not Showing**:
- Open Indicator Panel
- Check if indicator is registered
- If not registered: Styler may not be running
- If registered: Check color/type visibility

**2. Wrong Color or Type**:
- Verify indicator properties in panel
- Compare to expected values
- Check styler logic

**3. Multiple Overlapping Indicators**:
- Panel shows all indicators for same span
- Review which stylers are conflicting
- Adjust styler priorities or disable conflicting ones

**4. Performance Issues**:
- Check indicator count
- Too many indicators (>1000) may impact performance
- Review which stylers are creating excessive indicators

**Plugin Development**:
When developing custom stylers:
1. Add indicators via `AddIndicator()`
2. Open Indicator Panel
3. Verify indicator appears with correct properties
4. Adjust styler code as needed
5. Force Refresh and recheck

**Clearing Indicators**:
Indicators are managed by stylers. To clear:
1. Disable the styler (Settings → Stylers)
2. Force Refresh editor
3. Indicators removed

---

## Declaration Command

### Declare Function

**Description**: Opens a dialog to assist in inserting a declare function statement for an external PeopleCode function (ie a FUNCLIB function).

**When to Use**:
- Calling functions from other PeopleCode programs

**Access**:
- Command Palette: Search for "Declare Function"
- Requires active editor and DB connection

**Usage**:
- Open dialog and allow function index to build
- Search for the function you want to call
- Select it or press enter

**Behavior**:
- AppRefiner inserts the appropriate declare function statement
- AppRefiner generates an example invocation of the method at your cursor location
---

## Command Summary Table

| Command | Shortcut | Requires Editor | Requires Database | Description |
|---------|----------|----------------|-------------------|-------------|
| Database: Connect to DB | - | No | N/A | Open database connection dialog |
| Database: Disconnect DB | - | No | Yes (to disconnect) | Close active database connection |
| Editor: Clear Annotations | - | Yes | No | Remove event mapping annotations |
| Editor: Force Refresh | - | Yes | No | Re-parse and refresh all editor analysis |
| SQL: Format SQL | - | Yes | No | Format embedded SQL statements |
| Debug: Open Debug Console | - | No | No | View real-time debug logs |
| Debug: Open Indicator Panel | - | Yes | No | View active styler indicators |
| Declare Function | - | Yes | No | Declare external function signature |


## Related Features

- **[Settings Reference](../user-guide/settings-reference.md)**: Complete settings documentation
- **[Database Integration](database-integration.md)**: Database connection details
- **[Linting](linting/overview.md)**: Linter system using Force Refresh
- **[Code Styling](code-styling.md)**: Styler system using Indicator Panel
- **[Event Mapping Detection](event-mapping-detection.md)**: Feature using Clear Annotations
- **[Type Checking](type-checking.md)**: Uses Declare Function declarations

*For more information on AppRefiner commands and features, see the [Feature Documentation](../features/) and [User Guide](../user-guide/).*
