using System.Collections.Concurrent;
using PeopleCodeTypeInfo.Inference;

namespace PeopleCodeTypeInfo.Contracts;

/// <summary>
/// Base class for resolving type metadata for custom PeopleCode types (classes, interfaces, function libraries).
/// Provides built-in generational cache eviction to prevent unbounded memory growth.
/// Subclasses implement the actual resolution logic while this base class handles caching automatically.
/// </summary>
/// <remarks>
/// This design avoids circular dependencies by separating concerns:
/// - PeopleCodeParser extracts TypeMetadata from parsed AST
/// - Host application stores and provides TypeMetadata through resolver implementations
/// - PeopleCodeTypeInfo queries metadata without needing parser reference
///
/// Typical implementation pattern:
/// 1. Host gets source code (from database, file system, etc.)
/// 2. Host parses source using PeopleCodeParser
/// 3. Host runs TypeMetadataBuilder visitor to extract TypeMetadata
/// 4. Subclass implements Core methods to perform the actual resolution
/// 5. Base class handles caching automatically with generational eviction
/// 6. PeopleCodeTypeInfo uses resolver to query custom types
/// </remarks>
public abstract class ITypeMetadataResolver
{
    private readonly TypeCache _cache;

    /// <summary>
    /// Creates a new type metadata resolver with default cache settings.
    /// </summary>
    /// <param name="cacheGenerationsBeforeCleanup">Number of cache operations before triggering cleanup (default: 5000)</param>
    /// <param name="cacheGenerationEvictionThreshold">Age threshold for evicting entries (default: 2500)</param>
    protected ITypeMetadataResolver(int cacheGenerationsBeforeCleanup = 5000, int cacheGenerationEvictionThreshold = 2500)
    {
        _cache = new TypeCache(cacheGenerationsBeforeCleanup, cacheGenerationEvictionThreshold);
    }

    /// <summary>
    /// Attempts to retrieve type metadata for a custom type.
    /// Automatically caches results for performance.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the type:
    /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
    /// - Function Library: Any identifier for a program containing function declarations
    /// </param>
    /// <returns>
    /// TypeMetadata if the type is found, null otherwise.
    /// </returns>
    public TypeMetadata? GetTypeMetadata(string qualifiedName)
    {
        // Check cache first (which also records the lookup for a refinement in progress)
        var cached = _cache.Get(qualifiedName);
        if (cached != null)
        {
            return cached;
        }

        // Call subclass implementation
        var metadata = GetTypeMetadataCore(qualifiedName);

        // Cache non-null results
        if (metadata != null)
        {
            _cache.Set(qualifiedName, metadata);
        }

        return metadata;
    }

    /// <summary>
    /// Attempts to retrieve type metadata for a custom type asynchronously.
    /// Automatically caches results for performance.
    /// </summary>
    /// <param name="qualifiedName">
    /// The qualified name of the type:
    /// - App Class/Interface: "PKG:Class", "PKG:SUBPKG:Class", etc.
    /// - Function Library: Any identifier for a program containing function declarations
    /// </param>
    /// <returns>
    /// Task that resolves to TypeMetadata if the type is found, null otherwise.
    /// </returns>
    public async Task<TypeMetadata?> GetTypeMetadataAsync(string qualifiedName)
    {
        // Check cache first (which also records the lookup for a refinement in progress)
        var cached = _cache.Get(qualifiedName);
        if (cached != null)
        {
            return cached;
        }

        // Call subclass implementation
        var metadata = await GetTypeMetadataCoreAsync(qualifiedName);

        // Cache non-null results
        if (metadata != null)
        {
            _cache.Set(qualifiedName, metadata);
        }

        return metadata;
    }

    /// <summary>
    /// Resolves the data type of a field on a record.
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <returns>TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
    public Types.TypeInfo GetFieldType(string fieldName)
    {
        return GetFieldTypeCore(fieldName);
    }

    /// <summary>
    /// Resolves the data type of a field on a record asynchronously.
    /// </summary>
    /// <param name="recordName">The record name (e.g., "AAP_YEAR")</param>
    /// <param name="fieldName">The field name (e.g., "START_DT")</param>
    /// <returns>Task that resolves to TypeInfo representing the field's data type, or AnyTypeInfo if unknown</returns>
    public Task<Types.TypeInfo> GetFieldTypeAsync( string fieldName)
    {
        return GetFieldTypeCoreAsync(fieldName);
    }

    /// <summary>
    /// Manually triggers cache eviction, removing stale entries.
    /// This can be called independently of automatic eviction.
    /// </summary>
    public void EvictNow()
    {
        _cache.EvictNow();
    }

