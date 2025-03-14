using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System.Collections.Generic;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Represents the result of a refactoring operation
    /// </summary>
    public class RefactorResult
    {
        /// <summary>
        /// Whether the refactoring was successful
        /// </summary>
        public bool Success { get; }
        
        /// <summary>
        /// Optional message providing details about the result
        /// </summary>
        public string? Message { get; }
        
        /// <summary>
        /// Creates a new refactoring result
        /// </summary>
        /// <param name="success">Whether the refactoring was successful</param>
        /// <param name="message">Optional message providing details</param>
        public RefactorResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }
        
        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static RefactorResult Successful => new RefactorResult(true);
        
        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        public static RefactorResult Failed(string message) => new RefactorResult(false, message);
    }
    
    /// <summary>
    /// Represents a change to be applied to the source code
    /// </summary>
    public abstract class CodeChange
    {
        /// <summary>
        /// The starting index in the source where the change begins
        /// </summary>
        public int StartIndex { get; }
        
        /// <summary>
        /// A description of what this change does
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Creates a new code change
        /// </summary>
        /// <param name="startIndex">The starting index of the change</param>
        /// <param name="description">A description of the change</param>
        protected CodeChange(int startIndex, string description)
        {
            StartIndex = startIndex;
            Description = description;
        }
        
        /// <summary>
        /// Applies this change to the given source code builder
        /// </summary>
        public abstract void Apply(StringBuilder source);
    }
    
    /// <summary>
    /// Represents a change that deletes text from the source
    /// </summary>
    public class DeleteChange : CodeChange
    {
        /// <summary>
        /// The ending index (inclusive) in the source where the deletion ends
        /// </summary>
        public int EndIndex { get; }
        
        /// <summary>
        /// Creates a new deletion change
        /// </summary>
        /// <param name="startIndex">The starting index where deletion begins</param>
        /// <param name="endIndex">The ending index (inclusive) where deletion ends</param>
        /// <param name="description">A description of what is being deleted</param>
        public DeleteChange(int startIndex, int endIndex, string description)
            : base(startIndex, description)
        {
            EndIndex = endIndex;
        }
        
        /// <summary>
        /// Applies the deletion to the source
        /// </summary>
        public override void Apply(StringBuilder source)
        {
            source.Remove(StartIndex, EndIndex - StartIndex + 1);
        }
    }
    
    /// <summary>
    /// Represents a change that inserts text into the source
    /// </summary>
    public class InsertChange : CodeChange
    {
        /// <summary>
        /// The text to insert at the start index
        /// </summary>
        public string TextToInsert { get; }
        
        /// <summary>
        /// Creates a new insertion change
        /// </summary>
        /// <param name="startIndex">The index where insertion should occur</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        public InsertChange(int startIndex, string textToInsert, string description)
            : base(startIndex, description)
        {
            TextToInsert = textToInsert;
        }
        
        /// <summary>
        /// Applies the insertion to the source
        /// </summary>
        public override void Apply(StringBuilder source)
        {
            source.Insert(StartIndex, TextToInsert);
        }
    }
    
    /// <summary>
    /// Represents a change that replaces text in the source
    /// </summary>
    public class ReplaceChange : CodeChange
    {
        /// <summary>
        /// The ending index (inclusive) in the source where the replacement ends
        /// </summary>
        public int EndIndex { get; }
        
        /// <summary>
        /// The new text to replace the old text with
        /// </summary>
        public string NewText { get; }
        
        /// <summary>
        /// Creates a new replacement change
        /// </summary>
        /// <param name="startIndex">The starting index where replacement begins</param>
        /// <param name="endIndex">The ending index (inclusive) where replacement ends</param>
        /// <param name="newText">The new text to replace the old text with</param>
        /// <param name="description">A description of what is being replaced</param>
        public ReplaceChange(int startIndex, int endIndex, string newText, string description)
            : base(startIndex, description)
        {
            EndIndex = endIndex;
            NewText = newText;
        }
        
        /// <summary>
        /// Applies the replacement to the source
        /// </summary>
        public override void Apply(StringBuilder source)
        {
            source.Remove(StartIndex, EndIndex - StartIndex + 1);
            source.Insert(StartIndex, NewText);
        }
    }
    
    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations
    /// </summary>
    public abstract class BaseRefactor : PeopleCodeParserBaseListener
    {
        private readonly List<CodeChange> _changes = new();
        
        /// <summary>
        /// The token stream of the source code being refactored
        /// </summary>
        protected ITokenStream? TokenStream { get; private set; }
        
        /// <summary>
        /// The source text being refactored
        /// </summary>
        protected string? SourceText { get; private set; }
        
        /// <summary>
        /// The current result of the refactoring operation
        /// </summary>
        protected RefactorResult Result { get; set; } = RefactorResult.Successful;

        /// <summary>
        /// Initializes the refactor with source code and token stream
        /// </summary>
        public void Initialize(string sourceText, ITokenStream tokenStream)
        {
            SourceText = sourceText;
            TokenStream = tokenStream;
            _changes.Clear();
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
            
            if (_changes.Count == 0) return SourceText;

            // Sort changes from last to first to avoid index shifting
            _changes.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

            var result = new StringBuilder(SourceText);
            foreach (var change in _changes)
            {
                change.Apply(result);
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the list of changes that will be applied
        /// </summary>
        public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();

        /// <summary>
        /// Adds a new replacement change using parser context
        /// </summary>
        /// <param name="context">The parser rule context to replace</param>
        /// <param name="newText">The new text to replace with</param>
        /// <param name="description">A description of what is being replaced</param>
        protected void ReplaceNode(ParserRuleContext context, string newText, string description)
        {
            // Handle case where node has a Start but no Stop (empty node)
            if (context.Stop == null)
            {
                // Treat as an insert operation at the start position
                InsertText(context.Start.StartIndex, newText, description);
            }
            else
            {
                // Normal case - replace the entire node
                _changes.Add(new ReplaceChange(
                    context.Start.StartIndex, 
                    context.Stop.StopIndex, 
                    newText, 
                    description
                ));
            }
        }

        /// <summary>
        /// Adds a replacement change with explicit start and end positions
        /// </summary>
        /// <param name="startIndex">The starting index where replacement begins</param>
        /// <param name="endIndex">The ending index (inclusive) where replacement ends</param>
        /// <param name="newText">The new text to replace with</param>
        /// <param name="description">A description of what is being replaced</param>
        protected void ReplaceText(int startIndex, int endIndex, string newText, string description)
        {
            _changes.Add(new ReplaceChange(startIndex, endIndex, newText, description));
        }

        /// <summary>
        /// Adds a new insertion change
        /// </summary>
        /// <param name="position">The position where text should be inserted</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertText(int position, string textToInsert, string description)
        {
            _changes.Add(new InsertChange(position, textToInsert, description));
        }
                
        /// <summary>
        /// Adds a new insertion change after a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to insert after</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertAfter(ParserRuleContext context, string textToInsert, string description)
        {
            _changes.Add(new InsertText(context.Stop.StopIndex + 1, textToInsert, description));
        }
        
        /// <summary>
        /// Adds a new insertion change before a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to insert before</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertBefore(ParserRuleContext context, string textToInsert, string description)
        {
            _changes.Add(new InsertText(context.Start.StartIndex, textToInsert, description));
        }

        /// <summary>
        /// Adds a new deletion change
        /// </summary>
        /// <param name="startIndex">The starting index of text to delete</param>
        /// <param name="endIndex">The ending index (inclusive) of text to delete</param>
        /// <param name="description">A description of what is being deleted</param>
        protected void DeleteText(int startIndex, int endIndex, string description)
        {
            _changes.Add(new DeleteChange(startIndex, endIndex, description));
        }
        
        /// <summary>
        /// Adds a new deletion change to remove a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to delete</param>
        /// <param name="description">A description of what is being deleted</param>
        protected void DeleteNode(ParserRuleContext context, string description)
        {
            _changes.Add(new DeleteChange(
                context.Start.StartIndex, 
                context.Stop.StopIndex, 
                description
            ));
        }

        /// <summary>
        /// Gets the original text for a parser rule context
        /// </summary>
        protected string? GetOriginalText(ParserRuleContext context)
        {
            if (SourceText == null) return null;
            
            return SourceText.Substring(
                context.Start.StartIndex,
                context.Stop.StopIndex - context.Start.StartIndex + 1
            );
        }
    }
}
