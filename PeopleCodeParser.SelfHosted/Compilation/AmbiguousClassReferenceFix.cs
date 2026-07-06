namespace PeopleCodeParser.SelfHosted.Compilation;

/// <summary>
/// FixContext payload for <see cref="DiagnosticCode.AmbiguousClassReference"/>: the
/// unqualified class name plus every fully-qualified import path that provides it.
/// The paths are already resolved at check time, so the UI layer can build one
/// "Use {path}" quick fix per entry without re-querying the database.
/// </summary>
public sealed record AmbiguousClassReferenceFix(string ClassName, IReadOnlyList<string> ConflictingPaths);
