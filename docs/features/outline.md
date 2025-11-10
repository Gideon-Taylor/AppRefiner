# Outline Dialog

The Outline Dialog provides quick navigation within the current PeopleCode file by displaying an organized tree of all methods, properties, functions, and instance variables. With instant filtering and keyboard navigation, Outline makes it easy to jump to any definition in large files.

## Overview

The Outline Dialog offers:

- **Structured View** - All definitions organized by type and scope
- **Instant Search** - Real-time filtering as you type
- **Scope Organization** - Methods grouped by public/protected/private
- **Quick Navigation** - Jump to any definition with Enter
- **Keyboard-Driven** - Full keyboard support for efficient navigation
- **Toggle Grouping** - Switch between grouped and flat views
- **No Database Required** - Works entirely from parsed file content

## Opening the Outline

**Keyboard Shortcut:** `Ctrl+Shift+O`

Press `Ctrl+Shift+O` from any PeopleCode editor to open the Outline Dialog for the current file.

## Dialog Layout

The Outline Dialog consists of:

- **Header Bar** - "AppRefiner - Go To Definition" title
- **Search Box** - Filter definitions in real-time
- **Tree View** - Organized list of definitions with icons and grouping

## Definition Types

Outline displays these definition types:

### Methods

All method declarations and implementations in the current class:

```
Methods
  ├─ Public
  │   ├─ ProcessEmployee: void
  │   └─ GetEmployeeData: record
  ├─ Protected
  │   └─ ValidateInput: boolean
  └─ Private
      └─ CalculateTax: number
```

**Shows:**
- Method name
- Return type
- Scope (Public, Protected, Private)

### Properties

All property declarations with getters and setters:

```
Properties
  ├─ EmployeeName: string
  ├─ RecordCount: number
  └─ IsActive: boolean
```

**Shows:**
- Property name
- Property type

### Functions

All standalone functions (not methods):

```
Functions
  ├─ CalculateTax: number
  ├─ FormatDate: string
  └─ ValidateRecord: boolean
```

**Shows:**
- Function name
- Return type
- [Global] scope indicator

### Instance Variables

Property getters and setters when displayed separately:

```
Instance Variables
  ├─ EmployeeName [Getter]: string
  └─ EmployeeName [Setter]: string
```

**Shows:**
- Property name with [Getter]/[Setter] indicator
- Property type

## Basic Navigation

### Opening and Selecting

**Typical workflow:**

1. Press `Ctrl+Shift+O` to open Outline
2. Tree view appears with all definitions
3. First definition is automatically selected
4. Press `Enter` to jump to selected definition
5. Editor navigates to that definition and closes dialog

### Keyboard Navigation

Full keyboard control eliminates mouse usage:

| Key | Action |
|-----|--------|
| `Ctrl+Shift+O` | Open Outline Dialog |
| `Down Arrow` (from search) | Move focus to tree |
| `Up/Down Arrow` (in tree) | Navigate through definitions |
| `Left/Right Arrow` | Collapse/expand groups |
| `Enter` | Jump to selected definition |
| `Escape` | Close dialog |
| Any letter | Start typing to filter |

**Efficient Navigation:**
1. `Ctrl+Shift+O` → Opens Outline
2. Type filter text → Narrows results
3. `Down` → Enter tree view
4. `Up/Down` → Select definition
5. `Enter` → Jump to definition

### Mouse Navigation

Mouse users can:

- Click definitions to select them
- Double-click to jump to definition
- Right-click for context menu (Toggle Grouping)
- Use scrollbar for long lists

## Filtering and Search

### Real-Time Filtering

Type in the search box to filter definitions instantly:

```
Search: "process"
→ Shows only definitions containing "process" in:
  - Name
  - Type
  - Definition type (method, property, etc.)
  - Scope (public, protected, private)
```

### Filter Matching

Filtering is case-insensitive and matches:

- **Name**: Method/property/function name
- **Type**: Return type or property type
- **Definition Type**: "method", "property", "function"
- **Scope**: "public", "protected", "private", "global"

### Filter Examples

**Find all public items:**
```
Search: "public"
→ Shows all public methods
```

**Find all string returns:**
```
Search: "string"
→ Shows methods/properties returning string
```

**Find specific method:**
```
Search: "employee"
→ Shows ProcessEmployee, GetEmployee, etc.
```

**Find all getters:**
```
Search: "getter"
→ Shows all property getters
```

### Clearing Filter

- **Delete all text** in search box to show all definitions
- **Escape** clears search and closes dialog