    /// <summary>
    /// Clears all cached metadata, including component interface lookups, and resets generation counter.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _componentInterfaces.Clear();
        _componentInterfaceFailures.Clear();
        _componentInterfacesSeen.Clear();
    }

    /// <summary>
    /// Gets the number of cached type metadata entries.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets all classes in the specified application package.
    /// Used for resolving wildcard imports (e.g., import Package:*).
    /// </summary>
    /// <param name="packagePath">The package path (e.g., "PKG" or "PKG:SUBPKG")</param>
    /// <returns>List of class names in the package (not including subpackages)</returns>
    public List<string> GetClassesInPackage(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return new List<string>();
        }

        // Call subclass implementation
        return GetClassesInPackageCore(packagePath);
    }

    /// <summary>
    /// Whether <paramref name="fieldName"/> is a field on the given record definition.
    /// </summary>
    /// <returns>
    /// <c>true</c> / <c>false</c> when the record's field list is known;
    /// <c>null</c> when metadata is unavailable (no DB, unknown record, error) so
    /// callers can skip validation instead of false-positiving.
    /// </returns>
    public bool? RecordHasField(string recordName, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(recordName) || string.IsNullOrWhiteSpace(fieldName))
            return null;
        return RecordHasFieldCore(recordName, fieldName);
    }

    /// <summary>
    /// How long a Found or Missing component interface lookup is trusted before it is fetched again.
    /// </summary>
    public static TimeSpan ComponentInterfaceRecheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a lookup whose Core threw is answered Unknown without calling Core again, so a
    /// failing database isn't re-queried (and its driver timeout paid) on every member access.
    /// </summary>
    public static TimeSpan ComponentInterfaceFailureRetryInterval { get; set; } = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, (CompIntfcLookup Lookup, DateTime FetchedUtc)> _componentInterfaces = new();

    // Names whose Core threw, with when; answered Unknown until the retry interval passes
    private readonly ConcurrentDictionary<string, DateTime> _componentInterfaceFailures = new();

    // Every name ever looked up, cached or not, so invalidating all also evicts metadata that
    // depended on an Unknown or expired lookup
    private readonly ConcurrentDictionary<string, byte> _componentInterfacesSeen = new();

    /// <summary>
    /// Looks up a component interface definition by name, case-insensitively. Found and Missing are
    /// cached for <see cref="ComponentInterfaceRecheckInterval"/>. Unknown is never cached as an
    /// answer, but when Core throws the name is answered Unknown without calling Core again for
    /// <see cref="ComponentInterfaceFailureRetryInterval"/>. Records <c>CompIntfc:NAME</c> as a
    /// dependency of a refinement in progress.
    /// </summary>
    public CompIntfcLookup GetComponentInterface(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return CompIntfcLookup.Unknown;

        var upperName = name.ToUpperInvariant();
        RefinementDependencyCollector.Record(ComponentInterfaceDependency(upperName));
        _componentInterfacesSeen.TryAdd(upperName, 0);

        if (_componentInterfaceFailures.TryGetValue(upperName, out var failedUtc))
        {
            if (DateTime.UtcNow - failedUtc < ComponentInterfaceFailureRetryInterval)
                return CompIntfcLookup.Unknown;
            _componentInterfaceFailures.TryRemove(upperName, out _);
        }

        bool hadEntry = _componentInterfaces.TryGetValue(upperName, out var cached);
        if (hadEntry && DateTime.UtcNow - cached.FetchedUtc < ComponentInterfaceRecheckInterval)
            return cached.Lookup;

        CompIntfcLookup lookup;
        try
        {
            lookup = GetComponentInterfaceCore(upperName);
        }
        catch (Exception ex)
        {
            // A failed lookup is never "doesn't exist"
            lookup = CompIntfcLookup.Unknown;
            _componentInterfaceFailures[upperName] = DateTime.UtcNow;
            try
            {
                OnComponentInterfaceLookupFailed(upperName, ex);
            }
            catch
            {
                // Reporting must not turn a lookup into a failure
            }
        }

        if (lookup.Status == CompIntfcLookupStatus.Unknown)
        {
            _componentInterfaces.TryRemove(upperName, out _);
        }
        else
        {
            // A recheck that finds the CI changed purges what was inferred from the old answer
            if (hadEntry
                && (cached.Lookup.Status != lookup.Status
                    || cached.Lookup.Definition?.Version != lookup.Definition?.Version))
                _cache.Remove(ComponentInterfaceDependency(upperName));
            _componentInterfaces[upperName] = (lookup, DateTime.UtcNow);
        }

        return lookup;
    }

    /// <summary>
    /// Drops the cached lookup for <paramref name="name"/>, or every lookup when null, and evicts
    /// the type metadata whose refinement depended on it.
    /// </summary>
    public void InvalidateComponentInterface(string? name)
    {
        if (name == null)
        {
            foreach (var seen in _componentInterfacesSeen.Keys)
                _cache.Remove(ComponentInterfaceDependency(seen));
            _componentInterfaces.Clear();
            _componentInterfaceFailures.Clear();
            return;
        }

        var upperName = name.ToUpperInvariant();
        _componentInterfaces.TryRemove(upperName, out _);
        _componentInterfaceFailures.TryRemove(upperName, out _);
        _cache.Remove(ComponentInterfaceDependency(upperName));
    }

    /// <summary>
    /// The upper-cased names with a cached Found or Missing lookup.
    /// </summary>
    public IReadOnlyCollection<string> CachedComponentInterfaces => _componentInterfaces.Keys.ToArray();

    private static string ComponentInterfaceDependency(string upperName) => "CompIntfc:" + upperName;

    /// <summary>
    /// Subclasses implement this method to perform the actual type metadata resolution.
    /// The base class handles caching automatically.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type to resolve</param>
    /// <returns>TypeMetadata if found, null otherwise</returns>
    protected abstract TypeMetadata? GetTypeMetadataCore(string qualifiedName);

    /// <summary>
    /// Subclasses implement this method to perform the actual type metadata resolution asynchronously.
    /// The base class handles caching automatically.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type to resolve</param>
    /// <returns>Task that resolves to TypeMetadata if found, null otherwise</returns>
    protected abstract Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName);

    /// <summary>
    /// Subclasses implement this method to perform the actual field type resolution.
    /// </summary>
    /// <param name="recordName">The record name</param>
    /// <param name="fieldName">The field name</param>
    /// <returns>TypeInfo representing the field's data type</returns>
    protected abstract Types.TypeInfo GetFieldTypeCore(string fieldName);

    /// <summary>
    /// Subclasses implement this method to perform the actual field type resolution asynchronously.
    /// </summary>
    /// <param name="recordName">The record name</param>
    /// <param name="fieldName">The field name</param>
    /// <returns>Task that resolves to TypeInfo representing the field's data type</returns>
    protected abstract Task<Types.TypeInfo> GetFieldTypeCoreAsync(string fieldName);

    /// <summary>
    /// Subclasses implement this method to query all classes in an application package.
    /// Used for resolving wildcard imports.
    /// </summary>
    /// <param name="packagePath">The package path (e.g., "PKG" or "PKG:SUBPKG")</param>
    /// <returns>List of class names in the package</returns>
    protected abstract List<string> GetClassesInPackageCore(string packagePath);

    /// <summary>
    /// Subclasses implement record field membership. Default is unknown (null).
    /// </summary>
    protected virtual bool? RecordHasFieldCore(string recordName, string fieldName) => null;

    /// <summary>
    /// Subclasses fetch a component interface definition by upper-cased name. Default is Unknown.
    /// Throw on failure (rather than return Unknown) so the base class holds off retrying for
    /// <see cref="ComponentInterfaceFailureRetryInterval"/>.
    /// </summary>
    protected virtual CompIntfcLookup GetComponentInterfaceCore(string name) => CompIntfcLookup.Unknown;

    /// <summary>
    /// Called when <see cref="GetComponentInterfaceCore"/> throws: at most once per name per
    /// <see cref="ComponentInterfaceFailureRetryInterval"/>. Default does nothing.
    /// </summary>
    protected virtual void OnComponentInterfaceLookupFailed(string name, Exception exception) { }

    public TypeCache Cache
    {
        get { return _cache; }
    }
}

