using AppRefiner;
using AppRefiner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Stylers
{
    public interface IStyler
    {
        public List<Indicator> Indicators { get; }

        protected void AddIndicator((int Start, int Stop) span, IndicatorType type, uint color, string? tooltip = null);

        public void Reset();

        /// <summary>
        /// Whether this styler is currently active/enabled
        /// </summary>
        bool Active { get; set; }
        
        /// <summary>
        /// Description of what this styler does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Specifies whether this styler requires a database connection
        /// </summary>
        DataManagerRequirement DatabaseRequirement { get; }
        
        /// <summary>
        /// The database manager instance, if available
        /// </summary>
        IDataManager? DataManager { get; set; }
        
        /// <summary>
        /// The ScintillaEditor instance, if available
        /// </summary>
        ScintillaEditor? Editor { get; set; }
    }
}