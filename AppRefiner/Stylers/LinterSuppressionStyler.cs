using PeopleCodeParser.SelfHosted.Nodes;
using System.Text.RegularExpressions;
using AppRefiner.PeopleCode;
using System.Linq;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that highlights linter suppression comments.
/// This is a self-hosted equivalent to the AppRefiner's LinterSuppressionStyler.
/// </summary>
public class LinterSuppressionStyler : BaseStyler
{
    private const uint HIGHLIGHT_COLOR = 0x50CB5040; // Highlight color for suppression comments
    private readonly Regex suppressionPattern = new(@"#AppRefiner\s+suppress\s+\((.*?)\)", RegexOptions.Compiled);

    public override string Description => "Linter suppressions";

    /// <summary>
    /// Processes the entire program and looks for suppression comments
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
    /// Processes comments from the ProgramNode to find suppression directives
    /// </summary>
    private void ProcessComments(ProgramNode programNode)
    {
        if (programNode.Comments == null)
            return;

        foreach (var comment in programNode.Comments)
        {
            string text = comment.Text;

            // Check if this is a block comment containing suppression directive
            if (text.StartsWith("/*") && text.EndsWith("*/"))
            {
                var match = suppressionPattern.Match(text);
                if (match.Success)
                {
                    string suppressedRules = match.Groups[1].Value;

                    // Create indicator for the suppression comment
                    string tooltip = $"Suppressed rules: {suppressedRules}";
                    
                    AddIndicator(comment.SourceSpan, IndicatorType.HIGHLIGHTER, HIGHLIGHT_COLOR, tooltip);
                }
            }
            // Also check line comments (though less common for suppression)
            else if (text.StartsWith("//"))
            {
                var match = suppressionPattern.Match(text);
                if (match.Success)
                {
                    string suppressedRules = match.Groups[1].Value;

                    // Create indicator for the suppression comment
                    string tooltip = $"Suppressed rules: {suppressedRules}";
                    
                    AddIndicator(comment.SourceSpan, IndicatorType.HIGHLIGHTER, HIGHLIGHT_COLOR, tooltip);
                }
            }
        }
    }
}