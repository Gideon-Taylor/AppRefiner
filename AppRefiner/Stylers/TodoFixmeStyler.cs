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

        // Counters for different comment types
        public int TodoCount { get; private set; }
        public int FixmeCount { get; private set; }
        public int NoteCount { get; private set; }
        public int CustomCount { get; private set; }

        // Custom comment markers
        private readonly List<(string Marker, uint Color)> _customMarkers = new();
        
        // Collection of comments to add as annotations at the end
        private readonly List<(string Type, string Content, int LineNumber)> _collectedComments = new();

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
            TodoCount = 0;
            FixmeCount = 0;
            NoteCount = 0;
            CustomCount = 0;
            
            _collectedComments.Clear();
            Highlights = new List<CodeHighlight>();
            Annotations = new List<CodeAnnotation>();
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

        public override void ExitProgram([NotNull] ProgramContext context)
        {
            // If no comments were found, no need to add annotations
            if (_collectedComments.Count == 0)
                return;
            
            // Get the last token to determine the end of the program
            int lastLine = 0;
            if (context.Stop != null)
            {
                lastLine = context.Stop.Line;
            }
            
            // Build a summary string
            var summaryBuilder = new System.Text.StringBuilder();
            
            // Add overall summary
            summaryBuilder.AppendLine($"Found: {TodoCount} TODOs, {FixmeCount} FIXMEs, {NoteCount} NOTEs" + 
                (CustomCount > 0 ? $", {CustomCount} custom markers" : ""));
            
            // Group comments by type
            var todoComments = _collectedComments.Where(c => c.Type.Equals("TODO", StringComparison.OrdinalIgnoreCase)).ToList();
            var fixmeComments = _collectedComments.Where(c => c.Type.Equals("FIXME", StringComparison.OrdinalIgnoreCase)).ToList();
            var noteComments = _collectedComments.Where(c => c.Type.Equals("NOTE", StringComparison.OrdinalIgnoreCase)).ToList();
            var customComments = _collectedComments.Where(c => 
                !c.Type.Equals("TODO", StringComparison.OrdinalIgnoreCase) && 
                !c.Type.Equals("FIXME", StringComparison.OrdinalIgnoreCase) && 
                !c.Type.Equals("NOTE", StringComparison.OrdinalIgnoreCase)).ToList();
            
            // Add TODOs
            if (todoComments.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("TODOs:");
                for (int i = 0; i < todoComments.Count; i++)
                {
                    var (_, content, lineNumber) = todoComments[i];
                    summaryBuilder.AppendLine($"  #{i + 1}: {content} (line {lineNumber})");
                }
            }
            
            // Add FIXMEs
            if (fixmeComments.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("FIXMEs:");
                for (int i = 0; i < fixmeComments.Count; i++)
                {
                    var (_, content, lineNumber) = fixmeComments[i];
                    summaryBuilder.AppendLine($"  #{i + 1}: {content} (line {lineNumber})");
                }
            }
            
            // Add NOTEs
            if (noteComments.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("NOTEs:");
                for (int i = 0; i < noteComments.Count; i++)
                {
                    var (_, content, lineNumber) = noteComments[i];
                    summaryBuilder.AppendLine($"  #{i + 1}: {content} (line {lineNumber})");
                }
            }
            
            // Add custom markers
            if (customComments.Count > 0)
            {
                // Group by marker type
                var customGroups = customComments.GroupBy(c => c.Type);
                
                foreach (var group in customGroups)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine($"{group.Key}s:");
                    
                    var comments = group.ToList();
                    for (int i = 0; i < comments.Count; i++)
                    {
                        var (_, content, lineNumber) = comments[i];
                        summaryBuilder.AppendLine($"  #{i + 1}: {content} (line {lineNumber})");
                    }
                }
            }
            
            // Add the single annotation with all comments
            Annotations?.Add(new CodeAnnotation
            {
                Message = summaryBuilder.ToString().TrimEnd(),
                LineNumber = lastLine
            });
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
                    AddHighlight(comment, color);
                    
                    // Extract the content after the marker
                    var match = Regex.Match(cleanedComment, $@"^{marker}\s*:?\s*(.*)", RegexOptions.IgnoreCase);
                    string content = match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : "";
                    if (string.IsNullOrWhiteSpace(content))
                        continue;
                    // Determine the type of comment for counting
                    if (marker.Equals("TODO", StringComparison.OrdinalIgnoreCase))
                    {
                        TodoCount++;
                        _collectedComments.Add(("TODO", content, comment.Line));
                    }
                    else if (marker.Equals("FIXME", StringComparison.OrdinalIgnoreCase))
                    {
                        FixmeCount++;
                        _collectedComments.Add(("FIXME", content, comment.Line));
                    }
                    else if (marker.Equals("NOTE", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteCount++;
                        _collectedComments.Add(("NOTE", content, comment.Line));
                    }
                    else
                    {
                        CustomCount++;
                        _collectedComments.Add((marker, content, comment.Line));
                    }
                    
                    // Only process the first matching marker
                    break;
                }
            }
        }

        private void AddHighlight(IToken token, uint color)
        {
            Highlights?.Add(new CodeHighlight
            {
                Start = token.StartIndex,
                Length = token.StopIndex - token.StartIndex + 1,
                Color = color
            });
        }
    }
}
