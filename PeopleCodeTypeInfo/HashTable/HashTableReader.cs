using PeopleCodeTypeInfo.Functions;

namespace PeopleCodeTypeInfo.HashTable;

/// <summary>
/// Generic hash table reader that can handle both fixed-size and variable-size data
/// using the strategy pattern for deserialization.
/// Supports reading names table from the beginning of the file.
/// </summary>
public class HashTableReader<T>
{
    private readonly HashTableEntry<T>[] _hashTable;
    private readonly byte[]? _dataSection;
    private readonly int _tableSize;
    private readonly IHashTableReaderStrategy<T> _strategy;
    private readonly INameTable? _nameTable;

    private HashTableReader(
        HashTableEntry<T>[] hashTable,
        byte[]? dataSection,
        int tableSize,
        IHashTableReaderStrategy<T> strategy,
        INameTable? nameTable = null)
    {
        _hashTable = hashTable;
        _dataSection = dataSection;
        _tableSize = tableSize;
        _strategy = strategy;
        _nameTable = nameTable;
    }

    /// <summary>
    /// Load a hash table from disk using the specified strategy.
    /// File layout: [Header] [Names Table] [Hash Table] [Data Section]
    /// </summary>
    public static HashTableReader<T> LoadFromFile(string filePath, IHashTableReaderStrategy<T> strategy)
    {
        using var file = new BinaryReader(File.OpenRead(filePath));

        // Read header
        int tableSize = file.ReadInt32();

        // Try to read names table - check if we have enough data for at least a names count
        INameTable? nameTable = null;
        if (file.BaseStream.Position + 4 <= file.BaseStream.Length)
        {
            // Peek at the next 4 bytes to see if they look like a names count
            var currentPos = file.BaseStream.Position;
            var possibleNamesCount = file.ReadInt32();

            // If it's a reasonable names count (0-10000), assume we have a names table
            if (possibleNamesCount >= 0 && possibleNamesCount <= 10000)
            {
                file.BaseStream.Position = currentPos; // Reset position
                nameTable = NameTableReader.ReadAsReadOnlyTable(file, buildReverseIndex: false);
            }
            else
            {
                file.BaseStream.Position = currentPos; // Reset position - this isn't a names table
            }
        }

        // Read hash table
        var hashTable = new HashTableEntry<T>[tableSize];
        for (int i = 0; i < tableSize; i++)
        {
            hashTable[i] = strategy.ReadEntry(file);
        }

        // Read data section (if needed)
        byte[]? dataSection = null;
        if (!strategy.IsFixedSize)
        {
            var remaining = (int)(file.BaseStream.Length - file.BaseStream.Position);
            if (remaining > 0)
            {
                dataSection = file.ReadBytes(remaining);
            }
        }

        return new HashTableReader<T>(hashTable, dataSection, tableSize, strategy, nameTable);
    }

    /// <summary>
    /// Look up data by hash value. Returns null if not found.
    /// </summary>
    public T? Lookup(uint targetHash)
    {
        // Find in hash table using linear probing
        int slot = (int)(targetHash % _tableSize);

        for (int probe = 0; probe < _tableSize; probe++)
        {
            var entry = _hashTable[slot];

            if (entry.IsEmpty)
            {
                // Empty slot - item not found
                return default(T);
            }

            if (entry.Hash == targetHash)
            {
                // Found it!
                if (_strategy.IsFixedSize)
                {
                    return entry.Data;
                }
                else
                {
                    // Read from data section
                    if (_dataSection == null)
                        throw new InvalidOperationException("Data section is missing for variable-size strategy");

                    using var dataReader = new BinaryReader(new MemoryStream(_dataSection));
                    return _strategy.ReadData(dataReader, entry.Offset, _dataSection);
                }
            }

            // Continue probing
            slot = (slot + 1) % _tableSize;
        }

        return default(T); // Not found after full table scan
    }

    /// <summary>
    /// Look up reference type data by hash value. Returns null if not found.
    /// </summary>
    public T? LookupReference(uint targetHash)
    {
        // Find in hash table using linear probing
        int slot = (int)(targetHash % _tableSize);

        for (int probe = 0; probe < _tableSize; probe++)
        {
            var entry = _hashTable[slot];

            if (entry.IsEmpty)
            {
                // Empty slot - item not found
                return default(T);
            }

            if (entry.Hash == targetHash)
            {
                // Found it!
                if (_strategy.IsFixedSize)
                {
                    return entry.Data;
                }
                else
                {
                    // Read from data section
                    if (_dataSection == null)
                        throw new InvalidOperationException("Data section is missing for variable-size strategy");

                    using var dataReader = new BinaryReader(new MemoryStream(_dataSection));
                    return _strategy.ReadData(dataReader, entry.Offset, _dataSection);
                }
            }

            // Continue probing
            slot = (slot + 1) % _tableSize;
        }

        return default(T); // Not found after full table scan
    }

    /// <summary>
    /// Get statistics about the hash table performance.
    /// </summary>
    public (int totalItems, double loadFactor, int maxProbeDistance, double avgProbeDistance) GetStats()
    {
        int totalItems = 0;
        int maxProbeDistance = 0;
        double totalProbeDistance = 0;

        for (int i = 0; i < _tableSize; i++)
        {
            if (!_hashTable[i].IsEmpty)
            {
                totalItems++;

                // Calculate probe distance for this entry
                int idealSlot = (int)(_hashTable[i].Hash % _tableSize);
                int probeDistance = (i >= idealSlot) ? (i - idealSlot) : (_tableSize - idealSlot + i);
                maxProbeDistance = Math.Max(maxProbeDistance, probeDistance);
                totalProbeDistance += probeDistance;
            }
        }

        double loadFactor = (double)totalItems / _tableSize;
        double avgProbeDistance = totalItems > 0 ? totalProbeDistance / totalItems : 0;

        return (totalItems, loadFactor, maxProbeDistance, avgProbeDistance);
    }
}
