using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Text.RegularExpressions;
namespace AppRefiner.Stylers;

/// <summary>
/// Highlights TODO, FIXME, and NOTE comments with different colors.
/// This is a self-hosted equivalent to the ANTLR-based TodoFixmeStyler.
/// </summary>
public class TodoFixmeStyler : BaseStyler
{
    // Different colors for different comment types
    private const uint TODO_COLOR = 0x0080FF60; // Orange
    private const uint FIXME_COLOR = 0x0000FF60; // Red
    private const uint NOTE_COLOR = 0x00FFFF60; // Yellow
    private const uint CUSTOM_COLOR = 0x00FF8060; // Bright Green

    // Custom comment markers with their colors
    private readonly List<(string Marker, uint Color, Regex Pattern)> _markers = new();

    public TodoFixmeStyler()
    {
        // Initialize with default markers and pre-compiled regex patterns
        _markers = new List<(string, uint, Regex)>
        {
            ("TODO", TODO_COLOR, new Regex(@"TODO\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("FIXME", FIXME_COLOR, new Regex(@"FIXME\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("NOTE", NOTE_COLOR, new Regex(@"NOTE\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("TBD", NOTE_COLOR, new Regex(@"TBD\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("HACK", CUSTOM_COLOR, new Regex(@"HACK\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("BUG", CUSTOM_COLOR, new Regex(@"BUG\s*:?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        };
    }

    public override string Description => "TODO/FIXME comments";

    /// <summary>
    /// Adds a custom comment marker to be highlighted
    /// </summary>
    /// <param name="marker">The marker text (e.g., "REVIEW")</param>
    /// <param name="color">The color to use for highlighting</param>
    public void AddCustomMarker(string marker, uint color)
    {
        if (!string.IsNullOrWhiteSpace(marker))
        {
            var pattern = new Regex($@"{Regex.Escape(marker)}\s*:?\s*(.*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _markers.Add((marker, color, pattern));
        }
    }

    /// <summary>
    /// Processes the entire program and looks for TODO/FIXME comments
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();

        // Visit the program first
        base.VisitProgram(node);

        // After visiting the AST, process comments from the ProgramNode
        ProcessComments(node);
    }

    /// <summary>
    /// Processes comments from the ProgramNode to find TODO/FIXME markers
    /// </summary>
    private void ProcessComments(ProgramNode programNode)
    {
        if (programNode.Comments == null)
            return;

        foreach (var comment in programNode.Comments)
        {
            ProcessComment(comment);
        }
    }

    /// <summary>
    /// Processes a single comment for TODO/FIXME markers
    /// </summary>
    private void ProcessComment(Token comment)
    {
        string commentText = comment.Text;

        // Skip empty comments
        if (string.IsNullOrWhiteSpace(commentText))
            return;

        // Skip multi-line comments for simplicity (matching original behavior)
        if (commentText.Contains('\n') || commentText.Contains('\r'))
            return;

        // Clean the comment text by removing comment markers
        string cleanedComment = CleanCommentText(commentText);

        // Check for all registered markers
        foreach (var (marker, color, pattern) in _markers)
        {
            var match = pattern.Match(cleanedComment);
            if (match.Success)
            {
                // Extract the content after the marker
                string content = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : "";

                AddIndicator(
                    comment.SourceSpan,
                    IndicatorType.HIGHLIGHTER,
                    color,
                    $"{marker}: {content}"
                );

                // Only process the first matching marker
                break;
            }
        }
    }

    /// <summary>
    /// Cleans comment text by removing comment markers (/* */, //, REM)
    /// </summary>
    private static string CleanCommentText(string commentText)
    {
        // Handle block comments /* ... */
        if (commentText.StartsWith("/*") && commentText.EndsWith("*/"))
        {
            return commentText.Substring(2, commentText.Length - 4).Trim();
        }

        // Handle line comments // ...
        if (commentText.StartsWith("//"))
        {
            return commentText.Substring(2).Trim();
        }

        // Handle REM comments (PeopleCode specific)
        if (commentText.StartsWith("REM", System.StringComparison.OrdinalIgnoreCase))
        {
            return commentText.Substring(3).Trim();
        }

        // Fallback: just trim whitespace and common comment characters
        return commentText.TrimStart('/', '*', ' ', '\t').TrimEnd('*', '/', ' ', '\t');
    }
}