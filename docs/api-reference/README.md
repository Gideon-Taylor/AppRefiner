# AppRefiner API Reference

This section provides detailed information about the programmatic interfaces of AppRefiner, focusing on how to extend its functionality and understand its core components.

## Extending AppRefiner (Plugins)

AppRefiner allows extension through custom plugins implemented in .NET DLLs. These plugins integrate seamlessly into the AppRefiner UI and analysis engine. See the following guides for details on creating each type of plugin:

- **[Creating Custom Linters](core-api/custom-linters.md)**: Extend code analysis with custom rules.
- **[Creating Custom Stylers](core-api/custom-stylers.md)**: Define new syntax highlighting or visual code markers.
- **[Creating Custom Refactors](core-api/custom-refactors.md)**: Implement custom code transformations.
- **[Creating Custom Tooltip Providers](core-api/custom-tooltips.md)**: Provide context-sensitive information on hover.

## Core API Reference

This provides detailed documentation on the base classes and interfaces used by AppRefiner and its plugins:

- **[Tooltip Provider API](core-api/custom-tooltips.md#key-api-components-reference)**: Details on `ITooltipProvider` / `BaseTooltipProvider`.
- **[Database API](core-api/database-api.md)**: Details on `IDataManager` interface for accessing database context within plugins.

*(Placeholder for any other relevant API details)*
