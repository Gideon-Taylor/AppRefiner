using DiffPlex.Model;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Visitors;
using System.Diagnostics;
using System.Text;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations that need scope and variable tracking.
    /// This class leverages the ScopedAstVisitor from the SelfHosted parser to provide automatic scope management.
    /// </summary>
    public abstract class BaseRefactor : ScopedAstVisitor<object>
    {
        #region Static Properties

        /// <summary>
        /// Gets the display name for this refactor
        /// </summary>
        public static string RefactorName => "Scoped Refactor";

        /// <summary>
        /// Gets the description for this refactor
        /// </summary>
        public static string RefactorDescription => "Scope-aware refactoring operation";

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

        #endregion

        #region IRefactor Properties

        /// <summary>
        /// Gets whether this refactor requires a user input dialog
        /// </summary>
        public virtual bool RequiresUserInputDialog => false;

        /// <summary>
        /// Gets whether this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        public virtual bool DeferDialogUntilAfterVisitor => false;

        /// <summary>
        /// Gets the type of a refactor that should be run immediately after this one completes successfully.
        /// Returns null if no follow-up refactor is needed.
        /// </summary>
        public virtual Type? FollowUpRefactorType => null;

        /// <summary>
        /// Gets whether this refactor should run even when the parser has syntax errors.
        /// Defaults to true for backward compatibility, but refactors that modify imports or 
        /// other structure-sensitive elements should set this to false.
        /// </summary>
        public virtual bool RunOnIncompleteParse => true;

        /// <summary>
        /// Gets whether type inference should be run on the freshly parsed program
        /// before this refactor's visitor executes. Inference is best-effort: without
        /// a database connection, builtins and literals still resolve but app class
        /// and record metadata lookups return null.
        /// </summary>
        public virtual bool RequiresTypeInference => false;

        /// <summary>
        /// Gets the ScintillaEditor instance
        /// </summary>
        public ScintillaEditor Editor { get; }

        /// <summary>
        /// Gets the current cursor position
        /// </summary>
        public int CurrentPosition { get; }

        /// <summary>
        /// Gets the current line number
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// Gets the current cursor position (alias for CurrentPosition)
        /// </summary>
        protected int CurrentCursorPosition => CurrentPosition;

        /// <summary>
        /// Gets the selection start byte position captured when the refactor was created
        /// </summary>
        public int SelectionStart { get; }

        /// <summary>
        /// Gets the selection end byte position (exclusive) captured when the refactor was created
        /// </summary>
        public int SelectionEnd { get; }

        /// <summary>
        /// Gets whether a non-empty selection existed when the refactor was created
        /// </summary>
        public bool HasSelection => SelectionEnd > SelectionStart;

        #endregion

        #region Private Fields

        protected string? originalSource;
        private byte[]? sourceBytes;
        private int cursorPosition = -1;
        private bool failed;
        private string? failureMessage;
        private readonly List<TextEdit> edits = new();

        #endregion

        /// <summary>
        /// Creates a new refactor instance
        /// </summary>
        protected BaseRefactor(ScintillaEditor editor)
        {
            Editor = editor;
            CurrentPosition = ScintillaManager.GetCursorPosition(editor);
            LineNumber = ScintillaManager.GetCurrentLineNumber(editor);
            (SelectionStart, SelectionEnd) = ScintillaManager.GetSelectionRange(editor);
        }

        #region Refactor Implementation

        /// <summary>
        /// Gets the main window handle for the editor
        /// </summary>
        public IntPtr GetEditorMainWindowHandle()
        {
            return Editor.AppDesignerProcess.MainWindowHandle;
        }

        /// <summary>
        /// Initializes the refactor with the source code and cursor position
        /// </summary>
        public virtual void Initialize(string source, int cursorPosition)
        {
            this.originalSource = source;
            sourceBytes = Encoding.UTF8.GetBytes(source);
            this.cursorPosition = cursorPosition;
            failed = false;
            failureMessage = null;
            edits.Clear();
        }

        /// <summary>
        /// Shows the refactor dialog (if required)
        /// </summary>
        public virtual bool ShowRefactorDialog()
        {
            return true;
        }

        /// <summary>
        /// Applies the refactoring changes to the document
        /// </summary>
        public virtual RefactorResult ApplyRefactoring()
        {
            if (failed)
            {
                return RefactorResult.Failed(failureMessage ?? "Unknown error");
            }

            // Success is determined by explicit failure, not edit count
            // Zero edits is a valid successful outcome (e.g., already in desired state)
            try
            {
                if (edits.Count > 0)
                {
                    ApplyEdits();
                }
                return RefactorResult.Successful;
            }
            catch (Exception ex)
            {
                return RefactorResult.Failed($"Error applying edits: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the result of the refactoring operation
        /// </summary>
        public virtual RefactorResult GetResult()
        {
            if (failed)
            {
                return RefactorResult.Failed(failureMessage ?? "Unknown error");
            }

            // Success is determined by explicit failure, not edit count
            // Zero edits is a valid successful outcome (e.g., already in desired state)
            return RefactorResult.Successful;
        }

        #endregion

        #region Text Editing Methods

        /// <summary>
        /// UTF-8 bytes of the original source. Spans and Scintilla positions are byte
        /// offsets, so all source slicing must go through this array, never string indexes.
        /// </summary>
        protected byte[] SourceBytes => sourceBytes ?? throw new InvalidOperationException("Initialize has not been called");

        /// <summary>
        /// Extracts source text for a byte range (end exclusive)
        /// </summary>
        protected string GetSourceText(int startByteIndex, int endByteIndex)
        {
            int start = Math.Max(0, startByteIndex);
            int end = Math.Min(SourceBytes.Length, endByteIndex);
            if (end <= start) return string.Empty;
            return Encoding.UTF8.GetString(SourceBytes, start, end - start);
        }

        /// <summary>
        /// Extracts source text for a source span
        /// </summary>
        protected string GetSourceText(SourceSpan span)
            => GetSourceText(span.Start.ByteIndex, span.End.ByteIndex);

        /// <summary>
        /// Gets the document's line-ending convention
        /// </summary>
        protected string NewLine => (originalSource ?? "").Contains("\r\n") ? "\r\n" : "\n";

        /// <summary>
        /// Gets the leading whitespace of the line containing the given byte position
        /// </summary>
        protected string GetLineIndent(int byteIndex)
        {
            int lineStart = Math.Min(byteIndex, SourceBytes.Length);
            while (lineStart > 0 && SourceBytes[lineStart - 1] != (byte)'\n')
                lineStart--;
            int i = lineStart;
            while (i < SourceBytes.Length && (SourceBytes[i] == (byte)' ' || SourceBytes[i] == (byte)'\t'))
                i++;
            return GetSourceText(lineStart, i);
        }

        /// <summary>
        /// Applies the collected edits to the document
        /// </summary>
        protected virtual void ApplyEdits()
        {
            // Apply edits in reverse order to avoid position shifting
            var sortedEdits = edits.OrderByDescending(e => e.StartIndex).ToList();
            foreach (var edit in sortedEdits)
            {
                ScintillaManager.ReplaceTextRange(Editor, edit.StartIndex, edit.EndIndex, edit.NewText);
            }
        }

        /// <summary>
        /// Adds a text edit to replace text at the given position
        /// </summary>
        protected void EditText(int startIndex, int endIndex, string newText, string description)
        {
            edits.Add(new TextEdit(startIndex, endIndex, newText, description));
        }

        /// <summary>
        /// Adds a text edit to replace text at the given source span
        /// </summary>
        protected void EditText(SourceSpan span, string newText, string description)
        {
            edits.Add(new TextEdit(span, newText, description));
        }

        /// <summary>
        /// Adds a text edit to insert text at the given position
        /// </summary>
        protected void InsertText(int position, string text, string description)
        {
            var existingDelete = edits.FirstOrDefault(e => e.StartIndex < e.EndIndex && string.IsNullOrEmpty(e.NewText));

            if (existingDelete != null)
            {
                edits.Remove(existingDelete);
                edits.Add(new TextEdit(existingDelete.StartIndex, existingDelete.EndIndex, text, $"Converted to edit: {existingDelete.Description} + {description}"));
                return;
            }

            edits.Add(new TextEdit(position, position, text, description));
        }

        /// <summary>
        /// Adds a text edit to insert text at the given source position
        /// </summary>
        protected void InsertText(SourcePosition position, string text, string description)
        {
            InsertText(position.ByteIndex, text, description);
        }

        /// <summary>
        /// Adds a text edit to delete text at the given position range
        /// </summary>
        protected void DeleteText(int startIndex, int endIndex, string description)
        {
            var existingInsert = edits.FirstOrDefault(e => e.StartIndex == startIndex && e.StartIndex == e.EndIndex);

            if (existingInsert != null)
            {
                edits.Remove(existingInsert);
                edits.Add(new TextEdit(startIndex, endIndex, existingInsert.NewText, $"Converted to edit: {description} + {existingInsert.Description}"));
                return;
            }

            edits.Add(new TextEdit(startIndex, endIndex, "", description));
        }

        /// <summary>
        /// Adds a text edit to delete text at the given source span
        /// </summary>
        protected void DeleteText(SourceSpan span, string description)
        {
            DeleteText(span.Start.ByteIndex, span.End.ByteIndex, description);
        }

        /// <summary>
        /// Marks the refactor as failed with the specified message
        /// </summary>
        protected void SetFailure(string message)
        {
            failed = true;
            failureMessage = message;
        }

        #endregion

        #region Helper Methods for Variable References

        /// <summary>
        /// Checks if a position is within a source span
        /// </summary>
        protected bool IsPositionInSpan(int position, SourceSpan span)
        {
            return position >= span.Start.ByteIndex && position <= span.End.ByteIndex;
        }

        /// <summary>
        /// Checks if a node contains the current cursor position
        /// </summary>
        protected bool NodeContainsCursor(AstNode node)
        {
            return node.SourceSpan.ContainsPosition(CurrentPosition);
        }

        #endregion

        /// <summary>
        /// Gets the list of edits for testing
        /// </summary>
        internal List<TextEdit> GetEdits() => edits;
    }
}