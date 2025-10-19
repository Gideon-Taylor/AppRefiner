using AppRefiner; // For ScintillaEditor, ModifierKeys etc.
using AppRefiner.Refactors;
using PeopleCodeParser.SelfHosted.Nodes; // For AST nodes
using System.Windows.Forms; // For Keys enum

namespace PluginSample
{
    /// <summary>
    /// Sample refactor that adds a comment before return statements.
    /// Demonstrates how to use the new self-hosted parser visitor pattern.
    /// </summary>
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

        // Override visitor methods from BaseAstVisitor to find refactoring targets
        public override void VisitReturn(ReturnStatementNode node)
        {
            // Example: Insert a comment before every return statement
            string comment = $"/* Sample Refactor Added Comment {DateTime.Now} */\r\n";

            // Get the position at the start of the return statement's line
            var returnLineStart = ScintillaManager.GetLineStartIndex(Editor, node.SourceSpan.Start.Line);

            // Insert the comment before the return statement
            InsertText(returnLineStart, comment, "Add sample comment");

            // Continue visiting child nodes
            base.VisitReturn(node);
        }

        // Optional: Override Initialize, ShowRefactorDialog, RequiresUserInputDialog etc. if needed
        // public override bool RequiresUserInputDialog => true; // If you need a dialog
        // public override bool ShowRefactorDialog() { /* Show dialog logic */ return true; }
    }
}
