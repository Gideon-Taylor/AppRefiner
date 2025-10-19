using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.HashTable;
using PeopleCodeTypeInfo.Types;
using System.Collections.Concurrent;
using System.Reflection;

namespace PeopleCodeTypeInfo.Database;

/// <summary>
/// Static database providing type information for builtin PeopleCode objects, methods, and properties.
/// Loads data from embedded typeinfo.dat resource using lazy initialization.
/// Thread-safe with LRU caching for performance.
/// </summary>
public static class PeopleCodeTypeDatabase
{
    private static readonly Lazy<DatabaseState> _state = new(() => Initialize());

    /// <summary>
    /// Maximum number of items to cache (default: 256).
    /// Must be set before first access to take effect.
    /// </summary>
    public static int MaxCacheSize { get; set; } = 256;

    private static DatabaseState Initialize()
    {
        // Load embedded resource
        var assembly = typeof(PeopleCodeTypeDatabase).Assembly;
        var resourceName = "PeopleCodeTypeInfo.typeinfo.dat";
        var tempPath = ExtractEmbeddedResource(assembly, resourceName);

        try
        {
            var reader = BuiltinObjectReaderStrategyExtensions.LoadBuiltinObjectHashFile(tempPath);
            var state = new DatabaseState(reader, MaxCacheSize);

            // Verify the binary format (basic smoke test)
            VerifyBinaryFormat(state);

            return state;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
    }

    private static string ExtractEmbeddedResource(Assembly assembly, string resourceName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"typeinfo_{Guid.NewGuid()}.dat");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'");
        }

        using var fileStream = File.Create(tempPath);
        stream.CopyTo(fileStream);

