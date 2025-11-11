# Tooltip Providers

## Overview

AppRefiner provides an extensible tooltip system that displays contextual information when you hover your mouse over code elements in the editor. The system includes 7 built-in tooltip providers that work together to deliver rich, intelligent tooltips covering variables, methods, types, scopes, and more.

**Key Features:**
- **Multiple Provider System**: Each tooltip provider specializes in different aspects of code analysis
- **Automatic Type Inference**: Shows inferred types for expressions without requiring explicit type annotations
- **Database Integration**: Some providers leverage database connections for enhanced information
- **Priority-Based Display**: Multiple tooltips can be combined when hovering over complex expressions
- **Individual Configuration**: Enable/disable each tooltip provider independently
- **Rich Formatting**: Tooltips use markdown-style formatting with icons and structured information
- **Scope-Aware Analysis**: Leverages AppRefiner's AST-based scope tracking for accurate information

## Available Tooltip Providers

AppRefiner includes the following 7 tooltip providers, listed by priority (highest to lowest):

| Priority | Provider Name | Database Required | Description |
|----------|---------------|-------------------|-------------|
| 100 | Active Indicators | No | Shows information about highlighted code regions (styler/linter issues) |
| 80 | Scope Context | No | Shows containing scope hierarchy when hovering over leading whitespace |
| 70 | PeopleSoft Object Info | Yes | Shows metadata for PeopleSoft objects like Record fields |
| 50 | Method Parameters | Optional | Shows method parameter information and signatures |
| 50 | Variable Info | No | Shows comprehensive variable information with usage statistics |
| 40 | Inferred Type | No | Shows inferred type for any expression |
| 0 | Application Class Details | Yes | Shows public/protected members of App Classes |

---

## 1. Active Indicators Tooltip Provider

### Purpose
Displays tooltips for code regions that have been highlighted by stylers or linters, explaining what issues were detected and whether quick fixes are available.

### When It Appears
- When you hover over any highlighted region in the code
- Highlights can come from:
  - Linters (code quality issues)
  - Stylers (visual indicators for patterns)
  - Type checking errors
  - Syntax errors

### Information Displayed
- **Issue Description**: Explanation of what the styler/linter detected
- **Quick Fix Availability**: Indicates if pressing `Ctrl+.` will offer automatic fixes
- **Multiple Issues**: If multiple stylers/linters marked the same region, all tooltips are shown

### Example Tooltip
```
Undefined variable: &invalidVar
This variable has not been declared.

Quick fixes... (Ctrl+.)
```

### Configuration
- **Setting Location**: Tooltips tab → "Highlight Tooltips"
- **Default State**: Enabled
- **Database Requirement**: None

### Priority
Highest priority (100) - Always appears first if hovering over a highlighted region

### Use Cases
- **Investigating Errors**: Understand why code is highlighted in red/yellow
- **Discovering Quick Fixes**: Learn which issues can be automatically corrected
- **Code Quality**: Review linter findings without opening the full linter panel

---

## 2. Scope Context Tooltip Provider

### Purpose
Shows the hierarchical structure of scopes containing the current line of code, helping you understand the context you're working in.

### When It Appears
- Only when hovering over **leading whitespace** at the beginning of a line
- Does not appear when hovering over actual code text

### Information Displayed
Shows a hierarchical list of containing scopes, including:
- **Methods/Functions**: With parameter signatures and return types
- **Control Flow**: If, Else, For, While, Repeat, Catch, Evaluate blocks
- **Indentation**: Nested scopes are indented to show structure
- **Conditions**: Shows loop conditions and if statement conditions

### Example Tooltip
```
Method ProcessData(RecordName as String) returns Boolean
  If &record.IsValid
    For &i = 1 To &maxCount
```

### Configuration
- **Setting Location**: Tooltips tab → "Scope Context"
- **Default State**: Enabled
- **Database Requirement**: None

