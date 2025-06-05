using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class TodoFixmeStyler : BaseStyler
    {
        // Different colors for different comment types
        private const uint TODO_COLOR = 0x0080FF60; // Orange
        private const uint FIXME_COLOR = 0x0000FF60; // Red
        private const uint NOTE_COLOR = 0x00FFFF60; // Yellow
        private const uint CUSTOM_COLOR = 0x00FF8060; // Bright Green

        // Custom comment markers
        private readonly List<(string Marker, uint Color)> _customMarkers = new();

        public TodoFixmeStyler()
        {
            Description = "Highlights TODO, FIXME, and NOTE comments with different colors.";
            Active = true;
            
            // Initialize with default markers
            _customMarkers = new List<(string, uint)>
            {
                ("TODO", TODO_COLOR),
                ("FIXME", FIXME_COLOR),
                ("NOTE", NOTE_COLOR),
                ("TBD", NOTE_COLOR),
                ("HACK", CUSTOM_COLOR),
                ("BUG", CUSTOM_COLOR)
            };
        }

        /// <summary>
        /// Adds a custom comment marker to be highlighted
        /// </summary>
        /// <param name="marker">The marker text (e.g., "REVIEW")</param>
        /// <param name="color">The color to use for highlighting</param>
        public void AddCustomMarker(string marker, uint color)
        {
            if (!string.IsNullOrWhiteSpace(marker))
            {
                _customMarkers.Add((marker, color));
            }
        }

        public override void Reset()
        {
            Indicators = new List<Indicator>();
        }

        public override void EnterProgram([NotNull] ProgramContext context)
        {
            // Process any comments in the token stream
            if (Comments != null)
            {
                foreach (var comment in Comments)
                {
                    ProcessComment(comment);
                }
            }
        }

        private void ProcessComment(IToken comment)
        {
            string commentText = comment.Text;
            
            // Skip empty comments
            if (string.IsNullOrWhiteSpace(commentText))
                return;

            // Check if the comment is a single line
            if (commentText.Contains('\n') || commentText.Contains('\r'))
                return;

            // Remove comment markers (/* */, //, REM)
            string cleanedComment = commentText.TrimStart('/', '*', ' ', '\t').TrimEnd('*', '/', ' ', '\t');
            
            // Check for all registered markers
            foreach (var (marker, color) in _customMarkers)
            {
                if (Regex.IsMatch(cleanedComment, $@"{marker}\s*:?.*", RegexOptions.IgnoreCase))
                {
                    // Extract the content after the marker
                    var match = Regex.Match(cleanedComment, $@"^{marker}\s*:?\s*(.*)", RegexOptions.IgnoreCase);
                    string content = match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : "";
                    
                    // Add an indicator for this comment
                    Indicators?.Add(new Indicator
                    {
                        Start = comment.StartIndex,
                        Length = comment.StopIndex - comment.StartIndex + 1,
                        Color = color,
                        Type = IndicatorType.HIGHLIGHTER,
                        Tooltip = $"{marker}: {content}",
                        QuickFixes = []
                    });
                    
                    // Only process the first matching marker
                    break;
                }
            }
        }
    }
}
