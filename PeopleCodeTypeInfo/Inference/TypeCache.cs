using System.Collections.Concurrent;

namespace PeopleCodeTypeInfo.Inference;

/// <summary>
/// In-memory cache for type metadata extracted from PeopleCode programs.
/// One instance per Application Designer connection.
/// Implements generational cache eviction to prevent unbounded memory growth.
/// </summary>
public class TypeCache
{
    /// <summary>
    /// Internal wrapper for cache entries that tracks generation metadata.
    /// </summary>
    private class CacheEntry
    {
        public TypeMetadata Metadata { get; }
        public long LastAccessGeneration { get; set; }

        public CacheEntry(TypeMetadata metadata, long generation)
        {
            Metadata = metadata;
            LastAccessGeneration = generation;
        }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly int _generationsBeforeCleanup;
    private readonly int _generationEvictionThreshold;
    private long _currentGeneration;
    private readonly ReaderWriterLockSlim _evictionLock = new();

    /// <summary>
    /// Creates a new TypeCache with default eviction settings.
    /// </summary>
    /// <param name="generationsBeforeCleanup">Number of cache operations before triggering cleanup (default: 5000)</param>
    /// <param name="generationEvictionThreshold">Age threshold for evicting entries (default: 2500)</param>
    public TypeCache(int generationsBeforeCleanup = 5000, int generationEvictionThreshold = 2500)
    {
        if (generationsBeforeCleanup <= 0)
            throw new ArgumentOutOfRangeException(nameof(generationsBeforeCleanup), "Must be positive");
        if (generationEvictionThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(generationEvictionThreshold), "Must be positive");
        if (generationEvictionThreshold >= generationsBeforeCleanup)
            throw new ArgumentException("Eviction threshold must be less than cleanup frequency");

        _generationsBeforeCleanup = generationsBeforeCleanup;
        _generationEvictionThreshold = generationEvictionThreshold;
    }

    /// <summary>
    /// Attempts to retrieve cached metadata for a type.
    /// Updates the last access generation for the entry.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type (e.g., "MY_PKG:MyClass" or just "MyClass")</param>
    /// <returns>The cached metadata if found, null otherwise</returns>
    public TypeMetadata? Get(string qualifiedName)
    {
        // Increment generation counter
        var generation = Interlocked.Increment(ref _currentGeneration);

        // Acquire read lock to prevent eviction during access
        _evictionLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(qualifiedName, out var entry))
            {
                // Update last access generation
                entry.LastAccessGeneration = generation;

                // Check if we need to trigger eviction
                if (generation % _generationsBeforeCleanup == 0)
                {
                    // Release read lock before eviction
                    _evictionLock.ExitReadLock();
                    EvictStaleEntries();
                    // Don't reacquire lock, we're returning
                    return entry.Metadata;
                }

                return entry.Metadata;
            }

            // Check eviction even on cache miss
            if (generation % _generationsBeforeCleanup == 0)
            {
                _evictionLock.ExitReadLock();
                EvictStaleEntries();
                return null;
            }

            return null;
        }
        finally
        {
            if (_evictionLock.IsReadLockHeld)
            {
                _evictionLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Stores metadata for a type in the cache.
    /// Tags the entry with the current generation.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type</param>
    /// <param name="metadata">The metadata to cache</param>
    public void Set(string qualifiedName, TypeMetadata metadata)
    {
        // Increment generation counter
        var generation = Interlocked.Increment(ref _currentGeneration);

        // Wrap metadata in cache entry with current generation
        var entry = new CacheEntry(metadata, generation);

        _evictionLock.EnterReadLock();
        try
        {
            _cache[qualifiedName] = entry;

            // Check if we need to trigger eviction
            if (generation % _generationsBeforeCleanup == 0)
            {
                _evictionLock.ExitReadLock();
                EvictStaleEntries();
            }
        }
        finally
        {
            if (_evictionLock.IsReadLockHeld)
            {
                _evictionLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Checks if metadata exists in the cache for a type.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type</param>
    /// <returns>True if cached, false otherwise</returns>
    public bool Contains(string qualifiedName)
    {
        _evictionLock.EnterReadLock();
        try
        {
            return _cache.ContainsKey(qualifiedName);
        }
        finally
        {
            _evictionLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes metadata for a specific type from the cache.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the type to remove</param>
    /// <returns>True if the entry was removed, false if it didn't exist</returns>
    public bool Remove(string qualifiedName)
    {
        _evictionLock.EnterReadLock();
        try
        {
            return _cache.TryRemove(qualifiedName, out _);
        }
        finally
        {
            _evictionLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all cached metadata and resets generation counter.
    /// </summary>
    public void Clear()
    {
        _evictionLock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _currentGeneration = 0;
        }
        finally
        {
            _evictionLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Manually triggers cache eviction, removing stale entries.
    /// This can be called independently of automatic eviction.
    /// </summary>
    public void EvictNow()
    {
        EvictStaleEntries();
    }

    /// <summary>
    /// Gets the number of cached type metadata entries.
    /// </summary>
    public int Count
    {
        get
        {
            _evictionLock.EnterReadLock();
            try
            {
                return _cache.Count;
            }
            finally
            {
                _evictionLock.ExitReadLock();
            }
        }
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

            // Remove entries older than the threshold
            var keysToRemove = _cache
                .Where(kvp => kvp.Value.LastAccessGeneration < evictionThreshold)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            // Reset generations for all remaining entries
            foreach (var kvp in _cache)
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