### Priority
High priority (80) - Shows before most other tooltips when hovering over whitespace

### Use Cases
- **Understanding Context**: Quickly see which method and control blocks you're inside
- **Deep Nesting**: Navigate complex nested structures without scrolling

### Tips
- Hover over the leading spaces/tabs of a line to activate
- Does not show the scope that starts on the current line (only parent scopes)

---

## 3. PeopleSoft Object Info Tooltip Provider

### Purpose
Displays metadata about PeopleSoft objects, particularly Record definitions with their field structure.

### When It Appears
- When hovering over `RECORD.RecordName` patterns
- When hovering over `Scroll.RecordName` patterns

### Information Displayed
- **Record Name**: The name of the PeopleSoft Record definition
- **Field List**: All fields in the record with:
  - Field name
  - Field type
  - Length/precision
  - Key indicator (*) for key fields
  - Required indicator (!) for required fields

### Example Tooltip
```
Record Definition: JOB

Fields (*=Key, !=Required):
* EMPLID (Char 11) !
* EMPL_RCD (Number 3,0) !
* EFFDT (Date) !
* EFFSEQ (Number 3,0) !
  BUSINESS_UNIT (Char 5)
  DEPTID (Char 10)
  JOBCODE (Char 6)
  POSITION_NBR (Char 8)
  ...
```

### Configuration
- **Setting Location**: Tooltips tab → "PeopleSoft Object Info"
- **Default State**: Enabled
- **Database Requirement**: **Yes** - Requires active database connection to query metadata

### Priority
High priority (70)

### Use Cases
- **Understanding Record Structure**: Quick reference for record structure

### Limitations
- Only works with `RECORD.Name` and `Scroll.Name` patterns or where Type Inference can determine the record name.
- Requires database connection - shows "Record not found" if disconnected

---

## 4. Method Parameters Tooltip Provider

### Purpose
Shows detailed parameter information, signatures, and return types for method calls and function calls.

### When It Appears
- When hovering over `%This.MethodName()` calls (current class methods)
- When hovering over `&variable.MethodName()` calls (object methods)
- When hovering over global function calls like `Split()`, `Left()`, etc.
- When hovering over declared external function calls

### Information Displayed

**For Class Methods:**
- Method name
- Access level (Public, Protected, Private)
- Parameters with types
- Return type
- Whether inherited from a parent class

**For Builtin Functions:**
- Function signature
- Parameter details with types
- Minimum/maximum argument counts
- Return type

**For Object Methods:**
- Method signature for builtin types (Row, Rowset, Record, etc.)
- Type information from inferred types

### Example Tooltips

**Class Method:**
```
Method: ValidateData (inherited from Base:Validator)
Access: Public
Returns: Boolean
Parameters:
   InputData as String
   CheckRequired as Boolean
```

**Builtin Function:**
```
Builtin: Split(String, Separator) -> array of String

Parameters:
   String as String
   Separator as String
Min args: 2
```

**Object Method:**
```
Builtin: GetRow(ScrollLevel) -> Row

Parameters:
   ScrollLevel as Integer
```

### Configuration
- **Setting Location**: Settings → Tooltips tab → "Method Parameters"
- **Default State**: Enabled
- **Database Requirement**: Optional - Enhanced functionality with database for cross-class lookups

### Priority
Medium priority (50)

### Use Cases
- **API Discovery**: Learn method signatures without documentation
- **Parameter Verification**: Confirm parameter order and types
- **Type Information**: Understand what methods expect and return
- **Builtin Functions**: Quick reference for PeopleCode builtin functions

### Advanced Features
- **Type Inference Integration**: Uses inferred types to find appropriate methods
- **Inheritance Chain Lookup**: Searches parent classes for inherited methods (requires database)
- **Builtin Database**: Has comprehensive builtin function signatures built-in
- **External Functions**: Shows signatures for declared external functions

---

## 5. Variable Info Tooltip Provider

