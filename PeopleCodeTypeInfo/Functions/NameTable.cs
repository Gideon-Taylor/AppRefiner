using System.Collections.Concurrent;
using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Thread-safe implementation of a shared name table that stores parameter names
/// and provides index-based lookup to reduce memory usage and file size.
/// </summary>
public class NameTable : INameTable
{
    private readonly ConcurrentDictionary<string, int> _nameToIndex = new();
    private readonly List<string> _indexToName = new();
    private readonly object _lock = new();

    /// <summary>
    /// Create an empty name table.
    /// </summary>
    public NameTable()
    {
    }

    /// <summary>
    /// Create a name table pre-populated with the given names.
    /// Useful when loading from a file.
    /// </summary>
    /// <param name="names">The names to pre-populate the table with</param>
    public NameTable(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            RegisterName(name);
        }
    }

    /// <summary>
    /// Register a name in the table and return its index.
    /// If the name already exists, returns the existing index.
    /// Thread-safe.
    /// </summary>
    /// <param name="name">The name to register</param>
    /// <returns>The index of the name in the table</returns>
    public int RegisterName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1; // Use -1 to indicate no name

        // Try to get existing index first (most common case)
        if (_nameToIndex.TryGetValue(name, out int existingIndex))
            return existingIndex;

        // Need to add new name - use lock for thread safety
        lock (_lock)
        {
            // Double-check in case another thread added it while we were waiting
            if (_nameToIndex.TryGetValue(name, out existingIndex))
                return existingIndex;

            // Add new name
            int newIndex = _indexToName.Count;
            _indexToName.Add(name);
            _nameToIndex[name] = newIndex;
            return newIndex;
        }
    }

    /// <summary>
    /// Get a name by its index. Returns null if index is out of range.
    /// Thread-safe for reading.
    /// </summary>
    /// <param name="index">The index of the name</param>
    /// <returns>The name at the given index, or null if not found</returns>
    public string? GetNameByIndex(int index)
    {
        if (index < 0 || index >= _indexToName.Count)
            return null;

        lock (_lock)
        {
            return index < _indexToName.Count ? _indexToName[index] : null;
        }
    }

    /// <summary>
    /// Get all registered names as an array for serialization.
    /// The array index corresponds to the name index.
    /// Thread-safe.
    /// </summary>
    /// <returns>Array of all registered names</returns>
    public string[] GetAllNames()
    {
        lock (_lock)
        {
            return _indexToName.ToArray();
        }
    }

    /// <summary>
    /// Get the total number of registered names.
    /// Thread-safe.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _indexToName.Count;
            }
        }
    }

    /// <summary>
    /// Check if the name table contains a specific name.
    /// Thread-safe.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>True if the name is registered</returns>
    public bool Contains(string name)
    {
        return _nameToIndex.ContainsKey(name);
    }

    /// <summary>
    /// Try to get the index of a name without registering it.
    /// Thread-safe.
    /// </summary>
    /// <param name="name">The name to look up</param>
    /// <param name="index">The index if found</param>
    /// <returns>True if the name was found</returns>
    public bool TryGetIndex(string name, out int index)
    {
        return _nameToIndex.TryGetValue(name, out index);
    }

    /// <summary>
    /// Get the total size in bytes of the serialized names table.
    /// This includes the count field and all string data.
    /// Thread-safe.
    /// </summary>
    public int NamesTableSize
    {
        get
        {
            lock (_lock)
            {
                int size = 4; // count field
                foreach (var name in _indexToName)
                {
                    var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                    size += 1 + nameBytes.Length; // length byte + string bytes
                }
                return size;
            }
        }
    }

    /// <summary>
    /// Clear all names from the table. Useful for testing.
    /// Thread-safe.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _nameToIndex.Clear();
            _indexToName.Clear();
        }
    }

    /// <summary>
    /// Create a read-only name lookup table from a string array.
    /// This is optimized for reading scenarios where names won't be modified.
    /// </summary>
    /// <param name="names">Array of names where index equals name index</param>
    /// <returns>A read-only name table</returns>
    public static ReadOnlyNameTable CreateReadOnly(string[] names)
    {
        return new ReadOnlyNameTable(names);
    }
}

/// <summary>
/// Read-only implementation of name table optimized for deserialization scenarios.
/// Uses direct array access for O(1) lookups with minimal memory overhead.
/// </summary>
public class ReadOnlyNameTable : INameTable
{
    private readonly string[] _names;
    private readonly Dictionary<string, int>? _nameToIndex;
    private readonly int _namesTableSize;

    /// <summary>
    /// Create a read-only name table from a string array.
    /// </summary>
    /// <param name="names">Array of names where index equals name index</param>
    /// <param name="buildReverseIndex">Whether to build reverse lookup index for Contains/TryGetIndex operations</param>
    /// <param name="namesTableSize">Pre-calculated size of the serialized names table in bytes</param>
    public ReadOnlyNameTable(string[] names, bool buildReverseIndex = false, int namesTableSize = 0)
    {
        _names = names;
        _namesTableSize = namesTableSize;

        if (buildReverseIndex)
        {
            _nameToIndex = new Dictionary<string, int>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                    _nameToIndex[names[i]] = i;
            }
        }
    }

    /// <summary>
    /// Not supported in read-only mode.
    /// </summary>
    public int RegisterName(string name)
    {
        throw new NotSupportedException("Cannot register names in read-only mode");
    }

    /// <summary>
    /// Get a name by its index using direct array access.
    /// Returns null if index is out of range.
    /// </summary>
    public string? GetNameByIndex(int index)
    {
        return index >= 0 && index < _names.Length ? _names[index] : null;
    }

    /// <summary>
    /// Get all names. Returns reference to internal array for efficiency.
    /// </summary>
    public string[] GetAllNames()
    {
        return _names;
    }

    /// <summary>
    /// Get the total number of names.
    /// </summary>
    public int Count => _names.Length;

    /// <summary>
    /// Check if the name table contains a specific name.
    /// Requires reverse index to be built.
    /// </summary>
    public bool Contains(string name)
    {
        if (_nameToIndex == null)
            throw new InvalidOperationException("Reverse index not built. Create with buildReverseIndex=true");

        return _nameToIndex.ContainsKey(name);
    }

    /// <summary>
    /// Try to get the index of a name.
    /// Requires reverse index to be built.
    /// </summary>
    public bool TryGetIndex(string name, out int index)
    {
        if (_nameToIndex == null)
        {
            index = -1;
            throw new InvalidOperationException("Reverse index not built. Create with buildReverseIndex=true");
        }

        return _nameToIndex.TryGetValue(name, out index);
    }

    /// <summary>
    /// Get the pre-calculated size in bytes of the serialized names table.
    /// If not provided during construction, calculates it on demand.
    /// </summary>
    public int NamesTableSize
    {
        get
        {
            if (_namesTableSize > 0)
                return _namesTableSize;

            // Calculate on demand if not provided
            int size = 4; // count field
            foreach (var name in _names)
            {
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                size += 1 + nameBytes.Length; // length byte + string bytes
            }
            return size;
        }
    }
}
