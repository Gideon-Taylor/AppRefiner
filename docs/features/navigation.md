# Code Navigation

AppRefiner provides powerful code navigation features that make it easy to explore codebases, jump between definitions, and track your navigation path. These features include Go To Definition, Navigation History, and Bookmarks—all designed to work together for efficient code exploration.

## Overview

AppRefiner's navigation system includes:

- **Go To Definition (F12)** - Jump directly to the definition of classes, methods, functions, properties, and variables
- **Navigation History (Alt+Left/Alt+Right)** - Navigate backward and forward through your code exploration path
- **Bookmarks (Ctrl+B / Ctrl+-)** - Mark code locations and return to them with a stack-based system

These features work together seamlessly: Go To Definition automatically creates history entries, allowing you to explore code and easily return to your starting point.

## Go To Definition

### What is Go To Definition?

Go To Definition allows you to quickly jump to where a symbol (class, method, function, variable, or property) is defined. This is essential for understanding code flow, exploring APIs, and navigating large codebases.

### Opening Definitions

Place your cursor on any symbol and press `F12` to jump to its definition.

**Keyboard Shortcut:** `F12`

**Supported Symbols:**
- Application Class names
- Method names (both declarations and implementations)
- Function names (both calls and declarations)
- Variable names
- Property names
- External function declarations

### How It Works

#### Within the Current File

When jumping to a definition in the same file, AppRefiner:

1. Locates the symbol's definition in the parsed AST
2. Navigates to that location
3. Selects the symbol name
4. Adds the previous location to navigation history

#### Cross-File Navigation

When jumping to a definition in another file (requires database connection), AppRefiner:

1. Queries the database for the symbol's location
2. Opens the target file in Application Designer
3. Navigates to the definition
4. Adds the previous location to navigation history

### Examples

#### Jumping to a Method Declaration

```peoplecode
method ProcessEmployee(&emplId as string);
   /* Cursor on ProcessEmployee at implementation */
   /* Press F12 → Jumps to declaration in class header */
End-method;
```

#### Jumping to a Method Implementation

```peoplecode
class EmployeeProcessor
   method ProcessEmployee(&emplId as string);  /* Cursor here */
   /* Press F12 → Jumps to implementation below */
```

#### Jumping to a Function Definition

```peoplecode
&result = CalculateTax(&amount);  /* Cursor on CalculateTax */
/* Press F12 → Jumps to function definition */

Function CalculateTax(&amount as number) Returns number
   /* Definition found here */
End-Function;
```

#### Jumping to a Variable Declaration

```peoplecode
&empRecord.GetField(&FIELD.EMPLID);  /* Cursor on &empRecord */
/* Press F12 → Jumps to variable declaration */

Local Record &empRecord;  /* Declaration found here */
```

#### Jumping to an External Function

```peoplecode
Function MyExternalFunction Declare Function RECORD.FIELD.EVENT;
/* Press F12 on function call → Opens RECORD.FIELD.EVENT and jumps to function */
```

### Navigation History Integration

Every time you use Go To Definition, AppRefiner:

1. **Records your starting position** in navigation history before jumping
2. **Jumps to the definition**
3. **Allows you to return** using `Alt+Left`

This creates a navigation trail you can follow backward and forward.

### Database Requirements

**Local Definitions (No Database Required):**
- Methods within the same class
- Functions within the same file
- Variables in the current scope
- Properties in the current class

**Cross-File Definitions (Database Required):**
- External function declarations pointing to other Record.Field.Event code
- Application Class references in other packages
- Methods inherited from base classes

### Error Messages

If Go To Definition cannot find a definition, you'll see one of these messages:

- **"Unable to find target program in the database"** - Database connection required or definition doesn't exist
- **"Definition not found"** - Symbol not recognized or ambiguous
- **No message** - If nothing happens, the symbol may not be supported for navigation

## Navigation History

### What is Navigation History?

Navigation History tracks your movement through code, allowing you to navigate backward and forward through locations you've visited—similar to back/forward buttons in a web browser.

### Using Navigation History

**Navigate Backward:** `Alt+Left` (Alt + Left Arrow)
**Navigate Forward:** `Alt+Right` (Alt + Right Arrow)

### How History is Captured

Navigation history entries are automatically created when you:

1. **Use Go To Definition (F12)** - Records your position before jumping
2. **Navigate to a different location after idle time** - Records current position when you've been editing/viewing and then jump elsewhere

### History Entry Contents

Each history entry stores:

- **File/Program** - Which file or PeopleCode program you were in
- **Cursor Position** - Exact line and column
- **Scroll Position** - First visible line for viewport restoration
- **Editor Handle** - Reference to validate the editor still exists

### Navigation Behavior

#### Navigating Backward (Alt+Left)

Moves to the previous location in history. AppRefiner:

1. Retrieves the previous history entry
2. Opens the file if not already open
3. Restores cursor position
4. Restores scroll position
5. Moves the history index backward

#### Navigating Forward (Alt+Right)

Moves to the next location in history (only available after navigating backward). AppRefiner:

1. Retrieves the next history entry
2. Opens the file if not already open
3. Restores cursor position and scroll position
4. Moves the history index forward

### History Capacity

Navigation history has no explicit limit but is managed per Application Designer process. History is cleared when:

- Application Designer is closed
- AppRefiner is disabled
- The process is terminated

### Typical Navigation Workflow

**Scenario:** You're in Method A, jump to Method B's definition, then to Method C, and want to return to Method A.

```
1. Start in Method A
2. Press F12 on Method B call → Jump to Method B definition (history entry created)
3. Press F12 on Method C call → Jump to Method C definition (history entry created)
4. Press Alt+Left → Return to Method B
5. Press Alt+Left → Return to Method A
6. Press Alt+Right → Return to Method B
7. Press Alt+Right → Return to Method C
```

### Integration with Other Features

Navigation History integrates with:

- **Go To Definition (F12)** - Automatically creates history entries
- **Smart Open (Ctrl+O)** - Opening files may create history entries
- **Better Find (Ctrl+F)** - Finding and navigating to matches can create entries

## Bookmarks

### What are Bookmarks?

Bookmarks provide a way to mark specific code locations and return to them later. Unlike navigation history (which tracks everywhere you've been), bookmarks are explicitly placed and follow a stack-based LIFO (Last-In-First-Out) model.

### Placing Bookmarks

**Keyboard Shortcut:** `Ctrl+B`

To place a bookmark:

1. Position your cursor on the line you want to mark
2. Press `Ctrl+B`
3. A gold highlight appears on the entire line
4. The bookmark is added to the top of the bookmark stack

### Visual Indicators

Bookmarks are displayed with a distinctive gold highlight covering the entire line. This makes bookmarked lines easy to identify when scrolling through code.

**Bookmark Color:** Gold/Yellow highlight

### Returning to Bookmarks

**Keyboard Shortcut:** `Ctrl+-` (Ctrl + Minus)

To return to the most recent bookmark:

1. Press `Ctrl+-`
2. AppRefiner navigates to the most recent bookmark
3. The cursor position is restored
4. The scroll position is restored
5. The bookmark highlight is removed
6. The bookmark is removed from the stack

### Stack-Based Behavior

Bookmarks follow a stack (LIFO) pattern:

- **Placing bookmarks** pushes them onto the stack
- **Returning to bookmarks** pops them off the stack
- You always return to the most recently placed bookmark

**Example Stack Workflow:**

```
1. Place bookmark at line 10 → Stack: [Line 10]
2. Place bookmark at line 50 → Stack: [Line 10, Line 50]
3. Place bookmark at line 100 → Stack: [Line 10, Line 50, Line 100]
4. Press Ctrl+- → Return to line 100 → Stack: [Line 10, Line 50]
5. Press Ctrl+- → Return to line 50 → Stack: [Line 10]
6. Press Ctrl+- → Return to line 10 → Stack: []
```

### Per-Editor Bookmarks

Bookmarks are stored per-editor. This means:

- Each open file has its own bookmark stack
- Switching between files preserves each file's bookmarks
- Bookmarks are cleared when the editor is closed

### Bookmark Persistence

**Current Session:** Bookmarks persist as long as the editor is open
**Across Sessions:** Bookmarks are NOT saved when you close Application Designer

To preserve bookmark locations permanently, consider using code comments or the TODO system.

### Clearing Bookmarks

Bookmarks can be cleared by:

1. **Returning to them** - Uses `Ctrl+-` repeatedly until all are gone
2. **Closing the editor** - Clears all bookmarks for that file
3. **Programmatically** - Using "Editor: Clear Annotations" command (clears all indicators)

### Use Cases for Bookmarks

#### Temporary Breadcrumbs

Mark locations you want to return to while exploring unfamiliar code:

```
1. Place bookmark at the method you're investigating
2. Explore related methods and functions
3. Press Ctrl+- to return to your starting point
```

#### Multi-Location Editing

Mark several locations you need to edit:

```
1. Place bookmarks at lines needing changes
2. Work on the first area
3. Press Ctrl+- to jump to the next bookmarked location
4. Continue until all bookmarks are addressed
```

#### Code Review Trail

Mark locations with issues during code review:

```
1. While reviewing, place bookmarks at problematic lines
2. Continue reviewing the entire file
3. Return to each bookmarked issue using Ctrl+-
```

## Navigation Features Comparison

| Feature | Automatic | Manual | Persistent | Direction | Use Case |
|---------|-----------|---------|------------|-----------|----------|
| **Go To Definition** | ✅ | ✅ | No | One-way (to definition) | Jump to symbol definitions |
| **Navigation History** | ✅ | No | Session | Bidirectional | Explore and return |
| **Bookmarks** | No | ✅ | Session | LIFO Stack | Mark locations for later |

### When to Use Each Feature

**Use Go To Definition (F12) when:**
- You want to see where a symbol is defined
- You're exploring API structure
- You need to understand how a method is implemented

**Use Navigation History (Alt+Left/Right) when:**
- You've jumped several levels deep and want to return
- You want to retrace your exploration path
- You need to move back and forth between related code

**Use Bookmarks (Ctrl+B) when:**
- You want to explicitly mark locations for later
- You're doing multi-location editing
- You need temporary breadcrumbs while working

### Combining All Three Features

The most powerful workflows combine all three:

**Example: Complex Refactoring**

```
1. Place bookmark at method you're refactoring (Ctrl+B)
2. Press F12 on method calls to understand usage (creates history)
3. Navigate through several call sites (F12 repeatedly)
4. Use Alt+Left to return through your exploration
5. Press Ctrl+- to return to your original bookmark
6. Begin refactoring with full context
```

## Keyboard Shortcuts Reference

| Shortcut | Action |
|----------|--------|
| `F12` | Go To Definition |
| `Alt+Left` | Navigate Backward in History |
| `Alt+Right` | Navigate Forward in History |
| `Ctrl+B` | Place Bookmark |
| `Ctrl+-` | Go to Previous Bookmark (and remove it) |

## Troubleshooting

### Go To Definition Not Working

**Problem:** Pressing F12 does nothing

**Solutions:**
- Ensure cursor is on a supported symbol (not whitespace or comments)
- Check database connection for cross-file navigation
- Verify the symbol has a definition (not built-in functions)
- Try selecting the entire symbol name before pressing F12

### Navigation History Empty

**Problem:** Alt+Left doesn't navigate anywhere

**Solutions:**
- Use Go To Definition (F12) first to create history entries
- Ensure you've navigated to at least two different locations
- Check that navigation history wasn't cleared by closing Application Designer

### Bookmarks Not Visible

**Problem:** Gold highlights don't appear after pressing Ctrl+B

**Solutions:**
- Verify the cursor is in an editor window
- Check that the editor is not in read-only mode
- Try using "Editor: Clear Annotations" to reset indicators
- Verify AppRefiner is enabled for the current process

### Can't Return to Bookmark

**Problem:** Ctrl+- doesn't navigate anywhere

**Solutions:**
- Ensure you've placed at least one bookmark using Ctrl+B
- Check that you haven't already returned to all bookmarks (stack is empty)
- Verify the editor where bookmark was placed is still open

### Definition Opens Wrong Location

**Problem:** F12 opens an unexpected file or location

**Solutions:**
- Ensure database connection is active and up-to-date
- Check for name conflicts (multiple symbols with same name)
- Verify you're on the correct symbol (not similar names)
- Try updating database cache if using cached definitions

## Tips and Best Practices

### 1. Use Navigation History for Deep Exploration

When diving deep into code:

1. Use F12 liberally to explore
2. Don't worry about getting lost
3. Use Alt+Left to retrace your steps
4. Alt+Right if you went back too far

### 2. Place Bookmarks Before Major Navigation

Before a complex exploration session:

1. Place a bookmark at your starting point
2. Explore freely using F12 and navigation
3. Press Ctrl+- to return instantly to your starting point

### 3. Combine with Better Find

Find all usages of a method, then:

1. Use F12 to jump to the definition
2. Use Alt+Left to return to your search results
3. Continue finding other usages

### 4. Create Bookmark Chains for Multi-Step Tasks

For tasks requiring multiple locations:

1. Place bookmarks in reverse order of work
2. Press Ctrl+- to work through them sequentially
3. Each location is automatically unmarked after visiting

### 5. Use Bookmarks for Code Review

While reviewing code:

1. Place bookmarks at issues found
2. Complete the review
3. Use Ctrl+- to revisit each issue
4. Address issues in LIFO order

## Database Requirements

### Features Requiring Database Connection

- **Cross-file Go To Definition** - Opening definitions in other PeopleCode programs
- **Application Class navigation** - Jumping to class definitions
- **External function navigation** - Following function declarations

### Features Working Offline

- **Local Go To Definition** - Within the same file
- **Navigation History** - All history tracking
- **Bookmarks** - All bookmark functionality

## Performance Considerations

### Navigation History Performance

- History entries are lightweight (just position data)
- No significant performance impact
- History size is reasonable for normal usage patterns

### Bookmark Performance

- Bookmarks use visual indicators in Scintilla
- Each bookmark adds one indicator
- Performance impact is negligible for typical usage (< 50 bookmarks)

### Go To Definition Performance

- **Local definitions**: Instant (AST-based)
- **Database queries**: Depends on connection speed and database load
- **First query may be slower**: Subsequent queries use caching

## Related Features

- **[Better Find and Replace](better-find-replace.md)** - Advanced search for finding symbol usages
- **[Outline Dialog](outline.md)** - Quick navigation within the current file
- **[Smart Open](smart-open.md)** - Quickly open files by name
- **[Code Styling](code-styling.md)** - Visual indicators for code issues
- **[Type Checking](type-checking.md)** - Type information for symbols

## Next Steps

- Try [Outline Dialog](outline.md) for quick within-file navigation
- Learn about [Smart Open](smart-open.md) for fast file switching
- Explore [Better Find](better-find-replace.md) for finding symbol references
- Read [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for all available shortcuts
