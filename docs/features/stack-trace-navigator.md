# Stack Trace Navigator

The Stack Trace Navigator transforms PeopleCode error investigation by parsing runtime stack traces and providing direct navigation to each frame. Instead of manually opening definitions and searching for line numbers, Stack Trace Navigator automatically extracts program paths, statement numbers, and allows single-click navigation to the exact error location.

## Overview

Stack Trace Navigator provides:

- **Automatic Parsing** - Recognizes PeopleCode stack trace formats
- **Direct Navigation** - Single-click navigation to any stack frame
- **Statement Number Support** - Jumps to exact statement numbers when available
- **Always-On-Top Display** - Stays visible while navigating code
- **Real-Time Updates** - 300ms debounced parsing as you paste
- **Error Pattern Detection** - Enhanced highlighting for specific error types
- **Database Integration** - Validates targets and resolves paths
- **Position to Right** - Automatically positions to avoid covering code

## Opening Stack Trace Navigator

**Keyboard Shortcut:** `Ctrl+Alt+S`

Press `Ctrl+Alt+S` from anywhere in Application Designer to open Stack Trace Navigator.

## Dialog Layout

The Stack Trace Navigator consists of:

- **Header Bar** - "Stack Trace Navigator" title with close button
- **Input Section** - Multi-line text box for pasting stack traces
- **Results Section** - List view showing parsed stack trace entries
- **Status Bar** - Shows parsing status and entry count

## Basic Usage

### Paste and Navigate Workflow

**Typical workflow:**

1. Press `Ctrl+Alt+S` to open Stack Trace Navigator
2. Copy error stack trace from PeopleCode runtime error
3. Paste into input text box (Ctrl+V)
4. Parsed entries appear automatically in results list
5. Click any entry to navigate to that code location
6. Dialog remains open for continued navigation

**Time to first navigation:** ~1 second

### Automatic Parsing

Stack traces are parsed automatically as you type or paste:

- **300ms delay** - Parsing starts 300ms after you stop typing
- **Real-time feedback** - Results update instantly after delay
- **Status updates** - Status bar shows "Parsing..." then entry count
- **Error handling** - Invalid lines are skipped, valid entries shown

### Navigation Behavior

When you select an entry (click or arrow keys):

1. **Target Resolution** - Entry is resolved to an OpenTarget
2. **File Opening** - Application Designer opens the definition
3. **Position Navigation** - Cursor moves to the statement number
4. **Selection** - Statement or relevant code is selected
5. **Dialog Stays Open** - Continue navigating other frames

## Supported Stack Trace Formats

Stack Trace Navigator recognizes standard PeopleCode stack trace formats:

### Format 1: Record.Field.Event

```
RECORD.FIELD.EVENT Name:eventname PCPC:12345 Statement:70
```

**Example:**
```
EMPLOYEE.EMPLID.FieldChange Name:FieldChange PCPC:4027 Statement:45
```

**Parsed Components:**
- Record: EMPLOYEE
- Field: EMPLID
- Event: FieldChange
- Statement: 45

### Format 2: Application Class Method

```
PACKAGE.CLASS.METHOD Name:methodname PCPC:12345 Statement:102
```

**Example:**
```
PT_SECURITY.SecurityManager.ValidateUser Name:ValidateUser PCPC:8192 Statement:102
```

**Parsed Components:**
- Package: PT_SECURITY
- Class: SecurityManager
- Method: ValidateUser
- Statement: 102

### Format 3: Component Event

```
COMPONENT.EVENT PCPC:5432 Statement:15
```

**Example:**
```
EMP_PERSONAL_DATA.Activate PCPC:5432 Statement:15
```

**Parsed Components:**
- Component: EMP_PERSONAL_DATA
- Event: Activate
- Statement: 15

### Format 4: "Called from:" Prefix

```
Called from:RECORD.FIELD.EVENT Name:eventname Statement:715
```

**Example:**
```
Called from:EMPLOYEE.NAME.FieldChange Name:FieldChange Statement:715
```

**Parsed Components:**
- Same as Format 1, with "Called from:" prefix removed

### Format 5: Multi-Level Class Paths

```
PACKAGE.SUBPACKAGE.CLASS.METHOD PCPC:64 Statement:2 (0,0)
```

