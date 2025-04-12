using AppRefiner.Database;
using AppRefiner.PeopleCode;

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
    }

    public abstract class BaseStyler : PeopleCodeParserBaseListener
    {
        public List<Indicator>? Indicators;
        public List<Antlr4.Runtime.IToken>? Comments;
        
        /// <summary>
        /// Resets the styler's state
        /// </summary>
        public virtual void Reset()
        {
            Indicators = [];
            Comments = [];
            DataManager = null;
        }

        public bool Active = false;
        public string Description = "Description not set";
        
        /// <summary>
        /// Specifies whether this styler requires a database connection
        /// </summary>
        public virtual DataManagerRequirement DatabaseRequirement { get; } = DataManagerRequirement.NotRequired;
        
        /// <summary>
        /// The database manager instance, if available
        /// </summary>
        public IDataManager? DataManager { get; set; }
    }
}
