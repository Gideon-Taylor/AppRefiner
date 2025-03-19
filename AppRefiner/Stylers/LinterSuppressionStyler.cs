using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Text.RegularExpressions;

namespace AppRefiner.Stylers
{
    public class LinterSuppressionStyler : BaseStyler
    {
        private readonly Regex suppressionPattern = new(@"#AppRefiner\s+suppress\s+\((.*?)\)", RegexOptions.Compiled);

        public LinterSuppressionStyler()
        {
            Description = "Highlights linter suppression comments";
            Active = true;
        }

        public override void Reset()
        {
            // Nothing to reset
        }

        public override void ExitProgram([NotNull] PeopleCodeParser.ProgramContext context)
        {
            base.ExitProgram(context);
            ProcessComments();
        }

        public void ProcessComments()
        {
            if (Comments == null || Highlights == null)
                return;

            foreach (var comment in Comments)
            {
                string text = comment.Text;

                // Check if this is a block comment
                if (text.StartsWith("/*") && text.EndsWith("*/"))
                {
                    var match = suppressionPattern.Match(text);
                    if (match.Success)
                    {
                        // This is a suppression comment, highlight it
                        Highlights.Add(new CodeHighlight
                        {
                            Start = comment.StartIndex,
                            Length = comment.Text.Length,
                            Color = HighlightColor.LinterSuppression
                        });

                        // We could parse the suppressions if needed
                        string suppressionContent = match.Groups[1].Value;
                        // Extract individual suppressions (ID:Number or ID:* format)
                        // string[] suppressions = suppressionContent.Split(',');
                    }
                }
            }
        }
    }
}
