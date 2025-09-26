using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace AppRefiner.Refactors.QuickFixes
{
    public class FixExceptionVariable : BaseRefactor
    {
        public new static string RefactorName => "Fix Exception Variable";
        public new static string RefactorDescription => "Renames exception variable references in the current catch block to the correct one";
        public new static bool RegisterKeyboardShortcut => false;
        public new static bool IsHidden => false;

        private List<IdentifierNode> catchIdentifiers = new();
        private string? wrongVarName;
        private string? correctVarName;
        private CatchStatementNode? targetCatch;
        private List<(int start, int length, string newText)> replacements = new();
        private int currentPos;
        private bool inTargetCatch = false;

        public FixExceptionVariable(ScintillaEditor editor) : base(editor)
        {
        }

        public override void VisitProgram(ProgramNode node)
        {
            Reset();
            currentPos = CurrentPosition;
            wrongVarName = null;
            correctVarName = null;
            targetCatch = null;
            catchIdentifiers.Clear();
            inTargetCatch = false;
            replacements.Clear();

            base.VisitProgram(node);

            if (wrongVarName != null && targetCatch != null && correctVarName != null)
            {
                // Filter matching identifiers from the collected list
                var matchingIds = catchIdentifiers
                    .Where(id => id.Name == wrongVarName && id.IdentifierType == IdentifierType.UserVariable)
                    .ToList();


                if (matchingIds.Count == 0)
                {
                    SetFailure("No matching references found to fix in the catch block.");
                    return;
                }


                foreach (var id in matchingIds)
                {
                    EditText(id.SourceSpan, correctVarName, "Rename to correct exception variable.");
                }
            }
            else
            {
                SetFailure("Could not identify the wrong exception variable or target catch block.");
            }
        }

        public override void VisitCatch(CatchStatementNode node)
        {
            bool isTarget = node.Body.SourceSpan.ContainsPosition(currentPos);

            if (isTarget)
            {
                targetCatch = node;
                if (node.ExceptionVariable != null)
                {
                    correctVarName = node.ExceptionVariable.Name;
                }
                catchIdentifiers.Clear();
                wrongVarName = null;
                inTargetCatch = true;
            }

            base.VisitCatch(node);

            if (isTarget)
            {
                inTargetCatch = false;
                // Now that we've collected identifiers, the processing happens in VisitProgram after full traversal
            }
        }

        public override void VisitIdentifier(IdentifierNode node)
        {
            if (inTargetCatch)
            {
                catchIdentifiers.Add(node);
                if (node.SourceSpan.ContainsPosition(currentPos))
                {
                    wrongVarName = node.Name;
                }
            }
        }
    }
}
