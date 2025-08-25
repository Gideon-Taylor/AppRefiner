using AppRefiner;
using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserPorting.Stylers
{
    public class ScopedStyler : ScopedAstVisitor<object>, IStyler
    {
        public List<Indicator> Indicators { get; } = [];

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

        public void AddIndicator(SourceSpan span, Indicator.IndicatorType type, uint color, string? tooltip = null)
        {
            AddIndicator((span.Start.ByteIndex, span.End.ByteIndex), type, color, tooltip);
        }

        public void AddIndicator((int Start, int Stop) span, Indicator.IndicatorType type, uint color, string? tooltip = null)
        {
            Indicators.Add(new Indicator
            {
                Start = span.Start,
                Length = span.Stop - span.Start + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip
            });
        }

        public new void Reset()
        {
            base.Reset();
            Indicators.Clear();
        }
    }
}
