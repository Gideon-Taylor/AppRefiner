using PeopleCodeParser.SelfHosted.Visitors;

namespace PeopleCodeParser.SelfHosted.Nodes;

/// <summary>
/// Represents an interpolated string expression: $"Hello, {&name}!"
/// </summary>
public class InterpolatedStringNode : ExpressionNode
{
    /// <summary>
    /// The parts of the interpolated string (alternating StringFragments and Interpolations)
    /// </summary>
    private readonly List<InterpolatedStringPart> _parts;
    public IReadOnlyList<InterpolatedStringPart> Parts => _parts;

    /// <summary>
    /// True if error recovery occurred during parsing (unterminated string, unclosed braces, etc.)
    /// </summary>
    public bool HasErrors { get; set; }

    public override bool HasSideEffects => Parts.Any(p => p is Interpolation i && i.Expression?.HasSideEffects == true);

    public InterpolatedStringNode(IEnumerable<InterpolatedStringPart> parts, bool hasErrors = false)
    {
        _parts = parts?.ToList() ?? new List<InterpolatedStringPart>();
        HasErrors = hasErrors;

        AddChildren(_parts);
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitInterpolatedString(this);
    }


    public override string ToString()
    {
        var content = string.Join("", Parts.Select(p => p.ToString()));
        return $"${content}";
    }
}

/// <summary>
/// Base class for parts of an interpolated string (either literal text or an expression)
/// </summary>
public abstract class InterpolatedStringPart : AstNode
{
}

/// <summary>
/// Represents a literal string fragment within an interpolated string
/// Example: In $"Hello, {&name}!", "Hello, " and "!" are StringFragments
/// </summary>
public class StringFragment : InterpolatedStringPart
{
    /// <summary>
    /// The literal text content of this fragment
    /// </summary>
    public string Text { get; }

    public StringFragment(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitStringFragment(this);
    }


    public override string ToString()
    {
        return Text;
    }
}

/// <summary>
/// Represents an interpolated expression within an interpolated string
/// Example: In $"Hello, {&name}!", {&name} is an Interpolation
/// </summary>
public class Interpolation : InterpolatedStringPart
{
    /// <summary>
    /// The expression to be interpolated (may be null for empty/error cases like {})
    /// </summary>
    public ExpressionNode? Expression { get; }

    /// <summary>
    /// True if this interpolation is incomplete or malformed (empty braces, unclosed, etc.)
    /// </summary>
    public bool HasErrors { get; set; }

    public Interpolation(ExpressionNode? expression, bool hasErrors = false)
    {
        Expression = expression;
        HasErrors = hasErrors;

        if (expression != null)
        {
            AddChild(expression);
        }
    }

    public override void Accept(IAstVisitor visitor)
    {
        visitor.VisitInterpolation(this);
    }


    public override string ToString()
    {
        if (Expression == null)
        {
            return "{}";
        }
        return $"{{{Expression}}}";
    }
}
