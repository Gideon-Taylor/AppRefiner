namespace PeopleCodeTypeInfo.HashTable;

/// <summary>
/// Generic hash table entry that can store either fixed-size data directly
/// or an offset to variable-size data in a separate section.
/// </summary>
public struct HashTableEntry<T>
{
    /// <summary>
    /// FNV1a32 hash of the key. 0 indicates an empty slot.
    /// </summary>
    public uint Hash { get; set; }

    /// <summary>
    /// For fixed-size strategies: the actual data
    /// For variable-size strategies: not used (data is accessed via offset)
    /// </summary>
    public T Data { get; set; }

    /// <summary>
    /// For variable-size strategies: offset into the data section
    /// For fixed-size strategies: not used
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Check if this entry is empty (unused slot in hash table).
    /// </summary>
    public readonly bool IsEmpty => Hash == 0;

    /// <summary>
    /// Create a new hash table entry for fixed-size data.
    /// </summary>
    public static HashTableEntry<T> CreateFixedSize(uint hash, T data)
    {
        return new HashTableEntry<T>
        {
            Hash = hash,
            Data = data,
            Offset = 0
        };
    }

    /// <summary>
    /// Create a new hash table entry for variable-size data.
    /// </summary>
    public static HashTableEntry<T> CreateVariableSize(uint hash, uint offset)
    {
        return new HashTableEntry<T>
        {
            Hash = hash,
            Data = default(T),
            Offset = offset
        };
    }

    /// <summary>
    /// Create an empty hash table entry.
    /// </summary>
    public static HashTableEntry<T> Empty => new HashTableEntry<T>
    {
        Hash = 0,
        Data = default(T),
        Offset = 0
    };
}