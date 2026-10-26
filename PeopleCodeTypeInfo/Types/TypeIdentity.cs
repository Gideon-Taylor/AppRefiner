namespace PeopleCodeTypeInfo.Types;

/// <summary>
/// The definition names a value is known to belong to, beyond its PeopleCodeType: a record,
/// a field (with or without its record) or a component interface. A partial identity (e.g. a
/// Field with only its field name) is allowed. Empty strings count as null, and names compare
/// case-insensitively. Identity is not part of TypeInfo equality; compare it explicitly with
/// IsRefinementOf / ConflictsWith.
/// </summary>
public sealed record TypeIdentity(string? RecordName = null, string? FieldName = null, string? CompIntfcName = null)
{
    /// <summary>
    /// True when no name is known.
    /// </summary>
    public bool IsEmpty => Normalize(RecordName) == null && Normalize(FieldName) == null && Normalize(CompIntfcName) == null;

    /// <summary>
    /// True when this identity says at least as much as <paramref name="less"/>: every name known
    /// in <paramref name="less"/> is the same here. Adding detail is a refinement.
    /// </summary>
    public bool IsRefinementOf(TypeIdentity? less)
    {
        if (less == null) return true;
        return Covers(RecordName, less.RecordName)
            && Covers(FieldName, less.FieldName)
            && Covers(CompIntfcName, less.CompIntfcName);
    }

    /// <summary>
    /// True when some name is known in both identities and differs.
    /// </summary>
    public bool ConflictsWith(TypeIdentity? other)
    {
        if (other == null) return false;
        return Differs(RecordName, other.RecordName)
            || Differs(FieldName, other.FieldName)
            || Differs(CompIntfcName, other.CompIntfcName);
    }

    private static string? Normalize(string? name) => string.IsNullOrEmpty(name) ? null : name;

    private static bool Covers(string? mine, string? less)
    {
        less = Normalize(less);
        return less == null || string.Equals(Normalize(mine), less, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Differs(string? a, string? b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return a != null && b != null && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
