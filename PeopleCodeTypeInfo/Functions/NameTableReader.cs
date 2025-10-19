using System.Text;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Reads a name table from binary format.
/// Provides both mutable (NameTable) and read-only (ReadOnlyNameTable) implementations.
/// </summary>
public static class NameTableReader
{
    /// <summary>
    /// Read a name table from a binary reader and return as a NameTable
    /// </summary>
    public static NameTable Read(BinaryReader reader, bool buildReverseIndex = true)
    {
        var count = reader.ReadInt32();
        var names = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            var length = reader.ReadByte();
            if (length == 0)
            {
                names.Add("");
            }
            else
            {
                var bytes = reader.ReadBytes(length);
                names.Add(Encoding.UTF8.GetString(bytes));
            }
        }

        return new NameTable(names);
    }

    /// <summary>
    /// Read a name table from a binary reader and return as a read-only implementation
    /// </summary>
    public static INameTable ReadAsReadOnlyTable(BinaryReader reader, bool buildReverseIndex = true)
    {
        var count = reader.ReadInt32();
        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            var length = reader.ReadByte();
            if (length == 0)
            {
                names[i] = "";
            }
            else
            {
                var bytes = reader.ReadBytes(length);
                names[i] = Encoding.UTF8.GetString(bytes);
            }
        }

        return new ReadOnlyNameTable(names, buildReverseIndex);
    }

    /// <summary>
    /// Read-only name table implementation optimized for reading
    /// </summary>
    private class ReadOnlyNameTable : INameTable
    {
        private readonly string[] _names;
        private readonly Dictionary<string, int>? _reverseIndex;

        public int NamesTableSize { get; }
        public int Count => _names.Length;

        public ReadOnlyNameTable(string[] names, bool buildReverseIndex)
        {
            _names = names;

            if (buildReverseIndex)
            {
                _reverseIndex = new Dictionary<string, int>();
                for (int i = 0; i < names.Length; i++)
                {
                    _reverseIndex[names[i]] = i;
                }
            }

            // Calculate table size
            int size = 4; // Count header
            foreach (var name in names)
            {
                var byteCount = Encoding.UTF8.GetByteCount(name);
                size += 1 + Math.Min(byteCount, 255);
            }
            NamesTableSize = size;
        }

        public string? GetNameByIndex(int index)
        {
            if (index < 0 || index >= _names.Length)
                return null;
            return _names[index];
        }

        public int? GetIndexByName(string name)
        {
            if (_reverseIndex != null && _reverseIndex.TryGetValue(name, out var index))
                return index;
            return null;
        }
    }
}
