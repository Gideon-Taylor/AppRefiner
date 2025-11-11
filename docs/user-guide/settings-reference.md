# Settings Reference Guide

## Overview

AppRefiner provides extensive configuration options through its Settings dialog, allowing you to customize nearly every aspect of the application's behavior. Settings are organized into multiple tabs and groups, with all configurations persisted automatically across sessions.

**Key Features:**
- **Persistent Settings**: All configurations saved automatically to user profile
- **Tab Organization**: Settings grouped logically by category
- **Instant Application**: Most settings take effect immediately
- **Per-Process Configuration**: Settings apply to each Application Designer instance independently
- **Import/Export**: Settings stored in standard Windows user profile location

## Accessing Settings

The Settings dialog is accessible through AppRefiner's main interface:
- **Location**: Typically docked or floating window in Application Designer
- **Tabs Available**:
  1. **General** - Core editor settings and paths
  2. **Linters** - Enable/disable individual code analysis rules
  3. **Stylers** - Enable/disable visual indicators
  4. **Tooltips** - Enable/disable tooltip providers
  5. **Refactors** - Configure refactoring tools

---

## General Settings Tab

The General tab contains core configuration options organized into several groups:

### Settings Group

Core editor behavior and feature toggles.

#### Code Folding
- **Setting Name**: `codeFolding`
- **UI Label**: "Code Folding"
- **Default Value**: `True` (Enabled)
- **Type**: Boolean (Checkbox)

**Description**: Enables code folding functionality in the editor, allowing you to collapse and expand methods, functions, classes, and control structures.

**Effect When Enabled**:
- Fold markers appear in the editor margin
- Keyboard shortcuts active:
  - `Ctrl+Shift+[` - Collapse current fold
  - `Ctrl+Shift+]` - Expand current fold
  - `Ctrl+Shift+Alt+[` - Collapse all folds
  - `Ctrl+Shift+Alt+]` - Expand all folds
- Click fold markers to toggle sections

**Effect When Disabled**:
- All fold markers hidden
- Keyboard shortcuts inactive
- All code always expanded

**Recommended**: Enabled for large files and complex classes

**Related Features**: [Code Folding](../features/editor-tweaks/code-folding.md), Remember Folds setting

---

#### Auto Collapse
- **Setting Name**: `initCollapsed`
- **UI Label**: "Auto Collapse"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: When opening a file, automatically collapses all methods, functions, and classes to show only their signatures.

**Effect When Enabled**:
- Files open with all foldable regions collapsed
- Only class names and method signatures visible initially
- Must manually expand sections to view implementation
- Useful for getting quick overview of file structure

**Effect When Disabled**:
- Files open with all code expanded
- Full implementation visible immediately

**Recommended**: Enable for large files (500+ lines) or when you primarily navigate by outline

**Use Cases**:
- Large classes with many methods
- Reviewing overall file structure before diving into details
- Files you navigate frequently using Outline dialog

---

#### Only PPC
- **Setting Name**: `onlyPPC`
- **UI Label**: "Only PPC"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Restricts AppRefiner to only activate for PeopleCode files, ignoring SQL, HTML, and other file types opened in Application Designer.

**Effect When Enabled**:
- AppRefiner only attaches to PeopleCode editors
- SQL, HTML, XML, and other editors use default Application Designer behavior
- Reduces memory usage if you frequently work with non-PeopleCode files
- Linters, stylers, and features inactive for non-PeopleCode

**Effect When Disabled**:
- AppRefiner attempts to enhance all file types
- Some features may provide limited functionality for non-PeopleCode
- Syntax highlighting still applied where appropriate

**Recommended**: Enable if you primarily work with PeopleCode and want to reduce overhead for SQL/HTML files

---

#### Format SQL
- **Setting Name**: `betterSQL`
- **UI Label**: "Format SQL"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Enables automatic SQL formatting for SQL Objects, making queries more readable with a better indentation and structure.

