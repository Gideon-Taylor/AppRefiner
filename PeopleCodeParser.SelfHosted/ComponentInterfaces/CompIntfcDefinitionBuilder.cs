using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;

namespace PeopleCodeParser.SelfHosted.ComponentInterfaces;

/// <summary>
/// Builds a <see cref="CompIntfcDefinition"/> from a CI's PSBCDEFN values, its PSBCITEM rows and
/// the source of its Methods PeopleCode. Pure: the caller fetches the data, and the source isn't
/// kept in the definition.
/// </summary>
public static class CompIntfcDefinitionBuilder
{
    private const string RootParent = "PS_ROOT";

    private const int GetKeyType = 1;
    private const int CreateKeyType = 2;
    private const int CollectionType = 3;
    private const int PropertyType = 4;
    private const int FindKeyType = 5;

    private const int ReadOnlyAccess = 2;

    /// <summary>
    /// Builds the definition. Rows attach to their parent by name (PS_ROOT is the CI root) in
    /// sequence order; a row whose parent names no collection reachable from the root is skipped.
    /// Every implemented function of <paramref name="methodsSource"/> that isn't NoExport becomes a
    /// custom method; a null or blank source means none.
    /// </summary>
    public static CompIntfcDefinition Build(string ciName, int version, int standardMethodBits,
        IEnumerable<CompIntfcItemRow> rows, string? methodsSource)
    {
        var ordered = rows.OrderBy(r => r.Sequence).ToList();

        var root = new LevelBuilder(ciName, "");
        var collections = new Dictionary<string, LevelBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ordered.Where(r => r.BcType == CollectionType))
        {
            collections.TryAdd(row.Name, new LevelBuilder(row.Name, row.RecordName));
        }

        var getKeys = new List<CompIntfcProperty>();
        var createKeys = new List<CompIntfcProperty>();
        var findKeys = new List<CompIntfcProperty>();

        foreach (var row in ordered)
        {
            switch (row.BcType)
            {
                // Keys only exist at the CI root, whatever the row's parent says
                case GetKeyType: getKeys.Add(ToProperty(row)); continue;
                case CreateKeyType: createKeys.Add(ToProperty(row)); continue;
                case FindKeyType: findKeys.Add(ToProperty(row)); continue;
            }

            // A row with no parent name is an orphan too (the dictionary can't be asked for null)
            var parent = row.Parent == null
                ? null
                : string.Equals(row.Parent, RootParent, StringComparison.OrdinalIgnoreCase)
                    ? root
                    : collections.GetValueOrDefault(row.Parent);
            if (parent == null) continue;

            if (row.BcType == CollectionType)
            {
                // Only the first collection of a name was registered; a duplicate is skipped
                if (collections.TryGetValue(row.Name, out var collection) && !collection.IsAttached)
                {
                    collection.IsAttached = true;
                    parent.Children.Add(collection);
                }
            }
            else if (row.BcType == PropertyType)
            {
                parent.Properties.TryAdd(row.Name, ToProperty(row));
            }
        }

        return new CompIntfcDefinition
        {
            Name = ciName,
            Version = version,
            StandardMethods = (CompIntfcStandardMethods)standardMethodBits,
            Properties = root.Properties,
            Collections = BuildCollections(root),
            GetKeys = getKeys,
            CreateKeys = createKeys,
            FindKeys = findKeys,
            CustomMethods = BuildMethods(methodsSource),
        };
    }

    private static CompIntfcProperty ToProperty(CompIntfcItemRow row) =>
        new(row.Name, row.RecordName, row.FieldName, row.FieldType, row.Access == ReadOnlyAccess,
            string.IsNullOrWhiteSpace(row.Comment) ? null : row.Comment);

    /// <summary>
    /// Each collection is attached to one parent at most, so the levels reachable from the root
    /// form a tree; a parent cycle (A under B under A) is never reached from it.
    /// </summary>
    private static Dictionary<string, CompIntfcCollection> BuildCollections(LevelBuilder level)
    {
        var result = new Dictionary<string, CompIntfcCollection>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in level.Children)
        {
            result[child.Name] = new CompIntfcCollection
            {
                Name = child.Name,
                RecordName = child.RecordName,
                Properties = child.Properties,
                Collections = BuildCollections(child),
            };
        }
        return result;
    }

    private static Dictionary<string, CompIntfcMethod> BuildMethods(string? methodsSource)
    {
        var methods = new Dictionary<string, CompIntfcMethod>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(methodsSource)) return methods;

        var tokens = new PeopleCodeLexer(methodsSource).TokenizeAll();
        var program = new SelfHosted.PeopleCodeParser(tokens).ParseProgram();
        var signatures = TypeMetadataBuilder.ExtractMetadata(program).Methods;

        foreach (var function in program.Functions)
        {
            if (!function.IsImplementation || function.IsNoExport) continue;
            if (methods.ContainsKey(function.Name)) continue;
            if (!signatures.TryGetValue(function.Name, out var signature)) continue;

            methods[function.Name] = new CompIntfcMethod(function.Name, signature);
        }
        return methods;
    }

    /// <summary>
    /// A level being assembled: the root or a collection, with its rows gathered in sequence.
    /// </summary>
    private sealed class LevelBuilder(string name, string recordName)
    {
        public string Name { get; } = name;
        public string RecordName { get; } = recordName;

        /// <summary>True once the collection has been attached to a parent.</summary>
        public bool IsAttached { get; set; }

        public Dictionary<string, CompIntfcProperty> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<LevelBuilder> Children { get; } = new();
    }
}
