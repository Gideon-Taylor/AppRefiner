namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Interface for a name table that provides efficient string storage and retrieval.
/// Used for parameter names and other repeated strings in the binary format.
/// </summary>
/// <summary>
/// Interface for a shared name table that stores parameter names and provides index-based lookup.
/// Used to reduce memory usage and file size by storing strings once and referencing by index.
/// </summary>
public interface INameTable
{
    /// <summary>
    /// Register a name in the table and return its index.
    /// If the name already exists, returns the existing index.
    /// </summary>
    /// <param name="name">The name to register</param>
    /// <returns>The index of the name in the table</returns>
    int RegisterName(string name);

    /// <summary>
    /// Get a name by its index. Returns null if index is out of range.
    /// </summary>
    /// <param name="index">The index of the name</param>
    /// <returns>The name at the given index, or null if not found</returns>
    string? GetNameByIndex(int index);

    /// <summary>
    /// Get all registered names as an array for serialization.
    /// The array index corresponds to the name index.
    /// </summary>
    /// <returns>Array of all registered names</returns>
    string[] GetAllNames();

    /// <summary>
    /// Get the total number of registered names.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Check if the name table contains a specific name.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>True if the name is registered</returns>
    bool Contains(string name);

    /// <summary>
    /// Try to get the index of a name without registering it.
    /// </summary>
    /// <param name="name">The name to look up</param>
    /// <param name="index">The index if found</param>
    /// <returns>True if the name was found</returns>
    bool TryGetIndex(string name, out int index);

    /// <summary>
    /// Get the total size in bytes of the serialized names table.
    /// This includes the count field and all string data.
    /// </summary>
    int NamesTableSize { get; }
}
