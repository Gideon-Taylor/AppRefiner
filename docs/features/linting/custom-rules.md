# Custom Lint Rules

AppRefiner allows you to create and implement custom lint rules to enforce your team's specific coding standards and best practices.

## Overview

While AppRefiner comes with a comprehensive set of built-in lint rules, you may have specific requirements or coding standards unique to your organization. The custom lint rules feature allows you to extend AppRefiner's linting capabilities with your own rules.

## Creating a Custom Lint Rule

### Basic Structure

Custom lint rules in AppRefiner are created by extending the `BaseLintRule` class:

```csharp
using AppRefiner.Linters;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace MyCompany.AppRefiner.CustomLinters
{
    public class MyCustomLintRule : BaseLintRule
    {
        public override string LINTER_ID => "CUSTOM-001";
        
        public MyCustomLintRule()
        {
            Description = "Description of what this lint rule checks for";
            Type = ReportType.Warning; // Or Error, Information, etc.
            Active = true;
        }
        
        // Override parser listener methods to implement your rule
        public override void EnterSomeGrammarRule(SomeGrammarRuleContext context)
        {
            // Your lint rule logic here
            
            // If an issue is found, add a report
            AddReport(
                1, // Report number
                "Description of the issue found",
                ReportType.Warning,
                context.Start.Line,
                (context.Start.StartIndex, context.Stop.StopIndex)
            );
        }
    }
}
```

### Parser Listener Methods

Your custom lint rule can override any of the listener methods from the ANTLR4 parser to analyze different parts of the code. Common methods include:

- `EnterFunctionDeclaration` / `ExitFunctionDeclaration`
- `EnterMethodDeclaration` / `ExitMethodDeclaration`
- `EnterVariableDeclaration` / `ExitVariableDeclaration`
- `EnterIfStatement` / `ExitIfStatement`
- `EnterExpression` / `ExitExpression`

### Adding Reports

When your rule detects an issue, you can add a report to notify the user:

```csharp
AddReport(
    1, // Report number
    "Variable name does not follow naming convention",
    ReportType.Warning,
    context.Start.Line,
    (context.Start.StartIndex, context.Stop.StopIndex)
);
```

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

## Installing Custom Lint Rules

To install custom lint rules in AppRefiner:

1. Compile your custom lint rules into a DLL
2. Place the DLL in the AppRefiner plugins directory.
3. Restart AppRefiner

**Note**: The Plugin directoy can be changed via the "Plugins..." button on AppRefiner's main window.

## Managing Custom Lint Rules

Once installed, custom lint rules appear alongside built-in rules in the AppRefiner settings:

## Best Practices for Custom Lint Rules

1. **Use clear, descriptive IDs**: Make your LINTER_ID descriptive and include a company prefix
2. **Provide helpful messages**: Error messages should clearly explain the issue
3. **Test thoroughly**: Test your rules with various code patterns to avoid false positives
4. **Document your rules**: Create documentation explaining the purpose and rationale of each rule
5. **Version your rules**: Use versioning to track changes to your custom rules

## Advanced Features

### Rule Configuration
**Note**: Currently configurations for linter rules is not supported. This is a planned feature. Below is how it might look:

You can make your rules configurable by adding properties that can be set in the AppRefiner UI:

```csharp
public class ConfigurableFunctionLengthRule : BaseLintRule
{
    public int MaxFunctionLength { get; set; } = 100;
    
    public override string LINTER_ID => "CUSTOM-FUNC-002";
    
    // Implementation as before, but using MaxFunctionLength property
}
```

### Database Integration

If your lint rule needs to check against database objects, you can use the database integration:

```csharp
public class RecordFieldExistsRule : BaseLintRule
{
    public override string LINTER_ID => "CUSTOM-DB-001";
    
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;
    
    // Implementation that uses DataManager to validate data against database
}
```

## Related Features

- [Linting Overview](overview.md)
- [Available Lint Rules](available-rules.md)
- [Suppressing Lint Warnings](suppressing-warnings.md)
