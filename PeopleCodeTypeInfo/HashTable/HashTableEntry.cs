namespace PeopleCodeTypeInfo.HashTable;

/// <summary>
/// Represents a single entry in a hash table, supporting both fixed-size and variable-size data.
/// </summary>
public struct HashTableEntry<T>
{
    /// <summary>
    /// Hash value for this entry (0 indicates empty slot)
    /// </summary>
    public uint Hash { get; set; }

    /// <summary>
    /// File offset for variable-size data (used when data is stored separately)
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Actual data for fixed-size entries (null for variable-size)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Whether this entry is empty (Hash == 0)
    /// </summary>
    public bool IsEmpty => Hash == 0;

    public HashTableEntry(uint hash, uint offset, T? data = default)
    {
        Hash = hash;
        Offset = offset;
        Data = data;
    }
}
