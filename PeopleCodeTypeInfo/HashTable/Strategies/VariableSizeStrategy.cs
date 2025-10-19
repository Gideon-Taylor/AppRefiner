namespace PeopleCodeTypeInfo.HashTable.Strategies;

/// <summary>
/// Strategy for hash tables with variable-size data stored in a separate data section.
/// Used for complex types like functions and objects that have varying sizes.
/// </summary>
public class VariableSizeStrategy<T> : IHashTableStrategy<T>
{
    private readonly Func<T, uint> _hashFunc;
    private readonly Action<BinaryWriter, T> _writeDataFunc;

    public bool IsFixedSize => false;

    public VariableSizeStrategy(Func<T, uint> hashFunc, Action<BinaryWriter, T> writeDataFunc)
    {
        _hashFunc = hashFunc ?? throw new ArgumentNullException(nameof(hashFunc));
        _writeDataFunc = writeDataFunc ?? throw new ArgumentNullException(nameof(writeDataFunc));
    }

    public uint Hash(T item) => _hashFunc(item);

    public void WriteData(BinaryWriter writer, T item) => _writeDataFunc(writer, item);
}
