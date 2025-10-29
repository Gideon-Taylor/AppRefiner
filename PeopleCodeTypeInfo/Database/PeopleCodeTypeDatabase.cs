using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.HashTable;
using PeopleCodeTypeInfo.Types;
using System.Collections.Concurrent;
using System.Reflection;

namespace PeopleCodeTypeInfo.Database;

/// <summary>
/// Static database providing type information for builtin PeopleCode objects, methods, and properties.
/// Loads data from embedded typeinfo.dat resource using lazy initialization.
/// Thread-safe with generational cache eviction for performance.
/// </summary>
public static class PeopleCodeTypeDatabase
{
    private static readonly Lazy<DatabaseState> _state = new(() => Initialize());

    private static DatabaseState Initialize()
    {
        // Load embedded resource
        var assembly = typeof(PeopleCodeTypeDatabase).Assembly;
        var resourceName = "PeopleCodeTypeInfo.typeinfo.dat";
        var tempPath = ExtractEmbeddedResource(assembly, resourceName);

        try
        {
            var reader = BuiltinObjectReaderStrategyExtensions.LoadBuiltinObjectHashFile(tempPath);
            var state = new DatabaseState(reader);

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
        /// <summary>
        /// Wrapper for cache entries that tracks last access generation
        /// </summary>
        private class CacheEntry<T>
        {
            public T Value { get; }
            public long LastAccessGeneration { get; set; }

            public CacheEntry(T value, long generation)
            {
                Value = value;
                LastAccessGeneration = generation;
            }
        }

        private readonly HashTableReader<BuiltinObjectInfo> _reader;
        private readonly ConcurrentDictionary<string, CacheEntry<BuiltinObjectInfo>> _objectCache;
        private readonly ConcurrentDictionary<string, CacheEntry<object>> _namedItemCache;
        private long _currentGeneration;
        private readonly int _generationsBeforeCleanup;
        private readonly int _generationEvictionThreshold;
        private readonly ReaderWriterLockSlim _evictionLock = new();

        public DatabaseState(HashTableReader<BuiltinObjectInfo> reader)
        {
            _reader = reader;
            // Use aggressive defaults: cleanup every 5000 operations, evict after 2500
            _generationsBeforeCleanup = 5000;
            _generationEvictionThreshold = 2500;
            _objectCache = new ConcurrentDictionary<string, CacheEntry<BuiltinObjectInfo>>();
            _namedItemCache = new ConcurrentDictionary<string, CacheEntry<object>>();
        }

        public BuiltinObjectInfo? GetObject(string name)
        {
            // Increment generation counter
            var generation = Interlocked.Increment(ref _currentGeneration);

            var cacheKey = name.ToLowerInvariant();

            // Acquire read lock to prevent eviction during access
            _evictionLock.EnterReadLock();
            try
            {
                // Check cache
                if (_objectCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    // Update last access generation
                    cachedEntry.LastAccessGeneration = generation;

                    // Check if we need to trigger eviction
                    if (generation % _generationsBeforeCleanup == 0)
                    {
                        _evictionLock.ExitReadLock();
                        EvictStaleEntries();
                        return cachedEntry.Value;
                    }

                    return cachedEntry.Value;
                }

                // Check eviction even on cache miss
                if (generation % _generationsBeforeCleanup == 0)
                {
                    _evictionLock.ExitReadLock();
                    EvictStaleEntries();
                    // Don't reacquire lock, we're about to do a read operation
                }
                else
                {
                    _evictionLock.ExitReadLock();
                }

                // Lookup from reader (outside of lock)
                var obj = BuiltinObjectReaderStrategyExtensions.LookupObject(_reader, name);

                // Cache the result
                if (obj != null)
                {
                    var entry = new CacheEntry<BuiltinObjectInfo>(obj, generation);
                    _objectCache[cacheKey] = entry;
                }

                return obj;
            }
            finally
            {
                if (_evictionLock.IsReadLockHeld)
                {
                    _evictionLock.ExitReadLock();
                }
            }
        }

        public FunctionInfo? GetMethod(string objectName, string methodName)
        {
            var obj = GetObject(objectName) as IObjectInfo;
            if (obj == null) return null;

            // Increment generation counter for method lookup
            var generation = Interlocked.Increment(ref _currentGeneration);

            var methodHash = HashUtilities.FNV1a32Hash(methodName);
            var cacheKey = $"{objectName.ToLowerInvariant()}:{methodHash}";

            // Acquire read lock to prevent eviction during access
            _evictionLock.EnterReadLock();
            try
            {
                // Check cache
                if (_namedItemCache.TryGetValue(cacheKey, out var cachedEntry) && cachedEntry.Value is FunctionInfo cachedMethod)
                {
                    // Update last access generation
                    cachedEntry.LastAccessGeneration = generation;
                    cachedMethod.Name = methodName;

                    // Check if we need to trigger eviction
                    if (generation % _generationsBeforeCleanup == 0)
                    {
                        _evictionLock.ExitReadLock();
                        EvictStaleEntries();
                        return cachedMethod;
                    }

                    return cachedMethod;
                }

                // Check eviction even on cache miss
                if (generation % _generationsBeforeCleanup == 0)
                {
                    _evictionLock.ExitReadLock();
                    EvictStaleEntries();
                }
                else
                {
                    _evictionLock.ExitReadLock();
                }

                // Lookup from object (outside of lock)
                var method = obj.LookupMethodByHash(methodHash);
                if (method != null)
                {
                    method.Name = methodName;
                    var entry = new CacheEntry<object>(method, generation);
                    _namedItemCache[cacheKey] = entry;
                }

                return method;
            }
            finally
            {
                if (_evictionLock.IsReadLockHeld)
                {
                    _evictionLock.ExitReadLock();
                }
            }
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

            // Increment generation counter for property lookup
            var generation = Interlocked.Increment(ref _currentGeneration);

            var propertyHash = HashUtilities.FNV1a32Hash(propertyName);
            var cacheKey = $"{objectName.ToLowerInvariant()}:{propertyHash}_prop";

            // Acquire read lock to prevent eviction during access
            _evictionLock.EnterReadLock();
            try
            {
                // Check cache
                if (_namedItemCache.TryGetValue(cacheKey, out var cachedEntry) && cachedEntry.Value is Functions.PropertyInfo cachedProp)
                {
                    // Update last access generation
                    cachedEntry.LastAccessGeneration = generation;

                    // Check if we need to trigger eviction
                    if (generation % _generationsBeforeCleanup == 0)
                    {
                        _evictionLock.ExitReadLock();
                        EvictStaleEntries();
                        return cachedProp;
                    }

                    return cachedProp;
                }

                // Check eviction even on cache miss
                if (generation % _generationsBeforeCleanup == 0)
                {
                    _evictionLock.ExitReadLock();
                    EvictStaleEntries();
                }
                else
                {
                    _evictionLock.ExitReadLock();
                }

                // Lookup from object (outside of lock)
                var property = obj.LookupPropertyByHash(propertyHash);
                if (property != null)
                {
                    var entry = new CacheEntry<object>(property, generation);
                    _namedItemCache[cacheKey] = entry;
                }

                return property;
            }
            finally
            {
                if (_evictionLock.IsReadLockHeld)
                {
                    _evictionLock.ExitReadLock();
                }
            }
        }

        public IEnumerable<Functions.PropertyInfo> GetAllProperties(string objectName)
        {
            var obj = GetObject(objectName);
            return obj?.GetAllProperties() ?? Enumerable.Empty<Functions.PropertyInfo>();
        }

        /// <summary>
        /// Performs generational cache eviction by removing entries that haven't been accessed
        /// within the eviction threshold. Resets all generations after cleanup.
        /// </summary>
        private void EvictStaleEntries()
        {
            _evictionLock.EnterWriteLock();
            try
            {
                var currentGen = _currentGeneration;
                var evictionThreshold = currentGen - _generationEvictionThreshold;

                // Evict stale entries from object cache
                var objectKeysToRemove = _objectCache
                    .Where(kvp => kvp.Value.LastAccessGeneration < evictionThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in objectKeysToRemove)
                {
                    _objectCache.TryRemove(key, out _);
                }

                // Evict stale entries from named item cache (methods/properties)
                var namedItemKeysToRemove = _namedItemCache
                    .Where(kvp => kvp.Value.LastAccessGeneration < evictionThreshold)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in namedItemKeysToRemove)
                {
                    _namedItemCache.TryRemove(key, out _);
                }

                // Reset generations for all remaining entries
                foreach (var kvp in _objectCache)
                {
                    kvp.Value.LastAccessGeneration = 0;
                }

                foreach (var kvp in _namedItemCache)
                {
                    kvp.Value.LastAccessGeneration = 0;
                }

                // Reset generation counter
                _currentGeneration = 0;
            }
            finally
            {
                _evictionLock.ExitWriteLock();
            }
        }
    }
}
