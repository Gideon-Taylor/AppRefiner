using AppRefiner.Database;
using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Shows the Message Catalog entry for MsgGet/MsgGetText/MsgGetExplainText/
    /// CreateException/MessageBox/MsgBoxButtonOverride calls whose message_set and
    /// message_num arguments are integer literals.
    /// </summary>
    public class MessageCatalogTooltipProvider : BaseTooltipProvider
    {
        private const int ExplainTruncateLength = 500;

        public override string Name => "Message Catalog";
        public override string Description => "Shows message catalog text when hovering MsgGet-style calls";
        public override int Priority => 60;
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        public override void VisitFunctionCall(FunctionCallNode node)
        {
            base.VisitFunctionCall(node);

            if (DataManager == null) return;
            // Only the hovered call triggers a lookup — the visitor walks the whole program
            if (!node.SourceSpan.IsValid || !ContainsPosition(node.SourceSpan)) return;
            if (node.Function is not IdentifierNode ident) return;
            if (!MessageCatalogFunctions.TryGetArgPositions(ident.Name, out var argInfo)) return;
            if (!TryGetIntLiteral(node, argInfo.SetArg, out int setNumber)) return;
            if (!TryGetIntLiteral(node, argInfo.NumArg, out int messageNumber)) return;

            var entry = MessageCatalogCache.GetEntry(DataManager, setNumber, messageNumber);
            if (entry == null)
            {
                RegisterTooltip(node.SourceSpan, $"No catalog entry for {setNumber}/{messageNumber}");
                return;
            }

            var setDescription = MessageCatalogCache.GetMessageSets(DataManager)
                .FirstOrDefault(s => s.SetNumber == setNumber)?.Description;

            RegisterTooltip(node.SourceSpan, FormatTooltip(entry, setDescription));
        }

        private static bool TryGetIntLiteral(FunctionCallNode node, int argIndex, out int value)
        {
            value = 0;
            if (node.Arguments.Count <= argIndex) return false;
            if (node.Arguments[argIndex] is not LiteralNode literal) return false;
            if (literal.LiteralType != LiteralType.Integer) return false;

            try
            {
                value = Convert.ToInt32(literal.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatTooltip(MessageCatalogEntry entry, string? setDescription)
        {
            var parts = new List<string>
            {
                $"Message Catalog {entry.SetNumber}/{entry.MessageNumber}" +
                    (string.IsNullOrEmpty(setDescription) ? "" : $" — {setDescription}"),
                $"Severity: {entry.Severity}",
                $"\"{entry.MessageText}\""
            };

            if (!string.IsNullOrWhiteSpace(entry.ExplainText))
            {
                string explain = entry.ExplainText.Trim();
                if (explain.Length > ExplainTruncateLength)
                {
                    explain = explain.Substring(0, ExplainTruncateLength) + "…";
                }
                parts.Add("");
                parts.Add($"Explain: {explain}");
            }

            return string.Join("\n", parts);
        }
    }
}