## Grouping Modes

Outline supports two display modes:

### Grouped View (Default)

Definitions organized hierarchically by type and scope:

```
Methods
  ├─ Public
  │   ├─ Method1: string
  │   └─ Method2: number
  ├─ Protected
  │   └─ Method3: boolean
  └─ Private
      └─ Method4: void

Properties
  ├─ Property1: string
  └─ Property2: number

Functions
  └─ Function1: boolean
```

**Benefits:**
- Clear organization by type
- Easy to see scope boundaries
- Understand class structure at a glance

### Flat View

All definitions in a single list:

```
ProcessEmployee: void
GetEmployeeData: record
ValidateInput: boolean
EmployeeName: string
RecordCount: number
CalculateTax: number
```

**Benefits:**
- Faster scanning for specific items
- Simpler when filtering
- No collapsing/expanding needed

### Toggling Grouping

**To switch between modes:**

1. Right-click anywhere in the tree view
2. Select **"Toggle Grouping"**
3. View refreshes in the alternate mode

**Or use keyboard:**
1. Focus tree view
2. Right-click key (or Shift+F10)
3. Select "Toggle Grouping"

## Tooltips

Hover over any definition to see detailed information:

```
ProcessEmployee: void

[Public] Method ProcessEmployee of type void at line 45
```

Tooltip shows:
- Full name
- Type
- Scope with visual indicator
- Line number

## Use Cases and Workflows

### Navigate to Specific Method

**Scenario:** Jump to the `ProcessEmployee` method in a large class

**Workflow:**
1. `Ctrl+Shift+O` → Opens Outline
2. Type `process` → Filters to ProcessEmployee
3. `Enter` → Jumps to method

**Time:** ~2 seconds

### Explore Class Structure

**Scenario:** Understand what public methods a class exposes

**Workflow:**
1. `Ctrl+Shift+O` → Opens Outline
2. Expand **Methods > Public** group
3. Review list of public methods
4. Press `Escape` to close (or jump to one with `Enter`)

### Find All String-Returning Methods

**Scenario:** Locate methods that return strings

**Workflow:**
1. `Ctrl+Shift+O` → Opens Outline
2. Type `string` in search → Filters to string types
3. Review filtered results
4. Select desired method with arrows
5. `Enter` to jump

### Quick Property Review

**Scenario:** See all properties in a class

**Workflow:**
1. `Ctrl+Shift+O` → Opens Outline
2. Navigate to **Properties** group
3. Review property list
4. Jump to specific property or close

### Find Private Methods

**Scenario:** Locate private helper methods

**Workflow:**
1. `Ctrl+Shift+O` → Opens Outline
2. Type `private` in search → Shows only private items
3. Or navigate to **Methods > Private** group
4. Select and jump to method

## Display Details

### Node Format

Each definition displays as:
```
Name: Type
```

**Examples:**
- `ProcessEmployee: void`
- `EmployeeName: string`
- `ValidateRecord: boolean`
- `GetRecordCount: number`

### Group Headers

Groups show definition counts:
```
Methods (15)
Properties (8)
Functions (3)
```

### Scope Indicators

Methods are grouped under scope labels:
```
Public (5)
Protected (3)
Private (7)
```

### Icon Indicators

Tree view uses standard icons:
- **Folder** - Group nodes (Methods, Properties, etc.)
- **Document** - Individual definitions

## Integration with Other Features

### Works With Navigation History

Jumping to a definition via Outline:

1. Creates navigation history entry
2. Allows `Alt+Left` to return
3. Integrates seamlessly with [Navigation features](navigation.md)

### Works With Bookmarks

Before using Outline to explore:

1. Press `Ctrl+B` to place bookmark
2. Use Outline to jump around
3. Press `Ctrl+-` to return to bookmark

### Complements Go To Definition

- **Outline** - Jump within current file
- **Go To Definition (F12)** - Jump to symbol definition (may be in another file)

Use both together for comprehensive navigation.

## Performance

### Fast and Lightweight

Outline Dialog is highly performant:

- **Parsing:** Instant (uses cached AST from editor)
- **Display:** Instant for files up to 10,000 lines
- **Filtering:** Real-time, no noticeable lag
- **Memory:** Minimal footprint

### Large File Handling

For very large files (5,000+ lines):

- Initial display is instant
- Tree expansion may take 100-200ms
- Filtering remains fast
- No practical size limits

## Comparison to Other Navigation

