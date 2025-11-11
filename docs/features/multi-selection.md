# Multi-Selection Support

## Overview

AppRefiner provides multi-cursor editing capabilities through Scintilla's built-in multiple selection feature. When enabled, you can place multiple cursors in your code and edit multiple locations simultaneously, similar to modern IDEs like Visual Studio Code and Sublime Text.

**Key Features:**
- **Multiple Cursors**: Click with `Ctrl` to add additional cursors anywhere in the document
- **Simultaneous Editing**: Type once, edit everywhere - changes apply to all cursor positions
- **Multi-Paste**: Paste content into each selection independently
- **Rectangular Selection**: Select columns of text for columnar editing
- **Virtual Space**: Edit beyond line endings for perfect column alignment
- **Visual Feedback**: All cursors blink and are clearly visible

**Use Cases:**
- Rename variables in multiple locations simultaneously
- Add identical code to multiple lines
- Edit columnar data or aligned comments
- Refactor repetitive code patterns
- Bulk text transformations

---

## Enabling Multi-Selection

### Via Settings

1. Open AppRefiner Settings
2. Navigate to the **Editor Tweaks** tab
3. Locate the "Settings" group
4. Check the **"Multiple Selection"** checkbox
5. Setting takes effect immediately for all open editors

**Setting Details:**
- **Name**: Multiple Selection
- **Default**: Disabled (unchecked)
- **Applies To**: All editors in all Application Designer processes
- **Persistence**: Setting saved and remembered across sessions

---

## Creating Multiple Cursors

### Ctrl+Click Method

The primary way to create multiple cursors is using `Ctrl+Click`:

1. Position your primary cursor somewhere in the document
2. Hold down `Ctrl` key
3. Click at another location to add a second cursor
4. Continue `Ctrl+Clicking` to add more cursors
5. Release `Ctrl` and start typing

**Example:**
```peoplecode
Local string &firstName;
Local string &lastName;
Local string &fullName;
```

To change all `string` to `String` (capitalized):
1. `Ctrl+Click` before the `s` in `string` on line 1
2. `Ctrl+Click` before the `s` in `string` on line 2
3. `Ctrl+Click` before the `s` in `string` on line 3
4. Press `Delete` to remove lowercase `s`
5. Type `S` to add uppercase `S`

**Result:**
```peoplecode
Local String &firstName;
Local String &lastName;
Local String &fullName;
```

---

### Click and Drag Selection

You can also create multiple selections by click-dragging with `Ctrl`:

1. Position your primary cursor and select some text
2. Hold down `Ctrl`
3. Click and drag to select text at another location
4. Both selections are now active

**Example:**
```peoplecode
&result1 = ProcessData(&input1);
&result2 = ProcessData(&input2);
&result3 = ProcessData(&input3);
```

To change all `ProcessData` to `TransformData`:
1. Double-click `ProcessData` on line 1 to select it
2. `Ctrl+Double-click` `ProcessData` on line 2
3. `Ctrl+Double-click` `ProcessData` on line 3
4. Type `TransformData`

---

### Rectangular (Column) Selection

Create a vertical column of cursors for columnar editing:

**Method 1: Alt+Shift+Arrow Keys**
1. Position cursor at starting point
2. Hold `Alt+Shift`
3. Use Arrow Keys to expand rectangular selection
4. Release and start typing

**Method 2: Alt+Drag**
1. Position cursor at top-left of region
2. Hold `Alt`
3. Click and drag to bottom-right of region
4. Release and start typing

**Example - Columnar Editing:**

Before:
```peoplecode
&value1  100
&value2  200
&value3  300
```

Add `=` operator using rectangular selection:
1. Place cursor before `100`
2. `Alt+Shift+Down` twice to select column
3. Type ` = ` to add operator

After:
```peoplecode
&value1 = 100
&value2 = 200
&value3 = 300
```

---

## Editing with Multiple Cursors

### Typing

When you have multiple cursors, anything you type appears at all cursor positions simultaneously:

- **Letters**: Typed at each cursor
- **Backspace**: Deletes character before each cursor
- **Delete**: Deletes character after each cursor
- **Enter**: Inserts line break at each cursor
- **Tab**: Inserts tab at each cursor

**Example - Adding Prefix:**
```peoplecode
firstName
lastName
emailAddress
```

To add `get` prefix:
1. `Ctrl+Click` before each variable name
2. Type `get`

Result:
```peoplecode
getfirstName
getlastName
getemailAddress
```

---

### Copy and Paste

#### Standard Paste
Copying and pasting with multiple cursors:

**Single Source, Multiple Destinations:**
1. Copy text from single location (`Ctrl+C`)
2. Create multiple cursors
3. Paste (`Ctrl+V`)
4. Same text pasted at each cursor

**Example:**
```peoplecode
&logger.Log();
&validator.Validate();
&processor.Process();
```

To add parameters:
1. Copy `"Starting operation"` (with quotes)
2. `Ctrl+Click` inside each `()` parentheses
3. Paste
4. All method calls now have the parameter

Result:
```peoplecode
&logger.Log("Starting operation");
&validator.Validate("Starting operation");
&processor.Process("Starting operation");
```

---

#### Multi-Line Paste

When pasting multiple lines with multiple cursors:

**Behavior:**
- If clipboard has N lines and you have N cursors
- Each line pasted into corresponding cursor position
- Useful for distributing data across multiple locations

**Example:**

Clipboard contains:
```
DEBUG
INFO
WARNING
```

Code before paste:
```peoplecode
&level1 = ;
&level2 = ;
&level3 = ;
```

Place cursors after each `=`:
```peoplecode
&level1 = |
&level2 = |
&level3 = |
```

After pasting:
```peoplecode
&level1 = DEBUG
&level2 = INFO
&level3 = WARNING
```

---

### Selection and Movement

#### Arrow Key Navigation
- **Arrow Keys**: All cursors move together
- **Home**: All cursors move to line start
- **End**: All cursors move to line end
- **Page Up/Down**: All cursors scroll together

#### Selection Expansion
- **Shift+Arrow**: Extends selection at each cursor
- **Ctrl+Shift+End**: Selects from each cursor to end of document
- **Ctrl+A**: Clears multiple cursors, selects all (returns to single cursor)

#### Word Boundaries
- **Ctrl+Left/Right**: Moves all cursors by word
- **Ctrl+Shift+Left/Right**: Selects word at each cursor

---

*For more information on editing features, see [Code Folding](editor-tweaks/code-folding.md), [Better Find/Replace](better-find-replace.md), and [Refactoring Tools](../user-guide/using-refactoring-tools.md).*
