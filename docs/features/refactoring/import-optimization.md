# Import Optimization

Import optimization in AppRefiner helps you manage import statements in your PeopleCode files, ensuring they are organized, necessary, and efficient.

## Overview

PeopleCode applications often require importing various packages, classes, and interfaces. Over time, as code evolves, import statements can become disorganized, redundant, or missing. AppRefiner's import optimization feature helps you maintain clean, efficient import statements.

## Key Features

### 1. Remove Unused Imports

AppRefiner can identify and remove import statements that aren't used in your code:

- **Usage analysis**: Scans your code to determine which imports are actually used
- **Safe removal**: Only removes imports that are definitely unused
- **Preservation options**: Optionally preserve certain imports even if unused

### 2. Add Missing Imports

AppRefiner can identify and add import statements that are needed but missing:

- **Reference detection**: Identifies unresolved references that require imports
- **Import suggestion**: Suggests appropriate import statements
- **Ambiguity resolution**: Helps resolve ambiguous references

### 3. Organize Imports

AppRefiner can reorganize import statements according to configurable rules:

- **Grouping**: Group imports by category (system, application, custom)
- **Sorting**: Sort imports alphabetically within groups
- **Formatting**: Apply consistent formatting to import statements
- **Duplicate removal**: Eliminate duplicate import statements

## How to Use Import Optimization

### Using the Context Menu

1. Right-click anywhere in a PeopleCode file
2. Select **Refactor > Optimize Imports**
3. Review the preview of changes
4. Click **Apply** to perform the optimization

### Using Keyboard Shortcuts

1. Press **Ctrl+R, O** in a PeopleCode file
2. Review the preview of changes
3. Press **Enter** to apply or **Escape** to cancel

### Using the Refactor Menu

1. Go to **Edit > Refactor > Optimize Imports**
2. Review the preview of changes
3. Click **Apply** to perform the optimization

### Automatic Import Optimization

You can configure AppRefiner to automatically optimize imports:

1. Go to **Tools > Options > AppRefiner > Code Editing**
2. Enable **Optimize imports on save**
3. Configure the optimization options

## Import Optimization Options

When optimizing imports, AppRefiner provides several configuration options:

### 1. Removal Options

- **Remove unused imports**: Whether to remove imports that aren't used
- **Keep commented imports**: Whether to preserve imports that have comments
- **Preserve specific imports**: List of imports to never remove even if unused

### 2. Organization Options

- **Group imports**: Whether to group imports by category
- **Sort imports**: Whether to sort imports alphabetically
- **Group order**: The order in which import groups should appear
- **Blank lines between groups**: Whether to add blank lines between groups

### 3. Formatting Options

- **Import statement style**: Format of import statements (one per line, grouped, etc.)
- **Indentation**: How to indent import statements
- **Comments**: How to handle comments associated with imports

## Examples of Import Optimization

### Before Optimization

```peoplecode
import PT_PEOPLESOFT:API:*;  /* Unused */
import PT_PEOPLESOFT:API:Logger;
import MYAPP:Utilities;
import PT_PEOPLESOFT:API:Configuration;
import java:util:*;
import MYAPP:Constants;  /* Unused */
import java:io:*;  /* Unused */
```

### After Optimization

```peoplecode
/* System imports */
import java:util:*;

/* PeopleSoft imports */
import PT_PEOPLESOFT:API:Configuration;
import PT_PEOPLESOFT:API:Logger;

/* Application imports */
import MYAPP:Utilities;
```

## Best Practices for Import Management

1. **Use specific imports**: Import specific classes rather than using wildcards when possible
2. **Organize imports logically**: Group related imports together
3. **Comment import purpose**: Add comments to explain non-obvious imports
4. **Regularly optimize**: Run import optimization regularly to keep imports clean
5. **Be consistent**: Follow consistent import organization patterns across your codebase

## Common Import Patterns in PeopleCode

AppRefiner supports various import organization patterns:

### 1. Category-Based Organization

```peoplecode
/* System imports */
import java:io:*;
import java:util:*;

/* PeopleSoft imports */
import PT_PEOPLESOFT:API:*;

/* Application imports */
import MYAPP:*;
```

### 2. Alphabetical Organization

```peoplecode
import java:io:*;
import java:util:*;
import MYAPP:Constants;
import MYAPP:Utilities;
import PT_PEOPLESOFT:API:Configuration;
import PT_PEOPLESOFT:API:Logger;
```

### 3. Purpose-Based Organization

```peoplecode
/* Utility imports */
import java:util:*;
import MYAPP:Utilities;

/* Configuration imports */
import PT_PEOPLESOFT:API:Configuration;
import MYAPP:Constants;

/* Logging imports */
import PT_PEOPLESOFT:API:Logger;
```

## Handling Special Cases

### 1. Imports with Side Effects

Some imports may have side effects (initialization code that runs when imported). AppRefiner preserves these imports even if they appear unused:

```peoplecode
import MYAPP:Initializer;  /* Has side effects - preserved */
```

### 2. Conditional Imports

AppRefiner handles conditional imports appropriately:

```peoplecode
If %ApplicationRelease >= "9.2" Then
   import PT_PEOPLESOFT:API:NewFeature;
Else
   import PT_PEOPLESOFT:API:LegacyFeature;
End-If;
```

### 3. Dynamic Imports

AppRefiner recognizes dynamic imports:

```peoplecode
Local string &packageName = "MYAPP:Modules:" | &moduleName;
import @(&packageName);
```

## Related Features

- [Refactoring Overview](overview.md)
- [Variable Renaming](variable-renaming.md)
- [FlowerBox Headers](flowerbox-headers.md)
