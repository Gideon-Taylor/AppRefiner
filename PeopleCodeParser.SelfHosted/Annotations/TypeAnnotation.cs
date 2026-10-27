using System.Text.RegularExpressions;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeTypeInfo.Database;
using PeopleCodeTypeInfo.Types;

namespace PeopleCodeParser.SelfHosted.Annotations;

/// <summary>
/// Which kind of declaration an annotation target may be placed on (besides <c>any</c>).
/// </summary>
public enum AnnotationCategory
{
    ApiObject,
    Record,
    Field
}

/// <summary>
/// The type named by a <c>/* as T */</c> annotation. <paramref name="Written"/> is T exactly as
/// typed, for use in messages.
/// </summary>
public sealed record AnnotationTarget(PeopleCodeType Type, TypeIdentity? Identity, string Written)
{
    public AnnotationCategory Category => Type switch
    {
        PeopleCodeType.Record => AnnotationCategory.Record,
        PeopleCodeType.Field => AnnotationCategory.Field,
        _ => AnnotationCategory.ApiObject
    };

    /// <summary>
    /// An annotation fits an <c>any</c> declaration, or one of its own category's declared type.
    /// </summary>
    public bool Fits(string declaredTypeName)
    {
        if (declaredTypeName.Equals("any", StringComparison.OrdinalIgnoreCase))
            return true;

        var required = Category switch
        {
            AnnotationCategory.Record => "record",
            AnnotationCategory.Field => "field",
            _ => "apiobject"
        };
        return declaredTypeName.Equals(required, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A comment recognised as a <c>/* as T */</c> annotation. Exactly one of
/// <paramref name="Target"/> and <paramref name="Error"/> is non-null.
/// </summary>
public sealed record ParsedAnnotation(Token Comment, AnnotationTarget? Target, string? Error);

/// <summary>
/// Parses <c>/* as T */</c> type annotation comments (spec §7). Only block comments whose whole
/// trimmed body is <c>as T</c> count; anything else is not an annotation.
/// </summary>
public static class TypeAnnotationParser
{
    private static readonly Regex AnnotationBody =
        new(@"^as\s+(\S+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns null when <paramref name="comment"/> is not an annotation. Otherwise returns the
    /// parsed target, or an error when T is not a recognised annotation type.
    /// </summary>
    public static ParsedAnnotation? TryParse(Token comment)
    {
        if (comment.Type != TokenType.BlockComment)
            return null;

        var body = comment.Text;
        if (body.StartsWith("/*", StringComparison.Ordinal))
            body = body[2..];
        if (body.EndsWith("*/", StringComparison.Ordinal))
            body = body[..^2];

        var match = AnnotationBody.Match(body.Trim());
        if (!match.Success)
            return null;

        var written = match.Groups[1].Value;
        var target = ParseTarget(written);
        return target != null
            ? new ParsedAnnotation(comment, target, null)
            : new ParsedAnnotation(comment, null, UnknownTypeMessage(written));
    }

    private static AnnotationTarget? ParseTarget(string written)
    {
        var parts = written.Split('.');
        if (parts.Any(string.IsNullOrEmpty))
            return null;

        if (parts.Length == 1)
        {
            if (!PeopleCodeTypeDatabase.IsApiObjectSubtype(written))
                return null;

            // A subtype the PeopleCodeType enum doesn't know can't be a target; report it as unknown
            var type = BuiltinTypeExtensions.FromString(written);
            return type != PeopleCodeType.Unknown ? new AnnotationTarget(type, null, written) : null;
        }

        var keyword = parts[0];
        if (keyword.Equals("CompIntfc", StringComparison.OrdinalIgnoreCase))
            return ParseCompIntfcTarget(parts, written);

        if (keyword.Equals("Record", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 2)
                return new AnnotationTarget(PeopleCodeType.Record, new TypeIdentity(RecordName: parts[1]), written);
            if (parts.Length == 3)
                return new AnnotationTarget(PeopleCodeType.Field, new TypeIdentity(RecordName: parts[1], FieldName: parts[2]), written);
        }

        if (keyword.Equals("Field", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            return new AnnotationTarget(PeopleCodeType.Field, new TypeIdentity(FieldName: parts[1]), written);

        return null;
    }

    /// <summary>
    /// <c>CompIntfc.X</c> is the CI, <c>CompIntfc.X.Coll[.Child…]</c> an item of that collection,
    /// and a trailing <c>[]</c> on the last part the collection itself. The CI itself takes no <c>[]</c>.
    /// </summary>
    private static AnnotationTarget? ParseCompIntfcTarget(string[] parts, string written)
    {
        if (parts.Length == 2)
        {
            return IsName(parts[1])
                ? new AnnotationTarget(PeopleCodeType.ComponentInterface, new TypeIdentity(CompIntfcName: parts[1]), written)
                : null;
        }

        var path = parts[2..];
        var type = PeopleCodeType.DataItem;
        if (path[^1].EndsWith("[]", StringComparison.Ordinal))
        {
            path[^1] = path[^1][..^2];
            type = PeopleCodeType.DataCollection;
        }

        if (!IsName(parts[1]) || !path.All(IsName))
            return null;

        return new AnnotationTarget(type, new TypeIdentity(CompIntfcName: parts[1], CompIntfcPath: string.Join('.', path)), written);
    }

    private static bool IsName(string part) => part.Length > 0 && !part.Contains('[') && !part.Contains(']');

    /// <summary>
    /// A CI, CI item or CI collection in annotation syntax (<c>CompIntfc.X</c>, <c>CompIntfc.X.A.B</c>,
    /// <c>CompIntfc.X.A.B[]</c>), or null when <paramref name="identity"/> names no CI.
    /// </summary>
    public static string? FormatCompIntfc(PeopleCodeType? type, TypeIdentity? identity)
    {
        if (string.IsNullOrEmpty(identity?.CompIntfcName))
            return null;

        var name = $"CompIntfc.{identity.CompIntfcName}";
        if (type is PeopleCodeType.DataItem or PeopleCodeType.DataCollection && !string.IsNullOrEmpty(identity.CompIntfcPath))
            name += $".{identity.CompIntfcPath}";
        return type == PeopleCodeType.DataCollection ? name + "[]" : name;
    }

    private static string UnknownTypeMessage(string written)
    {
        var message = $"Unknown type '{written}' in annotation.";
        var hint = written.ToLowerInvariant() switch
        {
            "compintfc" => " Did you mean ComponentInterface or CompIntfc.NAME?",
            "record" => " Did you mean Record.NAME?",
            "field" => " Did you mean Field.NAME or Record.REC.FIELD?",
            _ => ""
        };
        return message + hint;
    }
}
