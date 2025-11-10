# Smart Open Dialog

AppRefiner's Smart Open Dialog revolutionizes how you open PeopleSoft definitions in Application Designer. Instead of navigating through menus and typing exact names, Smart Open provides instant fuzzy search across all definition types with a modern, keyboard-driven interface.

## Overview

Smart Open replaces Application Designer's default Open dialog with a powerful search system that:

- **Searches Across 24 Definition Types** - Records, Pages, Components, Application Classes, SQL, and more
- **Fuzzy Matching** - Find definitions by partial names or descriptions
- **Split Search** - Search ID and description simultaneously or separately
- **Configurable Types** - Enable/disable specific definition types
- **Grouped Results** - Results organized by definition type with counts
- **Fast and Responsive** - 300ms typing delay for instant feedback
- **Always Available** - Press `Ctrl+O` from anywhere in Application Designer

## Opening Smart Open

**Keyboard Shortcut:** `Ctrl+O`

When the "Override Open" setting is enabled (default), pressing `Ctrl+O` in Application Designer opens Smart Open instead of the default Open dialog.

### Bypassing Smart Open

If you need to access Application Designer's native Open dialog:

1. Right-click anywhere in the Smart Open dialog
2. Select **"Use Application Designer Open Dialog..."**
3. The native dialog will open

## Dialog Layout

The Smart Open dialog consists of:

- **Header Bar** - "AppRefiner - Smart Open" title with dark background
- **Search Panel** - Two search fields for ID and Description
- **Results Tree** - Grouped results organized by definition type
- **Status Indicators** - Result counts and placeholder messages

## Basic Search

### ID Search

The **ID** field searches definition names (the primary identifier):

```
ID: EMPLOYEE         → Finds records, pages, components with "EMPLOYEE" in the name
ID: GBL_             → Finds all definitions starting with "GBL_"
ID: Install          → Finds items like "INSTALL_MENU", "Installation", etc.
```

**Features:**
- Case-insensitive matching
- Partial matching (substring search)
- Matches beginning, middle, or end of names
- Uses SQL wildcard patterns (`%` automatically added)

### Description Search

The **Description** field searches definition descriptions/long names:

```
Description: employee data    → Finds items described as "Employee Data Entry"
Description: security         → Finds security-related definitions
Description: approval         → Finds approval workflow items
```

**Features:**
- Searches long description field
- Case-insensitive matching
- Partial matching with wildcards

### Combined Search

Use both fields together for precise results:

```
ID: INSTALL
Description: menu
→ Finds installation-related menus only

ID: EMP
Description: security
→ Finds employee security definitions
```

## Advanced Search Features

### Split Search with Space

Type a space in the ID field to automatically split your search:

```
ID: EMPLOYEE DATA    (contains space)
→ Converts to:
   ID: EMPLOYEE
   Description: DATA%[existing description text]
```

This allows rapid multi-field searching from a single field.

### Search Timing

Smart Open implements a **300ms typing delay** - searches execute 300ms after you stop typing. This provides:

- Instant feedback as you type
- Reduced database load (fewer queries)
- Smooth typing experience without lag

### Keyboard Navigation

Full keyboard navigation eliminates mouse usage:

| Key | Action |
|-----|--------|
| `Down Arrow` (from search field) | Move focus to results tree |
| `Up/Down Arrow` (in tree) | Navigate through results |
| `Left/Right Arrow` | Collapse/expand groups |
| `Enter` | Open selected definition |
| `Escape` | Close dialog |
| `Tab` | Switch between ID and Description fields |

**Efficient Workflow:**
1. Press `Ctrl+O` to open Smart Open
2. Type search term (focus starts in ID field)
3. Press `Down` to enter results
4. Use `Up/Down` to navigate
5. Press `Enter` to open

## Results Display

### Grouped by Type

Results are automatically grouped by definition type with counts:

```
Application Class (5)
  ├─ PT_SECURITY:SecurityManager
  ├─ EMPLOYEE:EmployeeProcessor
  └─ ...

Component (3)
  ├─ EMP_PERSONAL_DATA
  ├─ EMPLOYEE_BENEFITS
  └─ ...

Record (12)
  ├─ EMPLOYEE
  ├─ EMPLOYEE_ADDRESS
  └─ ...
```

### Group Indicators

- **Bold text** - Group headers
- **(Count)** - Number of results in each group
- **Expandable/Collapsible** - Click or use arrow keys to expand/collapse

### Result Limits

By default, Smart Open shows **10 results per type**. This prevents overwhelming result lists while keeping the interface fast.

