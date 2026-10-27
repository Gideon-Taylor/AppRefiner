using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Contracts;

/// <summary>
/// The standard methods a component interface can enable (PSBCDEFN.BCSTDMETHODS bits).
/// GetPropertyByName, SetPropertyByName and the CopyRowset family are on every CI and aren't gated.
/// </summary>
[Flags]
public enum CompIntfcStandardMethods
{
    None = 0,
    Cancel = 1,
    Create = 2,
    Find = 4,
    Get = 8,
    Save = 16,
}

/// <summary>
/// A property of a CI root, a collection, or a key list, mapped to its record field.
/// </summary>
public sealed record CompIntfcProperty(string Name, string RecordName, string FieldName,
    PeopleCodeType FieldType, bool IsReadOnly, string? Comment);

/// <summary>
/// A level of a component interface: the CI root or one of its collections.
/// Name and member lookups are case-insensitive.
/// </summary>
public abstract class CompIntfcNode
{
    private static readonly IReadOnlyDictionary<string, CompIntfcProperty> NoProperties =
        new Dictionary<string, CompIntfcProperty>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, CompIntfcCollection> NoCollections =
        new Dictionary<string, CompIntfcCollection>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The CI name for the root, the collection name otherwise.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The scroll record of a collection; empty for the root.
    /// </summary>
    public string RecordName { get; init; } = "";

    /// <summary>
    /// Properties at this level, keyed OrdinalIgnoreCase.
    /// </summary>
    public IReadOnlyDictionary<string, CompIntfcProperty> Properties { get; init; } = NoProperties;

    /// <summary>
    /// Child collections at this level, keyed OrdinalIgnoreCase.
    /// </summary>
    public IReadOnlyDictionary<string, CompIntfcCollection> Collections { get; init; } = NoCollections;
}

/// <summary>
/// A collection of a component interface (a scroll of its component).
/// </summary>
public sealed class CompIntfcCollection : CompIntfcNode { }

/// <summary>
/// A user-defined CI method: an exported function of the CI's Methods PeopleCode.
/// </summary>
public sealed record CompIntfcMethod(string Name, FunctionInfo Signature);

/// <summary>
/// A component interface definition: the root level plus its keys, enabled standard methods and
/// custom methods. Built from PSBCDEFN / PSBCITEM rows and the CI's Methods PeopleCode.
/// </summary>
public sealed class CompIntfcDefinition : CompIntfcNode
{
    private static readonly IReadOnlyDictionary<string, CompIntfcMethod> NoMethods =
        new Dictionary<string, CompIntfcMethod>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// PSBCDEFN.VERSION, used to notice a changed definition.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// The gated standard methods this CI enables.
    /// </summary>
    public CompIntfcStandardMethods StandardMethods { get; init; }

    public IReadOnlyList<CompIntfcProperty> GetKeys { get; init; } = Array.Empty<CompIntfcProperty>();
    public IReadOnlyList<CompIntfcProperty> CreateKeys { get; init; } = Array.Empty<CompIntfcProperty>();
    public IReadOnlyList<CompIntfcProperty> FindKeys { get; init; } = Array.Empty<CompIntfcProperty>();

    /// <summary>
    /// User-defined methods, keyed OrdinalIgnoreCase.
    /// </summary>
    public IReadOnlyDictionary<string, CompIntfcMethod> CustomMethods { get; init; } = NoMethods;

    /// <summary>
    /// The node at a dotted collection path from the root ("Roles.RouteControls"); null or empty
    /// is the root itself. Null when any segment isn't a collection of the level before it.
    /// </summary>
    public CompIntfcNode? FindNode(string? path)
    {
        if (string.IsNullOrEmpty(path)) return this;

        CompIntfcNode node = this;
        foreach (var segment in path.Split('.'))
        {
            if (!node.Collections.TryGetValue(segment, out var child)) return null;
            node = child;
        }
        return node;
    }

    /// <summary>
    /// A property of the root by name: the root's properties first, then the get, create and find
    /// keys, since a key needn't also be listed as a property.
    /// </summary>
    public CompIntfcProperty? FindRootProperty(string name)
    {
        if (Properties.TryGetValue(name, out var property)) return property;
        return FindKey(GetKeys, name) ?? FindKey(CreateKeys, name) ?? FindKey(FindKeys, name);
    }

    /// <summary>
    /// False only for Cancel, Create, Find, Get or Save when this CI doesn't enable it; any other
    /// name isn't gated.
    /// </summary>
    public bool IsStandardMethodEnabled(string methodName)
    {
        var bit = methodName?.ToUpperInvariant() switch
        {
            "CANCEL" => CompIntfcStandardMethods.Cancel,
            "CREATE" => CompIntfcStandardMethods.Create,
            "FIND" => CompIntfcStandardMethods.Find,
            "GET" => CompIntfcStandardMethods.Get,
            "SAVE" => CompIntfcStandardMethods.Save,
            _ => CompIntfcStandardMethods.None,
        };
        return bit == CompIntfcStandardMethods.None || (StandardMethods & bit) != 0;
    }

    private static CompIntfcProperty? FindKey(IReadOnlyList<CompIntfcProperty> keys, string name)
    {
        foreach (var key in keys)
        {
            if (string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase)) return key;
        }
        return null;
    }
}

/// <summary>
/// One PSBCITEM row, the builder's input. BcType: 1 GETKEYS, 2 CREATEKEYS, 3 collection,
/// 4 property, 5 FINDKEYS. Parent 'PS_ROOT' is the CI root. Access: 1 read/write, 2 read-only.
/// </summary>
public sealed record CompIntfcItemRow(int BcType, string Parent, string Name, int Sequence, int Access,
    string RecordName, string FieldName, string? Comment, PeopleCodeType FieldType);

/// <summary>
/// The outcome of a CI lookup. Unknown (no database, error) is never "doesn't exist".
/// </summary>
public enum CompIntfcLookupStatus { Found, Missing, Unknown }

/// <summary>
/// A CI lookup result; Definition is set only when Status is Found.
/// </summary>
public readonly record struct CompIntfcLookup(CompIntfcLookupStatus Status, CompIntfcDefinition? Definition)
{
    public static CompIntfcLookup Unknown { get; } = new(CompIntfcLookupStatus.Unknown, null);
    public static CompIntfcLookup Missing { get; } = new(CompIntfcLookupStatus.Missing, null);
    public static CompIntfcLookup Found(CompIntfcDefinition definition) => new(CompIntfcLookupStatus.Found, definition);
}
