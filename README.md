# AppRefiner

AppRefiner is a powerful companion application designed to enhance PeopleSoft's Application Designer with modern development features. It adds code folding, linting, syntax highlighting, and refactoring tools that are not available in the native Application Designer environment.

Rather than replacing Application Designer, AppRefiner seamlessly integrates with it to provide additional functionality while you work with PeopleCode. The most efficient way to access AppRefiner's features is through the Command Palette (Ctrl+Shift+P), which works similarly to VS Code and provides quick access to all functionality.

## Key Features

### Editor Enhancements
- **Code Folding**: Collapse and expand code blocks for better organization
- **SQL Formatting**: Improved edit-time formatting for SQL objects
- **Annotations**: In-line visual feedback directly in the editor
- **Dark Mode**: Support for dark mode in code editors

### Code Analysis
- **Linting**: Automatically detect issues like empty catch blocks, nested if-else statements, SQL wildcard usage, and more
- **SQL Validation**: Validate SQL parameter binding, detect potential SQL injection risks, and validate SQL definition references
- **Style Analysis**: Highlight style issues such as meaningless variable names, properties used as variables, and unused imports

### Code Refactoring
- **Variable Renaming**: Easily rename local variables, parameters, and more
- **Import Optimization**: Clean up and organize import statements
- **FlowerBox Headers**: Add proper documentation headers to your code

### Productivity Features
- **Command Palette**: Quick access to all features (Ctrl+Shift+P)
- **Templates**: Pre-built code templates for common patterns
- **Database Integration**: Connect to Oracle PeopleSoft databases to enhance linting capabilities
- **Global Hotkeys**: Keyboard shortcuts that work directly inside Application Designer

## Getting Started

### Prerequisites
- Windows operating system
- .NET 8 Runtime or SDK
- PeopleSoft Application Designer installed and functioning

### Installation
1. Download the latest AppRefiner release
2. Extract to a location of your choice
3. Run AppRefiner.exe
4. AppRefiner will automatically detect Application Designer and begin enhancing its functionality

### Keyboard Shortcuts
- **Ctrl+Shift+P**: Open Command Palette
- **Alt+Left/Right**: Collapse/Expand current section
- **Ctrl+Alt+Left/Right**: Collapse/Expand all top-level sections
- **Ctrl+Shift+R**: Rename local variable
- **Ctrl+Alt+L**: Trigger code linting

## Documentation

For more detailed information about AppRefiner's features and how to use them, please refer to the [documentation](docs/README.md).

## Extensibility

AppRefiner is designed to be extensible through plugins, allowing you to create custom functionality that integrates seamlessly with the application.

### Creating Custom Plugins

You can extend AppRefiner with your own custom Stylers, Linters, and Refactors by following these steps:

1. Clone or download the AppRefiner repository
2. Create a new .NET 8 class library project in Visual Studio
3. Add a reference to the AppRefiner project in your solution
4. Create your custom classes by inheriting from the base classes:
   - For Linters: Inherit from `BaseLinter` or `ScopedLinter` (for scope-aware linting)
   - For Stylers: Inherit from `BaseStyler` or `ScopedStyler` (for scope-aware styling)
   - For Refactors: Inherit from `BaseRefactor` or `ScopedRefactor` (for scope-aware refactoring)
5. Compile your project and copy the resulting DLL to the AppRefiner's Plugins directory
6. Restart AppRefiner to load your custom plugins

Your custom functionality will appear alongside the built-in features in the Command Palette and other relevant menus.

For more detailed information about the APIs available for plugin development, refer to the [Core API documentation](docs/api-reference/core-api/README.md).

### Requesting New Features

If you have ideas for new Linters, Stylers, or Refactors but don't want to implement them yourself, you can open an issue on our GitHub repository. While we can't guarantee implementation of all requests, we welcome community input to help guide AppRefiner's development priorities.

## Building from Source

If you want to build AppRefiner from source, follow these instructions:

### Build Requirements
- Windows operating system
- Visual Studio 2022 with C++ development tools installed
- .NET 8 SDK
- Java 17 or later (required for ANTLR parser generation)
- PowerShell 5.1 or higher

### Build Steps

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/AppRefiner.git
   cd AppRefiner
   ```

2. Run the build script:
   ```powershell
   # For framework-dependent build (default)
   .\build.ps1
   
   # For self-contained build (includes .NET runtime)
   .\build.ps1 -SelfContained
   ```

3. The build script will:
   - Check for required build tools
   - Restore NuGet dependencies
   - Build the AppRefinerHook C++ project
   - Build the AppRefiner .NET project
   - Copy necessary files to the output directory
   - Create a ZIP file with the date in the filename

4. The output will be available in:
   - `publish/framework/` for framework-dependent builds
   - `publish/self-contained/` for self-contained builds
   - A ZIP file in the root directory with format `AppRefiner-yyyy-MM-dd-[type].zip`

### Troubleshooting

- If you encounter issues related to MSBuild not finding the C++ toolset, ensure Visual Studio 2022 with C++ development tools is properly installed.
- For .NET related errors, ensure you have the .NET 8 SDK installed by running `dotnet --version`.

## License
See the [LICENSE](LICENSE) file for details.

## Contributing
Contributions are welcome! Please feel free to submit a Pull Request.
