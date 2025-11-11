# AppRefiner Documentation Todo List

## Overview

This document tracks documentation gaps and inaccuracies identified in the AppRefiner feature documentation. The target audience is business developers using PeopleSoft Application Designer. All documentation should maintain a professional, factual tone focused on practical usage.

## Documentation Coverage Statistics

- **Linters**: 15/15 documented (100%)
- **Stylers**: 20/20 documented (100%)
- **Refactors**: 12/12 documented (100%) - user-triggered refactors only
- **Quick Fixes**: 6/6 documented (100%) - auto-suggested refactors (CorrectClassName exists but not yet connected to styler)
- **Tooltip Providers**: 0/6 documented (0%)
- **Keyboard Shortcuts**: 25/25 documented (100%)
- **Settings**: ~8/25 documented (32%)
- **Major Features**: ~7/15 major features documented (47%)

---

## Section 1: Corrections Needed for Existing Documentation

### 1.1 Keyboard Shortcuts (docs/user-guide/keyboard-shortcuts.md)

**Critical Inaccuracies:**
- Documented `Alt+Left/Right` as collapse/expand code folding
- Documented `Ctrl+Alt+Left/Right` as collapse/expand all
- **Actual Implementation:**
  - `Alt+Left/Right` - Navigate backward/forward in navigation history
  - `Ctrl+Shift+[/]` - Collapse/expand current fold level
  - `Ctrl+Shift+Alt+[/]` - Collapse/expand all folds

**Missing Keyboard Shortcuts:**
- `Ctrl+F` - Better Find
- `Ctrl+H` - Better Find Replace
- `Ctrl+Alt+S` - Stack Trace Navigator
- `F3` - Find Next
- `Shift+F3` - Find Previous
- `F12` - Go To Definition
- `Ctrl+B` - Place Bookmark
- `Ctrl+-` (minus) - Go to Previous Bookmark
- `Ctrl+Alt+E` - Generate Type Error Report
- `Ctrl+.` - Apply Quick Fix
- `Ctrl+Alt+T` - Apply Template
- `Ctrl+Shift+O` - Outline Dialog
- `Alt+L` (not Ctrl+Alt+L) - Toggle Linter Dialog

### 1.2 Linters (docs/user-guide/working-with-linters.md)

**Missing Linter:**
- `REUSED_FOR_ITER` - Detects the re-use of for loop iterators in nested for loops

**Incorrect Keyboard Shortcut:**
- Documented as `Ctrl+Alt+L` but actual shortcut is `Alt+L`

### 1.3 Code Styling (docs/features/code-styling.md)

**Missing Stylers (12 undocumented):**
1. Class Name Mismatch - Detects when class name doesn't match file name
2. Dead Code - Identifies unreachable code after return/exit statements
3. Find() Parameter Order - Validates Find function parameter ordering
4. Missing Constructors - Detects classes without constructors when needed
5. Missing Method Implementation - Methods declared but not implemented
6. Missing Semicolons - Statements missing required semicolons
7. Redeclared Variables - Variables declared multiple times in same scope
8. Reused For Iterators - For loop iterator reuse in nested loops
9. SQL Variable Count - Validates bind variable counts in SQL statements
10. Syntax Errors - Marks parse and syntax errors
11. Type Errors - Marks type inference and checking errors
12. Unimplemented Abstract Members - Abstract methods/properties not implemented
13. Wrong Exception Variable - Incorrect exception variable in catch block

### 1.4 Refactoring (docs/user-guide/using-refactoring-tools.md)

**Missing Refactors (5 undocumented):**
These are user-triggered refactors that can be invoked manually:
1. Concat Auto Complete - Expands += for string concatenation
2. MsgBox Auto Complete - Expands MsgBox() to MessageBox()
3. Declare Function - Opens dialog to declare external functions
4. Collect Local Variables - Groups variable declarations
5. Mark Statement Numbers - Adds statement number markers

**Quick Fixes (7 undocumented):**
Quick fixes are a special category of refactors that cannot be triggered directly by users. Instead, they are automatically suggested when the cursor is positioned on a styler issue and the user presses `Ctrl+.` (Apply Quick Fix). Each quick fix is associated with specific stylers that detect the issues they can resolve.

Quick fixes should be documented in their own section with:
- List of all available quick fixes
- Which styler(s) trigger each quick fix
- Examples of before/after code

