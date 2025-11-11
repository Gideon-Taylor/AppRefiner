using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltip showing the inferred type for any expression at the cursor position.
    /// Uses manual tree traversal to find the most specific AST node with type information.
    /// </summary>
    public class InferredTypeTooltipProvider : BaseTooltipProvider
    {
        public override string Name => "Inferred Type";

        public override string Description => "Shows inferred type information for expressions at the cursor position";

        public override int Priority => 40; // Lower priority than variable/method-specific tooltips

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;

        public override void ProcessProgram(ProgramNode program, int position, int lineNumber)
        {
            base.ProcessProgram(program, position, lineNumber);

            // Find the most specific node containing the cursor position that has type information
            var bestSpanLength = int.MaxValue;
            SourceSpan? bestSpan = null;
            TypeInfo? bestTypeInfo = null;
            AstNode? bestNode = null;
            var allNodes = program.FindNodes(n => n.SourceSpan.ContainsPositionExclusiveEnd(position)).ToList();
            foreach (var node in program.FindNodes(n => n.SourceSpan.ContainsPositionExclusiveEnd(position))) {

                if (node is IdentifierNode idn && node.Parent is FunctionCallNode fcn && fcn.Function is IdentifierNode idn2 && idn2 == idn) {
                    return;
                }


                if (node.SourceSpan.ByteLength < bestSpanLength)
                {
                    var typeInfo = node.GetInferredType();
                    if (typeInfo != null)
                    {
                        bestSpan = node.SourceSpan;
                        bestNode = node;
                        bestSpanLength = node.SourceSpan.ByteLength;
                        bestTypeInfo = typeInfo;
                    }
                }

            }
                           

            // Filter out unknown types - only show tooltip for concrete type information
            if (bestSpan.HasValue && bestTypeInfo != null && bestTypeInfo.Kind != TypeKind.Unknown)
            {
                string tooltipText = FormatTypeInfo(bestTypeInfo);
                RegisterTooltip(bestSpan.Value, tooltipText);
            }
            
        }

        

        /// <summary>
        /// Formats type information for display in the tooltip.
        /// Uses minimal formatting - just the type name.
        /// </summary>
        /// <param name="typeInfo">The type information to format</param>
        /// <returns>Formatted tooltip text</returns>
        private string FormatTypeInfo(TypeInfo typeInfo)
        {
            return $"Inferred Type: {typeInfo.Name}";
        }
    }
}
