using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// QuickFix wrapper that instantiates AddImport with a specific package path.
    /// Extracts the package path from the editor's QuickFixContext.
    /// </summary>
    public class AddImportQuickFix : BaseRefactor
    {
        public new static string RefactorName => "Add Import (QuickFix)";
        public new static string RefactorDescription => "Adds import via QuickFix selection";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly AddImport _innerRefactor;

        public AddImportQuickFix(ScintillaEditor editor) : base(editor)
        {
            // Read package path from editor state (set by AutoCompleteService.HandleQuickFixSelection)
            string? packagePath = editor.QuickFixContext as string;

            if (string.IsNullOrEmpty(packagePath))
                throw new InvalidOperationException("QuickFix context does not contain package path");

            Debug.Log($"AddImportQuickFix: Creating AddImport refactor for package path: {packagePath}");

            // Instantiate the real AddImport refactor with the package path
            _innerRefactor = new AddImport(editor, packagePath);
        }

        public override void VisitProgram(ProgramNode node)
        {
            // Delegate to the inner AddImport refactor
            _innerRefactor.VisitProgram(node);

            // Copy edits from inner refactor to this refactor
            var innerEdits = _innerRefactor.GetEdits();
            foreach (var edit in innerEdits)
            {
                EditText(edit.StartIndex, edit.EndIndex, edit.NewText, edit.Description);
            }

            Debug.Log($"AddImportQuickFix: Applied {innerEdits.Count} edits from AddImport refactor");
        }
    }
}
