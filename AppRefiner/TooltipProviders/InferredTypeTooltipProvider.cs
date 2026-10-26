using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Narrowing;
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
                string tooltipText = FormatTypeInfo(bestTypeInfo, bestNode);
                RegisterTooltip(bestSpan.Value, tooltipText);
            }
            
        }

        

        /// <summary>
        /// Formats type information for display in the tooltip (see <see cref="TypeDisplay"/>).
        /// An identifier also says where its type came from, or which types its uses still allow.
        /// </summary>
        /// <param name="typeInfo">The type information to format</param>
        /// <param name="node">The node the type belongs to</param>
        /// <returns>Formatted tooltip text</returns>
        private string FormatTypeInfo(TypeInfo typeInfo, AstNode? node)
        {
            if (node is not IdentifierNode id)
                return $"Inferred Type: {TypeDisplay.Format(typeInfo)}";

            var source = id.GetRefinementSource();
            if (source != null)
            {
                var origin = source.Kind switch
                {
                    RefinementKind.Annotation => "annotated",
                    RefinementKind.Assignment => "assigned",
                    RefinementKind.Narrowing => source.Node is MemberAccessNode decidingUse
                        ? $"inferred from {UsageNarrowingAnalyzer.Decider(decidingUse)}"
                        : "inferred",
                    _ => null
                };
                if (origin != null)
                    return $"Inferred Type: {TypeDisplay.Format(typeInfo)}, {origin} on line {source.Line + 1}";
            }

            // Undecided: still plain ApiObject, with the candidates its uses leave open
            if (id.GetNarrowingCandidates() is { Count: > 0 } candidates
                && typeInfo is BuiltinObjectTypeInfo { Name: var name }
                && name.Equals("ApiObject", StringComparison.OrdinalIgnoreCase))
            {
                return $"Inferred Type: ApiObject ({UsageNarrowingAnalyzer.CandidatePhrase(candidates)})";
            }

            return $"Inferred Type: {TypeDisplay.Format(typeInfo)}";
        }
    }
}
