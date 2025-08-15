using Antlr4.Runtime;
using AppRefiner.Database;
using AppRefiner.PeopleCode;
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
        public List<(Type RefactorClass, string Description)> QuickFixes { get; set; }
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

        public ScintillaEditor? Editor { get; set; }

        /// <summary>
        /// Helper method to add an indicator using a parser rule context with automatic byte-index conversion for Scintilla positioning.
        /// </summary>
        /// <param name="context">The parser rule context</param>
        /// <param name="type">The indicator type</param>
        /// <param name="color">The indicator color</param>
        /// <param name="tooltip">Optional tooltip text</param>
        /// <param name="quickFixes">Optional quick fixes</param>
        protected void AddIndicator(ParserRuleContext context, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            AddIndicator(context.Start, (context.Stop ?? context.Start), type, color, tooltip, quickFixes);
        }

        /// <summary>
        /// Helper method to add an indicator using tokens with automatic byte-index conversion for Scintilla positioning.
        /// </summary>
        /// <param name="startToken">The start token</param>
        /// <param name="endToken">The end token</param>
        /// <param name="type">The indicator type</param>
        /// <param name="color">The indicator color</param>
        /// <param name="tooltip">Optional tooltip text</param>
        /// <param name="quickFixes">Optional quick fixes</param>
        protected void AddIndicator(IToken startToken, IToken endToken, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            
            Indicators?.Add(new Indicator
            {
                Start = startToken.ByteStartIndex(),
                Length = endToken.ByteStopIndex() - endToken.ByteStartIndex() + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip,
                QuickFixes = quickFixes ?? new List<(Type RefactorClass, string Description)>()
            });
        }

        /// <summary>
        /// Helper method to add an indicator using a single token with automatic byte-index conversion for Scintilla positioning.
        /// </summary>
        /// <param name="token">The token</param>
        /// <param name="type">The indicator type</param>
        /// <param name="color">The indicator color</param>
        /// <param name="tooltip">Optional tooltip text</param>
        /// <param name="quickFixes">Optional quick fixes</param>
        protected void AddIndicator(IToken token, IndicatorType type, uint color, string? tooltip = null, List<(Type RefactorClass, string Description)>? quickFixes = null)
        {
            Indicators?.Add(new Indicator
            {
                Start = token.ByteStartIndex(),
                Length = token.ByteStopIndex() - token.ByteStartIndex() + 1,
                Type = type,
                Color = color,
                Tooltip = tooltip,
                QuickFixes = quickFixes ?? new List<(Type RefactorClass, string Description)>()
            });
        }
    }
}