Available quick fixes:
1. Declare For Loop Iterator - Adds missing iterator declaration (UndefinedVariables styler)
2. Delete Unused Variable - Removes unused variable declarations (UnusedVariables styler)
3. Fix Exception Variable - Corrects exception variable in catch block (WrongExceptionVariableStyler)
4. Generate Base Constructor - Creates constructor with base class call (MissingConstructor styler)
5. Implement Abstract Members - Generates abstract method/property implementations (UnimplementedAbstractMembersStyler)
6. Implement Missing Method - Generates implementation for declared method (MissingMethodImplementation styler)

Quick fixes not yet connected:
- Correct Class Name - Code exists but not attached to ClassNameMismatch styler yet

**Documentation Updates Needed:**
- Create dedicated quick-fixes.md document
- In code-styling.md, add links from each styler to its associated quick fixes
- Clarify in using-refactoring-tools.md that quick fixes are invoked via Ctrl+. only

---

## Section 2: High Priority Undocumented Features

These are major user-facing features that significantly enhance developer productivity.

### 2.1 Smart Open Dialog (Ctrl+O)
**Priority**: Critical
**Location**: `AppRefiner/Dialogs/SmartOpenDialog.cs`
**Command**: "Open: Smart Open (Ctrl+O)"

**Description**: Database-driven fuzzy search system for opening PeopleSoft objects across all types (App Classes, Records, Pages, Components, etc.). Replaces Application Designer's basic Open dialog.

**Documentation Needed**:
- Fuzzy search algorithm and syntax
- Supported object types
- Configuration dialog for customizing searchable types
- Database connection requirement
- Performance considerations
- Keyboard navigation within dialog

### 2.2 Better Find/Replace (Ctrl+F / Ctrl+H)
**Priority**: Critical
**Location**: `AppRefiner/Dialogs/BetterFindDialog.cs`
**Commands**:
- "Editor: Better Find (Ctrl+F)"
- "Editor: Better Find Replace (Ctrl+H)"
- "Editor: Find Next (F3)"
- "Editor: Find Previous (Shift+F3)"

**Description**: Enhanced search and replace functionality with regex support, capture group replacement, and visual highlighting. Replaces Application Designer's basic Find/Replace.

**Documentation Needed**:
- Regex pattern syntax and examples
- Replace with capture groups ($1, $2, etc.)
- Search options (case-sensitive, whole word, regex, wrap around)
- Search direction (forward/backward)
- Visual highlighting indicators
- Per-editor search state persistence
- Search history management
- Keyboard shortcuts and navigation

### 2.3 Go To Definition (F12)
**Priority**: Critical
**Location**: `AppRefiner/Services/NavigationService.cs`
**Command**: "Navigation: Go To Definition (F12)"

**Description**: Jump to definition of classes, methods, functions, properties, and variables. Database-driven for cross-file navigation. Creates navigation history entries.

**Documentation Needed**:
- Supported symbol types (classes, methods, functions, variables, properties)
- Database requirement for cross-file navigation
- Navigation history integration
- Fallback behavior when definition not found
- Performance with large codebases

### 2.4 Navigation History (Alt+Left / Alt+Right)
**Priority**: Critical
**Location**: `AppRefiner/Services/NavigationService.cs`
**Commands**:
- "Navigation: Navigate Backward (Alt+Left)"
- "Navigation: Navigate Forward (Alt+Right)"

**Description**: Maintains history of cursor positions across files, allowing navigation back and forth through edit locations.

**Documentation Needed**:
- How navigation history is captured
- Integration with Go To Definition
- History capacity and management
- Clearing history
- Keyboard shortcuts

### 2.5 Bookmarks System (Ctrl+B / Ctrl+-)
**Priority**: High
**Location**: `AppRefiner/Services/BookmarkService.cs`
**Commands**:
- Place Bookmark: `Ctrl+B`
- Go to Previous Bookmark: `Ctrl+-` (minus)

**Description**: Stack-based bookmark system for marking and returning to code locations. Visual indicators in editor margin.

**Documentation Needed**:
- Placing bookmarks
- Bookmark stack behavior (LIFO)
- Visual indicators in margin
- Clearing bookmarks
- Bookmark persistence across sessions

### 2.6 Stack Trace Navigator (Ctrl+Alt+S)
**Priority**: High
**Location**: `AppRefiner/Dialogs/StackTraceNavigatorDialog.cs`
**Command**: "Stack Trace Navigator (Ctrl+Alt+S)"

