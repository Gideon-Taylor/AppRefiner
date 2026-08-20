using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    public class DeleteImport : BaseRefactor
    {
        /// <summary>
        /// Gets the display name of this refactoring operation.
        /// </summary>
        public new static string RefactorName => "QuickFix: Delete Import";

        /// <summary>
        /// Gets the description of this refactoring operation.
        /// </summary>
        public new static string RefactorDescription => "Deletes an unused or duplicate import declaration.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut registered.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists and discovery.
        /// </summary>
        public new static bool IsHidden => true;

        private ScintillaEditor editor;

        public DeleteImport(ScintillaEditor editor) : base(editor)
        {
            this.editor = editor;
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            var targetImport = node.Imports.FirstOrDefault(i => i.SourceSpan.ContainsPosition(CurrentPosition));

            if (targetImport == null)
            {
                SetFailure("No import declaration found at the cursor position.");
                return;
            }

            /* remove the whole line */
            var lineStart = ScintillaManager.GetLineStartIndex(editor, targetImport.SourceSpan.Start.Line);
            var lineLength = ScintillaManager.GetLineLength(editor, targetImport.SourceSpan.Start.Line);

            /* GetLineLength (SCI_LINELENGTH) already includes the line-end, so
               lineStart + lineLength is the start of the next line. */
            DeleteText(new SourceSpan(lineStart, lineStart + lineLength), "Delete import.");
        }
    }
}
