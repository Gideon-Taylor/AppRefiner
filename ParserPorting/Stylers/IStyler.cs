using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserPorting.Stylers
{
    public interface IStyler
    {
        public List<Indicator> Indicators { get; }

        protected void AddIndicator((int Start, int Stop) span, Indicator.IndicatorType type, uint color, string? tooltip = null);

        public void Reset();
    }
}
