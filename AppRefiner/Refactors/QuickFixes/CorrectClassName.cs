using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    public class CorrectClassName : BaseRefactor
    {
        private readonly string expectedName = string.Empty;
        private string currentClassName = string.Empty;
        private bool neededToReplace = false;

        public CorrectClassName(ScintillaEditor editor) : base(editor)
        {
            expectedName = editor.ClassPath.Split(":").LastOrDefault() ?? string.Empty;
        }

        public new static string RefactorName => "QuickFix: Correct Class Name";
        public new static string RefactorDescription => "Corrects the class name to match the expected name.";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => true;

        public override void VisitAppClass(AppClassNode node)
        {
            currentClassName = node.Name;
            if (!string.Equals(currentClassName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                neededToReplace = true;
                EditText(node.NameToken.SourceSpan, expectedName, "Correct class name to match expected name");

                var constructorMethod = node.Methods.Where(m => m.Name.Equals(currentClassName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (constructorMethod != null)
                    EditText(constructorMethod.NameToken.SourceSpan, expectedName, "Correct constructor name to match corrected class name");
            }

            base.VisitAppClass(node);
        }


    }
}