using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserPorting.Stylers
{
    public class BaseStyler : AstVisitorBase, IStyler
    {
        public List<Indicator> Indicators { get; } = new();

        public void AddIndicator(SourceSpan span, Indicator.IndicatorType type, uint color, string? tooltip = null)
        {
            AddIndicator((span.Start.ByteIndex, span.End.ByteIndex), type, color, tooltip);
        }

        public void AddIndicator((int Start, int Stop) span, Indicator.IndicatorType type, uint color, string? tooltip = null)
        {
            if (span.Start >= 0 && span.Stop >= span.Start)
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
        }

        public void Reset()
        {
            Indicators.Clear();
        }
    }
}
