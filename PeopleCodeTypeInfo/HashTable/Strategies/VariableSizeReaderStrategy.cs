namespace PeopleCodeTypeInfo.HashTable.Strategies;

/// <summary>
/// Base strategy for reading variable-length data from hash table storage.
/// Hash table entries store offsets to a separate data section.
/// </summary>
public class VariableSizeReaderStrategy<T> : IHashTableReaderStrategy<T>
{
    private readonly Func<T, uint> _hashFunc;
    private readonly Func<BinaryReader, uint, byte[], T> _readDataFunc;

    public bool IsFixedSize => false;
    public int EntrySize => 8; // hash(4) + offset(4)

    public VariableSizeReaderStrategy(
        Func<T, uint> hashFunc,
        Func<BinaryReader, uint, byte[], T> readDataFunc)
    {
        _hashFunc = hashFunc;
        _readDataFunc = readDataFunc;
    }

    public HashTableEntry<T> ReadEntry(BinaryReader reader)
    {
        uint hash = reader.ReadUInt32();
        uint offset = reader.ReadUInt32();

        return hash == 0
            ? HashTableEntry<T>.Empty
            : HashTableEntry<T>.CreateVariableSize(hash, offset);
    }

    public T ReadData(BinaryReader reader, uint offset, byte[] dataSection)
    {
        return _readDataFunc(reader, offset, dataSection);
    }

    public uint GetHash(T data)
    {
        return _hashFunc(data);
    }
}