**Effect When Enabled**:
- SQL statements are automatically formatted with a better indentation that normal App Designer formatting.

**Effect When Disabled**:
- SQL remains as originally formatted

**Recommended**: Enable if you frequently write complex SQL objects.

**Example Before**:
```peoplecode

```

**Example After** (formatted):
```peoplecode

```

**Related Features**: [SQL Formatting](../features/editor-tweaks/sql-formatting.md)

---

#### Auto Dark Mode
- **Setting Name**: `autoDark`
- **UI Label**: "Auto Dark Mode"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Automatically applies dark theme styling to Application Designer windows when AppRefiner is active.

**Effect When Enabled**:
- Application Designer editors styled with dark theme

**Effect When Disabled**:
- Application Designer uses standard Windows theme
- Default light appearance

**Recommended**: This feature was experimental, not recommended for general use.

---

#### Pair quotes and parens
- **Setting Name**: `autoPair`
- **UI Label**: "Pair quotes and parens"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Automatically inserts closing quotes, parentheses when you type the opening character.

**Effect When Enabled**:
- Typing `"` automatically inserts closing `"`
- Typing `(` automatically inserts closing `)`

- Cursor positioned between opening and closing characters
- Smart skip-over: Typing closing character when already present moves cursor past it

**Effect When Disabled**:
- Must manually type both opening and closing characters
- Standard typing behavior

**Recommended**: Enable for faster typing and fewer syntax errors

**Behavior Details**:
- **Selection Auto-Wrap**: Select text and type `"` wraps selection in quotes
- **Skip Existing**: If cursor is before `)`, typing `)` moves past it instead of inserting new one
- **Backspace Pairing**: Deleting opening character also deletes empty closing character

---

#### Prompt for DB Connection
- **Setting Name**: `promptForDB`
- **UI Label**: "Prompt for DB Connection"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Automatically displays the database connection dialog when opening an Application Designer process that doesn't have an active database connection.

**Effect When Enabled**:
- Connection dialog appears when opening first PeopleCode editor
- Can dismiss dialog to work offline
- Prompts only once per Application Designer process
- Reminder that database-dependent features require connection

**Effect When Disabled**:
- No automatic prompt
- Must manually connect via database menu/command
- Database-dependent features unavailable until connected

**Recommended**: Enable if you typically work with database connections and want automatic setup

**Database-Dependent Features**:
- Smart Open (Ctrl+O)
- Go To Definition for external classes
- PeopleSoft Object Info tooltips
- Application Class Details tooltips
- Event Mapping Detection
- Project Linting (if project contains multiple files)
- Type Checking for external App Classes

**Related Features**: [Database Integration](database-integration.md)

---

#### Center Dialogs
- **Setting Name**: `AutoCenterDialogs`
- **UI Label**: "Center Dialogs"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Automatically centers Application Designer dialog boxes on the Application Designer window instead of center of the monitor.

**Effect When Enabled**:
- Any dialog opened by Application Designer is centered over main window
- Consistent positioning regardless of monitor configuration
- Easier to find dialogs on multi-monitor setups

**Effect When Disabled**:
- Dialogs are centered on the screen
- May appear on different monitors

**Recommended**: Enable for multi-monitor setups, if App Designer isn't centered on screen, or if you frequently lose dialog windows

---

#### Multiple Selection
- **Setting Name**: `multiSelection`
- **UI Label**: "Multiple Selection"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Enables multi-cursor editing, allowing you to select and edit multiple locations simultaneously (similar to VS Code or Sublime Text).

**Effect When Enabled**:
- `Ctrl+Click` adds additional cursors
- Edit operations apply to all cursors simultaneously
- Copy/paste works across multiple selections

**Effect When Disabled**:
- Standard single-cursor editing

**Recommended**: Enable if you're familiar with multi-cursor editing from other IDEs

**Common Workflows**:
2. **Multiple Lines**:
   - `Ctrl+Click` at beginning of multiple lines
   - Type to add same text to all lines
