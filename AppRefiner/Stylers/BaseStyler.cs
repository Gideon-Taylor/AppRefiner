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

        /// <summary>
        /// Deferred QuickFix resolver with context.
        /// Invoked only when the user presses Ctrl+. on this indicator.
        /// Signature: (editor, position, context) => List<(Type, Description)>
        /// </summary>
        public Func<ScintillaEditor, int, object?, List<(Type RefactorClass, string Description)>>? DeferredQuickFixResolver { get; set; }

        /// <summary>
        /// Context data to pass to the deferred resolver (e.g., unimported class name).
        /// </summary>
        public object? DeferredQuickFixContext { get; set; }

        public Indicator()
        {
        }

        // Implement equality based on position and visual properties
        // QuickFixes are intentionally excluded from equality comparison
        public override bool Equals(object? obj)
        {
            if (obj is not Indicator other)
                return false;

            return Start == other.Start &&
                   Length == other.Length &&
                   Color == other.Color &&
                   Type == other.Type &&
                   Tooltip == other.Tooltip;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, Length, Color, Type, Tooltip);
        }

        public static bool operator ==(Indicator left, Indicator right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Indicator left, Indicator right)
        {
            return !(left == right);
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

        /// <summary>
        /// Adds an indicator with a deferred QuickFix resolver.
        /// The resolver will be invoked only when the user presses Ctrl+. on this indicator.
        /// </summary>
        /// <param name="span">Source span to highlight</param>
        /// <param name="type">Indicator type</param>
        /// <param name="color">Highlight color</param>
        /// <param name="tooltip">Optional tooltip text</param>
        /// <param name="deferredResolver">Resolver function to generate QuickFixes on demand</param>
        /// <param name="context">Optional context data to pass to the resolver</param>
        protected void AddIndicatorWithDeferredQuickFix(
            SourceSpan span,
            IndicatorType type,
            uint color,
            string? tooltip,
            Func<ScintillaEditor, int, object?, List<(Type RefactorClass, string Description)>> deferredResolver,
            object? context = null)
        {
            if (span.Start.ByteIndex >= 0 && span.End.ByteIndex >= span.Start.ByteIndex)
            {
                Indicators.Add(new Indicator
                {
                    Start = span.Start.ByteIndex,
                    Length = span.End.ByteIndex - span.Start.ByteIndex,
                    Type = type,
                    Color = color,
                    Tooltip = tooltip,
                    DeferredQuickFixResolver = deferredResolver,
                    DeferredQuickFixContext = context
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
