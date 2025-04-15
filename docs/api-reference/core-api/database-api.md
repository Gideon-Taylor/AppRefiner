# Database API

The Database API provides interfaces and classes for interacting with PeopleSoft databases within AppRefiner plugins (like custom Linters, Tooltip Providers, etc.). This document describes the core components available to plugins when database access is enabled and configured.

## Overview

The primary interface for database interaction within a plugin is `IDataManager`. An instance of this interface is made available to plugin base classes (e.g., `BaseLintRule.DataManager`) when the plugin declares a database requirement (`Optional` or `Required`) and a valid database connection exists.

## `IDataManager` Interface

Provides methods to query PeopleSoft metadata and object definitions.

```csharp
public interface IDataManager : IDisposable
{
    // Connection Status
    IDbConnection Connection { get; } // Access to lower-level connection (use with caution)
    bool IsConnected { get; }

    // Connection Management (Typically handled by AppRefiner itself)
    // bool Connect();
    // void Disconnect();

    // SQL Definitions
    string GetSqlDefinition(string objectName);
    Dictionary<string, string> GetAllSqlDefinitions();

    // HTML Definitions
    HtmlDefinition GetHtmlDefinition(string objectName);
    Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions();

    // PeopleCode Items (e.g., for Project Linting)
    List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName);
    List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName);
    bool LoadPeopleCodeItemContent(PeopleCodeItem item); // Loads PROGTXT for an item

    // Application Classes
    bool CheckAppClassExists(string appClassPath);
    string? GetAppClassSourceByPath(string appClassPath);
    PackageItems GetAppPackageItems(string packagePath); // List subpackages/classes

    // Record Definitions
    List<RecordFieldInfo>? GetRecordFields(string recordName);
}
```

### Key Methods for Plugins

-   **`IsConnected` (bool)**: Check if a connection is active before attempting calls.
-   **`GetSqlDefinition(string objectName)`**: Retrieves the text of a specific SQL Object by name.
-   **`GetHtmlDefinition(string objectName)`**: Retrieves an `HtmlDefinition` object (containing content and max bind number) for an HTML object by name.
-   **`CheckAppClassExists(string appClassPath)`**: Verifies if an Application Class exists (e.g., `MY_PKG:Utils:MyClass`).
-   **`GetAppClassSourceByPath(string appClassPath)`**: Retrieves the source code for an existing Application Class.
-   **`GetAppPackageItems(string packagePath)`**: Gets lists of immediate subpackages and classes within a given package path (e.g., `MY_PKG` or `MY_PKG:Utils`). Returns a `PackageItems` object.
-   **`GetRecordFields(string recordName)`**: Retrieves metadata for all fields in a specified record definition. Returns a list of `RecordFieldInfo` objects, or `null` if the record doesn't exist. Uses internal caching based on record version.
-   *(Project-related methods are primarily used by the built-in Project Linter feature)*.

## `IDbConnection` Interface

Provides lower-level database access. **Use with caution within plugins.** Prefer using the specific methods on `IDataManager` where possible.

```csharp
public interface IDbConnection : IDisposable
{
    string ConnectionString { get; set; }
    ConnectionState State { get; }
    string ServerName { get; }
    void Open();
    void Close();
    IDbCommand CreateCommand();
    DataTable ExecuteQuery(string sql, Dictionary<string, object>? parameters = null);
    int ExecuteNonQuery(string sql, Dictionary<string, object>? parameters = null);
}
```

## Data Models

Common data structures returned by `IDataManager` methods:

### `PeopleCodeItem`

Represents a PeopleSoft object containing PeopleCode (RecordField, AppPackage Class, AppEngine Action, etc.).

```csharp
public class PeopleCodeItem
{
    // Object identifiers (7 pairs defining the unique object)
    public int[] ObjectIDs { get; }
    public string[] ObjectValues { get; }
    
    // Content properties (populated by LoadPeopleCodeItemContent)
    public byte[]? ProgramText { get; private set; }
    public List<NameReference> NameReferences { get; private set; }
    public PeopleCodeType PeopleCodeType { get; private set; }
    
    // Methods
    public string GetProgramTextAsString(Encoding? encoding = null); // Decodes ProgramText
    public string BuildPath(); // Creates a user-friendly path representation
    // ... other internal methods
}
```

### `HtmlDefinition`

Represents an HTML definition.

```csharp
public class HtmlDefinition
{
    public string Name { get; } // Should match requested name
    public string Content { get; } // The actual HTML content
    public int MaxBindNum { get; } // Highest %Bind(:N) number found
}
```

### `NameReference`

Represents a name reference entry from `PSPCMNAMEDT`.

```csharp
public class NameReference
{
    public int NameNum { get; } // The NAMENUM value
    public string RecName { get; } // The RECNAME value
    public string RefName { get; } // The REFNAME value
}
```

### `RecordFieldInfo`

Represents metadata for a field within a record definition.

```csharp
public class RecordFieldInfo
{
    public string FieldName { get; }
    public int FieldNum { get; }
    public int FieldType { get; } // PeopleSoft field type enum value
    public int Length { get; }
    public int DecimalPos { get; }
    public int UseEdit { get; } // Bitmask for key properties (PrimaryKey, SearchKey, etc.)
    // Helper properties for UseEdit flags can be added
}
```

### `PackageItems`

Represents the contents of an Application Package path.

```csharp
public record PackageItems(string Path, List<string> SubPackages, List<string> Classes);
```

### `PeopleCodeType` Enum

Standard PeopleSoft object type IDs for PeopleCode containers.

```csharp
public enum PeopleCodeType { /* ... values like ApplicationEngine, RecordField, ApplicationPackage ... */ }
```

## `DataManagerRequirement` Enum

Used by plugins (`BaseLintRule`, `BaseStyler`, etc.) to declare database needs.

```csharp
public enum DataManagerRequirement { NotRequired, Optional, Required }
```

## Best Practices for Plugins Using `IDataManager`

1.  **Check `IsConnected`**: Verify connection before making calls.
2.  **Declare Requirement**: Set `DatabaseRequirement` correctly in your plugin class.
3.  **Handle Nulls**: Methods like `GetRecordFields` can return `null` if objects aren't found.
4.  **Performance**: Database calls can be slow; use judiciously. Caching (like for `GetRecordFields`) is handled internally where feasible.
5.  **Error Handling**: Wrap database calls in `try-catch` if necessary, although `IDataManager` implementations should handle common errors.