3. **Columnar Editing**:
   - Use multi-cursor for column-aligned changes

**Limitations**:
- More complex than PeopleCode's typical editing model
- Requires practice to use effectively

---

#### Line Selection Fix
- **Setting Name**: `lineSelectionFix`
- **UI Label**: "Line Selection Fix"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: PeopleTools 8.62 Application Designer introduced a bug preventing Shift + Up and Shift + Down from working for line selection.

**Effect When Enabled**:
- Intercepts Shift + Up/Down behavior to replicate the expected line selection behavio

**Effect When Disabled**:
- Standard behavior (broken in 8.62)

**Recommended**: Enable if you are on 8.62

---

#### Remember Folds
- **Setting Name**: `rememberFolds`
- **UI Label**: "Remember Folds"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Persists the fold state (collapsed/expanded) of methods and blocks when you close and reopen a file.

**Effect When Enabled**:
- Fold states saved when closing file
- Fold states restored when reopening same file
- Persists across Application Designer sessions
- Stored per file path

**Effect When Disabled**:
- Fold states reset each time file is opened
- All regions expanded by default (unless Auto Collapse enabled)

**Recommended**: Enable if you work with large files and want to maintain your preferred view

**Storage**: Fold states stored in user profile, keyed by file's qualified name (e.g., `APP_PACKAGE:MYCLASS.OnExecute`)

**Related Features**: Code Folding, Auto Collapse settings

---

#### Override Find/Replace
- **Setting Name**: `overrideFindReplace`
- **UI Label**: "Override Find/Replace"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Replaces Application Designer's default Find/Replace functionality with AppRefiner's Better Find/Replace feature.

**Effect When Enabled**:
- `Ctrl+F` opens Better Find dialog instead of Application Designer's Find
- `Ctrl+H` opens Better Find Replace dialog
- Application Designer's Find menu commands redirected
- Better Find features available:
  - Regex support
  - Capture group replacement
  - Visual highlighting
  - Search history
  - Per-file search state

**Effect When Disabled**:
- `Ctrl+F` opens Application Designer's default Find dialog
- Must use `Ctrl+Alt+F` for Better Find
- Application Designer Find/Replace unchanged

**Recommended**: Enable if you use regex or prefer Better Find's interface

**Related Features**: [Better Find/Replace](../features/better-find-replace.md)

---

#### Override Open
- **Setting Name**: `overrideOpen`
- **UI Label**: "Override Open"
- **Default Value**: `False` (Disabled)
- **Type**: Boolean (Checkbox)

**Description**: Replaces Application Designer's default Open dialog with AppRefiner's Smart Open feature for database-driven fuzzy search.

**Effect When Enabled**:
- `Ctrl+O` opens Smart Open dialog instead of Application Designer's Open
- Application Designer's File > Open commands redirected
- Smart Open features available:
  - Fuzzy search across all object types
  - Real-time filtering
  - Recent file history
  - Configurable object type filters

**Effect When Disabled**:
- `Ctrl+O` opens Application Designer's default Open dialog
- Must use command palette for Smart Open
- Application Designer Open functionality unchanged

**Recommended**: Enable if you have database connection and prefer Smart Open's speed

**Requirements**: Requires active database connection for full functionality

**Related Features**: [Smart Open](../features/smart-open.md), Config Open button

---

### Directories Group

Configures file system paths used by AppRefiner.

#### Lint Report Directory
- **Setting Name**: `LintReportPath`
- **UI Button**: "Lint Report Directory..."
- **Default Value**: `"LintingReports"` (relative to AppRefiner directory)
- **Type**: String (Directory Path)

**Description**: Specifies where HTML lint reports are saved when running Project Linting.

**Configuration**:
1. Click "Lint Report Directory..." button
2. Browse to desired directory
3. Path can be absolute or relative
4. Directory created automatically if doesn't exist

