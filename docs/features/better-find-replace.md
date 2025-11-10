# Better Find and Replace

AppRefiner's Better Find and Replace feature provides advanced search and replacement capabilities that significantly enhance the basic Find/Replace functionality in PeopleSoft Application Designer. With support for regular expressions, capture group replacements, visual highlighting, and comprehensive search options, Better Find makes code navigation and editing more efficient.

## Overview

Better Find/Replace offers:

- **Regular Expression Support** - Use powerful regex patterns for complex searches
- **Capture Group Replacement** - Replace text using regex capture groups ($1, $2, etc.)
- **Visual Highlighting** - All matches highlighted in the editor for easy identification
- **Search History** - Maintains history of recent searches and replacements
- **Per-Editor State** - Each editor remembers its own search settings and history
- **Multiple Search Scopes** - Search within selection, whole document, or current method
- **Results List** - View all matches with line numbers and context
- **Flexible Options** - Case sensitivity, whole word, word start, regex, and wrap-around

## Opening Better Find

There are three ways to open Better Find:

1. **Keyboard Shortcut (Find)**: Press `Ctrl+F`
2. **Keyboard Shortcut (Replace)**: Press `Ctrl+H` to open with replace mode enabled
3. **Command Palette**: Search for "Better Find" or "Better Find Replace"

The dialog opens as an always-on-top window, allowing you to search while keeping your code visible.

## Dialog Layout

The Better Find dialog consists of several sections:

- **Find Field** - Enter your search term or regex pattern
- **Replace Field** - Enter replacement text (visible when replace mode enabled)
- **Options Section** - Checkboxes for search options
- **Action Buttons** - Find, Replace, Mark, and Count operations
- **Results Panel** - Displays all matches when using "Find All" (collapsible)
- **Status Bar** - Shows search status and messages

## Basic Search Operations

### Finding Text

**To perform a simple find:**

1. Press `Ctrl+F` to open Better Find
2. Type your search term in the Find field
3. Press `Enter` or click **Find Next** to find the next occurrence
4. Press `Shift+Enter` or click **Find Previous** to find the previous occurrence

**Keyboard Shortcuts:**
- `F3` - Find next occurrence (works even when dialog is closed)
- `Shift+F3` - Find previous occurrence (works even when dialog is closed)
- `Enter` - Find next (when dialog is open)
- `Shift+Enter` - Find previous (when dialog is open)
- `Escape` - Close the dialog

### Search Wrapping

When **Wrap around** is enabled (default), searches continue from the beginning when reaching the end of the document, and vice versa for backward searches. When disabled, searches stop at document boundaries.

### Visual Highlighting

All matches in the current document are automatically highlighted with a distinctive indicator, making it easy to see the distribution of search results throughout your code. Highlights are cleared when:

- The dialog is closed
- A new search is performed
- You manually clear annotations

## Search Options

Better Find provides several options to refine your search:

### Match Case

When enabled, searches are case-sensitive.

**Example:**
- Search for `Employee` with Match Case enabled
- Matches: `Employee`, `EmployeeData`
- Does not match: `employee`, `EMPLOYEE`

### Whole Word

When enabled, only matches complete words, not partial matches within other words.

**Example:**
- Search for `Field` with Whole Word enabled
- Matches: `Field`, `Field.Value`
- Does not match: `GetField`, `FieldName`, `TextField`

### Word Start

When enabled, matches words that start with the search term.

**Example:**
- Search for `Get` with Word Start enabled
- Matches: `GetRecord`, `GetField`, `GetValue`
- Does not match: `TargetRecord`, `FieldGetter`

### Use Regex

Enables regular expression pattern matching using standard regex syntax.

**Example Patterns:**
```
\d+                 Matches one or more digits
\w+@\w+\.\w+        Matches email patterns
(Get|Set)\w+        Matches GetXXX or SetXXX methods
```

