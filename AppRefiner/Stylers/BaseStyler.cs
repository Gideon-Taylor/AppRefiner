using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.PeopleCode;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using System;
using System.Collections.Generic;

namespace AppRefiner.Stylers
{
    public enum IndicatorType
    {
        HIGHLIGHTER,
        SQUIGGLE,
        TEXTCOLOR
        // Future indicator types can be added here
    }

    public struct Indicator
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public uint Color { get; set; }
        public string? Tooltip { get; set; }
        public IndicatorType Type { get; set; }
        public List<(Type RefactorClass, string Description)> QuickFixes = new();

        public Indicator()
        {
        }
    }

    // New self-hosted parser-based styler base class
    public class BaseStyler : AstVisitorBase, IStyler
    {
        public List<Indicator> Indicators { get; } = new();

        /// <summary>
        /// Whether this styler is currently active/enabled
        /// </summary>
        public bool Active { get; set; } = false;
        
        /// <summary>
        /// Description of what this styler does
        /// </summary>
        public virtual string Description { get; set; } = "Description not set";

        /// <summary>
        /// Specifies whether this styler requires a database connection
        /// </summary>
        public virtual DataManagerRequirement DatabaseRequirement { get; } = DataManagerRequirement.NotRequired;
        
        /// <summary>
        /// The database manager instance, if available
        /// </summary>
        public IDataManager? DataManager { get; set; }
        
        /// <summary>
        /// The ScintillaEditor instance, if available
        /// </summary>
        public ScintillaEditor? Editor { get; set; }

        public void AddIndicator(SourceSpan span, IndicatorType type, uint color, string? tooltip = null)
        {
            AddIndicator((span.Start.ByteIndex, span.End.ByteIndex), type, color, tooltip);
        }

        public void AddIndicator((int Start, int Stop) span, IndicatorType type, uint color, string? tooltip = null)
        {
            if (span.Start >= 0 && span.Stop >= span.Start)
            {
                Indicators.Add(new Indicator
                {
                    Start = span.Start,
                    Length = span.Stop - span.Start,
                    Type = type,
                    Color = color,
                    Tooltip = tooltip
                });
            }
        }

        public virtual void Reset()
        {
            Indicators.Clear();
        }
    }
}
