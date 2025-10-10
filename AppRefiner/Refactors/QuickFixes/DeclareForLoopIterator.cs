using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// Quick fix that declares an undefined for loop iterator variable.
    /// Inserts a "Local number &varname;" declaration above the for loop.
    /// </summary>
    public class DeclareForLoopIterator : BaseRefactor
    {
        /// <summary>
        /// Gets the display name of this refactoring operation.
        /// </summary>
        public new static string RefactorName => "QuickFix: Declare For Loop Iterator";

        /// <summary>
        /// Gets the description of this refactoring operation.
        /// </summary>
        public new static string RefactorDescription => "Declares an undefined for loop iterator variable.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut registered.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists and discovery.
        /// </summary>
        public new static bool IsHidden => true;

        private ScintillaEditor editor;
        private ForStatementNode? targetForLoop;
        private string? iteratorVarName;

        public DeclareForLoopIterator(ScintillaEditor editor) : base(editor)
        {
            this.editor = editor;
        }

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            targetForLoop = null;
            iteratorVarName = null;

            base.VisitProgram(node);

            if (targetForLoop != null && iteratorVarName != null)
            {
                // Insert the variable declaration above the for loop
                InsertVariableDeclaration();
            }
            else
            {
                SetFailure("Could not identify an undefined for loop iterator at the current position.");
            }
        }

        public override void VisitFor(ForStatementNode node)
        {
            // Check if the cursor is on the iterator variable in this for loop
            if (node.IteratorToken.SourceSpan.ContainsPosition(CurrentPosition))
            {
                // Check if this variable is undefined in the current scope
                var scope = GetCurrentScope();
                var varsInScope = GetVariablesInScope(scope);

                string varName = node.Variable;

                // Check if the variable is already defined (with or without & prefix)
                bool isDefined = varsInScope.Any(v =>
                    v.Name.Equals(varName) ||
                    v.Name.Equals(varName.TrimStart('&')) ||
                    (varName.StartsWith('&') && v.Name.Equals(varName.Substring(1))));

                if (!isDefined)
                {
                    // This is an undefined iterator - we can declare it
                    targetForLoop = node;
                    iteratorVarName = varName.StartsWith('&') ? varName.Substring(1) : varName;
                }
            }

            base.VisitFor(node);
        }

        private void InsertVariableDeclaration()
        {
            if (targetForLoop == null || iteratorVarName == null)
                return;

            // Get the indentation level of the for loop
            var foldLevel = ScintillaManager.GetCurrentLineFoldLevel(editor, targetForLoop.SourceSpan.Start.Line);
            var padding = new string(' ', foldLevel.Level * 3);

            // Get the line start position for the for loop
            var forLoopLineStart = ScintillaManager.GetLineStartIndex(editor, targetForLoop.SourceSpan.Start.Line);

            // Create the declaration text
            var declarationText = $"{padding}Local number &{iteratorVarName};\r\n";

            // Insert the declaration above the for loop
            InsertText(forLoopLineStart, declarationText, "Declare for loop iterator");
        }
    }
}
