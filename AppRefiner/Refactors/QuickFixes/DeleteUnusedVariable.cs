using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors.QuickFixes
{
    public class DeleteUnusedVariable : ScopedRefactor
    {
        /// <summary>
        /// Gets the display name of this refactoring operation.
        /// </summary>
        public new static string RefactorName => "QuickFix: Delete Unused Variable Declaration";

        /// <summary>
        /// Gets the description of this refactoring operation.
        /// </summary>
        public new static string RefactorDescription => "Deletes an unused local variable, private instance variable, or parameter declaration.";

        /// <summary>
        /// This refactor should not have a keyboard shortcut registered.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists and discovery.
        /// </summary>
        public new static bool IsHidden => true;

        private ScintillaEditor editor;

        public DeleteUnusedVariable(ScintillaEditor editor) : base(editor)
        {
            this.editor = editor;
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);

            var targetVariable = GetAllVariables().Where(v => v.VariableNameInfo.SourceSpan.ContainsPosition(CurrentPosition) && v.IsUnused).FirstOrDefault();

            if (targetVariable != null)
            {
                /* Get the target variable's declaration */
                switch (targetVariable.Kind)
                {
                    case VariableKind.Local:
                        RemoveLocalVariable(targetVariable);
                        break;
                    case VariableKind.Parameter:
                        RemoveParameter(targetVariable);
                        break;
                    case VariableKind.Instance:
                        RemoveInstanceVariable(targetVariable);
                        break;
                    default:
                        SetFailure("Only Local, Instance, or Parameter variables can be deleted.");
                        break;

                }
            }

        }

        private void RemoveInstanceVariable(VariableInfo targetVariable)
        {
            var declNode = targetVariable.DeclarationNode;
            if (declNode is VariableNode varDecl)
            {
                if (varDecl.NameInfos.Count > 1)
                {
                    /* just remove this name? */

                    var newString = $"   instance {targetVariable.Type} {string.Join(", ", varDecl.NameInfos.Where(n => n.Name != targetVariable.Name).Select(v => v.Name))}";

                    EditText(varDecl.SourceSpan, newString, "Remove variable from declaration.");
                }
                else
                {
                    /* remove the whole line */
                    var lineStart = ScintillaManager.GetLineStartIndex(editor, varDecl.SourceSpan.Start.Line);
                    var lineLength = ScintillaManager.GetLineLength(editor, varDecl.SourceSpan.Start.Line);

                    /* + 1 to delete the \n ? */
                    DeleteText(new SourceSpan(lineStart, lineStart + lineLength + 1), "Delete unused local variable.");
                }
            }
        }

        private void RemoveParameter(VariableInfo targetVariable)
        {
            var declNode = targetVariable.DeclarationNode;
            // TODO: finish this

        }

        private void RemoveLocalVariable(VariableInfo variable)
        {
            var declNode = variable.DeclarationNode;

            if (declNode is LocalVariableDeclarationNode varDecl)
            {
                if (varDecl.VariableNameInfos.Count > 1)
                {
                    /* just remove this name? */

                    var newString = $"Local {varDecl.Type} {string.Join(", ", varDecl.VariableNameInfos.Where(n => n.Name != variable.Name).Select(v => v.Name))}";
                    var foldLevel = ScintillaManager.GetCurrentLineFoldLevel(editor, varDecl.SourceSpan.Start.Line);

                    var padding = new string(' ', foldLevel.Level * 3);

                    EditText(varDecl.SourceSpan, $"{padding}{newString}", "Remove variable from declaration.");
                }
                else
                {
                    /* remove the whole line */
                    var lineStart = ScintillaManager.GetLineStartIndex(editor, varDecl.SourceSpan.Start.Line);
                    var lineLength = ScintillaManager.GetLineLength(editor, varDecl.SourceSpan.Start.Line);

                    /* +1 to remove the \n ? */
                    DeleteText(new SourceSpan(lineStart, lineStart + lineLength + 1), "Delete unused local variable.");
                }
            }
            else if (declNode is LocalVariableDeclarationWithAssignmentNode varWithAssign)
            {
                /* remove the whole line */
                var lineStart = ScintillaManager.GetLineStartIndex(editor, varWithAssign.SourceSpan.Start.Line);
                var lineLength = ScintillaManager.GetLineLength(editor, varWithAssign.SourceSpan.Start.Line);
                DeleteText(new SourceSpan(lineStart, lineStart + lineLength), "Delete unused local variable.");
            }
        }

    }
}