See [Regular Expression Patterns](#regular-expression-patterns) for detailed regex examples.

### Wrap Around

When enabled, searches continue from the beginning when reaching the end (or vice versa for backward search). When disabled, searches stop at document boundaries and show "No more matches" message.

## Search Scope

Better Find allows you to limit your search to specific regions of code:

### Selection

Searches only within the currently selected text. Useful for refactoring within a specific method or block.

**Usage:**
1. Select the text region you want to search
2. Open Better Find
3. Choose **Selection** radio button
4. Perform your search

### Whole Document

Searches the entire document from beginning to end. This is the default scope.

### Method

Searches only within the current method, function, property getter, or property setter based on cursor position.

**Usage:**
1. Place cursor anywhere inside a method
2. Open Better Find
3. Choose **Method** radio button
4. Search is limited to that method's scope

## Replace Operations

### Enabling Replace Mode

Replace mode can be enabled in two ways:

1. **Open with Replace**: Press `Ctrl+H`
2. **Toggle in Dialog**: Check the **Replace** checkbox in the top-right

When enabled, the Replace field and replace buttons become visible.

### Replace Single Occurrence

**To replace one match at a time:**

1. Find the occurrence you want to replace (using Find Next/Previous)
2. The matched text will be selected
3. Click **Replace** button
4. The selection is replaced and the next occurrence is automatically selected

### Replace All

**To replace all occurrences:**

1. Enter your search term and replacement text
2. Select your desired scope (Selection, Whole Document, or Method)
3. Click **Replace All**
4. Status bar shows the number of replacements made

**Warning:** Replace All is immediate and cannot be undone except through Ctrl+Z. Consider using "Find All" first to preview all matches.

## Regular Expression Patterns

Better Find uses standard .NET regular expression syntax. Here are common patterns:

### Basic Patterns

| Pattern | Description | Example Match |
|---------|-------------|---------------|
| `.` | Any single character | `a.c` matches "abc", "a1c" |
| `\d` | Any digit | `\d+` matches "123" |
| `\w` | Any word character | `\w+` matches "Employee" |
| `\s` | Any whitespace | `\s+` matches spaces/tabs |
| `^` | Start of line | `^Local` matches lines starting with "Local" |
| `$` | End of line | `;$` matches lines ending with semicolon |

### Quantifiers

| Pattern | Description | Example |
|---------|-------------|---------|
| `*` | Zero or more | `ab*` matches "a", "ab", "abb" |
| `+` | One or more | `\d+` matches "123" but not "" |
| `?` | Zero or one | `colou?r` matches "color" and "colour" |
| `{n}` | Exactly n | `\d{4}` matches "2024" |
| `{n,}` | n or more | `\d{3,}` matches "123", "1234" |
| `{n,m}` | Between n and m | `\w{3,5}` matches 3-5 letter words |

### Character Classes

| Pattern | Description | Example |
|---------|-------------|---------|
| `[abc]` | Any of a, b, or c | `[aeiou]` matches vowels |
| `[^abc]` | Not a, b, or c | `[^0-9]` matches non-digits |
| `[a-z]` | Range | `[A-Z]` matches uppercase letters |
| `[a-zA-Z0-9]` | Multiple ranges | Alphanumeric characters |

### Groups and Alternation

| Pattern | Description | Example |
|---------|-------------|---------|
| `(abc)` | Capture group | `(Get\|Set)(\w+)` captures prefix and name |
| `(?:abc)` | Non-capturing group | `(?:Mr\|Ms)\.` matches titles without capturing |
| `a\|b` | Alternation (OR) | `Get\|Set` matches either "Get" or "Set" |

### Practical Examples

**Find variable declarations:**
```regex
Local\s+\w+\s+&\w+
```
Matches: `Local String &name`, `Local Record &rec`

**Find method calls:**
```regex
\.\s*\w+\s*\(
```
Matches: `.GetField(`, `.SelectByKey(`

**Find string literals:**
```regex
"[^"]*"
```
Matches: `"Hello"`, `"Employee Name"`

**Find comments:**
```regex
/\*[\s\S]*?\*/
```
Matches: `/* comment */`, `/* multi\nline */`

**Find variable references:**
```regex
&\w+
```
Matches: `&empRecord`, `&count`, `&isValid`

## Replacement with Capture Groups

One of the most powerful features is using regex capture groups in replacements:

### Capture Group Syntax

Capture groups are defined with parentheses `()` in the search pattern and referenced with `$1`, `$2`, etc. in the replacement text.

### Example 1: Swap Variable Names

**Find:**
```regex
Local (\w+) &(\w+)
```

**Replace:**
```
Local $1 &new$2
```

**Result:**
```peoplecode
Local String &name    →    Local String &newName
Local Number &count   →    Local Number &newCount
```

### Example 2: Convert Method Naming

**Find:**
```regex
get(\w+)\(
```

**Replace:**
```
Get$1(
```

**Result:**
```peoplecode
getField(       →    GetField(
getRecord(      →    GetRecord(
getValue(       →    GetValue(
```

### Example 3: Add Prefixes to Variables

**Find:**
```regex
&(\w+)
```

**Replace:**
```
&my$1
```

**Result:**
```peoplecode
&record    →    &myRecord
&field     →    &myField
```

### Example 4: Reformat String Concatenation

**Find:**
```regex
"(\w+)" \| "(\w+)"
```

**Replace:**
```
"$1 $2"
```

**Result:**
```peoplecode
"Hello" | "World"    →    "Hello World"
"First" | "Last"     →    "First Last"
```

## Advanced Features

### Count Matches

Click the **Count** button to display the total number of matches without navigating to them. The count appears in the status bar.

**Use cases:**
- Check how many times a variable is used
- Verify the number of method calls before refactoring
- Assess the scope of a replacement operation

### Mark All

Click **Mark All** to highlight all matches in the document without navigating. All matches remain highlighted until you close the dialog or perform a new search.

**Use cases:**
- Visualize the distribution of a pattern in your code
- Identify all occurrences before making changes
- Review all usages of a variable or method

### Find All

Click **Find All** to display a complete list of all matches with line numbers and context in the results panel.

**Features:**
- Shows line number and preview text for each match
- Click any result to jump to that location in the editor
- Results panel can be resized by dragging the splitter
- Toggle results visibility with the "Show Results" checkbox

**Use cases:**
- Review all matches before performing Replace All
- Navigate through complex search results efficiently
- Export or document all occurrences of a pattern

## Search History

Better Find maintains separate search and replace history for each editor.

**Features:**
- Drop-down arrows on Find and Replace fields show recent searches
- History persists as long as the editor is open
- Select from history to quickly repeat previous searches
- History is per-editor (each file remembers its own search terms)

**Navigation:**
- Click the drop-down arrow or press `Down` key in the combo box
- Use `Up`/`Down` arrows to browse history
- Press `Enter` to select

## Per-Editor State

Each editor maintains its own search state, including:

- Last search term
- Last replace term
- Search options (case, whole word, regex, etc.)
- Search history
- Replace history

This means switching between files preserves each file's search context.

## Integration with Code Editing

### Global Find Commands

Even when the Better Find dialog is closed, you can use global find commands:

- **F3** - Find Next: Repeats the last search forward
- **Shift+F3** - Find Previous: Repeats the last search backward

These commands use the search state from the last Better Find session in the current editor.

### Search State Persistence

Search state persists until:
- The editor is closed
- Application Designer is closed
- A new search is performed

This allows you to close the dialog and continue using F3/Shift+F3 for navigation.

## Configuration

Better Find behavior can be configured in AppRefiner Settings:

### Override Find/Replace

**Setting:** "Override Find/Replace" checkbox in Settings > General

When enabled, Better Find automatically replaces Application Designer's default Find (Ctrl+F) and Replace (Ctrl+H) commands. When disabled, you must invoke Better Find through the command palette or assign a custom shortcut.

**Default:** Enabled

## Tips and Tricks

### 1. Preview Before Replace All

Always use "Find All" before "Replace All" to review what will be changed:

1. Perform your search
2. Click "Find All" to see the results list
3. Review all matches
4. If satisfied, click "Replace All"

### 2. Use Method Scope for Refactoring

When refactoring a single method:

1. Place cursor inside the method
2. Select "Method" scope
3. Perform search/replace operations
4. Changes are limited to that method only

### 3. Combine Selection and Regex

For surgical edits:

1. Select the specific code block
2. Choose "Selection" scope
3. Use regex patterns for precise matching
4. Replace with capture groups for transformations

### 4. Leverage Word Start for Code Navigation

When searching for method calls or properties:

1. Enable "Word Start"
2. Search for common prefixes like "Get", "Set", "Create"
3. Quickly navigate through all matching methods

### 5. Use Visual Highlighting for Code Review

Enable "Mark All" to visually identify patterns:

- Find all SQL statements
- Locate all error handling blocks
- Identify all variable references

## Common Use Cases

### Rename a Variable

**Scenario:** Rename `&rec` to `&empRecord` throughout a method

**Steps:**
1. Place cursor in the method
2. Press `Ctrl+F`
3. Search for `&rec`
4. Enable "Whole Word" to avoid matching `&record`
5. Select "Method" scope
6. Replace with `&empRecord`
7. Click "Replace All"

### Find All SQL Statements

**Scenario:** Locate all SQL execution calls

**Steps:**
1. Press `Ctrl+F`
2. Enable "Use Regex"
3. Search pattern: `SQLExec\s*\(`
4. Click "Find All"
5. Review results list

### Convert String Concatenation

**Scenario:** Replace `|` concatenation with `String.join()`

**Steps:**
1. Press `Ctrl+H`
2. Enable "Use Regex"
3. Find: `"([^"]+)"\s*\|\s*"([^"]+)"`
4. Replace: `String.join({"$1", "$2"})`
5. Click "Find All" to preview
6. Click "Replace All"

### Find Uncommented Code Blocks

**Scenario:** Find methods without documentation comments

**Steps:**
1. Enable "Use Regex"
2. Search pattern: `^method\s+\w+`
3. Use "Find All" to review all methods
4. Manually check which lack comments

## Troubleshooting

### Dialog Doesn't Appear

**Problem:** Pressing Ctrl+F does nothing

**Solutions:**
- Check if "Override Find/Replace" is enabled in Settings
- Verify keyboard focus is in the editor window
- Try using Command Palette instead: Search for "Better Find"
- Check if another application is intercepting the shortcut

### Regex Pattern Not Working

**Problem:** Regex search returns no results

**Solutions:**
- Verify "Use Regex" checkbox is enabled
- Test your pattern in a regex tester (regex101.com)
- Remember to escape special characters: `\.`, `\(`, `\)`
- Check for proper escape sequences: `\d` not `d`, `\s` not `s`

### Replace All Not Working

**Problem:** Replace All button doesn't perform replacements

**Solutions:**
- Verify matches are found first with "Find Next" or "Find All"
- Check your search scope (Selection/Method may limit results)
- Ensure the editor is not read-only
- Verify regex pattern and replacement are valid

### Search Not Wrapping

**Problem:** Search stops at end of document

**Solutions:**
- Enable "Wrap Around" checkbox
- Check if search scope is set to "Selection" (wrapping is disabled for selections)

### Highlights Not Clearing

**Problem:** Previous search highlights remain visible

**Solutions:**
- Close and reopen Better Find dialog
- Use "Editor: Clear Annotations" command from Command Palette
- Open a new search (automatically clears previous highlights)

## Keyboard Shortcuts Reference

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Open Better Find |
| `Ctrl+H` | Open Better Find with Replace mode |
| `F3` | Find Next (even with dialog closed) |
| `Shift+F3` | Find Previous (even with dialog closed) |
| `Enter` | Find Next (when dialog open) |
| `Shift+Enter` | Find Previous (when dialog open) |
| `Escape` | Close dialog |
| `Alt+C` | Toggle Match Case |
| `Alt+W` | Toggle Whole Word |
| `Alt+R` | Toggle Use Regex |

## Related Features

- **[Navigation](navigation.md)** - Go To Definition, Navigation History, and Bookmarks
- **[Code Styling](code-styling.md)** - Visual indicators that can be searched for patterns
- **[Refactoring](../user-guide/using-refactoring-tools.md)** - Automated code transformations
- **[Settings Reference](../user-guide/settings-reference.md)** - Configuration options

## Next Steps

- Try [Quick Fixes](quick-fixes.md) for automated code corrections
- Learn about [Smart Open](smart-open.md) for fast file navigation
- Explore [Type Checking](type-checking.md) for finding type-related issues
- Read [Keyboard Shortcuts](../user-guide/keyboard-shortcuts.md) for full shortcut reference
