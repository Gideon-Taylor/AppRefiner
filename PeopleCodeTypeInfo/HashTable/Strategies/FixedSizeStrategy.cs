namespace PeopleCodeTypeInfo.HashTable.Strategies;

/// <summary>
/// Strategy for hash tables with fixed-size data stored directly in the hash table.
/// Used for simple types that can be efficiently stored inline.
/// </summary>
public class FixedSizeStrategy<T> : IHashTableStrategy<T>
{
    private readonly Func<T, uint> _hashFunc;
    private readonly Action<BinaryWriter, T> _writeDataFunc;

    public bool IsFixedSize => true;

    public FixedSizeStrategy(Func<T, uint> hashFunc, Action<BinaryWriter, T> writeDataFunc)
    {
        _hashFunc = hashFunc ?? throw new ArgumentNullException(nameof(hashFunc));
        _writeDataFunc = writeDataFunc ?? throw new ArgumentNullException(nameof(writeDataFunc));
    }

    public uint Hash(T item) => _hashFunc(item);

    public void WriteData(BinaryWriter writer, T item) => _writeDataFunc(writer, item);
}
