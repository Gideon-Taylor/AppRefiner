# Custom Lint Rules

AppRefiner allows you to create and implement custom lint rules to enforce your team's specific coding standards and best practices.

## Overview

While AppRefiner comes with a comprehensive set of built-in lint rules, you may have specific requirements or coding standards unique to your organization. The custom lint rules feature allows you to extend AppRefiner's linting capabilities with your own rules by creating plugins.

## Creating a Custom Lint Rule Plugin

### 1. Project Setup

- Create a new .NET Class Library project (targeting .NET Framework 4.8 or compatible with AppRefiner's target framework if different).
- Add references to:
    - `AppRefiner.exe` (or relevant AppRefiner core assembly)
    - `Antlr4.Runtime.Standard.dll` (Bundled with AppRefiner or available via NuGet)
    - `PeopleCodeParser.dll` (Bundled with AppRefiner)

### 2. Implement the Linter Class

Custom lint rules are created by extending the abstract `AppRefiner.Linters.BaseLintRule` class.

```csharp
using AppRefiner.Linters;
using AppRefiner.Database; // For IDataManager if needed
using AppRefiner.PeopleCode; // For parser context types
using Antlr4.Runtime; // For IToken
using System.Collections.Generic; // For IList
using static AppRefiner.PeopleCode.PeopleCodeParser; // Easier access to context types

namespace MyCompany.AppRefiner.CustomLinters
{
    public class MyCustomLintRule : BaseLintRule
    {
        // --- Required Overrides ---
        
        // Must provide a unique ID for the linter rule
        public override string LINTER_ID => "MYCOMPANY-001"; 

        // --- Constructor & Basic Properties ---
        public MyCustomLintRule()
        {
            // Set a user-friendly description for the UI
            Description = "Checks for my company's specific widget usage pattern";
            
            // Set the default report severity (cannot be changed by user currently)
            Type = ReportType.Warning; 
            
            // Set whether the rule is active by default
            Active = true; 
        }

        // --- Optional Overrides ---
        
        // Specify if database access is needed
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional; 

        // --- ANTLR Listener Implementation ---
        
        // Override relevant Enter/Exit methods from PeopleCodeParserBaseListener
        public override void EnterMethodCall(MethodCallContext context)
        {
            // Access database if needed and available
            if (this.DataManager != null && this.DatabaseRequirement != DataManagerRequirement.NotRequired)
            {
               // Use this.DataManager to query DB
            }

            // Your lint rule logic here...
            bool issueFound = CheckWidgetUsage(context);

            // --- Reporting Issues ---
            if (issueFound)
            {
                // Use AddReport to flag an issue
                AddReport(
                    reportNumber: 1, // A unique number (within this LINTER_ID) for this specific type of issue
                    message: "Widget used incorrectly.", 
                    type: ReportType.Error, // Override default severity if needed
                    line: context.Start.Line, // 1-based line number
                    span: (context.Start.StartIndex, context.Stop.StopIndex) // 0-based character indices
                );
            }
        }
        
        // Example helper method
        private bool CheckWidgetUsage(MethodCallContext context)
        {
            // Add complex logic here...
            return context.GetText().Contains("BadWidget");
        }

        // --- State Management --- 
        // (Not needed for simple rules, but useful for rules needing state across methods)
        public override void Reset()
        {
            // Called before processing a new document. 
            // Reset any internal state specific to the previous document.
            base.Reset(); // Ensures Reports list is cleared
        }
    }
}
```

### 3. Implement Analysis Logic

- Override `Enter*` and `Exit*` methods corresponding to the PeopleCode grammar rules (defined in `PeopleCodeParserBaseListener`) you need to inspect.
- Use the `context` parameter within these methods to examine the code structure and text.
- Access `this.DataManager` (after checking `DatabaseRequirement` and null) if you need database context.
- Call `AddReport(...)` when an issue is detected.

### 4. Compile the Plugin

- Build your Class Library project to produce a DLL (e.g., `MyCompany.AppRefiner.CustomLinters.dll`).

## Key API Components Reference

### `BaseLintRule` Class

Inherits from `Antlr4.Runtime.Tree.ParseTreeListener` (via `PeopleCodeParserBaseListener`).

-   **`LINTER_ID` (string, abstract override)**: REQUIRED. Unique identifier (e.g., "MYCOMPANY-NAMING-001"). Used internally and for suppression.
-   **`Description` (string)**: REQUIRED (in constructor). User-friendly description shown in the Linters Tab.
-   **`Type` (ReportType)**: REQUIRED (in constructor). Default severity for reports from this linter.
-   **`Active` (bool)**: REQUIRED (in constructor). Whether the linter is enabled by default.
-   **`DatabaseRequirement` (DataManagerRequirement, virtual override)**: OPTIONAL. Specify `NotRequired` (default), `Optional`, or `Required`. Controls access to `DataManager`.
-   **`DataManager` (IDataManager?)**: Provides access to database context if `DatabaseRequirement` allows and a connection exists.
-   **`Reports` (List<Report>?)**: Collection of issues found. Managed by the base class.
-   **`Comments` (IList<IToken>?)**: List of comments found by the lexer. Useful for rules analyzing comments.
-   **`SuppressionListener` (LinterSuppressionListener?)**: Internal listener used by `AddReport`.
-   **`Reset()` (virtual)**: Method to reset linter state before processing a new file. Call `base.Reset()` if overriding.
-   **`AddReport(int reportNumber, string message, ReportType type, int line, (int Start, int Stop) span)`**: Protected method to register an issue. It automatically checks suppression rules before adding.

### `Report` Class

Represents a single reported issue.

-   **`Message` (string)**: Human-readable issue description.
-   **`Type` (ReportType)**: Severity level for *this specific* report.
-   **`Line` (int)**: 1-based line number where the issue starts.
-   **`Span` ((int Start, int Stop))**: 0-based start and end character indices of the issue in the code.
-   **`LinterId` (string)**: ID of the rule that generated the report (set automatically).
-   **`ReportNumber` (int)**: Specific number for this type of issue within the linter (from `AddReport`).
-   **`GetFullId()` (string)**: Returns combined identifier (`LinterId:ReportNumber`) used for suppression.

### `ReportType` Enum

Defines report severity/type.

-   `GrayOut`: Suggests code should be visually de-emphasized (often used by Stylers).
-   `Style`: Style suggestions (lowest severity).
-   `Info`: Informational messages.
-   `Warning`: Potential problems.
-   `Error`: Critical issues that should likely be fixed.

### `DataManagerRequirement` Enum

Used in `BaseLintRule.DatabaseRequirement`.

-   `NotRequired`: Linter does not need DB access.
-   `Optional`: Linter can optionally use DB access for enhanced checks if available.
-   `Required`: Linter requires DB access to function correctly.

## Installing the Plugin

1. Compile your custom lint rules into a DLL.
2. Place the DLL in the AppRefiner plugins directory.
3. Restart AppRefiner.

The location of the plugins directory can be viewed or changed via the **Settings Tab** (using the "Set Plugin Directory" option or similar) in the main AppRefiner window.

## Managing Custom Linters

Once installed and AppRefiner is restarted, your custom linter(s) will appear in the **Linters Tab** alongside the built-in rules. You can enable or disable them using the checkboxes or the corresponding `Lint: Toggle [Your Linter Description]` command in the Command Palette.

## Best Practices

1.  **Unique & Clear IDs**: Use a clear convention (e.g., `COMPANY-CATEGORY-NUM`).
2.  **Actionable Messages**: Explain the issue clearly.
3.  **Performance**: Avoid excessively complex logic in listener methods.
4.  **Test Thoroughly**: Check against various code examples, including edge cases.
5.  **Consider `DatabaseRequirement`**: Only require DB access if essential.
6.  **Document**: Explain the rule's purpose.

## Advanced Features (Future / Not Implemented)

-   **Rule Configuration**: Currently, custom linters cannot expose configuration options through the UI. This is a potential future enhancement.

## Example Custom Lint Rules

### Function Length Checker

This rule checks if functions exceed a maximum length:

```csharp
public class FunctionLengthRule : BaseLintRule
{
    private const int MAX_FUNCTION_LENGTH = 100;
    private int startLine;
    
    public override string LINTER_ID => "CUSTOM-FUNC-001";
    
    public FunctionLengthRule()
    {
        Description = "Functions should not exceed 100 lines";
        Type = ReportType.Warning;
        Active = true;
    }
    
    public override void EnterFunctionDeclaration(FunctionDeclarationContext context)
    {
        startLine = context.Start.Line;
    }
    
    public override void ExitFunctionDeclaration(FunctionDeclarationContext context)
    {
        int functionLength = context.Stop.Line - startLine + 1;
        
        if (functionLength > MAX_FUNCTION_LENGTH)
        {
            AddReport(
                1, // Report number
                $"Function exceeds maximum length of {MAX_FUNCTION_LENGTH} lines (actual: {functionLength})",
                ReportType.Warning,
                startLine,
                (context.Start.StartIndex, context.Stop.StopIndex)
            );
        }
    }
}
```

### Company Naming Convention

This rule enforces a specific naming convention for variables:

```csharp
public class NamingConventionRule : BaseLintRule
{
    private readonly Regex localVarPattern = new Regex(@"^&l[A-Z][a-zA-Z0-9]*$");
    
    public override string LINTER_ID => "CUSTOM-VAR-001";
    
    public NamingConventionRule()
    {
        Description = "Local variables should follow the pattern &lCamelCase";
        Type = ReportType.Warning;
        Active = true;
    }
    
    public override void EnterVariableDeclaration(VariableDeclarationContext context)
    {
        if (context.LOCAL() != null)
        {
            string varName = context.variableName().GetText();
            
            if (!localVarPattern.IsMatch(varName))
            {
                AddReport(
                    1, // Report number
                    $"Local variable '{varName}' does not follow the naming convention &lCamelCase",
                    ReportType.Warning,
                    context.Start.Line,
                    (context.variableName().Start.StartIndex, context.variableName().Stop.StopIndex)
                );
            }
        }
    }
}
```

## Related Features

- [Linting Overview](../features/linting/overview.md) 