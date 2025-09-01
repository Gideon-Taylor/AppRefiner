using AppRefiner.Database;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Base class for tooltip providers that leverage the self-hosted AST parser.
    /// This class provides AST-based tooltip functionality without scope tracking.
    /// For advanced scope-aware tooltips, use ScopedAstTooltipProvider.
    /// </summary>
    public abstract class AstTooltipProvider : AstVisitorBase, ITooltipProvider
    {
        /// <summary>
        /// Collection of tooltip definitions identified during AST traversal.
        /// Key is the position range (start, length), value is the tooltip text.
        /// </summary>
        protected Dictionary<(int Start, int Length), string> TooltipDefinitions { get; private set; } = new();

        /// <summary>
        /// The parsed AST program node
        /// </summary>
        protected ProgramNode? Program { get; private set; }

        /// <summary>
        /// Current cursor position in the document, provided by the editor
        /// </summary>
        protected int CurrentPosition { get; private set; } = -1;

        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Determines if this provider is active and should process tooltips.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Priority of this tooltip provider. Higher priority providers are called first.
        /// </summary>
        public virtual int Priority { get; } = 0;

        /// <summary>
        /// Specifies whether this provider requires a database connection
        /// </summary>
        public virtual DataManagerRequirement DatabaseRequirement { get; } = DataManagerRequirement.NotRequired;

        /// <summary>
        /// The database manager instance, if available
        /// </summary>
        public IDataManager? DataManager { get; set; }

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// This method should be called after the AST has been processed.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="position">The position in the document where to show the tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        public virtual string? GetTooltip(ScintillaEditor editor, int position)
        {
            try
            {
                CurrentPosition = position;

                // Check if we have any tooltips for this position
                foreach (var entry in TooltipDefinitions)
                {
                    var (start, length) = entry.Key;
                    if (position >= start && position < start + length)
                    {
                        return entry.Value;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Called when the tooltip is hidden. Can be used for cleanup.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        public virtual void OnHideTooltip(ScintillaEditor editor)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Processes a parsed AST program for tooltip identification.
        /// This should be called by the tooltip system after parsing.
        /// </summary>
        /// <param name="program">The parsed program AST</param>
        public virtual void ProcessProgram(ProgramNode program)
        {
            try
            {
                Reset();
                Program = program;

                if (Program != null)
                {
                    Program.Accept(this);
                }
            }
            catch (Exception)
            {
                // Silently handle processing errors
            }
        }

        /// <summary>
        /// Parses an external class for database-integrated analysis
        /// </summary>
        protected ProgramNode? ParseExternalClass(string classPath)
        {
            if (DataManager == null) return null;

            try
            {
                string? sourceCode = DataManager.GetAppClassSourceByPath(classPath);
                if (string.IsNullOrEmpty(sourceCode)) return null;

                var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(sourceCode);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                return parser.ParseProgram();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public virtual void Reset()
        {
            TooltipDefinitions = new Dictionary<(int Start, int Length), string>();
            Program = null;
            CurrentPosition = -1;
        }

        /// <summary>
        /// Helper method to register a tooltip for a specific SourceSpan.
        /// This is the preferred method for AST node tooltip registration.
        /// </summary>
        /// <param name="span">The source span of the AST node</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(SourceSpan span, string tooltipText)
        {
            if (!span.IsValid || string.IsNullOrEmpty(tooltipText))
                return;

            RegisterTooltip(span.Start.ByteIndex, span.Length, tooltipText);
        }

        /// <summary>
        /// Helper method to register a tooltip for a specific position range.
        /// </summary>
        /// <param name="start">Start byte index of the text range</param>
        /// <param name="length">Length of the text range</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(int start, int length, string tooltipText)
        {
            if (length <= 0 || string.IsNullOrEmpty(tooltipText))
                return;

            TooltipDefinitions[(start, length)] = tooltipText;
        }

        /// <summary>
        /// Checks if the current cursor position is within or adjacent to the specified span
        /// </summary>
        protected bool ContainsPosition(SourceSpan span)
        {
            return span.IsValid && CurrentPosition >= span.Start.ByteIndex &&
                   CurrentPosition <= span.End.ByteIndex;
        }

        /// <summary>
        /// Checks if the current cursor position is within the specified range
        /// </summary>
        protected bool ContainsPosition(int start, int length)
        {
            return CurrentPosition >= start && CurrentPosition < start + length;
        }
    }
}