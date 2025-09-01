namespace PluginSample
{
    // Example using ParseTreeTooltipProvider for context-aware tooltips
    public class SampleTooltipProvider : ParseTreeTooltipProvider
    {
        public override string Name => "Sample Tooltip Provider";
        public override string Description => "Sample: Shows tooltip for function names.";
        public override int Priority => 10; // Higher priority means checked first

        // Optional: Specify token types you care about to optimize parsing
        // public override int[]? TokenTypes => new[] { PeopleCodeLexer.FUNCTION };

        public override void EnterFunctionDefinition(FunctionDefinitionContext context)
        {
            var functionNameNode = context.allowableFunctionName();
            if (functionNameNode != null)
            {
                string functionName = functionNameNode.GetText();
                string tooltip = $"Sample Tooltip: This is function \"{functionName}\"";

                // Use helper to register the tooltip for the function name node
                RegisterTooltip(functionNameNode, tooltip);
            }
        }
    }
}