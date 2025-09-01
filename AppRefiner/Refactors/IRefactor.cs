namespace AppRefiner.Refactors
{
    /// <summary>
    /// Common interface for all refactoring operations
    /// </summary>
    public interface IRefactor
    {
        /// <summary>
        /// Gets whether this refactor requires a user input dialog
        /// </summary>
        bool RequiresUserInputDialog { get; }

        /// <summary>
        /// Gets whether this refactor should defer showing the dialog until after the visitor has run
        /// </summary>
        bool DeferDialogUntilAfterVisitor { get; }

        /// <summary>
        /// Gets the type of a refactor that should be run immediately after this one completes successfully.
        /// Returns null if no follow-up refactor is needed.
        /// </summary>
        Type? FollowUpRefactorType { get; }

        /// <summary>
        /// Gets whether this refactor should run even when the parser has syntax errors.
        /// </summary>
        bool RunOnIncompleteParse { get; }

        /// <summary>
        /// Gets the ScintillaEditor instance
        /// </summary>
        ScintillaEditor Editor { get; }

        /// <summary>
        /// Gets the current cursor position
        /// </summary>
        int CurrentPosition { get; }

        /// <summary>
        /// Gets the current line number
        /// </summary>
        int LineNumber { get; }

        /// <summary>
        /// Shows the refactor dialog (if required)
        /// </summary>
        /// <returns>True if the dialog was confirmed, false if canceled</returns>
        bool ShowRefactorDialog();

        /// <summary>
        /// Applies the refactoring changes to the document
        /// </summary>
        /// <returns>Result of the refactoring operation</returns>
        RefactorResult ApplyRefactoring();

        /// <summary>
        /// Initializes the refactor with the source code and cursor position
        /// </summary>
        /// <param name="source">Source code to refactor</param>
        /// <param name="cursorPosition">Current cursor position</param>
        void Initialize(string source, int cursorPosition);

        /// <summary>
        /// Gets the main window handle for the editor
        /// </summary>
        IntPtr GetEditorMainWindowHandle();

        /// <summary>
        /// Gets the result of the refactoring operation
        /// </summary>
        RefactorResult GetResult();
    }
}