**Default Behavior**:
- Reports saved to `LintingReports` subdirectory next to AppRefiner.exe
- Each report named with timestamp: `LintReport_YYYYMMDD_HHMMSS.html`

**Recommended Path**: Network share or source-controlled directory for team sharing

**Related Features**: [Project Linting](../features/project-linting.md)

---

#### TNS_ADMIN Directory
- **Setting Name**: `TNS_ADMIN`
- **UI Button**: "TNS_ADMIN Directory..."
- **Default Value**: Empty (uses Oracle default)
- **Type**: String (Directory Path)

**Description**: Sets the `TNS_ADMIN` environment variable for Oracle database connections, pointing to the directory containing `tnsnames.ora`.

**Configuration**:
1. Click "TNS_ADMIN Directory..." button
2. Browse to directory containing `tnsnames.ora`
3. Path must be absolute
4. Restart Application Designer after changing

**When to Set**:
- Using Oracle database connections
- `tnsnames.ora` not in default Oracle installation location
- Multiple Oracle clients installed
- Using non-standard Oracle configuration

**Effect**:
- Overrides Oracle's default TNS_ADMIN search path
- AppRefiner passes this to Oracle driver when connecting

**Leave Empty If**:
- Using SQL Server (not applicable)
- Oracle uses default configuration
- `tnsnames.ora` already found correctly

**Related Features**: [Database Integration](database-integration.md)

---

### Auto Suggest Settings

Controls which auto-complete/suggestion features are enabled.

#### Variables

Shows auto-complete suggestions for variables when typing `&` character.

**Effect When Enabled**:
- Typing `&` shows list of accessible variables in current scope
- List includes:
  - Local variables
  - Instance variables (`instance` declarations)
  - Component variables (`component` declarations)
  - Global variables (`global` declarations)
  - Method parameters
- Filtered by what you type after `&`
- Select from list and press Enter to insert

**Effect When Disabled**:
- No variable suggestions
- Must type variable names manually

**Recommended**: Enable for faster variable access and fewer typos

**Related Features**: [Auto-Complete](../features/auto-suggest.md)

---

#### Call Signatures

Shows function/method parameter tooltips (call tips) when typing `(` after a function or method name or `,` while in argument list.

**Effect When Enabled**:
- Typing `(` after function name shows parameter list or `,` inside arguments list
- Call tip displays:
  - Function signature with parameter types
  - Current parameter highlighted as you type
  - Return type
  - Next allowed types
- Updates as you type each parameter

**Effect When Disabled**:
- No call tips shown
- Must remember parameter order and types

**Recommended**: Enable for faster coding and fewer parameter errors

**Supported Functions**:
- Builtin PeopleCode functions (Split, Left, Right, etc.)
- Builtin PeopleCode Objects (Row, Rowset, JsonObject, etc.)
- Methods in current class
- External class methods (with database connection)
- Declared external functions

**Related Features**: [Auto-Complete](../features/auto-suggest.md), Method Parameters Tooltip Provider

---

#### Methods/Props

Shows auto-complete suggestions for object methods and properties when typing `.` after an object reference.

**Effect When Enabled**:
- Typing `.` after `&variable` shows available methods and properties
- Typing `.` after `%This` shows current class members
- List includes:
  - Public methods
  - Public properties
  - Protected members (if in same class hierarchy)
  - Builtin type methods (for Row, Rowset, Record, etc.)
- Filtered by what you type after `.`

**Effect When Disabled**:
- No member suggestions
- Must type method/property names manually

**Recommended**: Enable for API discovery and faster coding

**Intelligence Sources**:
- **Builtin Types**: From PeopleCodeTypeDatabase (always available)
- **Current Class**: From AST analysis (always available), will include inherited members with DB connection active
- **External Classes**: From database queries (requires connection)

**Related Features**: [Auto-Complete](../features/auto-suggest.md)

---

#### System Variables

