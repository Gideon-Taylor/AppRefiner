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

    private static readonly Regex PartialAnnotationBody =
        new(@"^\s*as\s+([A-Za-z0-9_]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly TokenType[] DeclarationKeywords =
    {
        TokenType.Local, TokenType.Global, TokenType.Component, TokenType.Instance,
        TokenType.Property, TokenType.As, TokenType.Returns
    };

    /// <summary>
    /// True when <paramref name="textBeforeCursor"/> ends inside an unfinished <c>/* as T</c>
    /// annotation started on the current line. <paramref name="partial"/> is the T typed so far;
    /// <paramref name="category"/> is the annotated declaration's category, or null for <c>any</c>
    /// and value positions, where every type fits.
    /// </summary>
    public static bool TryGetPartialAnnotation(string textBeforeCursor, out AnnotationCategory? category, out string partial)
    {
        category = null;
        partial = "";

        // An unterminated comment runs to end of input, so it is the last token before EOF
        var tokens = new PeopleCodeLexer(textBeforeCursor).TokenizeAll();
        var significant = tokens.Where(t => t.Type != TokenType.EndOfFile).ToList();
        if (significant.Count < 2)
            return false;

        var comment = significant[^1];
        if (comment.Type != TokenType.BlockComment || comment.Text.Length >= 4 && comment.Text.EndsWith("*/", StringComparison.Ordinal))
            return false;

        var body = comment.Text[2..];
        if (body.Contains('\n') || body.Contains('\r'))
            return false;

        var match = PartialAnnotationBody.Match(body);
        if (!match.Success)
            return false;

        // The index only honours an annotation that starts on its anchor token's line
        var annotated = significant[^2];
        if (comment.SourceSpan.Start.Line != annotated.SourceSpan.End.Line)
            return false;

        // Package-qualified (MyPkg:MyClass) and array (array of T) declared types take no annotation
        var beforeAnnotated = significant.Count >= 3 ? significant[^3] : null;
        if (beforeAnnotated?.Type is TokenType.Colon or TokenType.Of)
            return false;

        if (beforeAnnotated != null && DeclarationKeywords.Contains(beforeAnnotated.Type))
        {
            // A declared type: only any, ApiObject, Record and Field take annotations
            if (annotated.Type == TokenType.Any)
            {
                partial = match.Groups[1].Value;
                return true;
            }

            category = annotated.Text.ToLowerInvariant() switch
            {
                "apiobject" => AnnotationCategory.ApiObject,
                "record" => AnnotationCategory.Record,
                "field" => AnnotationCategory.Field,
                _ => null
            };
            partial = category != null ? match.Groups[1].Value : "";
            return category != null;
        }

        // After an expression or its statement's ';' the annotation is on a value: any type fits
        var endsValue = annotated.Type.IsIdentifier() || annotated.Type.IsLiteral() ||
                        annotated.Type is TokenType.RightParen or TokenType.RightBracket or TokenType.Semicolon;
        if (!endsValue)
            return false;

        var valueEnd = significant.Count - (annotated.Type == TokenType.Semicolon ? 3 : 2);
        if (!IsAssignedValue(significant, valueEnd))
            return false;

        partial = match.Groups[1].Value;
        return true;
    }

    /// <summary>
    /// Whether the expression ending at <paramref name="end"/> is the value of an assignment or a
    /// declaration's initializer, the only values an annotation applies to: scanning back at bracket
    /// depth 0 reaches an <c>=</c> before an unmatched bracket or the start of the statement.
    /// </summary>
    private static bool IsAssignedValue(List<Token> tokens, int end)
    {
        int depth = 0;
        for (int i = end; i >= 0; i--)
        {
            var type = tokens[i].Type;
            if (type.IsCommentType())
                continue;

            if (type is TokenType.RightParen or TokenType.RightBracket)
                depth++;
            else if (type is TokenType.LeftParen or TokenType.LeftBracket)
            {
                if (depth-- == 0)
                    return false;
            }
            else if (depth == 0)
            {
                if (type == TokenType.Equal)
                    return true;
                if (StatementBoundaries.Contains(type))
                    return false;
            }
        }
        return false;
    }

    private static readonly HashSet<TokenType> StatementBoundaries =
    [
        TokenType.Semicolon, TokenType.If, TokenType.Then, TokenType.Else, TokenType.While,
        TokenType.Until, TokenType.Repeat, TokenType.For, TokenType.Evaluate, TokenType.When,
        TokenType.WhenOther, TokenType.Return, TokenType.Break, TokenType.Exit, TokenType.Error,
        TokenType.Warning
    ];

    /// <summary>
    /// The annotation types to suggest for <paramref name="category"/>, in display order. Entries
    /// ending in <c>.</c> are keyword prefixes the user completes with a definition name. They come
    /// first: the list keeps this order and selects the first match, so <c>CompI</c> must reach
    /// <c>CompIntfc.</c> before <c>CompIntfcCollection</c>.
    /// </summary>
    public static IReadOnlyList<string> SuggestedTypes(AnnotationCategory? category) => category switch
    {
        AnnotationCategory.ApiObject => ["CompIntfc.", .. PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()],
        AnnotationCategory.Record => ["Record."],
        AnnotationCategory.Field => ["Field.", "Record."],
        _ => ["CompIntfc.", "Field.", "Record.", .. PeopleCodeTypeDatabase.GetApiObjectSubtypeNames()]
    };

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
    /// and a trailing <c>[]</c> on the last part the collection itself. <c>CompIntfc.X[]</c> is the
    /// collection <c>Find()</c> returns: instances of the CI.
    /// </summary>
    private static AnnotationTarget? ParseCompIntfcTarget(string[] parts, string written)
    {
        if (parts.Length == 2)
        {
            if (parts[1].EndsWith("[]", StringComparison.Ordinal) && IsName(parts[1][..^2]))
                return new AnnotationTarget(PeopleCodeType.DataCollection, new TypeIdentity(CompIntfcName: parts[1][..^2]), written);

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
