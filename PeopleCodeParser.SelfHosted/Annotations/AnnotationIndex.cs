using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PeopleCodeParser.SelfHosted.Annotations;

/// <summary>
/// Finds a program's <c>/* as T */</c> annotations by position (spec §7). An annotation counts
/// only directly after a type node, or directly after an assignment's value (either side of the
/// <c>;</c>), on the same line. Use <see cref="ProgramNode.Annotations"/> for the cached instance.
/// </summary>
public sealed class AnnotationIndex
{
    // Annotations keyed by the end index of the significant token directly before them
    private readonly Dictionary<int, ParsedAnnotation> _byPreviousEnd = new();

    // Annotations directly after a ';', keyed by the end index of the token before the ';'
    private readonly Dictionary<int, ParsedAnnotation> _bySemicolonPrecedingEnd = new();

    private AnnotationIndex()
    {
    }

    public static AnnotationIndex Build(ProgramNode program)
    {
        var index = new AnnotationIndex();
        foreach (var context in program.CommentContexts)
        {
            if (context.Previous == null)
                continue;

            var annotation = TypeAnnotationParser.TryParse(context.Comment);
            if (annotation == null)
                continue;

            index._byPreviousEnd.TryAdd(context.Previous.SourceSpan.End.Index, annotation);
            if (context.Previous.Type == TokenType.Semicolon && context.BeforePrevious != null)
                index._bySemicolonPrecedingEnd.TryAdd(context.BeforePrevious.SourceSpan.End.Index, annotation);
        }
        return index;
    }

    /// <summary>
    /// The annotation directly after <paramref name="typeNode"/>, on the same line.
    /// </summary>
    public ParsedAnnotation? ForType(TypeNode? typeNode) =>
        typeNode == null ? null : Lookup(_byPreviousEnd, typeNode.SourceSpan);

    /// <summary>
    /// The annotation directly after <paramref name="value"/>, or directly after the <c>;</c>
    /// that follows it, on the same line.
    /// </summary>
    public ParsedAnnotation? ForValue(ExpressionNode? value) =>
        value == null
            ? null
            : Lookup(_byPreviousEnd, value.SourceSpan) ?? Lookup(_bySemicolonPrecedingEnd, value.SourceSpan);

    private static ParsedAnnotation? Lookup(Dictionary<int, ParsedAnnotation> map, SourceSpan anchor)
    {
        if (anchor.IsEmpty)
            return null;

        return map.TryGetValue(anchor.End.Index, out var annotation)
               && annotation.Comment.SourceSpan.Start.Line == anchor.End.Line
            ? annotation
            : null;
    }
}