**Example:**
```
PT_AB.PT_BC.MyClass.MyMethod PCPC:64 Statement:2 (0,0)
```

**Parsed Components:**
- Package hierarchy: PT_AB.PT_BC
- Class: MyClass
- Method: MyMethod
- Statement: 2

## Stack Trace Entry Display

### Results List Columns

| Column | Width | Description |
|--------|-------|-------------|
| **Entry** | 500px | Full display name with statement number |
| **Status** | 55px | "OK" or error indicator |

### Entry Format

Each parsed entry displays as:

```
RECORD.FIELD.EVENT (Statement: 45)
```

or

```
PACKAGE.CLASS.METHOD (Statement: 102)
```

### Status Indicators

- **OK** - Entry successfully parsed and validated
- **Error** - Entry could not be parsed or target not found
- *(empty)* - Entry not yet validated

### Entry Tooltips

Hover over entries to see:
- Raw stack trace line
- Parsed components
- Target path
- Statement number
- Any error messages

## Navigation Features

### Single-Click Navigation

Click any entry to navigate:

1. Application Designer opens the target definition
2. Cursor jumps to statement number (if available)
3. Relevant code is selected
4. Navigation history entry is created

### Keyboard Navigation

Full keyboard control:

| Key | Action |
|-----|--------|
| `Ctrl+Alt+S` | Open Stack Trace Navigator |
| `Up/Down Arrow` | Navigate through entries |
| `Enter` | Navigate to selected entry |
| `Escape` | Close dialog |
| `Tab` | Switch between input and results |

**Efficient keyboard workflow:**
1. `Ctrl+Alt+S` → Opens navigator
2. `Ctrl+V` → Paste stack trace
3. Wait 300ms → Automatic parsing
4. `Tab` → Focus results list
5. `Down/Up` → Select entry
6. `Enter` → Navigate to code

### Statement Number Navigation

When a statement number is available:

- **Exact positioning** - Cursor jumps to exact statement
- **Selection** - Statement is highlighted
- **Context** - Surrounding code is visible

When statement number is not available:

- **Method/Event start** - Cursor jumps to definition start
- **Manual search** - User must locate exact line

## Always-On-Top Behavior

Stack Trace Navigator is designed to stay visible while you navigate:

### Positioning

- **Auto-position to right** - Dialog appears at right edge of Application Designer
- **Vertically centered** - Aligned with center of Application Designer window
- **Avoid code coverage** - Positioned to minimize covering editor
- **Screen boundary aware** - Stays within screen bounds

### Draggable

- **Drag by header** - Click and drag header bar to reposition
- **Persistent positioning** - Position maintained while dialog is open
- **Cursor feedback** - Cursor changes to indicate drag mode

### Always-On-Top

- **Stays visible** - Dialog remains on top of other windows
- **Navigation friendly** - Doesn't interfere with code navigation
- **Auto-close on blur** - Closes if you switch to non-AppRefiner windows

## Error Pattern Detection

Stack Trace Navigator includes enhanced detection for specific error types:

### Supported Error Patterns

1. **Undefined Variable Errors**
   - Detects "Variable &name not defined" errors
   - Highlights exact variable reference in code
   - Provides enhanced selection of the undefined variable

2. **Type Mismatch Errors**
   - Detects type incompatibility errors
   - Highlights problematic expression
   - Shows expected vs actual types

3. **Null Reference Errors**
   - Detects null object access
   - Highlights the null reference
   - Shows the object that was null

### Enhanced Selection

When an error pattern is detected:

- **Precise highlighting** - Exact error location selected
- **Context awareness** - Related code included in selection
- **AST-based** - Uses parsed code structure for accuracy

## Database Requirements

### Required for Full Functionality

Stack Trace Navigator **requires a database connection** for:

- **Target validation** - Verifying definitions exist
- **Path resolution** - Resolving package/class/method paths
- **Opening definitions** - Instructing Application Designer to open files
- **Statement positioning** - Loading program source for navigation

### Without Database

Without a database connection:

- Stack traces can still be parsed
- Entries display in the list
- Navigation will fail (targets cannot be opened)
- Status shows "Error" for all entries

