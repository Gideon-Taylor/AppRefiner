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
    /// Read-only implementation of name table optimized for deserialization scenarios.
    /// Uses direct array access for O(1) lookups with minimal memory overhead.
    /// </summary>
    public class ReadOnlyNameTable : INameTable
    {
        private readonly string[] _names;
        private readonly Dictionary<string, int>? _nameToIndex;
        private readonly int _namesTableSize;

        /// <summary>
        /// Create a read-only name table from a string array.
        /// </summary>
        /// <param name="names">Array of names where index equals name index</param>
        /// <param name="buildReverseIndex">Whether to build reverse lookup index for Contains/TryGetIndex operations</param>
        /// <param name="namesTableSize">Pre-calculated size of the serialized names table in bytes</param>
        public ReadOnlyNameTable(string[] names, bool buildReverseIndex = false, int namesTableSize = 0)
        {
            _names = names;
            _namesTableSize = namesTableSize;

            if (buildReverseIndex)
            {
                _nameToIndex = new Dictionary<string, int>(names.Length);
                for (int i = 0; i < names.Length; i++)
                {
                    if (!string.IsNullOrEmpty(names[i]))
                        _nameToIndex[names[i]] = i;
                }
            }
        }

        /// <summary>
        /// Not supported in read-only mode.
        /// </summary>
        public int RegisterName(string name)
        {
            throw new NotSupportedException("Cannot register names in read-only mode");
        }

        /// <summary>
        /// Get a name by its index using direct array access.
        /// Returns null if index is out of range.
        /// </summary>
        public string? GetNameByIndex(int index)
        {
            return index >= 0 && index < _names.Length ? _names[index] : null;
        }

        /// <summary>
        /// Get all names. Returns reference to internal array for efficiency.
        /// </summary>
        public string[] GetAllNames()
        {
            return _names;
        }

        /// <summary>
        /// Get the total number of names.
        /// </summary>
        public int Count => _names.Length;

        /// <summary>
        /// Check if the name table contains a specific name.
        /// Requires reverse index to be built.
        /// </summary>
        public bool Contains(string name)
        {
            if (_nameToIndex == null)
                throw new InvalidOperationException("Reverse index not built. Create with buildReverseIndex=true");

            return _nameToIndex.ContainsKey(name);
        }

        /// <summary>
        /// Try to get the index of a name.
        /// Requires reverse index to be built.
        /// </summary>
        public bool TryGetIndex(string name, out int index)
        {
            if (_nameToIndex == null)
            {
                index = -1;
                throw new InvalidOperationException("Reverse index not built. Create with buildReverseIndex=true");
            }

            return _nameToIndex.TryGetValue(name, out index);
        }

        /// <summary>
        /// Get the pre-calculated size in bytes of the serialized names table.
        /// If not provided during construction, calculates it on demand.
        /// </summary>
        public int NamesTableSize
        {
            get
            {
                if (_namesTableSize > 0)
                    return _namesTableSize;

                // Calculate on demand if not provided
                int size = 4; // count field
                foreach (var name in _names)
                {
                    var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                    size += 1 + nameBytes.Length; // length byte + string bytes
                }
                return size;
            }
        }
    }
}
