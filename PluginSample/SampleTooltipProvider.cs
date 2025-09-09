using AppRefiner.TooltipProviders;
using PeopleCodeParser.SelfHosted.Nodes;

namespace PluginSample
{
    /// <summary>
    /// Sample tooltip provider that shows tooltips for function names.
    /// This demonstrates how to create a plugin tooltip provider using ScopedTooltipProvider.
    /// </summary>
    public class SampleTooltipProvider : BaseTooltipProvider
    {
        public override string Name => "Sample Tooltip Provider";
        public override string Description => "Sample: Shows tooltip for function names.";
        public override int Priority => 10; // Higher priority means checked first

        /// <summary>
        /// Override VisitFunction to provide tooltips for function definitions
        /// </summary>
        public override void VisitFunction(FunctionNode node)
        {
            // Check if the current position is within this function's name
            if (ContainsPosition(node.NameToken.SourceSpan))
            {
                string tooltip = $"Sample Tooltip: This is function \"{node.Name}\"";
                RegisterTooltip(node.NameToken.SourceSpan, tooltip);
            }

            base.VisitFunction(node);
        }

        /// <summary>
        /// Override VisitMethod to provide tooltips for method definitions
        /// </summary>
        public override void VisitMethod(MethodNode node)
        {
            // Check if the current position is within this method's name
            if (ContainsPosition(node.NameToken.SourceSpan))
            {
                string tooltip = $"Sample Tooltip: This is method \"{node.Name}\"";
                RegisterTooltip(node.NameToken.SourceSpan, tooltip);
            }

            base.VisitMethod(node);
        }
    }
}