Shows auto-complete suggestions for PeopleCode system variables when typing `%` character.

**Effect When Enabled**:
- Typing `%` shows list of system variables:
  - `%This`
  - `%Super`
  - `%Session`
  - `%Component`
  - `%Page`
  - `%Menu`
  - And many others
- Filtered by what you type after `%`

**Effect When Disabled**:
- No system variable suggestions
- Must type system variables manually

**Recommended**: Enable for discovering available system variables

**Related Features**: [Auto-Complete](../features/auto-suggest.md)

---

### Theme Settings

Controls visual appearance of Application Designer integration.

#### Theme Dropdown

Selects the icon theme used in auto-suggest dropdowns.

---

#### Filled Checkbox

Applies "filled" variant of selected theme, typically with more solid backgrounds and less transparency.

**Recommended**: Enable if you prefer more defined visual elements

---

### Event Mapping Group

Configures PeopleSoft Event Mapping detection and display.

#### Detect Event Mapping

Enables automatic detection and display of PeopleSoft Event Mapping configuration when opening component PeopleCode.

**Effect When Enabled**:
- Queries `PSAEEVENTMAP` table when opening component events
- Displays event-mapped classes in annotations:
  - Pre-event mappings at top of file
  - Post-event mappings at bottom of file
  - Override (Replace) mappings indicated
- Annotations show:
  - Sequence (Pre/Post/Replace)
  - Sequence number
  - App Class qualified name
  - Component and segment context

**Effect When Disabled**:
- No event mapping detection
- Annotations not displayed

**Recommended**: Enable when working with Event Mapping extensively

**Requirements**:
- Database connection required

**Related Features**: [Event Mapping Detection](../features/event-mapping-detection.md)

---

#### Show Event Mapped References

Controls whether event mapping annotations include cross-reference information showing where event-mapped classes are used.

**Effect When Enabled** (requires Detect Event Mapping also enabled):
- Annotations include full list of event mappings
- Shows all Pre, Post, and Replace sequences
- Detailed component/segment/event information displayed

**Effect When Disabled**:
- Only basic event mapping information shown
- Reduced annotation verbosity

**Recommended**: Enable if you need detailed event mapping context

**Related Settings**: Must have "Detect Event Mapping" enabled for this to have effect

---

### Show Radio Buttons (Event Mapping Display Format)

Controls how event-mapped class information is displayed in annotations.

#### Class Path

When selected, displays event-mapped classes as their qualified path only.

**Example**:
```
[Pre] Application:Utilities:EventHandler
```

**Pros**:
- Compact display
- Easy to see class structure
- Less scrolling required

**Cons**:
- Must open class to see implementation
- Can't preview class contents

---

#### Class Text

When selected, retrieves and displays the full source code of event-mapped classes in annotations.

**Example**:
```
[Pre] Application:Utilities:EventHandler
--------------------------------------
class EventHandler
   method OnExecute();
      /* Full class source here */
   end-method;
end-class;
```

**Pros**:
- See implementation without opening class
- Understand event flow in context
- No need to navigate to external classes

**Cons**:
- Very verbose annotations
- Requires more scrolling
- Slower loading (database queries for each class)
- Annotations can be overwhelming for files with many mappings

**Recommended**: Use "Class Path" for compact view, "Class Text" only when you need to see implementations inline

---

## Plugin Button

Opens the Plugin Manager dialog for loading, unloading, and configuring AppRefiner plugins.

**Plugin Manager Features**:
- View loaded plugins
- Enable/disable plugins
- Configure plugin directory
- Reload plugins without restarting

**Default Plugin Directory**: `Plugins` subdirectory next to AppRefiner.exe

**Plugin Types**:
- Custom Linters
- Custom Stylers
- Custom Refactors
- Custom Tooltip Providers
- Custom Commands

**Related Documentation**: See PluginSample project for example plugin implementation

---

## Config Open Button

Opens the Smart Open Configuration dialog to customize which PeopleSoft object types are searched.