**Description**: Parses PeopleCode stack traces from error messages and allows navigation to each frame with direct opening of definitions.

**Documentation Needed**:
- How to paste stack trace into dialog
- Stack frame parsing and display
- Navigation to specific frames
- Opening definitions from stack trace
- Supported stack trace formats

### 2.7 Quick Fixes (Ctrl+.)
**Priority**: High
**Location**: `AppRefiner/Refactors/QuickFixes/`
**Command**: "Editor: Apply Quick Fix (Ctrl+.)"

**Description**: Context-aware automatic code fixes for styler issues. Quick fixes are a special category of refactors that cannot be invoked directly through the command palette or refactoring menu. They are only triggered when the user positions the cursor on code with a styler issue and presses `Ctrl+.`. The system automatically suggests applicable quick fixes based on the styler that marked the code.

**Documentation Needed**:
- Explanation that quick fixes are auto-suggested, not manually invoked
- How to trigger quick fixes (Ctrl+. on styler issue)
- Complete list of available quick fixes with before/after examples
- Which styler(s) trigger each quick fix
- Cursor positioning requirements
- How to preview changes before applying
- Relationship between stylers and quick fixes

### 2.8 Auto-Complete and Auto-Suggest System
**Priority**: High
**Location**: `AppRefiner/Services/AutoCompleteService.cs`
**Settings**: Auto-Suggest section with 4 checkboxes

**Description**: Intelligent code completion system with four suggestion types, configurable individually.

**Suggestion Types**:
1. **Variable Suggestions** - Shows accessible variables when typing `&`
2. **Function Signatures** - Shows call tips with parameters when typing `(`
3. **Object Members** - Shows methods and properties when typing `.` after object
4. **System Variables** - Shows system variables when typing `%`

**Documentation Needed**:
- Each suggestion type with examples
- Configuration options for enabling/disabling each type
- Scope-aware variable suggestions
- Database integration for object member suggestions
- Performance considerations
- Keyboard navigation in suggestion lists

### 2.9 Type Checking System
**Priority**: High
**Location**: `AppRefiner/TypeSystem/`
**Command**: "Generate Type Error Report (Ctrl+Alt+E)"

**Description**: Real-time type inference and validation system that analyzes code for type errors and compatibility issues.

**Components**:
- Type inference for expressions and variables
- Type compatibility checking
- Type error reporting with detailed messages
- Visual indicators for type errors (TypeErrorStyler)
- Comprehensive type error report generation

**Documentation Needed**:
- How type inference works
- Types of errors detected
- Reading type error messages
- Generating and interpreting type error reports
- Configuring type checking behavior
- Performance impact

### 2.10 Snapshot and Version History System
**Priority**: High
**Location**: `AppRefiner/Services/SnapshotService.cs`
**Command**: "Snapshot: Revert to Previous Version"

**Description**: Automatic snapshot creation on save with diff viewing and revert capabilities.

**Documentation Needed**:
- Automatic snapshot creation timing
- Viewing snapshot history
- Comparing versions with diff view
- Reverting to previous versions
- Snapshot retention settings
- Storage location and management

### 2.11 Outline Dialog (Ctrl+Shift+O)
**Priority**: Medium
**Location**: `AppRefiner/Dialogs/OutlineDialog.cs`
**Command**: "Navigation: Outline (Ctrl+Shift+O)"

**Description**: Quick navigation to methods, properties, functions, getters, and setters within current file.

**Documentation Needed**:
- Supported symbol types in outline
- Filtering and search within outline
- Keyboard navigation
- Symbol icons and indicators

### 2.12 Project Linting
**Priority**: Medium
**Location**: `AppRefiner/Services/ProjectLintService.cs`
**Command**: "Project: Lint Project"

**Description**: Lint entire Application Designer project with progress tracking and comprehensive reporting.

**Documentation Needed**:
- Project scope definition
- Progress dialog and cancellation
- Report generation and export
- Database connection requirement
- Performance with large projects
- Configuring which linters run

### 2.13 Event Mapping Detection
**Priority**: Medium
**Settings**: "Detect Event Mapping" and "Show Event Mapped References" checkboxes

**Description**: Detects and highlights event mapping usage in PeopleCode for better understanding of event flow.

