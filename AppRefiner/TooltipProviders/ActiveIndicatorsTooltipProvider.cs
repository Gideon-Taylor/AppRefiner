using System;
using System.Linq;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides tooltips for highlighted regions in the editor.
    /// </summary>
    public class ActiveIndicatorsTooltipProvider : BaseTooltipProvider
    {
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Highlight Tooltips";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows tooltips for highlighted regions in the code";

        /// <summary>
        /// Higher priority to check highlight regions first
        /// </summary>
        public override int Priority => 100;

        /// <summary>
        /// Checks if there's a tooltip for a highlighted region at the given position.
        /// </summary>
        /// <param name="editor">The Scintilla editor.</param>
        /// <param name="position">Position to check for tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        public override string? GetTooltip(ScintillaEditor editor, int position)
        {
            // Validate editor
            if (editor == null || !editor.IsValid())
            {
                return null;
            }
            
            // Check if editor has highlight tooltips
            if (editor.ActiveIndicators == null || editor.ActiveIndicators.Count == 0)
            {
                return null;
            }
            
            // Find the first tooltip that contains the position
            var activeIndicators = editor.ActiveIndicators.Where(t => 
                t.Start <= position && t.Start + t.Length >= position);
            
            if (!activeIndicators.Any())
            {
                return null;
            }

            StringBuilder tooltips = new StringBuilder();
            foreach (var t in activeIndicators)
            {
                if (t.Tooltip != null)
                {
                    tooltips.AppendLine(t.GetTooltip());
                    tooltips.Append("\n");
                }
            }

            return tooltips.ToString().Trim();
        }
    }
} 