## Parsing Details

### Parsing Algorithm

1. **Split lines** - Stack trace split by newlines
2. **Pattern matching** - Each line tested against regex patterns
3. **Component extraction** - Program path and statement number extracted
4. **Target creation** - OpenTarget constructed from components
5. **Validation** - Target validated against database (if connected)

### Regex Patterns

The parser uses these patterns:

**Program Path:**
```regex
(?:Called from:)?\s*([A-Z_][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)
```

**Statement Number:**
```regex
Statement:(\d+)
```

### Ignored Lines

These line types are ignored during parsing:

- Empty lines
- Lines without program paths
- Header/footer text (e.g., "PeopleCode Error:")
- Timestamp lines
- Session information
- Lines with only whitespace

### Partial Stack Traces

Stack Trace Navigator handles partial traces:

- **Top frame only** - Can parse just the error line
- **Middle frames** - Can parse any subset of frames
- **Bottom frames** - Useful for seeing call origin

## Use Cases and Workflows

### Debugging Runtime Errors

**Scenario:** PeopleCode runtime error in production

**Workflow:**
1. Copy error message including stack trace from log or screen
2. Press `Ctrl+Alt+S` to open Stack Trace Navigator
3. Paste stack trace
4. Review the call stack (bottom to top)
5. Click the error frame (usually top frame)
6. Examine code at exact statement
7. Click caller frames to understand context
8. Fix issue

### Understanding Call Chains

**Scenario:** Error deep in call stack, need to understand how it was reached

**Workflow:**
1. Paste full stack trace
2. Navigate to error location (top frame)
3. Click each caller frame from bottom to top:
   - Bottom frame: Where execution started
   - Middle frames: Intermediate calls
   - Top frame: Where error occurred
4. Understand data flow through call chain
5. Identify root cause

### Comparing Error Scenarios

**Scenario:** Same error occurs in different contexts

**Workflow:**
1. Open Stack Trace Navigator
2. Paste first error's stack trace
3. Navigate and investigate
4. Clear input and paste second error's stack trace
5. Navigate and compare
6. Identify differences in call paths

### Testing Error Handling

**Scenario:** Verify error handling code paths

**Workflow:**
1. Trigger error condition in test environment
2. Copy resulting stack trace
3. Paste into Stack Trace Navigator
4. Navigate to error frame
5. Verify error handling code is reached
6. Check catch blocks and error logic

### Production Support

**Scenario:** Support team receives error from users

**Workflow:**
1. Receive stack trace from support ticket
2. Open Stack Trace Navigator
3. Paste stack trace
4. Navigate to error location
5. Identify issue
6. Provide fix or workaround
7. Update ticket with findings

## Tips and Best Practices

### 1. Copy Full Stack Traces

Always copy the complete stack trace:

```
Include:
- Error message
- All "Called from:" lines
- Statement numbers
- Any additional context
```

**Why:** More frames = better understanding of error context

### 2. Navigate Bottom-Up

Review stack traces from bottom to top:

1. **Bottom frame** - Entry point (often event handler)
2. **Middle frames** - Business logic calls
3. **Top frame** - Actual error location

**Why:** Understanding the call chain helps identify root causes

### 3. Keep Dialog Open

Don't close Stack Trace Navigator between frames:

- Dialog stays open for multi-frame navigation
- Position persists for efficient workflow
- Re-parsing not needed

### 4. Use with Bookmarks

Before navigating deep stack traces:

1. `Ctrl+B` - Place bookmark at current location
2. Navigate through stack frames
3. `Ctrl+-` - Return to original location

### 5. Compare with Better Find

After navigating to error:

1. Use Stack Trace Navigator to find error location
2. Use Better Find (`Ctrl+F`) to search for similar patterns
3. Find other potential error locations

### 6. Document Error Patterns

When investigating recurring errors:

1. Save stack traces for future reference
2. Note common call paths
3. Identify patterns in error locations
4. Update error handling preemptively

## Troubleshooting

### Dialog Doesn't Open

**Problem:** Pressing `Ctrl+Alt+S` does nothing

**Solutions:**
- Verify AppRefiner is enabled for Application Designer process
- Check keyboard focus is in Application Designer
- Try Command Palette: "Stack Trace Navigator"
- Restart Application Designer