**Documentation Needed**:
- What event mapping detection identifies
- Visual indicators for event-mapped code
- Configuration options
- Use cases and benefits

---

## Section 3: Medium Priority Undocumented Features

### 3.1 Tooltip Providers (6 total)
**Priority**: Medium
**Location**: `AppRefiner/TooltipProviders/`
**Settings Tab**: Tooltips with individual checkboxes

All tooltip providers are undocumented:

1. **Active Indicators Tooltip Provider**
   - Shows tooltips for highlighted regions in code
   - Explains what stylers/linters marked the region

2. **App Class Info Tooltip Provider**
   - Shows public and protected members when hovering over Application Class path
   - Requires database connection

3. **Method Parameters Tooltip Provider**
   - Shows method parameter information on method calls
   - Displays parameter types and names

4. **PeopleSoft Object Tooltip Provider**
   - Shows metadata about PeopleSoft objects when hovering
   - Requires database connection
   - Displays object type, description, and properties

5. **Scope Info Tooltip Provider**
   - Shows containing scope hierarchy when hovering at line start
   - Displays class, method, function context

6. **Variable Info Tooltip Provider**
   - Shows comprehensive variable information including:
     - Variable type and kind (local, global, instance, etc.)
     - Declaration location
     - Usage statistics
     - All references in code
   - Most detailed tooltip provider

**Documentation Needed**:
- Individual documentation for each provider
- Configuration options (enable/disable)
- Examples of tooltip displays
- Database requirements where applicable

### 3.2 Settings and Configuration Options
**Priority**: Medium
**Location**: Settings Tab

Many settings lack documentation:

**Undocumented Checkboxes**:
- Prompt for DB Connection - Controls automatic database connection dialog
- Remember Folds - Persists fold state across sessions
- Override Find/Replace - Enables Better Find to replace Application Designer's Find
- Override Open - Enables Smart Open to replace Application Designer's Open
- Center Dialogs - Automatically centers dialogs on Application Designer window
- Multiple Selection - Enables multi-cursor editing
- Line Selection Fix - Corrects line selection behavior in Scintilla
- Detect Event Mapping - Enables event mapping detection
- Show Event Mapped References - Shows event-mapped code references

**Undocumented Settings Groups**:
- Theme Settings (Theme dropdown, Filled checkbox)
- Path Settings (TNS_ADMIN, Lint Report, Debug Log Directory)
- Display Settings (Show Class Path vs Show Class Text radio buttons)

**Documentation Needed**:
- Complete settings reference guide
- Default values for each setting
- Impact of changing each setting
- Recommended configurations for different use cases

### 3.3 Theme System
**Priority**: Medium
**Location**: `AppRefiner/Services/ThemeService.cs`
**Settings**: Theme dropdown and Filled checkbox

**Description**: Multiple theme options with filled/outline variants for Application Designer integration.

**Documentation Needed**:
- Available themes and previews
- Filled vs Outline variants
- Automatic theme application
- Per-process theme support
- Creating custom themes

### 3.4 Multi-Selection Support
**Priority**: Low
**Settings**: "Multiple Selection" checkbox

**Description**: Edit multiple locations simultaneously similar to modern IDEs.

**Documentation Needed**:
- How to create multiple cursors
- Keyboard shortcuts for multi-selection
- Use cases and examples
- Configuration options

---

## Section 4: Low Priority Undocumented Features

### 4.1 Plugin System
**Priority**: Low
**Location**: `AppRefiner/Services/PluginService.cs`
**Settings**: "Plugins..." button

**Description**: Load and manage custom plugins that extend AppRefiner functionality.

**Documentation Needed**:
- Plugin architecture overview
- Loading plugins from directory
- Plugin discovery process
- Configuring plugin directory
- See PluginSample project as reference

### 4.2 Additional Commands
**Priority**: Low

Commands not currently documented:
- "Database: Disconnect DB"
- "Editor: Clear Annotations"
- "Editor: Force Refresh"
- "SQL: Format SQL"
- "Debug: Open Debug Console"
- "Debug: Open Indicator Panel"

### 4.3 Additional Dialogs
**Priority**: Low

Dialogs lacking documentation:
- Declare Function Dialog - For declaring external functions
- Diff View Dialog - For snapshot comparison
- Lint Project Progress Dialog - Progress tracking during project linting
- Smart Open Config Dialog - Configure Smart Open object types
- Type Error Report Dialog - Display comprehensive type errors
- Template Dialogs - Selection, parameter input, confirmation

