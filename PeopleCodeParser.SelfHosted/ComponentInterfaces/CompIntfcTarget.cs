using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.ComponentInterfaces;

/// <summary>
/// The definition level a named ComponentInterface (its root) or a DataItem with a CI path
/// refers to, when the lookup finds the definition and the path is in it. Shared by inference,
/// the compile checks and the editor features, so they agree on what a CI value's members are.
/// </summary>
public sealed class CompIntfcTarget
{
    public CompIntfcDefinition Definition { get; }
    public CompIntfcNode Node { get; }

    /// <summary>
    /// The CI name the value was tagged with.
    /// </summary>
    public string CompIntfcName { get; }

    /// <summary>
    /// The dotted collection path of an item; null for the root.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// "CompIntfc.USER_PROFILE" for the root, "CompIntfc.USER_PROFILE.Roles" for an item.
    /// </summary>
    public string DisplayName { get; }

    private CompIntfcTarget(CompIntfcDefinition definition, CompIntfcNode node, string ciName, string? path)
    {
        Definition = definition;
        Node = node;
        CompIntfcName = ciName;
        Path = path;
        DisplayName = path == null ? $"CompIntfc.{definition.Name}" : $"CompIntfc.{definition.Name}.{path}";
    }

    public bool IsRoot => ReferenceEquals(Node, Definition);

    /// <summary>
    /// The target of a named ComponentInterface or a DataItem with a CI path. Null without a
    /// resolver, name or path, when the lookup isn't Found, or when the path isn't in the definition.
    /// </summary>
    public static CompIntfcTarget? Find(TypeInfo type, ITypeMetadataResolver? resolver)
    {
        var identity = type.Identity;
        if (resolver == null || string.IsNullOrEmpty(identity?.CompIntfcName))
            return null;

        bool isRoot = type.PeopleCodeType == PeopleCodeType.ComponentInterface;
        bool isItem = type.PeopleCodeType == PeopleCodeType.DataItem && !string.IsNullOrEmpty(identity.CompIntfcPath);
        if (!isRoot && !isItem)
            return null;

        if (resolver.GetComponentInterface(identity.CompIntfcName)
            is not { Status: CompIntfcLookupStatus.Found, Definition: { } definition })
            return null;

        var path = isRoot ? null : identity.CompIntfcPath;
        if (definition.FindNode(path) is not { } node)
            return null;

        return new CompIntfcTarget(definition, node, identity.CompIntfcName, path);
    }

    /// <summary>
    /// The warning for a <c>CompIntfc.X[.path]</c> annotation whose CI doesn't exist (the lookup
    /// says Missing) or whose path isn't in the found definition. Null when the annotation names
    /// no CI, the CI and path are known, or the lookup doesn't answer (Unknown).
    /// </summary>
    public static string? AnnotationWarning(TypeIdentity? identity, ITypeMetadataResolver? resolver)
    {
        if (resolver == null || string.IsNullOrEmpty(identity?.CompIntfcName))
            return null;

        var lookup = resolver.GetComponentInterface(identity.CompIntfcName);
        if (lookup.Status == CompIntfcLookupStatus.Missing)
            return $"Component interface '{identity.CompIntfcName}' doesn't exist";

        if (lookup is not { Status: CompIntfcLookupStatus.Found, Definition: { } definition }
            || string.IsNullOrEmpty(identity.CompIntfcPath))
            return null;

        CompIntfcNode node = definition;
        var known = $"CompIntfc.{definition.Name}";
        foreach (var segment in identity.CompIntfcPath.Split('.'))
        {
            if (!node.Collections.TryGetValue(segment, out var child))
                return $"'{segment}' is not a collection of {known}";
            node = child;
            known += $".{child.Name}";
        }

        return null;
    }

    /// <summary>
    /// A property at this level; the root also has its keys.
    /// </summary>
    public CompIntfcProperty? FindProperty(string name) =>
        IsRoot ? Definition.FindRootProperty(name) : Node.Properties.GetValueOrDefault(name);

    /// <summary>
    /// Every property at this level by name, each name once: the level's properties, then (for
    /// the root) the get, create and find keys that aren't also properties.
    /// </summary>
    public IReadOnlyList<CompIntfcProperty> AllProperties()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CompIntfcProperty>();
        foreach (var property in Node.Properties.Values)
        {
            if (seen.Add(property.Name)) result.Add(property);
        }

        if (IsRoot)
        {
            foreach (var key in Definition.GetKeys.Concat(Definition.CreateKeys).Concat(Definition.FindKeys))
            {
                if (seen.Add(key.Name)) result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// A user-defined method of the CI; only the root has them.
    /// </summary>
    public CompIntfcMethod? FindCustomMethod(string name) =>
        IsRoot && Definition.CustomMethods.TryGetValue(name, out var method) ? method : null;

    /// <summary>
    /// A collection at this level as a value: a DataCollection one level deeper on the path.
    /// </summary>
    public TypeInfo CollectionType(CompIntfcCollection collection)
    {
        var path = string.IsNullOrEmpty(Path) ? collection.Name : $"{Path}.{collection.Name}";
        return TypeInfo.FromPeopleCodeType(PeopleCodeType.DataCollection)
            .WithIdentity(new TypeIdentity(CompIntfcName: CompIntfcName, CompIntfcPath: path));
    }

    /// <summary>
    /// A CI property's value type: its field's primitive type, any for anything else (Unknown).
    /// </summary>
    public static TypeInfo PropertyValueType(CompIntfcProperty property) =>
        property.FieldType is >= PeopleCodeType.String and <= PeopleCodeType.Boolean
            ? TypeInfo.FromPeopleCodeType(property.FieldType)
            : AnyTypeInfo.Instance;
}