### Purpose
Provides comprehensive, detailed information about variables including their type, scope, usage statistics, and all reference locations throughout the code.

### When It Appears
- When hovering over any variable reference (local, global, instance, component)
- When hovering over variable declarations
- When hovering over property references (`%This.PropertyName`)
- When hovering over parameters

### Information Displayed

The Variable Info tooltip is the most comprehensive, showing:

**Basic Information:**
- Variable name
- Inferred type
- Variable kind (with icon):
  - 🔸 Local variable
  - 🏠 Instance variable
  - 🌍 Global variable
  - 📦 Component variable
  - 📥 Method parameter
  - 🔒 Constant
  - 🏷️ Property

**Declaration Information:**
- 📍 Scope where declared
- 📍 Line number of declaration

**Safety Classification:**
- ✅ Safe to refactor / ⚠️ Unsafe to refactor
- Indicates if renaming the variable is safe

**Usage Statistics:**
- Total reference count
- Read operations count
- Write operations count
- Parameter annotations count
- Read/Write ratio (percentage breakdown)
- Unused indicator if never referenced

**Reference Locations:**
- Up to 5 reference locations with:
  - 📝 Declaration
  - 👁️ Read operations
  - ✏️ Write operations
  - 🏷️ Parameter annotations
  - Line numbers for each reference
  - Context information
- "... and X more references" if more than 5

**Shadowing Information:**
- ⚠️ Warnings if variable shadows another variable
- Shows which scope is being shadowed
- Indicates if this variable is shadowed by another

### Example Tooltip

**Comprehensive Variable:**
```
**&recordData** (Record)
🔸 Local variable
📍 Declared in: Method ProcessRecords
📍 Line: 42
✅ Safe to refactor

📊 **Usage Statistics:**
   • Total references: 12
   • Read operations: 8
   • Write operations: 4
   • Read/Write ratio: 67% / 33%

📍 **Reference Locations:**
   📝 Line 42: Declaration
   ✏️ Line 45: Write
   👁️ Line 48: Read (in condition)
   👁️ Line 52: Read
   👁️ Line 55: Read
   ... and 7 more references
```

**Unused Variable:**
```
**&unusedVar** (String)
🔸 Local variable
📍 Declared in: Method Initialize
📍 Line: 15
✅ Safe to refactor

📊 **Usage Statistics:**
   • *Unused variable*
```

**Shadowed Variable:**
```
**&counter** (Number)
🔸 Local variable
📍 Declared in: Method ProcessLoop
📍 Line: 78
⚠️ Unsafe to refactor

📊 **Usage Statistics:**
   • Total references: 5
   • Read operations: 4
   • Write operations: 1

⚠️ **Variable Shadowing:**
   • Shadows variable in Method ProcessData
```

### Configuration
- **Setting Location**: Settings → Tooltips tab → "Variable Info"
- **Default State**: Enabled
- **Database Requirement**: None - uses AST-based scope tracking

### Priority
Medium priority (50)

### Use Cases
- **Usage Analysis**: See how and where variables are used
- **Refactoring Safety**: Check if renaming is safe before refactoring
- **Finding References**: Locate all uses of a variable without searching
- **Dead Code Detection**: Identify unused variables
- **Understanding Data Flow**: See read vs. write patterns
- **Shadow Detection**: Find and fix variable shadowing issues

### Advanced Features
- **Scope Tracking**: Leverages `ScopedAstVisitor` for accurate scope analysis
- **Reference Classification**: Distinguishes between reads, writes, and annotations
- **Safety Analysis**: Determines refactoring safety based on scope and references
- **Shadow Detection**: Identifies both shadowing and shadowed variables
- **Usage Patterns**: Calculates read/write ratios for understanding variable usage

---

## 6. Inferred Type Tooltip Provider

### Purpose
Shows the inferred type for any expression at the cursor position, helping you understand what types are flowing through your code without explicit annotations.

