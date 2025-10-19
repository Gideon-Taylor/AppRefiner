using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Database;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Represents a built-in PeopleCode object with its methods and properties.
/// Uses smart storage: hash tables for large objects (like System), linear structures for small objects.
/// </summary>
public class BuiltinObjectInfo : IObjectInfo
{
    /// <summary>
    /// Name of the object (e.g., "Rowset", "Field", "System")
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The PeopleCode type of this builtin object (e.g., Rowset, Field, System)
    /// </summary>
    public PeopleCodeType Type { get; set; } = PeopleCodeType.Any;

    public uint DefaultMethodHash { get; set; } = 0;

    /// <summary>
    /// Methods stored as hash->ComposableFunctionInfo dictionary for O(1) lookup
    /// </summary>
    public Dictionary<uint, FunctionInfo>? Methods { get; set; }

    /// <summary>
    /// Properties stored as hash->PropertyInfo dictionary for O(1) lookup
    /// </summary>
    public Dictionary<uint, PropertyInfo>? Properties { get; set; }

    /// <summary>
    /// Total number of methods in this object
    /// </summary>
    public int MethodCount => Methods?.Count ?? 0;

    /// <summary>
    /// Total number of properties in this object
    /// </summary>
    public int PropertyCount => Properties?.Count ?? 0;

    public BuiltinObjectInfo() { }

    public BuiltinObjectInfo(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Add a method to this object
    /// </summary>
    public void AddMethod(FunctionInfo method)
    {
        Methods ??= new Dictionary<uint, FunctionInfo>();
        var hash = HashUtilities.FNV1a32Hash(method.Name);
        Methods[hash] = method;
        if (method.IsDefaultMethod)
        {
            DefaultMethodHash = hash;
        }
    }

    /// <summary>
    /// Add a property to this object
    /// </summary>
    public void AddProperty(string name, PropertyInfo property)
    {
        Properties ??= new Dictionary<uint, PropertyInfo>();
        var hash = HashUtilities.FNV1a32Hash(name);
        Properties[hash] = property;
    }

    /// <summary>
    /// Lookup a method by name
    /// </summary>
    public FunctionInfo? LookupMethod(string methodName)
    {
        if (Methods == null) return null;
        var hash = HashUtilities.FNV1a32Hash(methodName);
        return Methods.TryGetValue(hash, out var method) ? method : null;
    }

    /// <summary>
    /// Lookup a method by hash (for internal use with binary format)
    /// </summary>
    public FunctionInfo? LookupMethodByHash(uint methodHash)
    {
        if (Methods == null) return null;
        return Methods.TryGetValue(methodHash, out var method) ? method : null;
    }

    /// <summary>
    /// Lookup a property by name
    /// </summary>
    public PropertyInfo? LookupProperty(string propertyName)
    {
        if (Properties == null) return null;
        var hash = HashUtilities.FNV1a32Hash(propertyName);
        return Properties.TryGetValue(hash, out var property) ? property : null;
    }

    /// <summary>
    /// Lookup a property by hash (for internal use with binary format)
    /// </summary>
    public PropertyInfo? LookupPropertyByHash(uint propertyHash)
    {
        if (Properties == null) return null;
        return Properties.TryGetValue(propertyHash, out var property) ? property : null;
    }

    /// <summary>
    /// Initialize empty collections if they don't exist (for System object)
    /// </summary>
    public void EnsureInitialized()
    {
        Methods ??= new Dictionary<uint, FunctionInfo>();
        Properties ??= new Dictionary<uint, PropertyInfo>();
    }

    /// <summary>
    /// Get all methods as enumerable
    /// </summary>
    public IEnumerable<FunctionInfo> GetAllMethods()
    {
        return Methods?.Values ?? Enumerable.Empty<FunctionInfo>();
    }

    /// <summary>
    /// Get all properties as enumerable (hash-based, cannot provide names easily)
    /// </summary>
    public IEnumerable<PropertyInfo> GetAllProperties()
    {
        return Properties?.Values ?? Enumerable.Empty<PropertyInfo>();
    }


    public override string ToString()
    {
        return $"{Name}: {MethodCount} methods (HashTable), {PropertyCount} properties (HashTable)";
    }
}

/// <summary>
/// Builder class for constructing BuiltinObjectInfo with a fluent API
/// </summary>
public class BuiltinObjectBuilder
{
    private readonly BuiltinObjectInfo _objectInfo;

    public BuiltinObjectBuilder(string name)
    {
        _objectInfo = new BuiltinObjectInfo(name);
    }

    /// <summary>
    /// Add a method to the object
    /// </summary>
    public BuiltinObjectBuilder AddMethod(FunctionInfo method)
    {
        _objectInfo.AddMethod(method);
        return this;
    }

    /// <summary>
    /// Add a method using the builder pattern
    /// </summary>
    public BuiltinObjectBuilder AddMethod(string name, Action<FunctionBuilder> configure)
    {
        var builder = new FunctionBuilder(name);
        configure(builder);
        _objectInfo.AddMethod(builder.Build());
        return this;
    }

    /// <summary>
    /// Add a property to the object
    /// </summary>
    public BuiltinObjectBuilder AddProperty(string name, PropertyInfo property)
    {
        _objectInfo.AddProperty(name, property);
        return this;
    }

    /// <summary>
    /// Add a scalar property
    /// </summary>
    public BuiltinObjectBuilder AddProperty(string name, PeopleCodeType type)
    {
        _objectInfo.AddProperty(name, PropertyInfo.CreateScalar(type));
        return this;
    }

    /// <summary>
    /// Add an array property
    /// </summary>
    public BuiltinObjectBuilder AddArrayProperty(string name, PeopleCodeType type, byte dimensionality)
    {
        _objectInfo.AddProperty(name, PropertyInfo.CreateArray(type, dimensionality));
        return this;
    }

    /// <summary>
    /// Add an AppClass property
    /// </summary>
    public BuiltinObjectBuilder AddAppClassProperty(string name, string appClassPath, byte arrayDimensionality = 0)
    {
        _objectInfo.AddProperty(name, PropertyInfo.CreateAppClass(appClassPath, arrayDimensionality));
        return this;
    }

    /// <summary>
    /// Ensure the object collections are initialized (for large objects like System)
    /// </summary>
    public BuiltinObjectBuilder EnsureInitialized()
    {
        _objectInfo.EnsureInitialized();
        return this;
    }

    /// <summary>
    /// Build the final BuiltinObjectInfo
    /// </summary>
    public BuiltinObjectInfo Build()
    {
        return _objectInfo;
    }
}

/// <summary>
/// Extension methods for BuiltinObjectInfo
/// </summary>
public static class BuiltinObjectInfoExtensions
{
    /// <summary>
    /// Create a builder for BuiltinObjectInfo
    /// </summary>
    public static BuiltinObjectBuilder CreateBuilder(string name)
    {
        return new BuiltinObjectBuilder(name);
    }

    /// <summary>
    /// Create the System object with existing function and system variable data
    /// </summary>
    public static BuiltinObjectInfo CreateSystemObject(
        IEnumerable<FunctionInfo> functions,
        IEnumerable<(string name, PropertyInfo property)> systemVariables)
    {
        var systemObject = new BuiltinObjectInfo("System");
        systemObject.EnsureInitialized(); // Initialize collections

        // Add all functions as methods
        foreach (var function in functions)
        {
            systemObject.AddMethod(function);
        }

        // Add all system variables as properties
        foreach (var (name, property) in systemVariables)
        {
            systemObject.AddProperty(name, property);
        }

        return systemObject;
    }
}
