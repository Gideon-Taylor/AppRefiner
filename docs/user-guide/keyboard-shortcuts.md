# Keyboard Shortcuts

AppRefiner provides numerous keyboard shortcuts to enhance your productivity while working in PeopleSoft Application Designer. This guide lists all available shortcuts organized by category.

## Command Palette

The Command Palette is the central access point for AppRefiner's functionality:

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+P` | Open Command Palette |

The Command Palette works similarly to VS Code, providing quick access to all AppRefiner features directly within Application Designer.

## File and Object Operations

| Shortcut | Action |
|----------|--------|
| `Ctrl+O` | Smart Open - Fuzzy search for PeopleSoft objects across all types |

## Search and Navigation

### Search

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Better Find - Open enhanced search dialog with regex support |
| `Ctrl+H` | Better Find Replace - Open enhanced find and replace dialog |
| `F3` | Find Next - Find next occurrence using current search term |
| `Shift+F3` | Find Previous - Find previous occurrence using current search term |

### Code Navigation

| Shortcut | Action |
|----------|--------|
| `F12` | Go To Definition - Jump to definition of symbol at cursor |
| `Alt+Left` | Navigate Backward - Go back in navigation history |
| `Alt+Right` | Navigate Forward - Go forward in navigation history |
| `Ctrl+Shift+O` | Outline - Open outline dialog for quick navigation within current file |

### Bookmarks

| Shortcut | Action |
|----------|--------|
| `Ctrl+B` | Place Bookmark - Mark current cursor position |
| `Ctrl+-` (minus) | Go to Previous Bookmark - Jump to and remove the most recent bookmark |

## Code Folding

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+[` | Collapse Current Level - Collapse the fold at cursor position |
| `Ctrl+Shift+]` | Expand Current Level - Expand the fold at cursor position |
| `Ctrl+Shift+Alt+[` | Collapse All - Collapse all foldable blocks in the file |
| `Ctrl+Shift+Alt+]` | Expand All - Expand all folded blocks in the file |

## Code Quality and Analysis

### Linting

| Shortcut | Action |
|----------|--------|
| `Alt+L` | Toggle Linter Dialog - Run active linters and show results as inline annotations |

### Code Styling and Quick Fixes

| Shortcut | Action |
|----------|--------|
| `Ctrl+.` (period) | Apply Quick Fix - Automatically fix styler issue at cursor position |

### Type Checking

| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+E` | Generate Type Error Report - Create comprehensive type checking report |

## Code Refactoring

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+R` | Rename - Rename variables, parameters, and private methods |
| `Ctrl+Shift+I` | Resolve Imports - Scan for App Classes and ensure they are imported |
| `Ctrl+Alt+T` | Apply Template - Insert code template at cursor position |

## Debugging and Diagnostics

| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+S` | Stack Trace Navigator - Parse and navigate PeopleCode stack traces |

## Quick Reference by Category

### Most Frequently Used
- `Ctrl+Shift+P` - Command Palette
- `Ctrl+F` - Better Find
- `F12` - Go To Definition
- `Alt+Left/Right` - Navigate Back/Forward
- `Ctrl+.` - Quick Fix
- `Ctrl+Shift+I` - Resolve Imports

### File Operations
- `Ctrl+O` - Smart Open

### Search
- `Ctrl+F` / `Ctrl+H` - Find / Replace
- `F3` / `Shift+F3` - Find Next / Previous

### Navigation
- `F12` - Go To Definition
- `Alt+Left/Right` - History Navigation
- `Ctrl+Shift+O` - Outline
- `Ctrl+B` / `Ctrl+-` - Bookmarks

### Code Folding
- `Ctrl+Shift+[/]` - Collapse/Expand
- `Ctrl+Shift+Alt+[/]` - Collapse/Expand All

### Code Quality
- `Alt+L` - Run Linters
- `Ctrl+.` - Quick Fix
- `Ctrl+Alt+E` - Type Errors

### Refactoring
- `Ctrl+Shift+R` - Rename
- `Ctrl+Shift+I` - Resolve Imports
- `Ctrl+Alt+T` - Templates

## Customizing Shortcuts

Currently, AppRefiner does not support customizing built-in keyboard shortcuts through the user interface. However, custom plugins (linters, stylers, and refactors) can register their own keyboard shortcuts programmatically.

## Shortcut Conflicts

Some shortcuts may conflict with Application Designer or Windows shortcuts:
- If a shortcut doesn't work, check if it's being intercepted by Application Designer or Windows
- Some shortcuts only work when an AppRefiner-enhanced editor has focus
- Database-dependent features (Smart Open, Go To Definition) require an active database connection

## Next Steps

- Learn about [Working with Linters](working-with-linters.md) to improve code quality
- Explore [Quick Fixes](../features/quick-fixes.md) for automatic code corrections
- See [Using Refactoring Tools](using-refactoring-tools.md) for manual refactoring operations
