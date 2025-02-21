using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Collections.Generic;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Represents a code change to be applied during refactoring
    /// </summary>
    public struct CodeChange
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string NewText { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations
    /// </summary>
    public abstract class BaseRefactor : PeopleCodeParserBaseListener
    {
        protected List<CodeChange> Changes { get; } = new();
        protected ITokenStream TokenStream { get; private set; }
        protected string SourceText { get; private set; }

        /// <summary>
        /// Initializes the refactor with source code and token stream
        /// </summary>
        public void Initialize(string sourceText, ITokenStream tokenStream)
        {
            SourceText = sourceText;
            TokenStream = tokenStream;
            Changes.Clear();
        }

        /// <summary>
        /// Gets the refactored source code with all changes applied
        /// </summary>
        public string GetRefactoredCode()
        {
            if (Changes.Count == 0) return SourceText;

            // Sort changes from last to first to avoid index shifting
            Changes.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

            var result = new StringBuilder(SourceText);
            foreach (var change in Changes)
            {
                result.Remove(change.StartIndex, change.EndIndex - change.StartIndex);
                result.Insert(change.StartIndex, change.NewText);
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the list of changes that will be applied
        /// </summary>
        public IReadOnlyList<CodeChange> GetChanges() => Changes.AsReadOnly();

        /// <summary>
        /// Adds a new code change to the refactoring
        /// </summary>
        protected void AddChange(ParserRuleContext context, string newText, string description)
        {
            Changes.Add(new CodeChange
            {
                StartIndex = context.Start.StartIndex,
                EndIndex = context.Stop.StopIndex + 1,
                NewText = newText,
                Description = description
            });
        }

        /// <summary>
        /// Gets the original text for a parser rule context
        /// </summary>
        protected string GetOriginalText(ParserRuleContext context)
        {
            return SourceText.Substring(context.Start.StartIndex,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }
    }
}
