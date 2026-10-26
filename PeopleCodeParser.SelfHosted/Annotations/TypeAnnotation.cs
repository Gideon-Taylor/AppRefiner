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
            return PeopleCodeTypeDatabase.IsApiObjectSubtype(written)
                ? new AnnotationTarget(BuiltinTypeExtensions.FromString(written), null, written)
                : null;
        }

        var keyword = parts[0];
        if (keyword.Equals("CompIntfc", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            return new AnnotationTarget(PeopleCodeType.ComponentInterface, new TypeIdentity(CompIntfcName: parts[1]), written);

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
