namespace PeopleCodeTypeInfo.HashTable.Strategies;

/// <summary>
/// Reader strategy for hash tables with variable-size data.
/// Reads hash/offset pairs from the hash table section, then reads actual data from the data section.
/// </summary>
public class VariableSizeReaderStrategy<T> : IHashTableReaderStrategy<T>
{
    private readonly Func<T, uint> _hashFunc;
    private readonly Func<BinaryReader, uint, byte[], T> _readDataFunc;

    public bool IsFixedSize => false;

    public VariableSizeReaderStrategy(
        Func<T, uint> hashFunc,
        Func<BinaryReader, uint, byte[], T> readDataFunc)
    {
        _hashFunc = hashFunc ?? throw new ArgumentNullException(nameof(hashFunc));
        _readDataFunc = readDataFunc ?? throw new ArgumentNullException(nameof(readDataFunc));
    }

    public HashTableEntry<T> ReadEntry(BinaryReader reader)
    {
        var hash = reader.ReadUInt32();
        var offset = reader.ReadUInt32();
        return new HashTableEntry<T>(hash, offset, default);
    }

    public T ReadData(BinaryReader reader, uint offset, byte[] dataSection)
    {
        return _readDataFunc(reader, offset, dataSection);
    }
}
