using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Visitors;
using System.ComponentModel;

namespace PeopleCodeParser.SelfHosted;

/// <summary>
/// Base class for all AST nodes in the self-hosted PeopleCode parser.
/// Provides common functionality for source location tracking, parent-child relationships,
/// and visitor pattern support.
/// </summary>
public abstract class AstNode
{
    private AstNode? _parent;
    private readonly List<AstNode> _children = new();

    /// <summary>
    /// First token that this AST node was constructed from
    /// </summary>
    public Token? FirstToken { get; set; }

    /// <summary>
    /// Last token that this AST node was constructed from
    /// </summary>
    public Token? LastToken { get; set; }

    /// <summary>
    /// Explicit SourceSpan (for backward compatibility during transition)
    /// </summary>
    private SourceSpan _explicitSourceSpan;

    /// <summary>
    /// Source location of this node in the original text (calculated from tokens or explicit)
    /// </summary>
    public SourceSpan SourceSpan
    {
        get
        {
            // Prefer calculated span from tokens
            if (FirstToken != null && LastToken != null)
            {
                return new SourceSpan(FirstToken.SourceSpan.Start, LastToken.SourceSpan.End);
            }
            // Fall back to explicit span (for backward compatibility)
            return _explicitSourceSpan;
        }
        set
        {
            _explicitSourceSpan = value;
        }
    }

    /// <summary>
    /// Parent node in the AST tree, null for root nodes
    /// </summary>
    public AstNode? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;

            // Remove from old parent
            _parent?._children.Remove(this);

            // Set new parent
            _parent = value;

