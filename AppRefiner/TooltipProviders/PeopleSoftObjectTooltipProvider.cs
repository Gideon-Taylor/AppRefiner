using AppRefiner.Database;
using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeTypeInfo.Types;
using SQL.Formatter;
using SQL.Formatter.Core;
using SQL.Formatter.Language;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for PeopleSoft-specific object references.
    /// Currently supports Record.Name patterns.
    /// This is the self-hosted equivalent of the ANTLR-based PeopleSoftObjectTooltipProvider.
    /// </summary>
    public class PeopleSoftObjectTooltipProvider : BaseTooltipProvider
    {
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "PeopleSoft Object Info";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows metadata about PeopleSoft objects when hovering over them";

        /// <summary>
        /// Medium-high priority
        /// </summary>
        public override int Priority => 70;

        /// <summary>
        /// This provider requires database access to show record field information
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

        /// <summary>
        /// Processes the AST to find Record.Name patterns and register tooltips
        /// </summary>
        public override void ProcessProgram(ProgramNode program, int position, int lineNumber)
        {
            base.ProcessProgram(program, position, lineNumber);
        }

        /// <summary>
        /// Override to process member access expressions that might be Record.Name patterns
        /// </summary>
        public override void VisitMemberAccess(MemberAccessNode node)
        {
            if (!node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                base.VisitMemberAccess(node);
                return;
            }

            // Check if this is a RECORD.Name pattern
            if (IsRecordMemberAccess(node, out var isTargetHover))
            {
                ProcessRecordMemberAccess(node, isTargetHover);
            }

            if (IsSQLMemberAccess(node, out var isSQLTargetHover))
            {
                ProcessSQLMemberAccess(node, isSQLTargetHover);
            }

        }

        /// <summary>
        /// Checks if a member access expression is a RECORD.Name pattern
        /// </summary>
        private bool IsRecordMemberAccess(MemberAccessNode node, out bool isTargetHover)
        {
            isTargetHover = false;
            /* Default to type of the member access */
            var typeInfo = node.GetInferredType();

            /* If they are hovering over the left side, get its type instead */
            if (node.Target is IdentifierNode && node.Target.SourceSpan.ContainsPosition(CurrentPosition))
            {
                isTargetHover = true;
                typeInfo = node.Target.GetInferredType();
            }

            if (typeInfo is RecordTypeInfo)
            {
                return true;
            }

            // Check if the left side is an identifier with text "RECORD"
            return node.Target is IdentifierNode identifier &&
                   (string.Equals(identifier.Name, "RECORD", StringComparison.OrdinalIgnoreCase) || string.Equals(identifier.Name, "Scroll", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSQLMemberAccess(MemberAccessNode node, out bool isTargetHover)
        {
            isTargetHover = false;
            /* Default to type of the member access */
            var typeInfo = node.GetInferredType();

            /* If they are hovering over the left side, get its type instead */
            if (node.Target is IdentifierNode && node.Target.SourceSpan.ContainsPosition(CurrentPosition))
            {
                isTargetHover = true;
                typeInfo = node.Target.GetInferredType();
            }

            // Check if the left side is an identifier with text "RECORD"
            return node.Target is IdentifierNode identifier &&
                   string.Equals(identifier.Name, "SQL", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Processes a Record.Name member access expression to show record field information
        /// </summary>
        private void ProcessRecordMemberAccess(MemberAccessNode node, bool isTargetHover)
        {
            // Make sure we have DataManager available
            if (DataManager == null || !DataManager.IsConnected)
                return;

            // Get the record name from the member name
            string recordName = (isTargetHover ? node.Target.ToString() : node.MemberName).ToUpperInvariant();

            // Fetch record field info from the database
            List<RecordFieldInfo>? fields = DataManager.GetRecordFields(recordName);
            if (fields != null && fields.Any())
            {
                StringBuilder tooltipContent = new($"Record Definition: {recordName}\n\nFields (*=Key, !=Required):\n");

                // Add all fields
                foreach (var field in fields)
                {
                    tooltipContent.AppendLine(field.ToString());
                }

                // Register the tooltip for the member access node
                RegisterTooltip(node.SourceSpan, tooltipContent.ToString());
            }
            else
            {
                // Record not found or no fields
                RegisterTooltip(node.SourceSpan, $"Record '{recordName}' not found or has no fields.");
            }
        }

        private void ProcessSQLMemberAccess(MemberAccessNode node, bool isTargetHover)
        {
            // Make sure we have DataManager available
            if (DataManager == null || !DataManager.IsConnected)
                return;
            // Get the SQL name from the member name
            string sqlName = (isTargetHover ? node.Target.ToString() : node.MemberName).ToUpperInvariant();
            // Fetch SQL definition from the database
            var sqlDef = DataManager.GetSqlDefinition(sqlName);
            if (sqlDef != null)
            {
                StringBuilder tooltipContent = new();
                FormatConfig formatConfig = FormatConfig.Builder().Indent("  ")
                    .Uppercase(true)
                    .LinesBetweenQueries(2)
                    .MaxColumnLength(80)
                    .Build();
                var formatted = SqlFormatter.Of(Dialect.StandardSql)
                .Extend(cfg => cfg.PlusSpecialWordChars("%").PlusNamedPlaceholderTypes(new string[] { ":" }).PlusOperators(new string[] { "%Concat" }))
                .Format(sqlDef, formatConfig).Replace("\n", " \n");

                tooltipContent.AppendLine(formatted);
                // Register the tooltip for the member access node
                RegisterTooltip(node.SourceSpan, tooltipContent.ToString());
            }
            else
            {
                // SQL not found
                RegisterTooltip(node.SourceSpan, $"SQL '{sqlName}' not found.");
            }
        }

    }
}