**Configuration Options**:
- Enable/disable searching for specific object types:
  - App Classes
  - Records
  - Pages
  - Components
  - Menus
  - Component Interfaces
  - App Packages
  - And many more
- Set default selections
- Reorder object types in results

**Effect**:
- Controls which tables Smart Open queries
- Impacts search performance (fewer types = faster searches)
- Customizes results relevance

**Related Features**: [Smart Open](../features/smart-open.md), Override Open setting

---

## Debug Log Button

Opens the Debug Log modal dialog.

**When to Use**:
- Troubleshooting errors
- Reporting bugs to AppRefiner developers

---

## Linters Tab

The Linters tab displays a grid of all available linting rules with checkboxes to enable/disable each one.

### Grid Layout

| Column | Description |
|--------|-------------|
| Active | Checkbox to enable/disable the linter |
| Description | Human-readable description of what the linter checks |

### Managing Linter States

**Enabling a Linter**:
1. Navigate to Linters tab
2. Check the "Active" checkbox for desired linter
3. Linter runs immediately on next file edit or parse

**Disabling a Linter**:
1. Navigate to Linters tab
2. Uncheck the "Active" checkbox
3. Existing linter findings for that rule remain until next parse

### Linter State Persistence

- Linter states saved automatically to user profile
- Persists across Application Refiner sessions


### Available Linters

For complete list of linters and their descriptions, see [Working with Linters](working-with-linters.md).

**Related Features**: [Linting](../features/linting/overview.md), [Project Linting](../features/project-linting.md)

---

## Stylers Tab

The Stylers tab displays a grid of all available styling rules with checkboxes to enable/disable each one.

### Grid Layout

Same as Linters tab:

| Column | Description |
|--------|-------------|
| Active | Checkbox to enable/disable the styler |
| Description | Human-readable description of what the styler highlights |

### Managing Styler States

**Enabling a Styler**:
1. Navigate to Stylers tab
2. Check the "Active" checkbox for desired styler
3. Styler runs immediately on next file edit or parse

**Disabling a Styler**:
1. Navigate to Stylers tab
2. Uncheck the "Active" checkbox
3. Existing highlighting for that rule cleared immediately

### Styler State Persistence

- Styler states saved automatically to user profile
- Persists across Application Refiner sessions

### Available Stylers

For complete list of stylers and their descriptions, see [Code Styling](../features/code-styling.md).

**Common Stylers**:
- Syntax Error Styler
- Type Error Styler
- Undefined Variables Styler
- Unused Variables Styler
- Dead Code Styler
- And 15+ more

**Related Features**: [Code Styling](../features/code-styling.md), [Quick Fixes](../features/quick-fixes.md)

---

## Tooltips Tab

The Tooltips tab displays a grid of all available tooltip providers with checkboxes to enable/disable each one.

### Grid Layout

Same as Linters and Stylers tabs:

| Column | Description |
|--------|-------------|
| Active | Checkbox to enable/disable the tooltip provider |
| Description | Human-readable description of what information the provider shows |

### Managing Tooltip Provider States

**Enabling a Provider**:
1. Navigate to Tooltips tab
2. Check the "Active" checkbox for desired provider
3. Provider active immediately on next tooltip request

**Disabling a Provider**:
1. Navigate to Tooltips tab
2. Uncheck the "Active" checkbox
3. Provider stops providing tooltips immediately

### Tooltip State Persistence

- Tooltip states saved automatically to user profile
- Persists across Application Refiner sessions


### Available Tooltip Providers

For complete list and detailed descriptions, see [Tooltips](../features/tooltips.md).

**All 7 Providers**:
1. Active Indicators Tooltip Provider
2. Scope Context Tooltip Provider
3. PeopleSoft Object Info Tooltip Provider
4. Method Parameters Tooltip Provider
5. Variable Info Tooltip Provider
6. Inferred Type Tooltip Provider
7. Application Class Details Tooltip Provider

