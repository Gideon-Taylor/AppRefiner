using AppRefiner.Database;
using static AppRefiner.PeopleCode.PeopleCodeParser;

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

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            var functionName = context.genericID()?.GetText();
            if (!functionName?.Equals("GetHTMLText", StringComparison.OrdinalIgnoreCase) ?? true)
                return;

            var args = context.functionCallArguments();
            if (args?.expression() == null || args.expression().Length == 0)
                return;

            // Process the first argument which should be HTML reference
            var firstArg = args.expression()[0];
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
                    firstArg.Start.Line - 1,
                    firstArg
                );
                return;
            }

            // Validate bind parameter count
            var bindCount = htmlDef.BindCount;
            var providedBinds = args.expression().Length - 1; // Minus the HTML reference

            if (providedBinds < bindCount)
            {
                AddReport(
                    2,
                    $"GetHTMLText has too few bind parameters. Expected {bindCount}, got {providedBinds}.",
                    ReportType.Error,
                    context.Start.Line - 1,
                    context
                );
            }
            else if (providedBinds > bindCount && bindCount > 0)
            {
                AddReport(
                    3,
                    $"GetHTMLText has more bind parameters than needed. Expected {bindCount}, got {providedBinds}.",
                    ReportType.Warning,
                    context.Start.Line - 1,
                    context
                );
            }
        }

        private string? GetHtmlReference(ExpressionContext expr)
        {
            // Handle HTML.NAME format
            if (expr is DotAccessExprContext dotAccess)
            {
                var leftExpr = dotAccess.expression();
                if (leftExpr is IdentifierExprContext idExpr &&
                    idExpr.ident().GetText().Equals("HTML", StringComparison.OrdinalIgnoreCase))
                {
                    if (dotAccess.children[1] is DotAccessContext dotAccessCtx)
                    {
                        return dotAccessCtx.genericID().GetText();
                    }
                }
            }
            return null;
        }
    }
}
