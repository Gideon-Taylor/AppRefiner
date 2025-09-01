using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using AppRefiner.Services;
using System.Windows.Forms;
using System.Diagnostics;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Base class for implementing PeopleCode refactoring operations that need scope and variable tracking.
    /// This class leverages the ScopedAstVisitor from the SelfHosted parser to provide automatic scope management.
    /// </summary>
    public abstract class ScopedRefactor : ScopedAstVisitor<object>, IRefactor
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
        
        #endregion

        #region Private Fields

        private string? source;
        private int cursorPosition = -1;
        private bool failed;
        private string? failureMessage;
        private readonly List<TextEdit> edits = new();

        #endregion

        /// <summary>
        /// Creates a new refactor instance
        /// </summary>
        protected ScopedRefactor(ScintillaEditor editor)
        {
            Editor = editor;
            CurrentPosition = ScintillaManager.GetCursorPosition(editor);
            LineNumber = ScintillaManager.GetCurrentLineNumber(editor);
        }

        #region IRefactor Implementation

        /// <summary>
        /// Gets the main window handle for the editor
        /// </summary>
        public IntPtr GetEditorMainWindowHandle()
        {
            return Process.GetProcessById((int)Editor.ProcessId).MainWindowHandle;
        }

        /// <summary>
        /// Initializes the refactor with the source code and cursor position
        /// </summary>
        public virtual void Initialize(string source, int cursorPosition)
        {
            this.source = source;
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

            if (edits.Count == 0)
            {
                return RefactorResult.Failed("No changes to apply");
            }

            try
            {
                ApplyEdits();
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
            if (!DeferDialogUntilAfterVisitor)
            {
                return edits.Count > 0 ? RefactorResult.Successful : RefactorResult.Failed("No changes to apply");
            }

            // Always successful for deferred dialogs. they will report errors after the dialog runs
            // this is because some refactors require input from the user before they can generate the changes (like renaming variables).
            return RefactorResult.Successful;
        }

        #endregion

        #region Text Editing Methods

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
            edits.Add(new TextEdit(position, position, text, description));
        }

        /// <summary>
        /// Adds a text edit to insert text at the given source position
        /// </summary>
        protected void InsertText(SourcePosition position, string text, string description)
        {
            edits.Add(new TextEdit(position.ByteIndex, position.ByteIndex, text, description));
        }

        /// <summary>
        /// Adds a text edit to delete text at the given position range
        /// </summary>
        protected void DeleteText(int startIndex, int endIndex, string description)
        {
            edits.Add(new TextEdit(startIndex, endIndex, "", description));
        }

        /// <summary>
        /// Adds a text edit to delete text at the given source span
        /// </summary>
        protected void DeleteText(SourceSpan span, string description)
        {
            edits.Add(new TextEdit(span, "", description));
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