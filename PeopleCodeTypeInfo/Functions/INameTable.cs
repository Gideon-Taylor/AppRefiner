namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Interface for a name table that provides efficient string storage and retrieval.
/// Used for parameter names and other repeated strings in the binary format.
/// </summary>
public interface INameTable
{
    /// <summary>
    /// Get the total size of the names table in bytes (including count header)
    /// </summary>
    int NamesTableSize { get; }

    /// <summary>
    /// Get a name by its index in the table
    /// </summary>
    string? GetNameByIndex(int index);

    /// <summary>
    /// Get the index of a name in the table (for writing)
    /// </summary>
    int? GetIndexByName(string name);

    /// <summary>
    /// Number of names stored in the table
    /// </summary>
    int Count { get; }
}
