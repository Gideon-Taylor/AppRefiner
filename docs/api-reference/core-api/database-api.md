# Database API

The Database API provides interfaces and classes for interacting with PeopleSoft databases in AppRefiner. This document describes the core components of the Database API and how to use them effectively.

## Overview

The Database API is designed to provide a consistent interface for accessing PeopleSoft database objects, regardless of the underlying database system. The main components of this API are:

- `IDataManager`: The main interface for database operations
- `IDbConnection`: Interface for database connections
- Data models: `PeopleCodeItem`, `HtmlDefinition`, and other model classes

## IDataManager

`IDataManager` is the primary interface for interacting with PeopleSoft databases:

```csharp
public interface IDataManager : IDisposable
{
    IDbConnection Connection { get; }
    bool IsConnected { get; }
    
    bool Connect();
    void Disconnect();
    
    string GetSqlDefinition(string objectName);
    Dictionary<string, string> GetAllSqlDefinitions();
    
    HtmlDefinition GetHtmlDefinition(string objectName);
    Dictionary<string, HtmlDefinition> GetAllHtmlDefinitions();
    
    List<PeopleCodeItem> GetPeopleCodeItemsForProject(string projectName);
    List<PeopleCodeItem> GetPeopleCodeItemMetadataForProject(string projectName);
    bool LoadPeopleCodeItemContent(PeopleCodeItem item);
}
```

### Key Properties and Methods

- `Connection`: Gets the underlying database connection
- `IsConnected`: Indicates whether the manager is currently connected
- `Connect()`: Establishes a connection to the database
- `Disconnect()`: Closes the database connection
- `GetSqlDefinition()`: Retrieves SQL definitions
- `GetHtmlDefinition()`: Retrieves HTML definitions
- `GetPeopleCodeItemsForProject()`: Retrieves PeopleCode items for a project
- `LoadPeopleCodeItemContent()`: Loads the content for a PeopleCode item

## IDbConnection

`IDbConnection` provides a database-agnostic way to interact with the underlying database:

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

### Key Properties and Methods

- `ConnectionString`: Gets or sets the connection string
- `State`: Gets the current state of the connection
- `ServerName`: Gets the name of the database server
- `Open()`: Opens the database connection
- `Close()`: Closes the database connection
- `ExecuteQuery()`: Executes a SQL query and returns results
- `ExecuteNonQuery()`: Executes a non-query SQL command

## Data Models

### PeopleCodeItem

Represents a PeopleSoft PeopleCode object:

```csharp
public class PeopleCodeItem
{
    // Object identifiers
    public int ObjectId1 { get; }
    public string ObjectValue1 { get; }
    public int ObjectId2 { get; }
    public string ObjectValue2 { get; }
    // ... additional object ID/value pairs
    
    // Content properties
    public byte[]? ProgramText { get; private set; }
    public List<NameReference> NameReferences { get; private set; }
    public PeopleCodeType PeopleCodeType { get; private set; }
    
    // Methods
    public void SetProgramText(byte[] programText);
    public void SetNameReferences(List<NameReference> nameReferences);
    public string GetProgramTextAsString(Encoding? encoding = null);
    public string BuildPath();
    public ProjectItem ToProjectItem();
    public PeopleCodeType DeriveObjectType();
}
```

### HtmlDefinition

Represents an HTML definition in PeopleSoft:

```csharp
public class HtmlDefinition
{
    public string Name { get; }
    public string Type { get; }
    public string Content { get; }
    
    public HtmlDefinition(string name, string type, string content);
}
```

### NameReference

Represents a reference to a name in PeopleCode:

```csharp
public class NameReference
{
    public int NameNum { get; }
    public string RecName { get; }
    public string RefName { get; }
    
    public NameReference(int nameNum, string recName, string refName);
}
```

### PeopleCodeType

Enumeration of PeopleCode object types:

```csharp
public enum PeopleCodeType
{
    ApplicationEngine = 66,
    ApplicationPackage = 104,
    ComponentInterface = 74,
    Component = 10,
    ComponentRecField = 999,
    ComponentRecord = 998,
    Menu = 3,
    Message = 997,
    Page = 9,
    RecordField = 1,
    Subscription = 996
}
```

## DataManagerRequirement

Enumeration specifying database requirements for features:

```csharp
public enum DataManagerRequirement
{
    NotRequired,
    Optional,
    Required
}
```

## Implementing a Custom Data Manager

To implement a custom data manager for a specific database system:

```csharp
public class MyCustomDataManager : IDataManager
{
    private readonly IDbConnection _connection;
    
    public IDbConnection Connection => _connection;
    public bool IsConnected => _connection.State == ConnectionState.Open;
    
    public MyCustomDataManager(string connectionString)
    {
        _connection = new MyCustomDbConnection(connectionString);
    }
    
    public bool Connect()
    {
        try
        {
            _connection.Open();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public void Disconnect()
    {
        _connection.Close();
    }
    
    // Implement other methods...
    
    public void Dispose()
    {
        _connection.Dispose();
    }
}
```

## Best Practices

1. **Connection Management**: Always properly open and close connections
2. **Error Handling**: Implement robust error handling for database operations
3. **Caching**: Consider caching frequently accessed data
4. **Security**: Store connection strings securely
5. **Transactions**: Use transactions for operations that modify data

## See Also

- [Linter API](linter-api.md)
- [Refactor API](refactor-api.md)
- [Styler API](styler-api.md)
