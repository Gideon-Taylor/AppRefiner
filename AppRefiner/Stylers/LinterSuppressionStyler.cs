using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Text.RegularExpressions;

namespace AppRefiner.Stylers
{
    public class LinterSuppressionStyler : BaseStyler
    {
        private const uint HILIGHT_COLOR = 0x50CB5040;
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
            if (Comments == null || Indicators == null)
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
                        string suppressedRules = match.Groups[1].Value;
                        // This is a suppression comment, highlight it
                        Indicators.Add(new Indicator
                        {
                            Start = comment.StartIndex,
                            Length = comment.Text.Length,
                            Color = HILIGHT_COLOR,
                            Type = IndicatorType.HIGHLIGHTER,
                            Tooltip = $"Suppressed rules: {suppressedRules}",
                            QuickFixes = []
                        });
                    }
                }
            }
        }
    }
}
