using AppRefiner; // For ScintillaEditor, ModifierKeys etc.
using AppRefiner.Refactors;
using System.Windows.Forms; // For Keys enum

namespace PluginSample
{
    // Note: Refactors need a constructor accepting ScintillaEditor
    public class SampleRefactor : BaseRefactor
    {
        // Required constructor
        public SampleRefactor(ScintillaEditor editor) : base(editor) { }

        // Static properties define metadata for the refactoring
        public new static string RefactorName => "Sample Add Comment";
        public new static string RefactorDescription => "Adds a sample comment before return statements.";
        public new static bool RegisterKeyboardShortcut => true;
        public new static ModifierKeys ShortcutModifiers => ModifierKeys.Control | ModifierKeys.Alt;
        public new static Keys ShortcutKey => Keys.C;

        // Override methods from PeopleCodeParserBaseListener to find refactoring targets
        public override void EnterReturnStmt(ReturnStmtContext context)
        {
            // Example: Insert a comment before every return statement
            string comment = $"/* Sample Refactor Added Comment {DateTime.Now} */\n"; // Use \n for newline

            // Use helper methods from ScopedRefactor to add changes
            InsertBefore(context, comment, "Add sample comment");
        }

        // Optional: Override Initialize, ShowRefactorDialog, RequiresUserInputDialog etc. if needed
        // public override bool RequiresUserInputDialog => true; // If you need a dialog
        // public override bool ShowRefactorDialog() { /* Show dialog logic */ return true; }
    }
}