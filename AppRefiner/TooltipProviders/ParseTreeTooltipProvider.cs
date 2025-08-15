using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.Database;
using AppRefiner.PeopleCode;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Base class for tooltip providers that need access to the parse tree.
    /// Similar to Linters and Stylers, this allows for syntax-aware tooltips.
    /// </summary>
    public abstract class ParseTreeTooltipProvider : PeopleCodeParserBaseListener, ITooltipProvider
    {
        /// <summary>
        /// Collection of tooltip definitions identified during parse tree walking.
        /// Key is the position range (start, length), value is the tooltip text.
        /// </summary>
        protected Dictionary<(int Start, int Length), string> TooltipDefinitions { get; private set; } = new();
        
        /// <summary>
        /// Collection of comment tokens from the lexer.
        /// </summary>
        public IList<IToken>? Comments { get; set; }
        
        /// <summary>
        /// Line number at the current position, provided by Scintilla.
        /// </summary>
        public int LineNumber { get; set; } = -1;
        
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
        /// Token types that this provider is interested in. Empty/null means all tokens.
        /// Only tokens matching these types will trigger parse tree analysis for tooltip generation.
        /// </summary>
        public virtual int[]? TokenTypes => null;

        /// <summary>
        /// Checks if the provider can provide a tooltip for a token at the given position.
        /// </summary>
        /// <param name="token">The token to check</param>
        /// <param name="position">The position in the document</param>
        /// <returns>True if this provider can handle the token, false otherwise</returns>
        public virtual bool CanProvideTooltipForToken(IToken token, int position)
        {
            // If no specific token types are defined, this provider handles all tokens
            if (TokenTypes == null || TokenTypes.Length == 0)
                return true;
                
            // Check if the token type is in the list of supported types
            foreach (var type in TokenTypes)
            {
                if (token.Type == type)
                {
                    // Make sure the position is within or adjacent to the token
                    return (position >= token.ByteStartIndex() && position <= token.ByteStopIndex() + 1);
                }
            }
            
            return false;
        }

        /// <summary>
        /// Resets the internal state of the tooltip provider.
        /// </summary>
        public virtual void Reset()
        {
            TooltipDefinitions = new Dictionary<(int Start, int Length), string>();
            Comments = null;
            DataManager = null;
            LineNumber = -1;
        }

        /// <summary>
        /// Helper method to register a tooltip for a specific position range.
        /// </summary>
        /// <param name="start">Start index of the text range</param>
        /// <param name="length">Length of the text range</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(int start, int length, string tooltipText)
        {
            if (length <= 0 || string.IsNullOrEmpty(tooltipText))
                return;
                
            TooltipDefinitions[(start, length)] = tooltipText;
        }

        /// <summary>
        /// Helper method to register a tooltip for a token.
        /// </summary>
        /// <param name="token">The token to register the tooltip for</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(IToken token, string tooltipText)
        {
            RegisterTooltip(token.ByteStartIndex(), token.ByteStopIndex() - token.ByteStartIndex() + 1, tooltipText);
        }

        /// <summary>
        /// Helper method to register a tooltip for an ANTLR parser rule context.
        /// </summary>
        /// <param name="context">The parser rule context to register the tooltip for</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(ParserRuleContext context, string tooltipText)
        {
            if (context == null || context.Start == null || string.IsNullOrEmpty(tooltipText))
                return;
                
            var endToken = (context.Stop ?? context.Start);
            int start = context.Start.ByteStartIndex();
            int stop = endToken.ByteStopIndex();
            int length = stop - start + 1;
            
            RegisterTooltip(start, length, tooltipText);
        }

        /// <summary>
        /// Helper method to register a tooltip for an ANTLR terminal node.
        /// </summary>
        /// <param name="node">The terminal node to register the tooltip for</param>
        /// <param name="tooltipText">Tooltip text to display</param>
        protected void RegisterTooltip(ITerminalNode node, string tooltipText)
        {
            if (node == null || string.IsNullOrEmpty(tooltipText))
                return;
                
            RegisterTooltip(node.Symbol, tooltipText);
        }


        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="position">The position in the document where to show the tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        public virtual string? GetTooltip(ScintillaEditor editor, int position)
        {
            if (TooltipDefinitions.Count == 0)
                return null;
                
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

        /// <summary>
        /// Called when the tooltip is hidden. Can be used for cleanup.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        public virtual void OnHideTooltip(ScintillaEditor editor)
        {
            // Default implementation does nothing
        }
    }
} 