**Related Features**: [Tooltips](../features/tooltips.md)

---

## Hidden/Advanced Settings

Some settings are not exposed in the UI but can be configured by editing the settings file directly.

### MaxFileSnapshots
- **Setting Name**: `MaxFileSnapshots`
- **Default Value**: `10`
- **Type**: Integer
- **Location**: Not exposed in UI

**Description**: Maximum number of snapshots retained per file in the Snapshot History system.

**Effect**:
- When limit reached, oldest snapshots are automatically deleted
- Set to `0` to disable automatic cleanup
- Set higher for more history retention

**To Change**:
1. Close AppRefiner
2. Edit `%APPDATA%\AppRefiner\user.config`
3. Find `<setting name="MaxFileSnapshots">`
4. Change value
5. Restart AppRefiner

**Related Features**: [Snapshot History](../features/snapshot-history.md)

---

### SnapshotDatabasePath
- **Setting Name**: `SnapshotDatabasePath`
- **Default Value**: `%APPDATA%\AppRefiner\Snapshots.db`
- **Type**: String (File Path)
- **Location**: Not exposed in UI

**Description**: Location of SQLite database storing file snapshots.

**To Change**:
1. Close AppRefiner
2. Edit `%APPDATA%\AppRefiner\user.config`
3. Find `<setting name="SnapshotDatabasePath">`
4. Change path (absolute or relative)
5. Restart AppRefiner

**Use Cases**:
- Store snapshots on network drive for backup
- Use different databases for different projects
- Move snapshots to SSD for performance

---

### FunctionCacheDatabasePath
- **Setting Name**: `FunctionCacheDatabasePath`
- **Default Value**: `%APPDATA%\AppRefiner\FunctionCache.db`
- **Type**: String (File Path)
- **Location**: Not exposed in UI

**Description**: Location of SQLite database caching function definitions for performance.

**Effect**:
- Caches function signatures from PeopleCodeTypeDatabase
- Reduces database queries for function lookups
- Improves auto-complete and tooltip performance

**To Clear Cache**:
1. Close AppRefiner
2. Delete `FunctionCache.db` file
3. Restart AppRefiner (cache rebuilds automatically)

---

## Settings File Locations

AppRefiner stores settings in standard Windows user profile directories:

### Primary Settings
- **Location**: `%APPDATA%\AppRefiner\user.config`
- **Format**: XML
- **Contains**: All user-configurable settings

### Backup and Migration

**To Backup Settings**:
1. Close AppRefiner and Application Designer
2. Copy `%APPDATA%\AppRefiner` directory
3. Store backup in safe location

**To Migrate to New Machine**:
1. Install AppRefiner on new machine
2. Close AppRefiner if running
3. Copy backed-up `AppRefiner` directory to `%APPDATA%` on new machine
4. Launch AppRefiner

**To Reset to Defaults**:
1. Close AppRefiner and Application Designer
2. Delete `%APPDATA%\AppRefiner\user.config`
3. Launch AppRefiner (creates new settings with defaults)

---

## Related Features

- **[Linting](../features/linting/overview.md)**: Configure linter rules in Linters tab
- **[Code Styling](../features/code-styling.md)**: Configure stylers in Stylers tab
- **[Tooltips](../features/tooltips.md)**: Configure tooltip providers in Tooltips tab
- **[Smart Open](../features/smart-open.md)**: Affected by Override Open and Config Open settings
- **[Better Find/Replace](../features/better-find-replace.md)**: Affected by Override Find/Replace setting
- **[Auto-Complete](../features/auto-suggest.md)**: Configured in Auto Suggest Settings group
- **[Event Mapping Detection](../features/event-mapping-detection.md)**: Configured in Event Mapping group
- **[Database Integration](database-integration.md)**: Affects many database-dependent features

---

*For more information on specific features controlled by these settings, see the related feature documentation listed above.*
