using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// In-memory name table implementation with bidirectional lookup.
/// Supports both reading from and writing to binary format.
/// </summary>
public class NameTable : INameTable
{
    private readonly List<string> _names = new();
    private readonly Dictionary<string, int> _reverseIndex = new();

    public int NamesTableSize { get; private set; }
    public int Count => _names.Count;

    /// <summary>
    /// Create an empty name table
    /// </summary>
    public NameTable()
    {
        UpdateTableSize();
    }

    /// <summary>
    /// Create a name table from a list of names
    /// </summary>
    public NameTable(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            AddName(name);
        }
        UpdateTableSize();
    }

    /// <summary>
    /// Add a name to the table (if not already present)
    /// </summary>
    public int AddName(string name)
    {
        if (_reverseIndex.TryGetValue(name, out var existingIndex))
        {
            return existingIndex;
        }

        var index = _names.Count;
        _names.Add(name);
        _reverseIndex[name] = index;
        UpdateTableSize();
        return index;
    }

    public string? GetNameByIndex(int index)
    {
        if (index < 0 || index >= _names.Count)
            return null;
        return _names[index];
    }

    public int? GetIndexByName(string name)
    {
        if (_reverseIndex.TryGetValue(name, out var index))
            return index;
        return null;
    }

    /// <summary>
    /// Write the name table to a binary writer
    /// Format: [count: int32] [name1_length: byte] [name1_bytes] [name2_length: byte] [name2_bytes] ...
    /// </summary>
    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(_names.Count);
        foreach (var name in _names)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            writer.Write((byte)Math.Min(bytes.Length, 255));
            writer.Write(bytes, 0, Math.Min(bytes.Length, 255));
        }
    }

    /// <summary>
    /// Calculate the total size of the names table in bytes
    /// </summary>
    private void UpdateTableSize()
    {
        int size = 4; // Count header
        foreach (var name in _names)
        {
            var byteCount = Encoding.UTF8.GetByteCount(name);
            size += 1 + Math.Min(byteCount, 255); // Length byte + actual bytes
        }
        NamesTableSize = size;
    }
}
