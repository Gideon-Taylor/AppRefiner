using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

/// <summary>
/// In-memory ITypeMetadataResolver for compile-check tests. Back it with AddClass /
/// AddPackageClasses; anything not added resolves to null (class does not exist).
/// Shared by all Phase 3/4 resolver-backed check tests — extend the stored
/// TypeMetadata (Methods, Constructor, BaseClassName, ...) as later checks need it.
/// </summary>
internal sealed class FakeTypeMetadataResolver : ITypeMetadataResolver
{
    private readonly Dictionary<string, TypeMetadata> _types = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _recordFields = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a class the resolver knows about. When no metadata is supplied, a
    /// minimal TypeMetadata (QualifiedName + Name) is stored.
    /// </summary>
    public void AddClass(string qualifiedName, TypeMetadata? metadata = null)
    {
        _types[qualifiedName] = metadata ?? new TypeMetadata
        {
            QualifiedName = qualifiedName,
            Name = qualifiedName.Split(':').Last(),
        };
    }

    /// <summary>
    /// Registers the class names returned by GetClassesInPackage for a package path.
    /// </summary>
    public void AddPackageClasses(string packagePath, params string[] classNames)
    {
        _packages[packagePath] = classNames.ToList();
    }

    /// <summary>
    /// Registers the field list for a record definition (RecordHasField answers from this).
    /// Records with no registration return null (unknown) from RecordHasField.
    /// </summary>
    public void AddRecordFields(string recordName, params string[] fieldNames)
    {
        _recordFields[recordName] = new HashSet<string>(fieldNames, StringComparer.OrdinalIgnoreCase);
    }

    protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
        => _types.TryGetValue(qualifiedName, out var metadata) ? metadata : null;

    protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
        => Task.FromResult(GetTypeMetadataCore(qualifiedName));

    protected override TypeInfo GetFieldTypeCore(string fieldName)
        => PeopleCodeTypeInfo.Types.AnyTypeInfo.Instance;

    protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName)
        => Task.FromResult<TypeInfo>(PeopleCodeTypeInfo.Types.AnyTypeInfo.Instance);

    protected override List<string> GetClassesInPackageCore(string packagePath)
        => _packages.TryGetValue(packagePath, out var classes) ? classes : new List<string>();

    protected override bool? RecordHasFieldCore(string recordName, string fieldName)
    {
        if (!_recordFields.TryGetValue(recordName, out var fields))
            return null;
        return fields.Contains(fieldName);
    }
}