### 4.4 Advanced Features
**Priority**: Low

Advanced features that may need documentation:
- Function Cache Manager - Performance optimization for function definitions
- Folding Manager - Fold state persistence management
- Dialog Centering Service - Auto-center dialogs on Application Designer
- Lexilla Detection - Adapt to different Scintilla/Lexilla versions
- Results List Interception - Intercept Application Designer results for file opening

---

## Section 5: Suggested New Documentation Files

The following new documentation files should be created in `docs/features/`:

### High Priority
1. `smart-open.md` - Smart Open Dialog feature
2. `better-find-replace.md` - Better Find/Replace feature
3. `navigation.md` - Go To Definition, Navigation History, Bookmarks
4. `stack-trace-navigator.md` - Stack Trace Navigator feature
5. `quick-fixes.md` - Quick Fixes system
6. `auto-complete-suggestions.md` - Auto-Complete/Suggest system
7. `type-checking.md` - Type Checking system
8. `snapshots.md` - Snapshot and Version History

### Medium Priority
9. `tooltips.md` - All 6 Tooltip Providers
10. `outline.md` - Outline Dialog
11. `project-linting.md` - Project Linting feature
12. `event-mapping.md` - Event Mapping Detection
13. `themes.md` - Theme System
14. `settings-reference.md` - Complete settings documentation

### Low Priority
15. `plugins.md` - Plugin System
16. `multi-selection.md` - Multi-Selection feature
17. `advanced-features.md` - Advanced features overview

---

## Section 6: Documentation Standards

All documentation should adhere to these standards:

1. **Tone**: Professional and factual, appropriate for business developers
2. **Structure**: Use clear headings, bullet points, and examples
3. **Audience**: Assume familiarity with PeopleSoft but not necessarily with advanced IDEs
4. **Examples**: Include practical examples with screenshots where helpful
5. **Prerequisites**: Clearly state any requirements (database connection, settings, etc.)
6. **Keyboard Shortcuts**: Always include relevant keyboard shortcuts
7. **Configuration**: Document all related settings and options
8. **Troubleshooting**: Include common issues and solutions

---

## Progress Tracking

### Corrections Completed: 4/4
- [x] Keyboard shortcuts corrections (All 25+ shortcuts documented correctly)
- [x] Linters corrections (added REUSED_FOR_ITER, fixed Alt+L shortcut)
- [x] Code styling additions (All 20 stylers documented with quick fix links)
- [x] Quick Fixes documentation (6 quick fixes documented)

### High Priority Features Documented: 13/13 ✓ COMPLETE
- [x] Smart Open Dialog
- [x] Better Find/Replace
- [x] Go To Definition (covered in navigation.md)
- [x] Navigation History (covered in navigation.md)
- [x] Bookmarks System (covered in navigation.md)
- [x] Stack Trace Navigator
- [x] Quick Fixes
- [x] Auto-Complete and Auto-Suggest
- [x] Type Checking System
- [x] Snapshot/Version History
- [x] Outline Dialog
- [x] Project Linting
- [x] Event Mapping Detection

### Medium Priority Features Documented: 3/3 ✓ COMPLETE
- [x] Tooltip Providers (all 7 - found InferredTypeTooltipProvider not listed)
- [x] Settings and Configuration (complete reference)
- [x] Theme System (covered in Settings Reference)

### Low Priority Features Documented: 4/4 ✓ COMPLETE
- [x] Multi-Selection Support
- [x] Plugin System
- [x] Additional Commands
- [x] Additional Dialogs/Advanced Features

### New Documentation Files Created: 3/17
- [x] quick-fixes.md
- [x] auto-suggest.md
- [x] type-checking.md
- See Section 5 for complete list

---

## Estimated Effort

- **Corrections**: ~8 hours
- **High Priority**: ~40 hours (13 features × ~3 hours average)
- **Medium Priority**: ~16 hours
- **Low Priority**: ~8 hours
- **Total**: ~72 hours of documentation work

---

## Next Steps

1. Review and approve this todo list
2. Prioritize which features to document first based on user impact
3. Create documentation templates for consistency
4. Begin with corrections to existing documentation
5. Proceed through high-priority features systematically
6. Consider user feedback on documentation needs