### When It Appears
- When hovering over any expression that has a successfully inferred type
- Only shows for concrete types (filters out "Unknown" types)
- Shows for the most specific expression at the cursor position

### Information Displayed
- **Inferred Type**: The type name inferred by AppRefiner's type inference system
- Minimal, concise display: `Inferred Type: TypeName`

### Supported Expressions
- Literals: `"text"` → String, `42` → Number
- Variables: `&myVar` → (depends on declaration)
- Function calls: `Split(&text, ",")` → Array of String
- Method calls: `&record.GetField(Field.EMPLID)` → Field
- Binary operations: `&a + &b` → (based on operand types)
- Member access: `&object.Property` → (property type)
- Array access: `&array[1]` → (element type)
- Object creation: `create Application:Package:Class()` → Application:Package:Class

### Example Tooltips
```
Inferred Type: String
```

```
Inferred Type: Array of Number
```

```
Inferred Type: Application:Utilities:DataProcessor
```

```
Inferred Type: Rowset
```

### Configuration
- **Setting Location**: Settings → Tooltips tab → "Inferred Type"
- **Default State**: Enabled
- **Database Requirement**: None - works with local type inference

### Priority
Low priority (40) - Only shows if no higher-priority tooltip applies

### Use Cases
- **Type Discovery**: Understand what type an expression produces
- **Debugging Type Issues**: Verify type flow in complex expressions
- **Learning PeopleCode**: See what types different operations produce
- **Type Checking**: Validate that expressions have expected types

### How Type Inference Works
- Uses `TypeInferenceVisitor` from the type system
- Propagates types through the AST
- Resolves builtin types (String, Number, Row, etc.) without database
- Resolves custom App Classes with database connection
- Falls back to "Unknown" for unresolvable types (not shown)

### Advanced Features
- **Most Specific Node**: Finds the smallest AST node with type information at cursor
- **Expression Support**: Works on sub-expressions, not just top-level statements
- **Filters Unknowns**: Only shows when concrete type information is available
- **Function Call Filtering**: Doesn't show for function names themselves (avoids clutter)

---

## 7. Application Class Details Tooltip Provider

### Purpose
Shows the public and protected members (methods and properties) of Application Classes when you hover over their paths in import statements or type annotations.

### When It Appears
- When hovering over Application Class paths in `import` statements
- When hovering over Application Class type references in code
- When hovering over base class references in class declarations

### Information Displayed

**For Classes:**
- Class name
- Base class (if extends another class)
- Public methods with:
  - Visibility (Public/Protected)
  - Abstract indicator
  - Method name and parameters
  - Return type
- Public properties with:
  - Visibility
  - Property type
  - Property name
  - Accessors (Get/Set/Get&Set/ReadOnly)

**For Interfaces:**
- Interface name
- Base interface (if extends another interface)
- Method and property declarations with same details as classes

### Example Tooltips

**Class with Methods and Properties:**
```
Class: DataProcessor
Extends: Application:Utilities:BaseProcessor
---
(Public) Method: Initialize(Config as String): Boolean
(Public) Method: ProcessData(Input as Array): Number
(Protected) Method: ValidateInput(Data as Any): Boolean
(Public) Property: String StatusMessage {Get/Set}
(Public) Property: Boolean IsReady {ReadOnly}
```

**Interface:**
```
Interface: IValidator
Extends: Application:Utilities:IBase
---
(Public) Abstract Method: Validate(Data as Any): Boolean
(Public) Abstract Method: GetErrorMessage(): String
(Public) Property: Number ErrorCount {Get}
```

**Extended Class (Shows Protected Members):**
```
Class: CustomProcessor
Extends: Application:Utilities:BaseProcessor
---
(Public) Method: Process(): Boolean
(Protected) Method: ValidateInput(Data as Any): Boolean
(Protected) Property: String ConfigPath {Get/Set}
```

