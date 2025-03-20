# Import Optimization

AppRefiner provides two powerful refactoring tools for managing import statements in your PeopleCode files: **Optimize Imports** and **Resolve Imports**.

## Overview

PeopleCode applications often require importing various packages, classes, and interfaces. Over time, as code evolves, import statements can become disorganized, redundant, or missing. AppRefiner's import optimization features help you maintain clean, efficient import statements.

## Optimize Imports

The **Optimize Imports** refactor helps you clean up and organize your existing import statements by:

### Key Features

1. **Removing Unused Imports**
   - Analyzes your code to identify which imports are actually used
   - Automatically removes imports that aren't referenced in your code
   - Preserves imports that are used, either directly or through wildcards

2. **Consolidating Imports**
   - Converts multiple imports from the same package into a single wildcard import
   - Only applies consolidation when there are more than 2 imports from the same package

3. **Organizing Imports by Package**
   - Groups imports by their root package
   - Orders imports alphabetically by package name
   - Maintains a clean, consistent structure

### How It Works

The Optimize Imports refactor:

1. Scans your code for all import statements
2. Tracks which imports are actually used in your code
3. Removes any imports that aren't referenced
4. Consolidates multiple imports from the same package into wildcards when appropriate
5. Organizes the remaining imports by package hierarchy
6. Replaces the original imports block with the optimized version

### Example

**Before Optimization:**

```peoplecode
import PT_PEOPLESOFT:API:Logger;
import MYAPP:Utilities;
import PT_PEOPLESOFT:API:Configuration;
import MYAPP:Constants;
import PT_PEOPLESOFT:API:Errors;
```

**After Optimization:**

```peoplecode
import MYAPP:Constants;
import MYAPP:Utilities;
import PT_PEOPLESOFT:API:*;
```

## Resolve Imports

The **Resolve Imports** refactor helps you ensure all class references have proper import statements by:

### Key Features

1. **Identifying Used Classes**
   - Scans your code for all fully qualified class references
   - Creates a list of unique class paths that need to be imported

2. **Creating Explicit Imports**
   - Generates explicit import statements for each class reference
   - Ensures all used classes are properly imported

3. **Organizing Imports Alphabetically**
   - Orders all imports alphabetically by the full class path
   - Creates a clean, consistent imports section

### How It Works

The Resolve Imports refactor:

1. Scans your code for all fully qualified class references (containing ":")
2. Builds a list of unique class paths that are used
3. Generates explicit import statements for each class path
4. Orders the imports alphabetically
5. Either replaces the existing imports block or adds a new one at the beginning of the file

### Example

If your code contains references to classes like:

```peoplecode
Local PT_PEOPLESOFT:API:Logger &logger = create PT_PEOPLESOFT:API:Logger();
Local MYAPP:Utilities:StringHelper &helper = create MYAPP:Utilities:StringHelper();
```

The Resolve Imports refactor will generate:

```peoplecode
import MYAPP:Utilities:StringHelper;
import PT_PEOPLESOFT:API:Logger;
```

## How to Use Import Optimization


### Using Keyboard Shortcuts

- For **Optimize Imports**: Use the Command Palette and type "Optimize Imports"
- For **Resolve Imports**: Press **Ctrl+Shift+I** in a PeopleCode file or use the Command Palette and type "Resolve Imports"

## Best Practices for Import Management

1. **Use Resolve Imports first**: If you have missing imports, run Resolve Imports to add them
2. **Then use Optimize Imports**: After resolving imports, run Optimize Imports to clean up and organize them
3. **Review the changes**: Always review the changes made by the refactoring tools
4. **Run regularly**: Use these tools regularly to maintain clean import statements

## When to Use Each Refactor

- **Use Optimize Imports when**:
  - You have unused imports that need to be removed
  - Your imports are disorganized
  - You want to consolidate multiple imports into wildcards

- **Use Resolve Imports when**:
  - You have fully qualified class references without imports
  - You want to ensure all used classes have explicit imports
  - You're starting a new file and want to generate all necessary imports

By combining these two powerful refactoring tools, you can ensure your PeopleCode files have clean, efficient, and properly organized import statements.
