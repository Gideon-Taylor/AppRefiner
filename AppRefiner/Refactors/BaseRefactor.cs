using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;
using System.Diagnostics;
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
        public static RefactorResult Successful => new(true);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        public static RefactorResult Failed(string message) => new(false, message);
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

        /// <summary>
        /// Calculates how this change affects a cursor position
        /// </summary>
        /// <param name="cursorPosition">The current cursor position</param>
        /// <returns>The new cursor position after applying this change</returns>
        public abstract int UpdateCursorPosition(int cursorPosition);
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
        /// Length of the deleted text
        /// </summary>
        public int DeleteLength => EndIndex - StartIndex + 1;

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
            source.Remove(StartIndex, DeleteLength);
        }

        /// <summary>
        /// Updates cursor position based on this deletion
        /// </summary>
        /// <param name="cursorPosition">The current cursor position</param>
        /// <returns>The adjusted cursor position</returns>
        public override int UpdateCursorPosition(int cursorPosition)
        {
            if (cursorPosition <= StartIndex)
            {
                // Cursor is before deletion, no change needed
                return cursorPosition;
            }
            else if (cursorPosition <= EndIndex)
            {
                // Cursor is within deleted text, move to start of deletion
                return StartIndex;
            }
            else
            {
                // Cursor is after deleted text, shift backward by deleted length
                return cursorPosition - DeleteLength;
            }
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

        /// <summary>
        /// Updates cursor position based on this insertion
        /// </summary>
        /// <param name="cursorPosition">The current cursor position</param>
        /// <returns>The adjusted cursor position</returns>
        public override int UpdateCursorPosition(int cursorPosition)
        {
            if (cursorPosition < StartIndex)
            {
                // Cursor is before insertion point, no change needed
                return cursorPosition;
            }
            else
            {
                // Cursor is at or after insertion point, shift forward by inserted text length
                return cursorPosition + TextToInsert.Length;
            }
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
        /// The length of the original text being replaced
        /// </summary>
        public int OldLength => EndIndex - StartIndex + 1;

        /// <summary>
        /// The net change in length (positive if new text is longer, negative if shorter)
        /// </summary>
        public int LengthDelta => NewText.Length - OldLength;

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
            source.Remove(StartIndex, OldLength);
            source.Insert(StartIndex, NewText);
        }

        /// <summary>
        /// Updates cursor position based on this replacement
        /// </summary>
        /// <param name="cursorPosition">The current cursor position</param>
        /// <returns>The adjusted cursor position</returns>
        public override int UpdateCursorPosition(int cursorPosition)
        {
            if (cursorPosition < StartIndex)
            {
                // Cursor is before replacement, no change needed
                return cursorPosition;
            }
            else if (cursorPosition <= EndIndex)
            {
                // Cursor is within replacement
                // Move to end of new text if cursor was inside replaced section
                return StartIndex + NewText.Length;
            }
            else
            {
                // Cursor is after replacement, adjust by the change in length
                return cursorPosition + LengthDelta;
            }
        }
    }

    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations
    /// </summary>
    /// <remarks>
    /// Creates a new instance of the BaseRefactor class
    /// </remarks>
    /// <param name="editor">The ScintillaEditor instance to use for this refactor</param>
    /// <remarks>
    /// Creates a new instance of the BaseRefactor class
    /// </remarks>
    /// <param name="editor">The ScintillaEditor instance to use for this refactor</param>
    /// <param name="currentPosition">The current cursor position in the editor</param>
    /// <param name="lineNumber">The current line number in the editor</param>
    public abstract class BaseRefactor(ScintillaEditor editor) : AppRefiner.PeopleCode.PeopleCodeParserBaseListener
    {
        /// <summary>
        /// Gets the display name for this refactor
        /// </summary>
        public static string RefactorName => "Base Refactor";

        /// <summary>
        /// Gets the description for this refactor
        /// </summary>
        public static string RefactorDescription => "Base refactoring operation";

        /// <summary>
        /// Gets whether this refactor requires a user input dialog
        /// </summary>
        public virtual bool RequiresUserInputDialog => false;

        /// <summary>
        /// Gets whether this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        public virtual bool DeferDialogUntilAfterVisitor => false;

        /// <summary>
        /// Gets whether this refactor should have a keyboard shortcut registered
        /// </summary>
        public static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// Gets whether this refactor should be hidden from refactor lists and discovery
        /// </summary>
        public static bool IsHidden => false;

        /// <summary>
        /// Gets the keyboard shortcut modifier keys for this refactor
        /// </summary>
        public static ModifierKeys ShortcutModifiers => ModifierKeys.Control;

        /// <summary>
        /// Gets the keyboard shortcut key for this refactor
        /// </summary>
        public static Keys ShortcutKey => Keys.None;

        protected ScintillaEditor Editor { get; } = editor ?? throw new ArgumentNullException(nameof(editor));
        protected int CurrentPosition { get; } = ScintillaManager.GetCursorPosition(editor);
        protected int LineNumber { get; } = ScintillaManager.GetCurrentLineNumber(editor);

        private string? source;
        private CommonTokenStream? tokenStream;
        private int cursorPosition = -1;
        private bool failed;
        private string? failureMessage;
        private readonly List<CodeChange> changes = new();

        /// <summary>
        /// Gets the main window handle for the editor
        /// </summary>
        /// <returns>The main window handle</returns>
        protected IntPtr GetEditorMainWindowHandle()
        {
            return Process.GetProcessById((int)Editor.ProcessId).MainWindowHandle;
        }

        /// <summary>
        /// Shows the dialog for this refactor
        /// </summary>
        /// <returns>True if the user confirmed, false if canceled</returns>
        public virtual bool ShowRefactorDialog()
        {
            // Base implementation just returns true (no dialog needed)
            return true;
        }

        /// <summary>
        /// Initializes the refactor with source code and token stream
        /// </summary>
        public virtual void Initialize(string sourceText, CommonTokenStream tokenStream, int cursorPosition = -1)
        {
            source = sourceText;
            this.tokenStream = tokenStream;
            changes.Clear();
            failed = false;
            failureMessage = null;
            this.cursorPosition = cursorPosition;
        }

        /// <summary>
        /// Sets a failure status with an error message
        /// </summary>
        protected void SetFailure(string message)
        {
            failed = true;
            failureMessage = message;
        }

        /// <summary>
        /// Gets the result of the refactoring operation
        /// </summary>
        public RefactorResult GetResult() => failed ? RefactorResult.Failed(failureMessage ?? "Unknown error") : RefactorResult.Successful;

        /// <summary>
        /// Gets the refactored source code with all changes applied
        /// </summary>
        public string? GetRefactoredCode()
        {
            if (failed) return null;

            if (changes.Count == 0) return source;

            // Sort changes from last to first to avoid index shifting
            changes.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

            var result = new StringBuilder(source);

            // Process each change and update cursor position
            foreach (var change in changes)
            {
                if (cursorPosition >= 0)
                {
                    cursorPosition = change.UpdateCursorPosition(cursorPosition);
                }
                change.Apply(result);
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the updated cursor position after refactoring
        /// </summary>
        /// <returns>The new cursor position, or -1 if no cursor position was provided</returns>
        public int GetUpdatedCursorPosition()
        {
            return cursorPosition;
        }

        /// <summary>
        /// Gets the list of changes that will be applied
        /// </summary>
        public IReadOnlyList<CodeChange> GetChanges() => changes.AsReadOnly();

        /// <summary>
        /// Adds a new replacement change using parser context
        /// </summary>
        /// <param name="context">The parser rule context to replace</param>
        /// <param name="newText">The new text to replace with</param>
        /// <param name="description">A description of what is being replaced</param>
        protected void ReplaceNode(ParserRuleContext context, string newText, string description, bool eatExtraForSemicolon = false)
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
                changes.Add(new ReplaceChange(
                    context.Start.StartIndex,
                    eatExtraForSemicolon? context.Stop.StopIndex + 1 : context.Stop.StopIndex,
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
            changes.Add(new ReplaceChange(startIndex, endIndex, newText, description));
        }

        /// <summary>
        /// Adds a new insertion change
        /// </summary>
        /// <param name="position">The position where text should be inserted</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertText(int position, string textToInsert, string description)
        {
            changes.Add(new InsertChange(position, textToInsert, description));
        }

        /// <summary>
        /// Adds a new insertion change after a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to insert after</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertAfter(ParserRuleContext context, string textToInsert, string description)
        {
            changes.Add(new InsertChange(context.Stop.StopIndex + 1, textToInsert, description));
        }

        /// <summary>
        /// Adds a new insertion change before a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to insert before</param>
        /// <param name="textToInsert">The text to insert</param>
        /// <param name="description">A description of what is being inserted</param>
        protected void InsertBefore(ParserRuleContext context, string textToInsert, string description)
        {
            changes.Add(new InsertChange(context.Start.StartIndex, textToInsert, description));
        }

        /// <summary>
        /// Adds a new deletion change
        /// </summary>
        /// <param name="startIndex">The starting index of text to delete</param>
        /// <param name="endIndex">The ending index (inclusive) of text to delete</param>
        /// <param name="description">A description of what is being deleted</param>
        protected void DeleteText(int startIndex, int endIndex, string description)
        {
            changes.Add(new DeleteChange(startIndex, endIndex, description));
        }

        /// <summary>
        /// Adds a new deletion change to remove a parser rule context
        /// </summary>
        /// <param name="context">The parser rule context to delete</param>
        /// <param name="description">A description of what is being deleted</param>
        protected void DeleteNode(ParserRuleContext context, string description)
        {
            changes.Add(new DeleteChange(
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
            return source == null
                ? null
                : source.Substring(
                context.Start.StartIndex,
                context.Stop.StopIndex - context.Start.StartIndex + 1
            );
        }
    }
}
