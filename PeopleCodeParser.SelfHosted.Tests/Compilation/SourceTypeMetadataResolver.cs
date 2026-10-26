using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;
using TypeInfo = PeopleCodeTypeInfo.Types.TypeInfo;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

/// <summary>
/// Resolver backed by PeopleCode source, like the database resolver: each fetch parses the
/// program, extracts its metadata and runs <see cref="ReturnTypeRefiner"/> on it. Counts parses
/// per name so tests can see what the base class cached.
/// </summary>
internal sealed class SourceTypeMetadataResolver : ITypeMetadataResolver
{
    private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _parseCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _parseErrorCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a program's source under its resolver key ("PKG:Class" or "REC.FLD.Event").
    /// </summary>
    public void Add(string qualifiedName, string source) => _sources[qualifiedName] = source;

    /// <summary>
    /// How many times the program has been parsed (i.e. fetched past the cache).
    /// </summary>
    public int ParseCount(string qualifiedName) => _parseCounts.GetValueOrDefault(qualifiedName);

    /// <summary>
    /// Parse errors in the program's most recent parse.
    /// </summary>
    public int ParseErrorCount(string qualifiedName) => _parseErrorCounts.GetValueOrDefault(qualifiedName);

    protected override TypeMetadata? GetTypeMetadataCore(string qualifiedName)
    {
        if (!_sources.TryGetValue(qualifiedName, out var source))
            return null;

        _parseCounts[qualifiedName] = ParseCount(qualifiedName) + 1;
        var (program, errors) = ParseTestHelper.Parse(source);
        _parseErrorCounts[qualifiedName] = errors.Count;

        var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);
        ReturnTypeRefiner.Refine(program, metadata, this);
        return metadata;
    }

    protected override Task<TypeMetadata?> GetTypeMetadataCoreAsync(string qualifiedName)
        => Task.FromResult(GetTypeMetadataCore(qualifiedName));

    protected override TypeInfo GetFieldTypeCore(string fieldName)
        => PeopleCodeTypeInfo.Types.AnyTypeInfo.Instance;

    protected override Task<TypeInfo> GetFieldTypeCoreAsync(string fieldName)
        => Task.FromResult<TypeInfo>(PeopleCodeTypeInfo.Types.AnyTypeInfo.Instance);

    protected override List<string> GetClassesInPackageCore(string packagePath)
        => _sources.Keys
            .Where(k => k.StartsWith(packagePath + ":", StringComparison.OrdinalIgnoreCase)
                && k.IndexOf(':', packagePath.Length + 1) < 0)
            .Select(k => k[(packagePath.Length + 1)..])
            .ToList();
}
