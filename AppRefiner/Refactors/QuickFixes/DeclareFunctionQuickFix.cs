using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// QuickFix wrapper that declares a specific function chosen from the cache.
    /// The full FunctionSearchResult rides in editor.QuickFixContext (attached by
    /// UndeclaredFunctionStyler's resolver), so no re-query or parsing happens here.
    /// </summary>
    public class DeclareFunctionQuickFix : BaseRefactor
    {
        public new static string RefactorName => "Declare Function (QuickFix)";
        public new static string RefactorDescription => "Adds a Declare Function statement via QuickFix selection";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        private readonly DeclareFunction _innerRefactor;

        public DeclareFunctionQuickFix(ScintillaEditor editor) : base(editor)
        {
            if (editor.QuickFixContext is not FunctionSearchResult functionToDeclare)
                throw new InvalidOperationException("QuickFix context does not contain a FunctionSearchResult");

            Debug.Log($"DeclareFunctionQuickFix: declaring {functionToDeclare.FunctionName} from {functionToDeclare.FunctionPath}");
            _innerRefactor = new DeclareFunction(editor, functionToDeclare, insertExampleCall: false);
        }

        public override void VisitProgram(ProgramNode node)
        {
            _innerRefactor.VisitProgram(node);
            foreach (var edit in _innerRefactor.GetEdits())
            {
                EditText(edit.StartIndex, edit.EndIndex, edit.NewText, edit.Description);
            }
        }
    }
}
