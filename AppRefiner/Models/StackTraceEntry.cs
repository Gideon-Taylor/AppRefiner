using AppRefiner.Database.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Models
{

    /// <summary>
    /// Interface for error pattern matchers that can detect and provide enhanced highlighting for specific error types
    /// </summary>
    public interface IErrorPatternMatcher
    {
        /// <summary>
        /// The name of this error pattern
        /// </summary>
        string PatternName { get; }
        
        /// <summary>
        /// Checks if the stack trace matches this error pattern
        /// </summary>
        /// <param name="stackTrace">Full stack trace text</param>
        /// <returns>True if this pattern matches</returns>
        bool Matches(string stackTrace);
        
        /// <summary>
        /// Extracts error-specific data from the stack trace
        /// </summary>
        /// <param name="stackTrace">Full stack trace text</param>
        /// <returns>Dictionary of extracted data</returns>
        Dictionary<string, string> ExtractData(string stackTrace);
        
        /// <summary>
        /// Calculates enhanced selection span for the error based on parsed AST
        /// </summary>
        /// <param name="program">Parsed program AST</param>
        /// <param name="lineNumber">Line number where error occurred</param>
        /// <param name="errorData">Data extracted from stack trace</param>
        /// <returns>Enhanced selection span or null for default behavior</returns>
        SourceSpan? GetEnhancedSelection(ProgramNode program, int lineNumber, Dictionary<string, string> errorData);
    }
    
    /// <summary>
    /// Represents error pattern match results
    /// </summary>
    public class ErrorPatternMatch
    {
        public string PatternName { get; set; } = string.Empty;
        public Dictionary<string, string> ExtractedData { get; set; } = new();
        public SourceSpan? EnhancedSelection { get; set; }
    }

    /// <summary>
    /// Represents a parsed entry from a PeopleCode stack trace
    /// </summary>
    public class StackTraceEntry
    {
        /// <summary>
        /// The original raw text from the stack trace
        /// </summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// The parsed display name for the entry
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The line number in the stack trace (0-based)
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The parsed components (Record, Field, Event, etc.)
        /// </summary>
        public Dictionary<string, string> Components { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The object values extracted from the program path (dot-separated)
        /// </summary>
        public string[] ObjectValues { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The statement number extracted from the stack trace
        /// </summary>
        public int? StatementNumber { get; set; }

        /// <summary>
        /// Whether this entry represents a valid target that can be opened
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Error message if the entry could not be parsed or validated
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error pattern match information for enhanced highlighting
        /// </summary>
        public ErrorPatternMatch? ErrorPattern { get; set; }
        
        /// <summary>
        /// Pre-calculated OpenTarget string for navigation (includes SOURCETOKEN if applicable)
        /// </summary>
        public string? OpenTargetString { get; set; }
        
        /// <summary>
        /// Pre-calculated selection span for precise highlighting
        /// </summary>
        public SourceSpan? SelectionSpan { get; set; }
        
        /// <summary>
        /// Pre-calculated byte offset for statement navigation
        /// </summary>
        public int? ByteOffset { get; set; }
        
        /// <summary>
        /// Whether program text was successfully loaded and parsed
        /// </summary>
        public bool IsProgramParsed { get; set; }

        /// <summary>
        /// Additional metadata about the entry
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The resolved OpenTarget for this entry (cached after database lookup)
        /// </summary>
        public OpenTarget? ResolvedTarget { get; set; }

        /// <summary>
        /// Creates a new StackTraceEntry
        /// </summary>
        /// <param name="rawText">The original stack trace line</param>
        /// <param name="lineNumber">The line number in the stack trace</param>
        public StackTraceEntry(string rawText, int lineNumber = 0)
        {
            RawText = rawText ?? string.Empty;
            LineNumber = lineNumber;
            DisplayName = rawText?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Gets a component value by key
        /// </summary>
        /// <param name="key">The component key (e.g., "Record", "Field", "Event")</param>
        /// <returns>The component value or null if not found</returns>
        public string? GetComponent(string key)
        {
            return Components.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Sets a component value
        /// </summary>
        /// <param name="key">The component key</param>
        /// <param name="value">The component value</param>
        public void SetComponent(string key, string value)
        {
            Components[key] = value;
        }

        
        /// <summary>
        /// Returns a string representation of the entry
        /// </summary>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}