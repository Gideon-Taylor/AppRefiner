# AppRefiner

AppRefiner is a powerful code analysis and refactoring tool specifically designed for PeopleCode development. It helps developers identify potential issues, optimize code quality, and apply best practices through linting, styling, and automated refactoring.

## Features

### Code Analysis
- **Linting**: Automatically detect issues like:
  - Empty catch blocks
  - Nested if-else statements
  - Long expressions
  - SQL wildcard usage
  - Recursive function calls
  - Missing FlowerBox headers
  - And many more

### Code Refactoring
- **Automated Refactoring**: Improve your code with:
  - Local variable renaming
  - Import optimization
  - Add proper FlowerBox headers
  - SQL formatting and optimization

### Style Analysis
- **Code Styling**: Highlight style issues such as:
  - Meaningless variable names
  - Properties used as variables
  - Unused imports and variables

### Database Integration
- Connect to Oracle PeopleSoft databases
- Retrieve SQL definitions
- Get HTML definitions

### Editor Integration
- Integrates with Scintilla-based editors
- Applies syntax highlighting
- Provides code folding capabilities
- Supports annotations for visual feedback

### Productivity Features
- Global hotkey support for quick access
- Templates for common code patterns
- Dark mode support

## Getting Started

### Prerequisites
- Windows operating system
- .NET Framework 
- Oracle client (if connecting to Oracle databases)

### Installation
1. Clone this repository or download the latest release
2. Build the solution using Visual Studio
3. Run the AppRefiner.exe application

### Basic Usage
1. Launch the application
2. Use the hotkey (configurable) to analyze the code in your current editor
3. Review the reported issues
4. Apply suggested refactorings as needed

## Database Connection
For database-related functions:
1. Use the DB Connect dialog
2. Select your Oracle TNS name
3. Provide credentials
4. Connect and access database objects

## Templates
AppRefiner includes several code templates for common patterns:
- Decision inputs
- Grid/form events
- Plain classes

## License
See the [LICENSE](LICENSE) file for details.

## Contributing
Contributions are welcome! Please feel free to submit a Pull Request.
