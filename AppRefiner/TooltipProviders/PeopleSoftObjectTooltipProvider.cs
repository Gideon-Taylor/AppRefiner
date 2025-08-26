using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for PeopleSoft-specific object references.
    /// Currently supports Record.Name patterns.
    /// </summary>
    public class PeopleSoftObjectTooltipProvider : ParseTreeTooltipProvider
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
        /// We're looking for GENERIC_ID tokens that might be part of a Record.Name pattern
        /// </summary>
        public override int[]? TokenTypes => new int[] { PeopleCodeLexer.GENERIC_ID_LIMITED };

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        public override string? GetTooltip(ScintillaEditor editor, int position)
        {
            // First check if we have registered any tooltips
            if (TooltipDefinitions.Count > 0)
            {
                // Look through our registered tooltips to see if any match this position
                foreach (var entry in TooltipDefinitions)
                {
                    var (start, length) = entry.Key;
                    if (position >= start && position < start + length)
                    {
                        return entry.Value;
                    }
                }
            }
            
            // If we have no pre-registered tooltips, and we have database access,
            // we could do a real-time lookup as a fallback
            if (DataManager != null && DataManager.IsConnected)
            {
                // This is a real-time fallback in case our parse tree walking missed something
                // For example, if the document has changed since the last parse
                
                // Get current token at position (this would require access to the token stream)
                // For now, we'll rely on the registered tooltips only
            }
            
            return null;
        }

        /// <summary>
        /// Override to process dot access expressions that might be Record.Name patterns
        /// </summary>
        public override void EnterDotAccessExpr([NotNull] PeopleCode.PeopleCodeParser.DotAccessExprContext context)
        {
            // Make sure we have DataManager available
            if (DataManager == null || !DataManager.IsConnected)
                return;
            

            // Skip if this isn't a RECORD.Name pattern
            if (context.expression() is PeopleCode.PeopleCodeParser.IdentifierExprContext identExpr &&
                identExpr.ident() is PeopleCode.PeopleCodeParser.IdentGenericIDContext genericIdent &&
                genericIdent.genericID().GetText().Equals("RECORD", StringComparison.OrdinalIgnoreCase))
            {
                // Find the record name (the right side of the dot)
                // Make sure there's at least one dotAccess child in this context
                var dotAccessList = context.dotAccess();
                if (dotAccessList != null && dotAccessList.Length > 0)
                {
                    var firstDotAccess = dotAccessList[0];
                    if (firstDotAccess.genericID() != null)
                    {
                        // Get the record name from the first dot access element
                        var genericIdNode = firstDotAccess.genericID();
                        string recordName = genericIdNode.GetText();

                        // Fetch record field info from the database
                        List<RecordFieldInfo>? fields = DataManager.GetRecordFields(recordName);
                        if (fields != null && fields.Any())
                        {
                            StringBuilder tooltipContent = new($"Record Definition: {recordName.ToUpper()}\n\nFields (*=Key, !=Required):\n");

                            // Add all fields
                            foreach (var field in fields)
                            {
                                tooltipContent.AppendLine(field.ToString());
                            }

                            // Register the tooltip for this token
                            RegisterTooltip(genericIdNode, tooltipContent.ToString());
                        }
                        else
                        {
                            // Record not found or no fields
                            RegisterTooltip(genericIdNode, $"Record '{recordName.ToUpper()}' not found or has no fields.");
                        }
                    }
                    
                }
            }
        }
    }
} 