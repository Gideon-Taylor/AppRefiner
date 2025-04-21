using AppRefiner.Stylers;
using static AppRefiner.PeopleCode.PeopleCodeParser;
using System.Collections.Generic; // For List

namespace PluginSample
{
    public class SampleCommentStyler : BaseStyler
    {
        private const uint HIGHLIGHT_COLOR = 0xADD8E6; // Light Blue

        public SampleCommentStyler()
        {
            Description = "Sample: Highlights all comments.";
            Active = true;
        }

        // Comments are provided as a list `Comments`. We can process this within a suitable listener method.
        // For simplicity, let's use EnterProgram to iterate over comments.
        public override void EnterProgram(ProgramContext context)
        {
            if (Comments == null) return;

            foreach (var commentToken in Comments)
            {
                Indicators?.Add(new Indicator
                {
                    Start = commentToken.StartIndex,
                    Length = commentToken.StopIndex - commentToken.StartIndex + 1,
                    Color = HIGHLIGHT_COLOR,
                    Type = IndicatorType.HIGHLIGHTER, // Or TEXTCOLOR, SQUIGGLE
                    Tooltip = "Sample Styler: This is a comment"
                });
            }
        }
    }
} 