| Feature | Outline Dialog | Go To Definition | Better Find |
|---------|----------------|------------------|-------------|
| **Scope** | Current file only | Any file | Current file |
| **Shows** | Definitions list | Single definition | Search matches |
| **Navigation** | Jump to definition | Jump to definition | Jump to match |
| **Filtering** | By name/type/scope | N/A | By pattern |
| **Organization** | Hierarchical tree | N/A | Line-by-line |
| **Database Needed** | No | Sometimes | No |

### When to Use Outline

**Use Outline when:**
- Exploring structure of current file
- Jumping between methods in same class
- Understanding class organization
- Finding definitions by type or scope

**Use Go To Definition when:**
- Following symbol to its definition
- Definition may be in another file
- Need precise symbol resolution

**Use Better Find when:**
- Searching for text patterns
- Finding all occurrences of something
- Need regex matching

## Tips and Best Practices

### 1. Use Filtering Liberally

Don't scroll through long lists:

```
Type a few letters → Narrow to target → Enter
(Faster than scrolling)
```

### 2. Learn Filter Terms

Common filter terms:

- `public` → All public methods
- `string` → All string types
- `get` → All getter methods or "Get*" methods
- `validate` → All validation methods

### 3. Combine with Bookmarks

Before deep exploration:

1. `Ctrl+B` → Place bookmark
2. `Ctrl+Shift+O` → Open Outline
3. Jump around freely
4. `Ctrl+-` → Return to bookmark

### 4. Use Flat View for Quick Finding

When you know the exact name:

1. Toggle to Flat View (right-click → Toggle Grouping)
2. Type name
3. Jump immediately

### 5. Keyboard-Only Workflow

Master the keyboard flow:

```
Ctrl+Shift+O → Type filter → Down → Up/Down → Enter
(No mouse required)
```

### 6. Explore New Code

When inheriting unfamiliar code:

1. Open Outline (`Ctrl+Shift+O`)
2. Review Methods > Public first
3. Then Protected
4. Understand public API before internals

## Troubleshooting

### Dialog Doesn't Open

**Problem:** Pressing `Ctrl+Shift+O` does nothing

**Solutions:**
- Verify cursor is in a PeopleCode editor window
- Check AppRefiner is enabled for the process
- Try accessing via Command Palette: "Navigation: Outline"
- Restart Application Designer

### No Definitions Shown

**Problem:** Tree view is empty

**Solutions:**
- Check file contains class, methods, or functions
- Verify file has been parsed successfully (no syntax errors)
- Try editing the file slightly to trigger re-parse
- Check AppRefiner status bar for errors

### Filter Not Working

**Problem:** Typing doesn't filter results

**Solutions:**
- Ensure cursor is in search box (not tree view)
- Click search box to focus it
- Clear existing text and try again
- Close and reopen dialog

### Can't Navigate to Definition

**Problem:** Pressing Enter doesn't jump

**Solutions:**
- Ensure you selected an actual definition (not a group header)
- Check that definition node is selected (highlighted)
- Try double-clicking the definition
- Verify cursor is in tree view, not search box

### Grouping Toggle Doesn't Work

**Problem:** Right-click menu doesn't appear or toggle doesn't work

**Solutions:**
- Ensure you right-clicked in the tree view area
- Try clicking a definition node before right-clicking
- Check if context menu appears at all
- Close and reopen dialog

## Keyboard Shortcuts Reference

| Shortcut | Context | Action |
|----------|---------|--------|
| `Ctrl+Shift+O` | Editor | Open Outline Dialog |
| `Down Arrow` | Search box | Move to tree view |
| `Up/Down Arrow` | Tree view | Navigate definitions |
| `Left/Right Arrow` | Tree view | Collapse/expand groups |
| `Enter` | Tree view | Jump to selected definition |
| `Enter` | Search box | Jump to first filtered definition |
| `Escape` | Dialog | Close dialog |
| Any letter | Dialog | Start filtering |

## Related Features

- **[Navigation](navigation.md)** - Go To Definition, Navigation History, Bookmarks
- **[Smart Open](smart-open.md)** - Open definitions across files
- **[Better Find](better-find-replace.md)** - Search within current file
- **[Code Styling](code-styling.md)** - Visual indicators for issues
- **[Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md)** - Complete shortcut reference

## Next Steps

- Learn [Navigation features](navigation.md) for cross-file navigation
- Try [Smart Open](smart-open.md) for opening any PeopleSoft definition
- Explore [Better Find](better-find-replace.md) for pattern searching
- Master [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for efficiency
