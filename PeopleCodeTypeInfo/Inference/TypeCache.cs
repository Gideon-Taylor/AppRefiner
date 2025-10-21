using System.Collections.Concurrent;

namespace PeopleCodeTypeInfo.Inference;

/// <summary>
/// In-memory cache for type metadata extracted from PeopleCode programs.
/// One instance per Application Designer connection.
/// Lifetime: lives until AppRefiner disconnects or restarts (no eviction/expiration).
/// </summary>
public class TypeCache
{
    private readonly ConcurrentDictionary<string, TypeMetadata> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to retrieve cached metadata for a type.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type (e.g., "MY_PKG:MyClass" or just "MyClass")</param>
    /// <returns>The cached metadata if found, null otherwise</returns>
    public TypeMetadata? Get(string qualifiedName)
    {
        _cache.TryGetValue(qualifiedName, out var metadata);
        return metadata;
    }

    /// <summary>
    /// Stores metadata for a type in the cache.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type</param>
    /// <param name="metadata">The metadata to cache</param>
    public void Set(string qualifiedName, TypeMetadata metadata)
    {
        _cache[qualifiedName] = metadata;
    }

    /// <summary>
    /// Checks if metadata exists in the cache for a type.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type</param>
    /// <returns>True if cached, false otherwise</returns>
    public bool Contains(string qualifiedName)
    {
        return _cache.ContainsKey(qualifiedName);
    }

    /// <summary>
    /// Removes metadata for a specific type from the cache.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type to remove</param>
    /// <returns>True if the entry was removed, false if it didn't exist</returns>
    public bool Remove(string qualifiedName)
    {
        return _cache.TryRemove(qualifiedName, out _);
    }

    /// <summary>
    /// Clears all cached metadata.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached type metadata entries.
    /// </summary>
    public int Count => _cache.Count;
}
