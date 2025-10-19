namespace PeopleCodeTypeInfo.HashTable;

/// <summary>
/// Strategy interface for hash table operations (writing).
/// Defines how data is hashed and written to the hash table.
/// </summary>
public interface IHashTableStrategy<T>
{
    /// <summary>
    /// Compute hash for the given data item
    /// </summary>
    uint Hash(T item);

    /// <summary>
    /// Write the data item to a binary writer
    /// </summary>
    void WriteData(BinaryWriter writer, T item);

    /// <summary>
    /// Whether this strategy uses fixed-size data (data stored in hash table)
    /// or variable-size data (data stored in separate data section)
    /// </summary>
    bool IsFixedSize { get; }
}