**Configuration:** Adjust `MaxResultsPerType` in Smart Open Settings (see [Configuration](#configuration))

### Tooltips

Hover over any result to see:

```
EMPLOYEE
Employee Master Record
Path: RECORD.EMPLOYEE
```

Tooltips show:
- Full definition name
- Description (if available)
- Object path for navigation

## Supported Definition Types

Smart Open searches across **24 PeopleSoft definition types**:

### Most Common Types

| Type | Description |
|------|-------------|
| **Application Class** | Application Package classes |
| **Application Package** | Package containers |
| **Component** | Component definitions |
| **Page** | Page definitions (classic) |
| **Page (Fluid)** | Fluid page definitions |
| **Record** | Record definitions |
| **Field** | Field definitions |
| **Menu** | Menu definitions |
| **SQL** | SQL object definitions |

### Additional Types

| Type | Description |
|------|-------------|
| **Activity** | Activity definitions |
| **Analytic Model** | Analytic model definitions |
| **Analytic Type** | Analytic type definitions |
| **App Engine Program** | Application Engine programs |
| **Approval Rule Set** | Approval rule definitions |
| **Business Interlink** | Business interlink definitions |
| **Business Process** | Business process definitions |
| **Component Interface** | Component interface definitions |
| **File Layout** | File layout definitions |
| **File Reference** | File reference definitions |
| **HTML** | HTML definitions |
| **Image** | Image definitions |
| **Message** | Message catalog definitions |
| **Non Class PeopleCode** | Record.Field.Event PeopleCode |
| **Optimization Model** | Optimization model definitions |
| **Project** | Project definitions |
| **Style Sheet** | Style sheet definitions |

## Configuration

### Smart Open Settings

Access configuration through:

1. Right-click in Smart Open dialog
2. Select **"Smart Open Settings..."**
3. Opens configuration dialog

### Configurable Options

#### Enable/Disable Definition Types

Choose which types appear in search results:

- Checkboxes for each of 24 definition types
- Disable types you rarely search
- Reduces result clutter
- Improves search performance

**Example Configuration:**
```
☑ Application Class
☑ Component
☑ Page
☑ Record
☐ Activity
☐ Business Process
☐ File Layout
☐ Image
```

#### Max Results Per Type

Control how many results display per type:

- Default: **10** results per type
- Range: 1-100
- Lower values = faster display
- Higher values = more comprehensive results

**Use Cases:**
- **10 results** - Fast, focused searching (default)
- **25 results** - Broader result sets
- **50 results** - Comprehensive searching
- **100 results** - Maximum coverage (may be slow)

#### Sort By Last Update

Option to sort results by modification date:

- **Disabled (default)** - Sort alphabetically by name
- **Enabled** - Sort by last update date (newest first)

**When to enable:**
- Working on recent changes
- Finding newly created definitions
- Tracking recent modifications

**Note:** Last update sorting requires additional database queries and may reduce performance.

### Settings Persistence

All Smart Open settings are saved to:
```
%APPDATA%\AppRefiner\smartopen-config.json
```

Settings persist across Application Designer sessions.

## Integration with Application Designer

### Opening Definitions

When you select a definition and press Enter, Smart Open:

1. Closes the dialog
2. Instructs Application Designer to open the definition
3. Uses Application Designer's native open mechanism
4. Handles all object types identically to native dialog

### Override Setting

**Location:** AppRefiner Settings > General > "Override Open"

**When Enabled (default):**
- `Ctrl+O` opens Smart Open
- Native Open dialog is bypassed
- Right-click provides access to native dialog

**When Disabled:**
- `Ctrl+O` opens native Application Designer dialog
- Smart Open must be accessed through Command Palette
- Search for "Open: Smart Open (Ctrl+O)"

## Database Requirements

### Required

Smart Open **requires an active database connection** to function. Without a database connection:

- Smart Open cannot search definitions
- Opening the dialog shows an error or no results
- You must connect to a database first

### Database Performance

Smart Open queries the database in real-time:

- **Query Timing:** 300ms after typing stops
- **Query Scope:** Searches enabled definition types only
- **Query Limits:** Respects `MaxResultsPerType` setting
- **Caching:** Results are not cached (always fresh)

**Performance Tips:**
- Disable unused definition types to reduce query time
- Use specific search terms (3+ characters)
- Lower `MaxResultsPerType` for faster results
- Ensure stable database connection

## Common Search Patterns

### Find Application Classes

```
ID: PT_              → All classes starting with PT_
ID: Security         → Security-related classes
Description: API     → API-related classes
```

### Find Records by Prefix

```
ID: PS_              → All records starting with PS_
ID: EMPLOYEE         → Employee-related records
ID: %_VW             → All view records (ending in _VW)
```

### Find Components

```
ID: EMP_             → Employee components
Description: admin   → Administrative components
```

### Find Pages

```
ID: WF_              → Workflow pages
Description: search  → Search pages
```

### Find SQL Objects

```
ID: %_SEL            → All SQL selects (ending in _SEL)
Description: query   → Query SQL objects
```

### Find Projects

```
ID: INSTALL          → Installation projects
ID: UPG_             → Upgrade projects
```

## Use Cases and Workflows

### Quick Definition Lookup

**Scenario:** You need to open EMPLOYEE record

**Workflow:**
1. Press `Ctrl+O`
2. Type `emp`
3. Press `Down` to enter results
4. Press `Enter` to open first match (likely EMPLOYEE record)

**Time:** ~2 seconds

### Exploring Related Definitions

**Scenario:** Find all employee-related pages

**Workflow:**
1. Press `Ctrl+O`
2. Type `employee` in ID field
3. Navigate to "Page" group in results
4. Review all matching pages
5. Open desired page with `Enter`

### Finding by Description

**Scenario:** You remember a component is about "benefits" but can't remember its exact name

**Workflow:**
1. Press `Ctrl+O`
2. Leave ID field empty
3. Tab to Description field
4. Type `benefits`
5. Review Component group results
6. Open the correct component

### Cross-Type Search

**Scenario:** Find all "security" related definitions across all types

**Workflow:**
1. Press `Ctrl+O`
2. Type `security` in Description field
3. Review results across:
   - Application Classes
   - Components
   - Pages
   - Records
   - SQL
4. Navigate to specific type groups
5. Open desired definition

## Tips and Best Practices

### 1. Start with Partial Names

Don't type complete names:

```
Good:    emp       (finds EMPLOYEE, EMP_DATA, etc.)
Avoid:   EMPLOYEE  (only finds exact/close matches)
```

### 2. Use Description for Discovery

When you don't know the exact ID:

```
ID: [empty]
Description: workflow approval
→ Discovers all approval workflow items
```

### 3. Disable Unused Types

If you never search certain types:

1. Open Smart Open Settings
2. Uncheck: Activity, Business Process, File Layout, etc.
3. Faster searches, cleaner results

### 4. Learn Common Prefixes

PeopleSoft naming conventions:

```
PS_        → Delivered records
DERIVED_   → Derived records
%_VW       → Views
%_TBL      → Tables
PT_        → Delivered app classes
FUNCLIB_   → Function libraries
```

### 5. Navigate with Keyboard

Avoid the mouse:

```
Ctrl+O → Down → Down → Down → Enter
(Opens definition in ~3 keystrokes)
```

### 6. Use Split Search

For complex searches:

```
Type:  "EMPLOYEE DATA"  (with space)
Auto splits to:
  ID: EMPLOYEE
  Description: DATA%
```

## Troubleshooting

### No Results Appear

**Problem:** Search returns no results despite typing

**Solutions:**
- Verify database connection is active
- Check spelling of search term
- Try shorter/partial search terms
- Ensure definition type is enabled in settings
- Check `MaxResultsPerType` isn't set too low

### Dialog Doesn't Open

**Problem:** Pressing `Ctrl+O` opens native dialog or nothing

**Solutions:**
- Check "Override Open" is enabled in AppRefiner Settings
- Verify AppRefiner is enabled for the Application Designer process
- Try accessing through Command Palette: "Open: Smart Open"
- Restart Application Designer

### Search is Slow

**Problem:** Results take several seconds to appear

**Solutions:**
- Reduce `MaxResultsPerType` to 5 or fewer
- Disable unused definition types
- Use more specific search terms (3+ characters)
- Check database connection speed and stability
- Verify database server performance

### Wrong Definition Opens

**Problem:** Selecting a result opens a different definition

**Solutions:**
- Ensure you selected the correct node (not a group header)
- Check tooltip to verify target before opening
- Verify database connection is stable
- Try closing and reopening Smart Open

### Results Don't Update

**Problem:** Search results don't change after typing

**Solutions:**
- Wait 300ms after typing (automatic delay)
- Verify database connection is active
- Check for errors in Application Designer status bar
- Try closing and reopening Smart Open

## Performance Considerations

### Query Performance

Factors affecting search speed:

1. **Number of Enabled Types** - More types = more queries
2. **Max Results Per Type** - Higher limits = more data transfer
3. **Database Connection** - Network speed and latency
4. **Search Specificity** - Vague terms match more results

**Optimization:**
- Enable only types you frequently search
- Use specific search terms (avoid single letters)
- Set `MaxResultsPerType` to 10 or less
- Maintain stable database connection

### Memory Usage

Smart Open is lightweight:

- Results are not cached (fresh on each search)
- Dialog closes after opening a definition
- Minimal memory footprint
- No persistent background processes

## Comparison to Native Open

| Feature | Native Open | Smart Open |
|---------|-------------|------------|
| **Search Method** | Exact name entry | Fuzzy matching |
| **Search Fields** | Single field | ID + Description |
| **Types Searchable** | One at a time | All simultaneously |
| **Results Display** | List (one type) | Grouped tree (all types) |
| **Keyboard Navigation** | Limited | Full keyboard support |
| **Performance** | Fast (no search) | Real-time search |
| **Discovery** | Must know exact name | Browse and discover |
| **Configuration** | None | Customizable types/limits |

## Related Features

- **[Navigation](navigation.md)** - Go To Definition, Navigation History, and Bookmarks
- **[Better Find](better-find-replace.md)** - Advanced search within files
- **[Outline Dialog](outline.md)** - Navigate within current file
- **[Type Checking](type-checking.md)** - Find type-related issues
- **[Command Palette](../user-guide/command-palette.md)** - Access all commands

## Next Steps

- Configure [Smart Open Settings](#configuration) for your workflow
- Learn [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for faster navigation
- Try [Navigation features](navigation.md) for code exploration
- Explore [Database Integration](../user-guide/database-integration.md) for setup
