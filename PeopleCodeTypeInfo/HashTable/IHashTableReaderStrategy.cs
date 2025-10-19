namespace PeopleCodeTypeInfo.HashTable;

/// <summary>
/// Strategy interface for reading hash table entries from binary format.
/// Defines how to read entries and reconstruct data from the binary file.
/// </summary>
public interface IHashTableReaderStrategy<T>
{
    /// <summary>
    /// Read a hash table entry from the binary reader
    /// </summary>
    HashTableEntry<T> ReadEntry(BinaryReader reader);

    /// <summary>
    /// Read data from the data section (for variable-size strategies)
    /// </summary>
    /// <param name="reader">Binary reader positioned at start of data section</param>
    /// <param name="offset">Offset within the data section where this item's data begins</param>
    /// <param name="dataSection">The entire data section as a byte array for random access</param>
    T ReadData(BinaryReader reader, uint offset, byte[] dataSection);

    /// <summary>
    /// Whether this strategy uses fixed-size data (data stored in hash table)
    /// or variable-size data (data stored in separate data section)
    /// </summary>
    bool IsFixedSize { get; }
}
