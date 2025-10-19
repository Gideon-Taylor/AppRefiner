using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes; // For AST nodes

namespace PluginSample
{
    /// <summary>
    /// Sample styler that uses the AST visitor pattern to highlight method names.
    /// Demonstrates how to use the new self-hosted parser visitor pattern.
    /// </summary>
    public class SampleStyler : BaseStyler
    {
        private const uint METHOD_NAME_COLOR = 0x90EE90; // Light Green

        public SampleStyler()
        {
        }

        public override string Description => "Sample: Highlights method names green.";

        /// <summary>
        /// Processes the entire program and resets state
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            base.VisitProgram(node);
        }

        /// <summary>
        /// Override the method called when visiting a method implementation in the AST
        /// </summary>
        public override void VisitMethodImpl(MethodImplNode node)
        {
            // The method name and its location are available through the node properties
            if (node.Declaration != null && node.Declaration.NameToken != null)
            {
                // Add an indicator using the AddIndicator helper method
                AddIndicator(
                    node.Declaration.NameToken.SourceSpan,
                    IndicatorType.HIGHLIGHTER, // Use HIGHLIGHTER for background color
                    METHOD_NAME_COLOR,
                    "Sample Styler: Method Name"
                );
            }

            // Continue visiting child nodes
            base.VisitMethodImpl(node);
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
