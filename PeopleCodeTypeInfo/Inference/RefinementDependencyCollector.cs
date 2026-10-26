namespace PeopleCodeTypeInfo.Inference;

/// <summary>
/// Collects, per thread, the qualified names a refinement in progress looks up, so the refined
/// metadata can record what it was inferred from (<see cref="TypeMetadata.RefinementDependencies"/>)
/// and be evicted along with it. The parser's ReturnTypeRefiner installs a set per refinement frame;
/// <see cref="TypeCache.Get"/> records into whichever set is installed, and does nothing when none is.
/// </summary>
public static class RefinementDependencyCollector
{
    [ThreadStatic] private static HashSet<string>? _current;

    /// <summary>
    /// The set lookups on this thread record into, or null outside a refinement.
    /// </summary>
    public static HashSet<string>? Current
    {
        get => _current;
        set => _current = value;
    }

    /// <summary>
    /// Records a looked-up qualified name into the installed set, if any.
    /// </summary>
    public static void Record(string qualifiedName) => _current?.Add(qualifiedName);
}
