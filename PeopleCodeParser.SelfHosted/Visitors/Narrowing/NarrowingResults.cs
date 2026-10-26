using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Visitors.Narrowing;

/// <summary>
/// A counted use that no remaining candidate (or the settled type) has, with its N1/N2/N3 message.
/// </summary>
public sealed record NarrowingConflict(MemberAccessNode Use, string Message);

/// <summary>
/// What usage narrowing (spec §8) found for one <c>ApiObject</c> variable.
/// </summary>
public sealed class NarrowingResult
{
    public string VariableName { get; }

    /// <summary>
    /// The API type name, when the uses reached exactly one candidate.
    /// </summary>
    public string? NarrowedType { get; }

    /// <summary>
    /// The use that left one candidate.
    /// </summary>
    public MemberAccessNode? DecidingUse { get; }

    /// <summary>
    /// Remaining candidates in ordinal order: a single entry when decided, empty when no use was counted.
    /// </summary>
    public IReadOnlyList<string> Candidates { get; }

    public IReadOnlyList<NarrowingConflict> Conflicts { get; }

    internal NarrowingResult(
        string variableName,
        string? narrowedType,
        MemberAccessNode? decidingUse,
        IReadOnlyList<string> candidates,
        IReadOnlyList<NarrowingConflict> conflicts)
    {
        VariableName = variableName;
        NarrowedType = narrowedType;
        DecidingUse = decidingUse;
        Candidates = candidates;
        Conflicts = conflicts;
    }
}

/// <summary>
/// Usage narrowing results for a program, keyed by each variable's declaration node and name.
/// </summary>
public sealed class NarrowingResults
{
    private readonly Dictionary<VariableKey, NarrowingResult> _results;
    private readonly Dictionary<MemberAccessNode, NarrowingConflict> _conflicts;

    internal NarrowingResults(
        Dictionary<VariableKey, NarrowingResult> results,
        Dictionary<MemberAccessNode, NarrowingConflict> conflicts)
    {
        _results = results;
        _conflicts = conflicts;
    }

    /// <summary>
    /// The result for an eligible variable, or null if it isn't narrowed (not <c>ApiObject</c>,
    /// auto-declared or annotated). The name is matched case-insensitively.
    /// </summary>
    public NarrowingResult? For(AstNode declarationNode, string variableName) =>
        _results.TryGetValue(new VariableKey(declarationNode, variableName), out var result) ? result : null;

    /// <summary>
    /// The conflict reported on <paramref name="use"/>, if any.
    /// </summary>
    public NarrowingConflict? ConflictAt(MemberAccessNode use) =>
        _conflicts.TryGetValue(use, out var conflict) ? conflict : null;
}

/// <summary>
/// A variable's identity: its declaration node (by reference) and its name (case-insensitive).
/// </summary>
internal readonly struct VariableKey : IEquatable<VariableKey>
{
    public AstNode DeclarationNode { get; }
    public string Name { get; }

    public VariableKey(AstNode declarationNode, string name)
    {
        DeclarationNode = declarationNode;
        Name = name;
    }

    public bool Equals(VariableKey other) =>
        ReferenceEquals(DeclarationNode, other.DeclarationNode)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is VariableKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(DeclarationNode),
            StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
}
