namespace PeopleCodeParser.SelfHosted.Visitors.Models;

/// <summary>
/// Why a variable's refined type was set
/// </summary>
public enum RefinementKind
{
    /// <summary>
    /// Refined from the value assigned to the variable
    /// </summary>
    Assignment,

    /// <summary>
    /// Refined from a <c>/* as T */</c> type annotation
    /// </summary>
    Annotation,

    /// <summary>
    /// Narrowed from how the variable is used
    /// </summary>
    Narrowing
}

/// <summary>
/// Records where a variable's refined type came from. Kept beside the refined type and
/// never part of type comparisons.
/// </summary>
/// <param name="Kind">How the refinement was made</param>
/// <param name="Node">The node that set the refinement (declaration or assignment)</param>
public sealed record RefinementSource(RefinementKind Kind, AstNode Node)
{
    /// <summary>
    /// Zero-based line of the node that set the refinement
    /// </summary>
    public int Line => Node.SourceSpan.Start.Line;
}
