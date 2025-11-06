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
        // Check cache first
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
        // Check cache first
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
    /// Clears all cached metadata and resets generation counter.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached type metadata entries.
    /// </summary>
    public int Count => _cache.Count;

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
}