            // Add to new parent
            if (value != null && !value._children.Contains(this))
            {
                value._children.Add(this);
            }
        }
    }

    /// <summary>
    /// Child nodes in the AST tree (read-only)
    /// </summary>
    public IReadOnlyList<AstNode> Children => _children.AsReadOnly();

    /// <summary>
    /// Additional attributes that can be attached to nodes for semantic analysis
    /// </summary>
    public Dictionary<string, object> Attributes { get; } = new();

    /// <summary>
    /// Accept method for visitor pattern
    /// </summary>
    public abstract void Accept(IAstVisitor visitor);

    /// <summary>
    /// Accept method for visitor pattern with return value
    /// </summary>
    public abstract TResult Accept<TResult>(IAstVisitor<TResult> visitor);

    /// <summary>
    /// Add a child node to this node
    /// </summary>
    protected void AddChild(AstNode child)
    {
        if (child == null) return;
        child.Parent = this;
    }

    /// <summary>
    /// Add multiple child nodes to this node
    /// </summary>
    protected void AddChildren(params AstNode[] children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
    }

    /// <summary>
    /// Add multiple child nodes to this node
    /// </summary>
    protected void AddChildren(IEnumerable<AstNode> children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
    }

    /// <summary>
    /// Remove a child node from this node
    /// </summary>
    protected void RemoveChild(AstNode child)
    {
        if (child?.Parent == this)
        {
            child.Parent = null;
        }
    }

    /// <summary>
    /// Find the first ancestor of the specified type
    /// </summary>
    public T? FindAncestor<T>() where T : AstNode
    {
        var current = Parent;
        while (current != null)
        {
            if (current is T ancestor)
                return ancestor;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Find all descendants of the specified type
    /// </summary>
    public IEnumerable<T> FindDescendants<T>() where T : AstNode
    {
        foreach (var child in Children)
        {
            if (child is T match)
                yield return match;

            foreach (var descendant in child.FindDescendants<T>())
                yield return descendant;
        }
    }

    /// <summary>
    /// Get the root node of this AST
    /// </summary>
    public AstNode GetRoot()
    {
        var current = this;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    public IEnumerable<AstNode> FindNodes(Func<AstNode, bool> predicate)
    {
        var nodesToVisit = new Stack<AstNode>();
        nodesToVisit.Push(this);

        while (nodesToVisit.Count > 0)
        {
            var currentNode = nodesToVisit.Pop();

            if (predicate(currentNode))
            {
                yield return currentNode;
            }

            // Push children in reverse order to visit them in declaration order
            for (int i = currentNode.Children.Count - 1; i >= 0; i--)
            {
                nodesToVisit.Push(currentNode.Children[i]);
            }
        }
    }

    /// <summary>
    /// Get all leading comments (from trivia on the first token)
    /// </summary>
    public IEnumerable<Token> GetLeadingComments()
    {
        return FirstToken?.LeadingTrivia
            .Where(token => token.Type.IsCommentType()) ?? Enumerable.Empty<Token>();
    }

    /// <summary>
    /// Get all trailing comments (from trivia on the last token)
    /// </summary>
    public IEnumerable<Token> GetTrailingComments()
    {
        return LastToken?.TrailingTrivia
            .Where(token => token.Type.IsCommentType()) ?? Enumerable.Empty<Token>();
    }

    /// <summary>
    /// Get all comments associated with this node (both leading and trailing)
    /// </summary>
    public IEnumerable<Token> GetAllComments()
    {
        return GetLeadingComments().Concat(GetTrailingComments());
    }

    /// <summary>
    /// Get a string representation of this node for debugging
    /// </summary>
    public override string ToString()
    {
        var typeName = GetType().Name;
        if (SourceSpan != default)
        {
            return $"{typeName} [{SourceSpan.Start}-{SourceSpan.End}]";
        }
        return typeName;
    }
}

/// <summary>
/// Represents a span in the source text with start and end positions
/// </summary>
public struct SourceSpan : IEquatable<SourceSpan>
{
    /// <summary>
    /// Start position in the source text (inclusive)
    /// </summary>
    public SourcePosition Start { get; }

    /// <summary>
    /// End position in the source text (exclusive)
    /// </summary>
    public SourcePosition End { get; }

    /// <summary>
    /// Length of the span in characters
    /// </summary>
    public int Length => End.Index - Start.Index;

    /// <summary>
    /// Length of the span in UTF-8 bytes (for Scintilla editor integration)
    /// </summary>
    public int ByteLength => End.ByteIndex - Start.ByteIndex;

    /// <summary>
    /// True if this is an empty span
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// True if this span represents a valid source location
    /// </summary>
    public bool IsValid => Start.ByteIndex >= 0 && End.ByteIndex >= Start.ByteIndex;

    /// <summary>
    /// Checks if this span contains the given position
    /// </summary>
    public bool ContainsPosition(int position) => position >= Start.ByteIndex && position <= End.ByteIndex;
    public bool ContainsLine(int line) => line >= Start.Line && line <= End.Line;

    public SourceSpan(SourcePosition start, SourcePosition end)
    {
        Start = start;
        End = end;
    }

    public SourceSpan(int startIndex, int endIndex)
    {
        Start = new SourcePosition(startIndex, startIndex, 0, 0);
        End = new SourcePosition(endIndex, endIndex, 0, 0);

    }

    public SourceSpan(int startIndex, int startByteIndex, int endIndex, int endByteIndex, int startLine = 1, int startColumn = 1, int endLine = 1, int endColumn = 1)
    {
        Start = new SourcePosition(startIndex, startByteIndex, startLine, startColumn);
        End = new SourcePosition(endIndex, endByteIndex, endLine, endColumn);
    }

    public bool Equals(SourceSpan other)
    {
        return Start.Equals(other.Start) && End.Equals(other.End);
    }

    public override bool Equals(object? obj)
    {
        return obj is SourceSpan other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }

    public static bool operator ==(SourceSpan left, SourceSpan right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SourceSpan left, SourceSpan right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"[{Start}-{End}]";
    }

}

/// <summary>
/// Represents a position in the source text
/// </summary>
public struct SourcePosition : IEquatable<SourcePosition>, IComparable<SourcePosition>
{
    /// <summary>
    /// Zero-based character index in the source text
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Zero-based UTF-8 byte index in the source text (for Scintilla editor integration)
    /// </summary>
    public int ByteIndex { get; }

    /// <summary>
    /// One-based line number
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// One-based column number
    /// </summary>
    public int Column { get; }

    public SourcePosition(int index, int line = 1, int column = 1)
    {
        Index = index;
        ByteIndex = index; // Default to character index for compatibility
        Line = line;
        Column = column;
    }

    public SourcePosition(int index, int byteIndex, int line = 0, int column = 0)
    {
        Index = index;
        ByteIndex = byteIndex;
        Line = line;
        Column = column;
    }

    public bool Equals(SourcePosition other)
    {
        return Index == other.Index && ByteIndex == other.ByteIndex && Line == other.Line && Column == other.Column;
    }

    public override bool Equals(object? obj)
    {
        return obj is SourcePosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Index, ByteIndex, Line, Column);
    }

    public int CompareTo(SourcePosition other)
    {
        return Index.CompareTo(other.Index);
    }

    public static bool operator ==(SourcePosition left, SourcePosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SourcePosition left, SourcePosition right)
    {
        return !(left == right);
    }

    public static bool operator <(SourcePosition left, SourcePosition right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(SourcePosition left, SourcePosition right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(SourcePosition left, SourcePosition right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(SourcePosition left, SourcePosition right)
    {
        return left.CompareTo(right) >= 0;
    }

    public override string ToString()
    {
        return $"{Line}:{Column}";
    }
}

/// <summary>
/// Extension methods for Scintilla editor integration
/// </summary>
public static class SourcePositionExtensions
{
    /// <summary>
    /// Get Scintilla-compatible byte position
    /// </summary>
    public static int ToScintillaPosition(this SourcePosition pos)
    {
        return pos.ByteIndex;
    }

    /// <summary>
    /// Get Scintilla-compatible byte range
    /// </summary>
    public static (int start, int end) ToScintillaRange(this SourceSpan span)
    {
        return (span.Start.ByteIndex, span.End.ByteIndex);
    }
}