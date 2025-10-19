using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Database;

/// <summary>
/// Interface representing a PeopleCode object (builtin or app class) with methods and properties.
/// Allows uniform handling of objects from the typeinfo.dat (builtins) and runtime-resolved app classes.
/// </summary>
public interface IObjectInfo
{
    /// <summary>
    /// The name of the object (e.g., "Rowset", "MYAPP:MyClass").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The base type of the object (e.g., PeopleCodeType.BuiltinObject, AppClass).
    /// </summary>
    PeopleCodeType Type { get; }

    /// <summary>
    /// Number of methods in this object.
    /// </summary>
    int MethodCount { get; }

    /// <summary>
    /// Number of properties in this object.
    /// </summary>
    int PropertyCount { get; }

    /// <summary>
    /// Looks up a method by name (case-insensitive; computes hash if needed).
    /// </summary>
    /// <param name="methodName">The method name.</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    FunctionInfo? LookupMethod(string methodName);

    /// <summary>
    /// Looks up a method by hash (for internal use with binary format).
    /// </summary>
    /// <param name="methodHash">The FNV-1a hash of the method name.</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    FunctionInfo? LookupMethodByHash(uint methodHash);

    /// <summary>
    /// Looks up a property by name (case-insensitive; computes hash if needed).
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    PropertyInfo? LookupProperty(string propertyName);

    /// <summary>
    /// Looks up a property by hash (for internal use with binary format).
    /// </summary>
    /// <param name="propertyHash">The FNV-1a hash of the property name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    PropertyInfo? LookupPropertyByHash(uint propertyHash);

    /// <summary>
    /// Gets all methods for this object.
    /// </summary>
    /// <returns>Enumerable of FunctionInfo.</returns>
    IEnumerable<FunctionInfo> GetAllMethods();

    /// <summary>
    /// Gets all properties for this object.
    /// </summary>
    /// <returns>Enumerable of PropertyInfo.</returns>
    IEnumerable<PropertyInfo> GetAllProperties();
}

/// <summary>
/// Extension methods for IObjectInfo to compute hashes consistently.
/// </summary>
public static class ObjectInfoExtensions
{
    public static uint GetMethodHash(string methodName) => HashUtilities.FNV1a32Hash(methodName);
    public static uint GetPropertyHash(string propertyName) => HashUtilities.FNV1a32Hash(propertyName);
}
