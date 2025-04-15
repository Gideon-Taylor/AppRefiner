# Custom Tooltip Providers

The Tooltip Provider API allows plugins to display custom informational tooltips when the user hovers over specific elements in the Application Designer code editor.

## Overview

Custom Tooltip Providers enhance the development experience by offering context-sensitive information directly at the cursor's hover location. This can be used for:

-   Displaying variable type information.
-   Showing function/method parameter details.
-   Providing documentation or usage examples for specific objects or functions.
-   Explaining custom syntax or framework elements.

Tooltip providers are queried automatically when AppRefiner detects a hover event (`SCN_DWELLSTART`) in an enhanced editor window.

## Creating a Custom Tooltip Provider Plugin

### 1. Project Setup

-   Create a new .NET Class Library project (targeting .NET Framework 4.8 or compatible).
-   Add references to necessary AppRefiner assemblies:
    -   `AppRefiner.exe` (or core assembly)
    -   `Antlr4.Runtime.Standard.dll` (Potentially needed if analyzing code context)
    -   `PeopleCodeParser.dll` (Potentially needed if analyzing code context)

### 2. Implement the Tooltip Provider Class

Custom tooltip providers implement the `AppRefiner.TooltipProviders.ITooltipProvider` interface. Often, it's easier to inherit from the abstract `AppRefiner.TooltipProviders.BaseTooltipProvider` class.

**Example: Simple Keyword Tooltip**

```csharp
using AppRefiner.TooltipProviders;
using AppRefiner.Database; // If DB access is needed
using System; // For StringComparison

namespace MyCompany.AppRefiner.CustomTooltips
{
    public class KeywordTooltipProvider : BaseTooltipProvider // Inheriting from BaseTooltipProvider is common
    {
        // --- Required Properties ---
        
        // Name used internally (doesn't have to be globally unique)
        public override string Name => "My Keyword Tooltip";
        
        // Description shown in the Tooltips Tab in AppRefiner UI
        public override string Description => "Provides help for specific keywords.";

        // --- Optional Properties ---

        // Higher priority providers get queried first.
        public override int Priority => 10; 

        // Specify if database access is needed
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired; 

        // --- Constructor ---
        public KeywordTooltipProvider()
        {
            // Set default state if needed
            Active = true; 
        }

        // --- Core Logic: GetTooltip ---
        
        // This method is called by AppRefiner when a hover occurs.
        public override string? GetTooltip(ScintillaEditor editor, int position)
        {
            // Get the word/token at the hover position
            string? word = ScintillaManager.GetWordAtPosition(editor, position);

            if (string.IsNullOrEmpty(word))
            {
                return null; // No word at position, return null to let other providers try
            }

            // Check if the word matches a keyword we care about (case-insensitive)
            if (word.Equals("MY_CUSTOM_FUNCTION", StringComparison.OrdinalIgnoreCase))
            {
                // Return the tooltip text
                // Basic formatting like newlines is usually supported.
                return "MyCustomFunction(param1 As String, param2 As Number)\n---\nThis function processes custom widget data.";
            }
            
            // Optionally, access database if needed:
            // if (this.DataManager != null && this.DatabaseRequirement != DataManagerRequirement.NotRequired)
            // {
            //     var recordInfo = this.DataManager.GetRecordFields(word.ToUpper());
            //     if (recordInfo != null) return $"Record: {word}\nFields: {recordInfo.Count}";
            // }

            // If we don't handle this word, return null
            return null;
        }

        // --- Optional Cleanup ---
        public override void OnHideTooltip(ScintillaEditor editor)
        {
            // Called when the tooltip is hidden. 
            // Use for any cleanup specific to the last shown tooltip, if necessary.
            base.OnHideTooltip(editor);
        }
    }
}
```

### 3. Implement Tooltip Logic

-   Implement the `GetTooltip(ScintillaEditor editor, int position)` method.
-   Use the provided `editor` and `position` to determine the context (e.g., get the word, analyze surrounding code using ANTLR parser if needed).
-   If your provider can offer a tooltip for the current context, return the string content.
-   If your provider cannot handle the current context, **return `null`** to allow other providers to be queried.
-   If database access is needed, set `DatabaseRequirement` appropriately and use the `this.DataManager` property.

### 4. Compile the Plugin

-   Build your Class Library project to produce a DLL.

## Key API Components Reference

### `ITooltipProvider` Interface

Defines the contract for tooltip providers.

-   **`Name` (string)**: Internal identifier.
-   **`Description` (string)**: User-facing description for UI.
-   **`Active` (bool)**: Gets/sets whether the provider is enabled.
-   **`Priority` (int)**: Determines query order (higher priority first).
-   **`DatabaseRequirement` (DataManagerRequirement)**: Declares database needs.
-   **`DataManager` (IDataManager?)**: Provides DB access.
-   **`GetTooltip(ScintillaEditor editor, int position)` (string?)**: Core method to return tooltip text or `null`.
-   **`OnHideTooltip(ScintillaEditor editor)`**: Optional cleanup method.

### `BaseTooltipProvider` Class

Abstract base class implementing `ITooltipProvider`. Provides default implementations.

-   Provides default `Active = true`, `Priority = 0`, `DatabaseRequirement = NotRequired`.
-   Implements `OnHideTooltip` as no-op.
-   Requires subclasses to implement `Name`, `Description`, and `GetTooltip`.

*(Helper utilities like `ScintillaManager.GetWordAtPosition` are available for interacting with the editor)*

## Installing the Plugin

1.  Compile your custom tooltip provider(s) into a DLL.
2.  Place the DLL in the AppRefiner **plugins directory**.
3.  Restart AppRefiner.

(Plugin directory location is configured on the Settings Tab).

## Managing Custom Tooltip Providers

Installed providers appear in the **Tooltips Tab** in the main AppRefiner window. You can enable or disable them using the checkboxes.

## Best Practices

-   **Performance**: `GetTooltip` is called on hover; keep it fast. Avoid complex parsing or slow DB queries if possible.
-   **Return Null**: If your provider doesn't apply to the context, return `null` quickly.
-   **Clarity**: Make tooltip content concise and informative.
-   **Priority**: Use `Priority` if your provider should preempt or follow others for specific contexts.
-   **DB Usage**: Only require database access if essential for the tooltip information. 