using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Text;
using System.Text.RegularExpressions;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Linters
{
    public class TodoFixmeLinter : BaseLintRule
    {
        public override string LINTER_ID => "TODOS";

        // Different comment types for counters
        public int TodoCount { get; private set; }
        public int FixmeCount { get; private set; }
        public int NoteCount { get; private set; }
        public int CustomCount { get; private set; }

        // Collection of markers to identify in comments
        private readonly List<(string Marker, string Type)> _markers = new()
        {
            ("TODO", "TODO"),
            ("FIXME", "FIXME"),
            ("NOTE", "NOTE"),
            ("TBD", "NOTE"),
            ("HACK", "CUSTOM"),
            ("BUG", "CUSTOM")
        };

        // Collection of comments by type
        private readonly List<(string Type, string Content, int LineNumber, (int Start, int Stop) Span)> _collectedComments = new();

        public TodoFixmeLinter()
        {
            Description = "Reports TODO, FIXME, and NOTE comments with a summary at the end of the file";
            Active = true;
            Type = ReportType.Info;
        }

        /// <summary>
        /// Adds a custom marker to be identified in comments
        /// </summary>
        /// <param name="marker">The marker text (e.g., "REVIEW")</param>
        /// <param name="type">The type to categorize it as</param>
        public void AddCustomMarker(string marker, string type)
        {
            if (!string.IsNullOrWhiteSpace(marker))
            {
                _markers.Add((marker, type));
            }
        }

        public override void Reset()
        {
            TodoCount = 0;
            FixmeCount = 0;
            NoteCount = 0;
            CustomCount = 0;
            _collectedComments.Clear();
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
            // If no comments were found, no need to add report
            if (_collectedComments.Count == 0)
                return;
            
            // Get the last token to determine the end of the program
            int lastLine = 0;
            (int Start, int Stop) lastSpan = (0, 0);
            
            if (context.Stop != null)
            {
                lastLine = context.Stop.Line;
                lastSpan = (context.Stop.ByteStartIndex(), context.Stop.ByteStopIndex());
            }
            
            // Build a summary string
            var summaryBuilder = new StringBuilder();
            
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
                    var (_, content, lineNumber, _) = todoComments[i];
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
                    var (_, content, lineNumber, _) = fixmeComments[i];
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
                    var (_, content, lineNumber, _) = noteComments[i];
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
                        var (_, content, lineNumber, _) = comments[i];
                        summaryBuilder.AppendLine($"  #{i + 1}: {content} (line {lineNumber})");
                    }
                }
            }
            
            // Add a single report with the summary
            AddReport(1, summaryBuilder.ToString().TrimEnd(), ReportType.Info, lastLine, lastSpan);

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
            foreach (var (marker, type) in _markers)
            {
                if (Regex.IsMatch(cleanedComment, $@"{marker}\s*:?.*", RegexOptions.IgnoreCase))
                {
                    // Extract the content after the marker
                    var match = Regex.Match(cleanedComment, $@"^{marker}\s*:?\s*(.*)", RegexOptions.IgnoreCase);
                    string content = match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : "";
                    if (string.IsNullOrWhiteSpace(content))
                        continue;
                    
                    // Determine the type of comment for counting
                    if (type.Equals("TODO", StringComparison.OrdinalIgnoreCase))
                    {
                        TodoCount++;
                        _collectedComments.Add(("TODO", content, comment.Line, (comment.ByteStartIndex(), comment.ByteStopIndex())));
                    }
                    else if (type.Equals("FIXME", StringComparison.OrdinalIgnoreCase))
                    {
                        FixmeCount++;
                        _collectedComments.Add(("FIXME", content, comment.Line, (comment.ByteStartIndex(), comment.ByteStopIndex())));
                    }
                    else if (type.Equals("NOTE", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteCount++;
                        _collectedComments.Add(("NOTE", content, comment.Line, (comment.ByteStartIndex(), comment.ByteStopIndex())));
                    }
                    else
                    {
                        CustomCount++;
                        _collectedComments.Add((type, content, comment.Line, (comment.ByteStartIndex(), comment.ByteStopIndex())));
                    }
                    
                    // Only process the first matching marker
                    break;
                }
            }
        }
    }
} 