### No Entries Parsed

**Problem:** Stack trace pasted but results list is empty

**Solutions:**
- Verify stack trace contains program paths (RECORD.FIELD.EVENT format)
- Check for "Statement:" keywords in stack trace
- Try pasting just one line to test parsing
- Remove extra whitespace or formatting
- Ensure text is actual PeopleCode stack trace (not Java/C# traces)

### Navigation Fails

**Problem:** Clicking entry doesn't open definition

**Solutions:**
- Verify database connection is active
- Check that definition exists in database
- Ensure target path is correct (check for typos)
- Try manually opening definition to verify it exists
- Review status column for error messages

### Statement Number Not Found

**Problem:** Navigates to definition but wrong location

**Solutions:**
- Verify statement number in stack trace matches code
- Check if code has been modified since error
- Look for statement number markers in code (/* Statement: n */)
- Navigate manually if statement markers are missing
- Code may have changed since error occurred

### Dialog Covers Code

**Problem:** Dialog blocks view of code being examined

**Solutions:**
- Drag dialog by header bar to reposition
- Move dialog to second monitor if available
- Resize Application Designer window to be wider
- Close dialog after noting line numbers

### Parsing Takes Too Long

**Problem:** Delay between pasting and parsing is too long

**Solutions:**
- Wait for 300ms debounce delay (working as designed)
- Paste shorter stack traces for faster parsing
- Check database connection speed
- Very long traces (1000+ lines) may take several seconds

## Performance Considerations

### Parsing Performance

- **Fast patterns** - Regex patterns optimized for speed
- **Debounced** - 300ms delay prevents excessive parsing
- **Incremental** - Processes line by line
- **Lightweight** - Minimal memory usage

### Navigation Performance

- **Database queries** - Each navigation requires database lookup
- **Cached** - Parsed entries cached for re-navigation
- **Background** - Processing happens asynchronously
- **Responsive** - UI remains responsive during processing

### Large Stack Traces

For very large stack traces (500+ frames):

- **Parsing time** - May take 1-2 seconds
- **Display time** - List view renders incrementally
- **Memory** - Each entry uses minimal memory
- **Navigation** - No performance impact on navigation

## Integration with Other Features

### Works With Navigation History

Each navigation creates a history entry:

1. Navigate to stack frame
2. Navigation history entry created
3. `Alt+Left` returns to previous location
4. `Alt+Right` moves forward again

See [Navigation](navigation.md) for details.

### Works With Bookmarks

Before deep stack trace investigation:

1. `Ctrl+B` - Place bookmark
2. Navigate through stack frames
3. `Ctrl+-` - Return to bookmark

See [Navigation](navigation.md) for bookmark details.

### Complements Better Find

After finding error location:

1. Use Stack Trace Navigator to locate error
2. Use Better Find to search for related issues
3. Find similar code patterns that might have same bug

See [Better Find](better-find-replace.md) for details.

## Comparison to Manual Navigation

| Aspect | Manual Navigation | Stack Trace Navigator |
|--------|-------------------|----------------------|
| **Open Definition** | Type name in Open dialog | Single click |
| **Find Statement** | Scroll or Ctrl+G | Automatic |
| **Multiple Frames** | Repeat for each frame | Click each entry |
| **Time per Frame** | 30-60 seconds | 2-3 seconds |
| **Error Prone** | Typing errors common | Automatic parsing |
| **Call Stack** | Must track mentally | Visible in list |

**Time Savings:** ~90% reduction in navigation time for multi-frame stack traces

## Related Features

- **[Navigation](navigation.md)** - Go To Definition, Navigation History, Bookmarks
- **[Better Find](better-find-replace.md)** - Search for error patterns
- **[Smart Open](smart-open.md)** - Open definitions manually
- **[Code Styling](code-styling.md)** - Visual indicators for errors
- **[Type Checking](type-checking.md)** - Find type-related errors

## Next Steps

- Learn [Navigation features](navigation.md) for returning to previous locations
- Try [Better Find](better-find-replace.md) for finding similar error patterns
- Master [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for efficiency
- Explore [Database Integration](../user-guide/database-integration.md) for setup
