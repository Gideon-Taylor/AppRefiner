namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Utility class for hash functions used in the type system
/// </summary>
public static class HashUtilities
{
    /// <summary>
    /// FNV1a hash for function names (case-insensitive)
    /// </summary>
    public static uint FNV1a32Hash(string str)
    {
        uint hash = 0x811c9dc5;

        for (int i = 0; i < str.Length; i++)
        {
            hash ^= char.ToLowerInvariant(str[i]);
            hash *= 0x01000193;
        }

        return hash;
    }
}
