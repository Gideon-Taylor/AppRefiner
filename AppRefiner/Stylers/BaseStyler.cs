using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;

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

    // Unified self-hosted parser-based scoped styler - now the single base class for all stylers
    public class BaseStyler : ScopedAstVisitor<object>
    {
        public List<Indicator> Indicators { get; } = [];

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

        public void AddIndicator(SourceSpan span, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            AddIndicator((span.Start.ByteIndex, span.End.ByteIndex), type, color, tooltip, quickFixes);
        }

        public void AddIndicator((int Start, int Stop) span, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            if (span.Start >= 0 && span.Stop >= span.Start)
            {
                Indicators.Add(new Indicator
                {
                    Start = span.Start,
                    Length = span.Stop - span.Start,
                    Type = type,
                    Color = color,
                    Tooltip = tooltip,
                    QuickFixes = quickFixes ?? []
                });
            }
        }

        public new void Reset()
        {
            base.Reset();
            Indicators.Clear();
        }
    }
}