### Configuration
- **Setting Location**: Settings → Tooltips tab → "Application Class Details"
- **Default State**: Enabled
- **Database Requirement**: **Yes** - Must query database for class source code

### Priority
Default priority (0) - Shows after most other tooltips

### Use Cases
- **API Discovery**: Learn what methods and properties a class provides
- **Inheritance Understanding**: See what protected members are available in base classes
- **Interface Compliance**: Review interface requirements
- **Import Verification**: Confirm you're importing the correct class

### Special Behavior
- **Protected Member Visibility**: Shows protected members only when hovering over a base class in an `extends` clause
- **Database Source Lookup**: Retrieves and parses class source from database
- **AST Analysis**: Uses full AST parsing to extract member information

---

## Configuration and Management

### Accessing Tooltip Settings

1. Open AppRefiner Settings (typically via hotkey or menu)
2. Navigate to the **Tooltips** tab
3. You'll see a grid with all tooltip providers

### Settings Grid

The Tooltips settings grid displays:

| Column | Description |
|--------|-------------|
| Active | Checkbox to enable/disable the provider |
| Description | Human-readable description of what the provider does |

### Enabling/Disabling Providers

- **Check** the Active checkbox to enable a tooltip provider
- **Uncheck** the Active checkbox to disable a tooltip provider
- Changes take effect immediately
- Settings are persisted to AppRefiner configuration

### Recommended Configurations

**Maximum Information (Default):**
- All providers enabled
- Best for comprehensive code understanding
- May show multiple tooltips for complex expressions

**Performance-Focused:**
- Disable "Application Class Details" if database is slow
- Disable "PeopleSoft Object Info" if not using Record references
- Keep lightweight providers (Variable Info, Inferred Type, Scope Context) enabled

**Minimal Tooltips:**
- Enable only "Active Indicators" and "Variable Info"
- Reduces tooltip noise
- Focuses on essential information

**Database-Free:**
- Disable "Application Class Details" (requires database)
- Disable "PeopleSoft Object Info" (requires database)
- Set "Method Parameters" to enabled (works without database for builtins)

---

## How Tooltips Work Together

### Priority System

Tooltip providers have priorities that determine display order:
1. **Highest Priority (100)**: Active Indicators - Always first if region is highlighted
2. **High Priority (70-80)**: Scope Context, PeopleSoft Object Info
3. **Medium Priority (50)**: Method Parameters, Variable Info
4. **Low Priority (40)**: Inferred Type - General fallback
5. **Default Priority (0)**: Application Class Details

### Multiple Tooltips