/// <summary>
/// Null implementation of ITypeMetadataResolver that never resolves any types.
/// Useful for testing or when custom type resolution is not available.
/// </summary>
public class NullTypeMetadataResolver : ITypeMetadataResolver
{
    /// <summary>
    /// Singleton instance
    /// </summary>
    public static readonly NullTypeMetadataResolver Instance = new();

    public NullTypeMetadataResolver() : base() { }

    /// <summary>
    /// Always returns null indicating no type metadata was found.
    /// </summary>
    protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
    {
        return null;
    }

    /// <summary>
    /// Always returns null indicating no type metadata was found.
    /// </summary>
    protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
    {
        return Task.FromResult<TypeMetadata?>(null);
    }

    /// <summary>
    /// Always returns AnyTypeInfo.Instance indicating field type is unknown.
    /// Empty record name means runtime-inferred context.
    /// </summary>
    protected override Types.TypeInfo GetFieldTypeCore(string fieldName)
    {
        return Types.AnyTypeInfo.Instance;
    }

    /// <summary>
    /// Always returns AnyTypeInfo.Instance indicating field type is unknown.
    /// Empty record name means runtime-inferred context.
    /// </summary>
    protected override Task<Types.TypeInfo> GetFieldTypeCoreAsync(string fieldName)
    {
        // Empty record name means runtime-inferred context - return any
        return Task.FromResult<Types.TypeInfo>(Types.AnyTypeInfo.Instance);
    }

    /// <summary>
    /// Always returns empty list indicating no classes are available.
    /// </summary>
    protected override List<string> GetClassesInPackageCore(string packagePath)
    {
        return new List<string>();
    }
}
