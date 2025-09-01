using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppRefiner.Database;
using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for PeopleSoft-specific object references.
    /// Currently supports Record.Name patterns.
    /// This is the self-hosted equivalent of the ANTLR-based PeopleSoftObjectTooltipProvider.
    /// </summary>
    public class PeopleSoftObjectTooltipProvider : AstTooltipProvider
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
        public override void ProcessProgram(ProgramNode program)
        {
            base.ProcessProgram(program);
        }

        /// <summary>
        /// Override to process member access expressions that might be Record.Name patterns
        /// </summary>
        public override void VisitMemberAccess(MemberAccessNode node)
        {
            // Check if this is a RECORD.Name pattern
            if (IsRecordMemberAccess(node))
            {
                ProcessRecordMemberAccess(node);
            }

            base.VisitMemberAccess(node);
        }

        /// <summary>
        /// Checks if a member access expression is a RECORD.Name pattern
        /// </summary>
        private bool IsRecordMemberAccess(MemberAccessNode node)
        {
            // Check if the left side is an identifier with text "RECORD"
            return node.Target is IdentifierNode identifier &&
                   string.Equals(identifier.Name, "RECORD", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Processes a Record.Name member access expression to show record field information
        /// </summary>
        private void ProcessRecordMemberAccess(MemberAccessNode node)
        {
            // Make sure we have DataManager available
            if (DataManager == null || !DataManager.IsConnected)
                return;

            // Get the record name from the member name
            string recordName = node.MemberName.ToUpperInvariant();

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
    }
}