- AppRefiner can display **multiple tooltips** for a single hover location
- Tooltips are separated by blank lines
- Higher priority tooltips appear first
- Example: Hovering over a variable might show:
  1. Active Indicators (if there's a linter issue)
  2. Variable Info (comprehensive variable details)
  3. Inferred Type (the variable's type)

### Tooltip Cancellation

- Moving the mouse cancels the current tooltip
- Pressing `Escape` hides tooltips
- Tooltips auto-hide when you start typing

### Interaction with Other Features

- **Quick Fixes**: Active Indicators tooltip shows when `Ctrl+.` is available
- **Type Checking**: Inferred Type integrates with type checking system
- **Go To Definition**: Variable Info shows declaration locations that can be navigated to
- **Linters/Stylers**: Active Indicators displays their findings

---

## Database Integration

### Providers Requiring Database

Three providers require database connections for full functionality:

1. **PeopleSoft Object Info**: Must query `PSRECDEFN` and `PSRECFIELD` tables
2. **Application Class Details**: Must retrieve App Class source code from database
3. **Method Parameters**: Enhanced with database (works partially without)

### Behavior Without Database

- **PeopleSoft Object Info**: Shows "Record not found" message
- **Application Class Details**: Cannot retrieve external class information
- **Method Parameters**: Still works for:
  - Builtin functions (Split, Left, etc.)
  - Methods in the current file
  - Inferred types from current file
  - But cannot look up external App Classes

### Database Connection Tips

- Configure database connection via Settings → Database tab
- Enable "Prompt for DB Connection" to auto-connect when opening editors
- Tooltip providers will automatically detect database availability
- No errors shown if database is unavailable - tooltips gracefully degrade

---

## Type Inference Integration

### How Type Inference Powers Tooltips

Multiple tooltip providers leverage AppRefiner's type inference system:

- **Method Parameters**: Uses inferred types to find appropriate methods on objects
- **Inferred Type**: Directly displays the result of type inference
- **Variable Info**: Includes inferred types in variable information

### Type Inference Process

1. **AST Parsing**: Code is parsed into Abstract Syntax Tree
2. **Type Metadata Extraction**: Current file's classes/methods are cataloged
3. **Type Propagation**: Types flow through expressions using `TypeInferenceVisitor`
4. **Tooltip Generation**: Providers read type information from AST node attributes

### Type Inference Accuracy

- **Builtins**: 100% accurate for PeopleCode builtin types
- **Literals**: 100% accurate (strings, numbers, booleans, etc.)
- **Local Variables**: High accuracy if declaration is in same file
- **App Classes**: Requires database for external classes
- **Complex Expressions**: Generally accurate for most PeopleCode patterns

---

## Performance Considerations

### Tooltip Performance

- **Lazy Evaluation**: Tooltip providers only process AST when hovering
- **Caching**: AST is cached per file, not re-parsed for every tooltip
- **Priority Short-Circuit**: High-priority tooltips can prevent lower-priority processing
- **Database Queries**: Cached to avoid repeated lookups

### Performance Tips

1. **Disable Unused Providers**: Turn off providers you don't use
2. **Database Providers**: If database is slow, disable "Application Class Details"
3. **Large Files**: Tooltip performance scales with file size - consider splitting large files
4. **Hover Delay**: Wait for the natural tooltip delay before moving mouse

### Known Performance Scenarios

- **First Hover**: May be slower as AST is parsed and type inference runs
- **Subsequent Hovers**: Fast due to caching
- **Database Lookups**: "Application Class Details" may have slight delay on first use
- **Large Classes**: "Application Class Details" may take longer for classes with many members

---

## Troubleshooting

### Tooltips Not Appearing

**Problem**: No tooltips show when hovering over code.

**Solutions**:
1. Check Settings → Tooltips tab - ensure providers are enabled
2. Verify you're hovering long enough (default system delay)
3. Check if the provider requires database connection
4. Verify the file is PeopleCode (tooltips only work for PeopleCode files)
5. Check that the editor has finished parsing (look for indicators/highlights)

### "Record Not Found" for PeopleSoft Objects

**Problem**: Hovering over `RECORD.Name` shows "Record not found".

**Solutions**:
1. Verify database connection is active (Settings → Database)
2. Check that the record name is correct (case-insensitive but must match)
3. Ensure you have permissions to query metadata tables
4. Confirm the record exists in the connected environment

### Class Members Not Showing

**Problem**: Application Class Details tooltip shows "(No public members found)".

**Solutions**:
1. Verify the class has public methods or properties
2. Check database connection (required for external classes)
3. Ensure the class path is correct (e.g., `Application:Package:ClassName`)
4. If hovering over a base class, protected members only show for extended classes

### Multiple Conflicting Tooltips

**Problem**: Too many tooltips showing at once, hard to read.

**Solutions**:
1. Disable lower-priority providers you don't need
2. Use Scope Context only when hovering over whitespace
3. Disable "Inferred Type" if you find it redundant with Variable Info
4. Move mouse slightly to cancel and try again

### Variable Info Not Showing Statistics

**Problem**: Variable Info tooltip shows "Variable not found in scope analysis".

**Solutions**:
1. Ensure variable is declared in the same file
2. Check that the file has been fully parsed (no syntax errors)
3. Verify the variable name is correct (including & prefix)
4. Global variables from other files may not be tracked

---

## Tips and Best Practices

### Maximizing Tooltip Utility

1. **Hover Over Whitespace**: Use Scope Context by hovering at line beginning
2. **Check Active Indicators First**: Always investigate highlighted code
3. **Variable Analysis**: Use Variable Info to find unused variables before refactoring
4. **Type Verification**: Use Inferred Type to verify complex expressions
5. **API Learning**: Use Method Parameters and Application Class Details to learn APIs

### Keyboard Shortcuts

While hovering with tooltips:
- **Escape**: Hide current tooltip
- **Ctrl+.**: Apply quick fix (if shown in Active Indicators tooltip)
- **F12**: Go to definition (use after seeing declaration location in Variable Info)

### Workflow Integration

**During Development:**
1. Write code
2. Hover over variables to check types and usage
3. Hover over method calls to verify parameters
4. Check highlighted regions for issues
5. Use tooltips to understand complex expressions

**During Code Review:**
1. Hover over whitespace to see scope context
2. Check variable usage statistics
3. Verify method parameters and return types
4. Review class member visibility
5. Understand data flow through type inference

**During Debugging:**
1. Use Variable Info to track variable usage
2. Check type flow with Inferred Type
3. Verify method calls with Method Parameters
4. Review scope hierarchy with Scope Context
5. Investigate highlighted issues with Active Indicators

---

## Integration with Other Features

### Linters and Stylers

- **Active Indicators**: Directly displays linter/styler findings
- **Quick Fixes**: Tooltips show when fixes are available
- **Visual Feedback**: Tooltips explain why code is highlighted

### Type Checking

- **Type Errors**: Shown via Active Indicators
- **Type Inference**: Powers Inferred Type and Method Parameters tooltips
- **Type Validation**: Variable Info shows inferred types

### Navigation

- **Go To Definition**: Variable Info shows declaration locations
- **Reference Finding**: Variable Info lists all reference locations
- **Scope Understanding**: Scope Context helps navigate nested code

### Refactoring

- **Safety Analysis**: Variable Info indicates if refactoring is safe
- **Usage Statistics**: Shows read/write patterns before refactoring
- **Shadow Detection**: Warns about variable shadowing issues
- **Quick Fixes**: Active Indicators promotes available refactorings

### Auto-Complete

- **Type Information**: Method Parameters complements auto-complete
- **Member Discovery**: Application Class Details shows available members
- **API Learning**: Tooltips provide information auto-complete doesn't show

---

## Comparison with Application Designer

| Feature | Application Designer | AppRefiner Tooltips |
|---------|---------------------|---------------------|
| Variable Information | None | Comprehensive with usage stats |
| Method Signatures | Limited (help file) | Inline with parameters and return types |
| Scope Context | None | Full hierarchy display |
| Type Information | None | Inferred types for expressions |
| Record Fields | Separate lookup | Inline RECORD.Name tooltips |
| Class Members | Separate window | Inline class member display |
| Linter Issues | Separate panel | Inline with highlighted code |
| Quick Fix Discovery | N/A | Shows when fixes available |
| Reference Locations | Separate find | Inline reference list |
| Inheritance Info | Requires navigation | Shows inherited members inline |

**Key Advantages:**
- **Inline Information**: No need to switch contexts or open separate windows
- **Multiple Providers**: Comprehensive information from different perspectives
- **Type Intelligence**: Understands types without explicit annotations
- **Usage Analytics**: Shows how variables and methods are used
- **Performance**: Fast, cached lookups with lazy evaluation

---

## Extension and Customization

### Creating Custom Tooltip Providers

AppRefiner's tooltip system is extensible through plugins. To create custom tooltip providers:

1. **Create Plugin Project**: Reference AppRefiner in a new .NET 8 class library
2. **Inherit from BaseTooltipProvider**: Implement required properties
3. **Override Visitor Methods**: Process AST nodes of interest
4. **Register Tooltips**: Call `RegisterTooltip()` for source spans
5. **Deploy Plugin**: Copy DLL to Plugins directory

### BaseTooltipProvider API

Key members for custom tooltip providers:

```csharp
public abstract class BaseTooltipProvider : ScopedAstVisitor<object>
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual int Priority { get; } = 0;
    public virtual DataManagerRequirement DatabaseRequirement { get; }

    protected void RegisterTooltip(SourceSpan span, string tooltipText);
    protected VariableInfo? GetVariableAtPosition();
    protected IEnumerable<VariableInfo> GetVariablesAtPosition();
}
```

### Example Custom Provider

```csharp
public class MyTooltipProvider : BaseTooltipProvider
{
    public override string Name => "My Custom Tooltip";
    public override string Description => "Shows custom information";
    public override int Priority => 60;

    public override void VisitIdentifier(IdentifierNode node)
    {
        if (node.SourceSpan.ContainsPosition(CurrentPosition))
        {
            string tooltip = $"Custom info for: {node.Name}";
            RegisterTooltip(node.SourceSpan, tooltip);
        }
        base.VisitIdentifier(node);
    }
}
```

---

## Related Features

- **[Linters](working-with-linters.md)**: Provide the issues shown by Active Indicators
- **[Code Styling](code-styling.md)**: Stylers create highlights explained by tooltips
- **[Type Checking](type-checking.md)**: Powers type inference for tooltips
- **[Quick Fixes](quick-fixes.md)**: Available fixes shown in tooltips
- **[Navigation](navigation.md)**: Complement tooltips with go-to-definition
- **[Auto-Complete](auto-suggest.md)**: Works alongside tooltips for code intelligence

---

## Frequently Asked Questions

### Q: Can I change tooltip delay time?
**A**: Tooltip delay is controlled by Windows system settings, not AppRefiner. To change:
1. Open Windows Settings → Accessibility → Mouse pointer and touch
2. Adjust tooltip duration

### Q: Why do some tooltips show emojis and others don't?
**A**: Different providers use different formatting styles. Variable Info uses rich formatting with emojis for visual clarity, while others use simpler text formatting.

### Q: Can tooltips show for SQL code blocks?
**A**: Currently, tooltips are PeopleCode-specific and do not analyze SQL blocks. SQL formatting is a separate feature.

### Q: Do tooltips slow down the editor?
**A**: No. Tooltips use lazy evaluation and only process when you hover. AST parsing is cached, so subsequent tooltips are fast.

### Q: Can I see tooltip information for variables in other files?
**A**: Limited. Variable Info only tracks variables in the current file. For external App Classes, use "Application Class Details" (requires database).

### Q: Why doesn't Scope Context appear over code text?
**A**: By design. Scope Context only appears when hovering over leading whitespace to avoid interfering with other tooltips.

### Q: Can I combine tooltip information from multiple providers?
**A**: Yes, automatically. AppRefiner combines multiple tooltips when they apply to the same location, separated by blank lines.

---

## Next Steps

1. **Configure Providers**: Enable tooltip providers that match your workflow
2. **Test Hovering**: Try hovering over variables, methods, and highlighted code
3. **Explore Type Inference**: Use Inferred Type tooltip to understand expression types
4. **Review Linter Issues**: Use Active Indicators to investigate code quality issues
5. **Learn Keyboard Shortcuts**: Press `Ctrl+.` when tooltips show quick fix availability
6. **Try Database Features**: Connect database to enable PeopleSoft Object Info and Application Class Details
7. **Adjust Configuration**: Disable providers you don't find useful to reduce tooltip noise

---

*For more information on related features, see [Working with Linters](working-with-linters.md), [Type Checking](type-checking.md), and [Quick Fixes](quick-fixes.md).*
