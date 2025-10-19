using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Database;

/// <summary>
/// Stub implementation of PeopleCode type database.
/// This will eventually load and query type information from a typeinfo.dat file,
/// but for now returns null/empty results to allow compilation.
/// TODO: Implement actual data loading from binary format
/// </summary>
public class PeopleCodeTypeDatabase
{
    /// <summary>
    /// Creates a new stub database instance.
    /// </summary>
    public PeopleCodeTypeDatabase()
    {
        // TODO: Initialize with actual data loading
    }

    /// <summary>
    /// Gets a builtin object by name (case-insensitive).
    /// </summary>
    /// <param name="name">The name of the object (e.g., "Rowset", "System").</param>
    /// <returns>The BuiltinObjectInfo if found, null otherwise.</returns>
    public BuiltinObjectInfo? GetObject(string name)
    {
        // TODO: Load from binary format
        return null;
    }

    /// <summary>
    /// Gets a method from an object by name.
    /// </summary>
    /// <param name="objectName">The object name (e.g., "System").</param>
    /// <param name="methodName">The method name (e.g., "abs").</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    public FunctionInfo? GetMethod(string objectName, string methodName)
    {
        // TODO: Load from binary format
        return null;
    }

    /// <summary>
    /// Gets all methods for an object.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <returns>Enumerable of FunctionInfo for the object.</returns>
    public IEnumerable<FunctionInfo> GetAllMethods(string objectName)
    {
        // TODO: Load from binary format
        return Enumerable.Empty<FunctionInfo>();
    }

    /// <summary>
    /// Gets a property from an object by name.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    public PropertyInfo? GetProperty(string objectName, string propertyName)
    {
        // TODO: Load from binary format
        return null;
    }

    /// <summary>
    /// Gets all properties for an object.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <returns>Enumerable of PropertyInfo for the object.</returns>
    public IEnumerable<PropertyInfo> GetAllProperties(string objectName)
    {
        // TODO: Load from binary format
        return Enumerable.Empty<PropertyInfo>();
    }

    /// <summary>
    /// Resolves a type by name.
    /// For polymorphic types like $same, $element, returns appropriate PolymorphicTypeInfo.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <param name="contextObject">Optional context object for resolution.</param>
    /// <param name="contextType">Optional context type for resolution.</param>
    /// <returns>The resolved TypeInfo.</returns>
    public TypeInfo ResolveType(string typeName, BuiltinObjectInfo? contextObject = null, TypeWithDimensionality? contextType = null)
    {
        // Handle polymorphic types
        if (typeName == "$same")
        {
            return new SameAsObjectTypeInfo();
        }
        if (typeName == "$element")
        {
            return new ElementOfObjectTypeInfo();
        }
        if (typeName == "$same_as_first")
        {
            return new SameAsFirstParameterTypeInfo();
        }
        if (typeName == "$array_of_first")
        {
            return new ArrayOfFirstParameterTypeInfo();
        }

        // TODO: Implement lookup for builtin types and app classes
        return UnknownTypeInfo.Instance;
    }

    /// <summary>
    /// Gets a global function by name (functions that exist in System scope).
    /// </summary>
    /// <param name="functionName">The function name.</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    public FunctionInfo? GetFunction(string functionName)
    {
        // TODO: Load from binary format - this would query System object's methods
        return null;
    }

    /// <summary>
    /// Gets a system variable (global variable) by name.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    public PropertyInfo? GetSystemVariable(string variableName)
    {
        // TODO: Load from binary format - this would query System object's properties
        return null;
    }
}

/// <summary>
/// Interface for custom type resolution (for application-specific types).
/// TODO: Implement when needed for external program resolution
/// </summary>
public interface ICustomTypeResolver
{
    /// <summary>
    /// Resolves a custom type (typically an AppClass).
    /// </summary>
    /// <param name="typeName">The fully qualified type name.</param>
    /// <returns>The IObjectInfo if resolved, null otherwise.</returns>
    IObjectInfo? ResolveCustomType(string typeName);
}