        return tempPath;
    }

    private static void VerifyBinaryFormat(DatabaseState state)
    {
        // Basic smoke test: Ensure "System" object exists and has methods
        var systemObj = state.GetObject("System");
        if (systemObj == null || systemObj.MethodCount == 0)
        {
            throw new InvalidDataException("Invalid typeinfo.dat: Missing core 'System' object or methods.");
        }
    }

    /// <summary>
    /// Gets a builtin object by name (case-insensitive).
    /// </summary>
    /// <param name="name">The name of the object (e.g., "Rowset", "System").</param>
    /// <returns>The BuiltinObjectInfo if found, null otherwise.</returns>
    public static BuiltinObjectInfo? GetObject(string name)
        => _state.Value.GetObject(name);

    /// <summary>
    /// Gets a method from a builtin object by name.
    /// </summary>
    /// <param name="objectName">The object name (e.g., "System").</param>
    /// <param name="methodName">The method name (e.g., "abs").</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    public static FunctionInfo? GetMethod(string objectName, string methodName)
        => _state.Value.GetMethod(objectName, methodName);

    /// <summary>
    /// Gets all methods for a builtin object.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <returns>Enumerable of FunctionInfo for the object.</returns>
    public static IEnumerable<FunctionInfo> GetAllMethods(string objectName)
        => _state.Value.GetAllMethods(objectName);

    /// <summary>
    /// Gets a property from a builtin object by name.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    public static Functions.PropertyInfo? GetProperty(string objectName, string propertyName)
        => _state.Value.GetProperty(objectName, propertyName);

    /// <summary>
    /// Gets all properties for a builtin object.
    /// </summary>
    /// <param name="objectName">The object name.</param>
    /// <returns>Enumerable of PropertyInfo for the object.</returns>
    public static IEnumerable<Functions.PropertyInfo> GetAllProperties(string objectName)
        => _state.Value.GetAllProperties(objectName);

    /// <summary>
    /// Gets a global builtin function by name (functions in System scope).
    /// </summary>
    /// <param name="functionName">The function name.</param>
    /// <returns>The FunctionInfo if found, null otherwise.</returns>
    public static FunctionInfo? GetFunction(string functionName)
        => GetMethod("System", functionName);

    /// <summary>
    /// Gets a system variable (global builtin variable) by name.
    /// </summary>
    /// <param name="variableName">The variable name.</param>
    /// <returns>The PropertyInfo if found, null otherwise.</returns>
    public static Functions.PropertyInfo? GetSystemVariable(string variableName)
        => GetProperty("System", variableName);

    /// <summary>
    /// Resolves a builtin type by name.
    /// Returns polymorphic TypeInfo for $same, $element, etc.
    /// Returns concrete TypeInfo for builtin types like "string", "Record", etc.
    /// Returns UnknownTypeInfo for unrecognized types.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <returns>The resolved TypeInfo.</returns>
    public static Types.TypeInfo ResolveType(string typeName)
    {
        // Handle polymorphic types
        if (typeName == "$same") return SameAsObjectTypeInfo.Instance;
        if (typeName == "$element") return ElementOfObjectTypeInfo.Instance;
        if (typeName == "$same_as_first") return SameAsFirstParameterTypeInfo.Instance;
        if (typeName == "$array_of_first") return ArrayOfFirstParameterTypeInfo.Instance;

        // Try builtin type resolution
        var peopleCodeType = BuiltinTypeExtensions.FromString(typeName);
        if (peopleCodeType != PeopleCodeType.Unknown)
        {
            return Types.TypeInfo.FromPeopleCodeType(peopleCodeType);
        }

        return new UnknownTypeInfo(typeName);
    }

    /// <summary>
    /// Generates a signature string for a FunctionInfo.
    /// </summary>
    /// <param name="method">The FunctionInfo.</param>
    /// <param name="methodName">Optional name to use if method.Name is empty.</param>
    /// <returns>The generated signature string.</returns>
    public static string GetSignature(FunctionInfo method, string? methodName = null)
    {
        if (string.IsNullOrEmpty(method.Name))
        {
            methodName ??= "UnnamedMethod";
        }
        else
        {
            methodName = method.Name;
        }

        var paramStr = string.Join(", ", method.Parameters.Select(p => p.ToString()));
        var returnStr = method.ReturnType.ToString();
        if (method.IsUnionReturn && method.ReturnUnionTypes != null)
        {
            returnStr = string.Join("|", method.ReturnUnionTypes.Select(t => t.ToString()));
        }

        return $"{methodName}({paramStr}) -> {returnStr}";
    }

    /// <summary>
    /// Internal state holder for the database
    /// </summary>
    private class DatabaseState
    {
        private readonly HashTableReader<BuiltinObjectInfo> _reader;
        private readonly ConcurrentDictionary<string, BuiltinObjectInfo> _objectCache;
        private readonly ConcurrentDictionary<string, object> _namedItemCache;
        private readonly ConcurrentQueue<string> _objectLruQueue;
        private readonly ConcurrentQueue<string> _namedItemLruQueue;
        private readonly int _maxCacheSize;

        public DatabaseState(HashTableReader<BuiltinObjectInfo> reader, int maxCacheSize)
        {
            _reader = reader;
            _maxCacheSize = maxCacheSize;
            _objectCache = new ConcurrentDictionary<string, BuiltinObjectInfo>();
            _objectLruQueue = new ConcurrentQueue<string>();
            _namedItemCache = new ConcurrentDictionary<string, object>();
            _namedItemLruQueue = new ConcurrentQueue<string>();
        }

        public BuiltinObjectInfo? GetObject(string name)
        {
            var cacheKey = name.ToLowerInvariant();
            if (_maxCacheSize > 0 && _objectCache.TryGetValue(cacheKey, out var cachedObj))
            {
                _objectLruQueue.Enqueue(cacheKey);
                return cachedObj;
            }

            var obj = BuiltinObjectReaderStrategyExtensions.LookupObject(_reader, name);
            if (obj != null && _maxCacheSize > 0)
            {
                EvictIfNeeded(_objectCache, _objectLruQueue);
                _objectCache[cacheKey] = obj;
                _objectLruQueue.Enqueue(cacheKey);
            }
            return obj;
        }

        public FunctionInfo? GetMethod(string objectName, string methodName)
        {
            var obj = GetObject(objectName) as IObjectInfo;
            if (obj == null) return null;

            var methodHash = HashUtilities.FNV1a32Hash(methodName);
            var cacheKey = $"{objectName.ToLowerInvariant()}:{methodHash}";

            if (_maxCacheSize > 0 && _namedItemCache.TryGetValue(cacheKey, out var cachedItem) && cachedItem is FunctionInfo cachedMethod)
            {
                _namedItemLruQueue.Enqueue(cacheKey);
                cachedMethod.Name = methodName;
                return cachedMethod;
            }

            var method = obj.LookupMethodByHash(methodHash);
            if (method != null)
            {
                method.Name = methodName;
                if (_maxCacheSize > 0)
                {
                    EvictIfNeeded(_namedItemCache, _namedItemLruQueue);
                    _namedItemCache[cacheKey] = method;
                    _namedItemLruQueue.Enqueue(cacheKey);
                }
            }
            return method;
        }

        public IEnumerable<FunctionInfo> GetAllMethods(string objectName)
        {
            var obj = GetObject(objectName);
            return obj?.GetAllMethods() ?? Enumerable.Empty<FunctionInfo>();
        }

        public Functions.PropertyInfo? GetProperty(string objectName, string propertyName)
        {
            var obj = GetObject(objectName) as IObjectInfo;
            if (obj == null) return null;

            var propertyHash = HashUtilities.FNV1a32Hash(propertyName);
            var cacheKey = $"{objectName.ToLowerInvariant()}:{propertyHash}_prop";

            if (_maxCacheSize > 0 && _namedItemCache.TryGetValue(cacheKey, out var cachedItem) && cachedItem is Functions.PropertyInfo cachedProp)
            {
                _namedItemLruQueue.Enqueue(cacheKey);
                return cachedProp;
            }

            var property = obj.LookupPropertyByHash(propertyHash);
            if (property != null && _maxCacheSize > 0)
            {
                EvictIfNeeded(_namedItemCache, _namedItemLruQueue);
                _namedItemCache[cacheKey] = property;
                _namedItemLruQueue.Enqueue(cacheKey);
            }
            return property;
        }

        public IEnumerable<Functions.PropertyInfo> GetAllProperties(string objectName)
        {
            var obj = GetObject(objectName);
            return obj?.GetAllProperties() ?? Enumerable.Empty<Functions.PropertyInfo>();
        }

        private void EvictIfNeeded(ConcurrentDictionary<string, object> cache, ConcurrentQueue<string> lruQueue)
        {
            while (cache.Count > _maxCacheSize)
            {
                if (lruQueue.TryDequeue(out var key))
                {
                    cache.TryRemove(key, out _);
                }
            }
        }

        private void EvictIfNeeded(ConcurrentDictionary<string, BuiltinObjectInfo> cache, ConcurrentQueue<string> lruQueue)
        {
            while (cache.Count > _maxCacheSize)
            {
                if (lruQueue.TryDequeue(out var key))
                {
                    cache.TryRemove(key, out _);
                }
            }
        }
    }
}
