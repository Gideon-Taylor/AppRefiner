using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes; // For AST nodes
using PeopleCodeParser.SelfHosted.Lexing; // For Token

namespace PluginSample
{
    /// <summary>
    /// Sample styler that highlights all comments.
    /// Demonstrates how to access and process comments with the new self-hosted parser.
    /// </summary>
    public class SampleCommentStyler : BaseStyler
    {
        private const uint HIGHLIGHT_COLOR = 0xADD8E6; // Light Blue

        public SampleCommentStyler()
        {
        }

        public override string Description => "Sample: Highlights all comments.";

        /// <summary>
        /// Processes the entire program to find and highlight comments
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            Reset();

            // Visit the program AST first
            base.VisitProgram(node);

            // After visiting the AST, process comments from the ProgramNode
            ProcessComments(node);
        }

        /// <summary>
        /// Processes all comments in the program
        /// </summary>
        private void ProcessComments(ProgramNode programNode)
        {
            if (programNode.Comments == null)
                return;

            foreach (var commentToken in programNode.Comments)
            {
                // Add an indicator for each comment using the AddIndicator helper method
                AddIndicator(
                    commentToken.SourceSpan,
                    IndicatorType.HIGHLIGHTER, // Or TEXTCOLOR, SQUIGGLE
                    HIGHLIGHT_COLOR,
                    "Sample Styler: This is a comment"
                );
            }
        }

        /// <summary>
        /// Resets the styler to its initial state
        /// </summary>
        protected override void OnReset()
        {
            // Base class handles clearing indicators automatically
            base.OnReset();
        }
    }
}
