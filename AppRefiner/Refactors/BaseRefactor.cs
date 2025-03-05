using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using AppRefiner.Refactors.CodeChanges;
using System.Collections.Generic;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Represents the result of a refactoring operation
    /// </summary>
    public class RefactorResult
    {
        public bool Success { get; }
        public string? Message { get; }
        
        public RefactorResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }
        
        public static RefactorResult Successful => new RefactorResult(true);
        public static RefactorResult Failed(string message) => new RefactorResult(false, message);
    }
    
    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations
    /// </summary>
    public abstract class BaseRefactor : PeopleCodeParserBaseListener
    {
        protected List<CodeChange> Changes { get; } = new();
        protected ITokenStream? TokenStream { get; private set; }
        protected string? SourceText { get; private set; }
        protected RefactorResult Result { get; set; } = RefactorResult.Successful;

        /// <summary>
        /// Initializes the refactor with source code and token stream
        /// </summary>
        public void Initialize(string sourceText, ITokenStream tokenStream)
        {
            SourceText = sourceText;
            TokenStream = tokenStream;
            Changes.Clear();
            Result = RefactorResult.Successful;
        }

        /// <summary>
        /// Sets a failure status with an error message
        /// </summary>
        protected void SetFailure(string message)
        {
            Result = RefactorResult.Failed(message);
        }

        /// <summary>
        /// Gets the result of the refactoring operation
        /// </summary>
        public RefactorResult GetResult() => Result;

        /// <summary>
        /// Gets the refactored source code with all changes applied
        /// </summary>
        public string? GetRefactoredCode()
        {
            if (!Result.Success) return null;
            
            if (Changes.Count == 0) return SourceText;

            // Sort changes from last to first to avoid index shifting
            Changes.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

            var result = new StringBuilder(SourceText);
            foreach (var change in Changes)
            {
                change.Apply(result);
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the list of changes that will be applied
        /// </summary>
        public IReadOnlyList<CodeChange> GetChanges() => Changes.AsReadOnly();

        /// <summary>
        /// Adds a new replacement change to the refactoring
        /// </summary>
        protected void AddChange(ParserRuleContext context, string newText, string description)
        {
            Changes.Add(new ReplaceChange(context.Stop.StopIndex + 1, newText)
            {
                StartIndex = context.Start.StartIndex,
                Description = description
            });
        }

        /// <summary>
        /// Adds a new insertion change to the refactoring
        /// </summary>
        protected void AddInsert(int position, string textToInsert, string description)
        {
            Changes.Add(new InsertChange(textToInsert) { 
                StartIndex = position,
                Description = description
            });
        }

        /// <summary>
        /// Adds a new delete change to the refactoring
        /// </summary>
        protected void AddDelete(int startIndex, int endIndex, string description)
        {
            Changes.Add(new DeleteChange
            {
                StartIndex = startIndex,
                EndIndex = endIndex,
                Description = description
            });
        }

        /// <summary>
        /// Gets the original text for a parser rule context
        /// </summary>
        protected string? GetOriginalText(ParserRuleContext context)
        {
            return SourceText?.Substring(context.Start.StartIndex,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }
    }
}
