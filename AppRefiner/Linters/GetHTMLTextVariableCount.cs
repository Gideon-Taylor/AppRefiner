using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Linters
{
    public class GetHTMLTextVariableCount : BaseLintRule
    {
        public override string LINTER_ID => "HTML_VAR_COUNT";

        public GetHTMLTextVariableCount()
        {
            Description = "Validate bind counts in GetHTMLText function calls.";
            Type = ReportType.Error;
            Active = true;
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override void Reset()
        {
            // Nothing to reset
        }

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            // Check if the function being called is "GetHTMLText"
            if (!(node.Function is IdentifierNode functionId))
                return;

            var functionName = functionId.Name;
            if (!functionName.Equals("GetHTMLText", StringComparison.OrdinalIgnoreCase))
                return;

            if (node.Arguments.Count == 0)
                return;

            // Process the first argument which should be HTML reference
            var firstArg = node.Arguments[0];
            var htmlRef = GetHtmlReference(firstArg);
            if (htmlRef == null)
                return;

            // Try to get HTML definition
            if (DataManager == null)
                return;

            var htmlDef = DataManager.GetHtmlDefinition(htmlRef);
            if (htmlDef == null || string.IsNullOrEmpty(htmlDef.Content))
            {
                AddReport(
                    1,
                    $"Invalid HTML definition: {htmlRef}",
                    ReportType.Error,
                    firstArg.SourceSpan.Start.Line,
                    firstArg.SourceSpan
                );
                return;
            }

            // Validate bind parameter count
            var bindCount = htmlDef.BindCount;
            var providedBinds = node.Arguments.Count - 1; // Minus the HTML reference

            if (providedBinds < bindCount)
            {
                AddReport(
                    2,
                    $"GetHTMLText has too few bind parameters. Expected {bindCount}, got {providedBinds}.",
                    ReportType.Error,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }
            else if (providedBinds > bindCount && bindCount > 0)
            {
                AddReport(
                    3,
                    $"GetHTMLText has more bind parameters than needed. Expected {bindCount}, got {providedBinds}.",
                    ReportType.Warning,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }

            base.VisitFunctionCall(node);
        }

        private string? GetHtmlReference(ExpressionNode expr)
        {
            // Handle HTML.NAME format
            if (expr is MemberAccessNode memberAccess)
            {
                if (memberAccess.Target is IdentifierNode targetId &&
                    targetId.Name.Equals("HTML", StringComparison.OrdinalIgnoreCase))
                {
                    return memberAccess.MemberName;
                }
            }
            return null;
        }
    }
}
