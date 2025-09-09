using PeopleCodeParser.SelfHosted.Nodes;
using System.Text.RegularExpressions;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for += shorthand (concatenation).
    /// </summary>
    public class ConcatAutoComplete : BaseRefactor
    {
        public new static string RefactorName => "Concat Auto Complete";
        public new static string RefactorDescription => "Auto-completes += shorthand to full concatenation expression";

        /// <summary>
        /// This refactor should not have a keyboard shortcut.
        /// </summary>
        public new static bool RegisterKeyboardShortcut => false;

        /// <summary>
        /// This refactor should be hidden from refactoring lists as it's auto-triggered.
        /// </summary>
        public new static bool IsHidden => true;

        private bool refactorApplied = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcatAutoComplete"/> class.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance to use for this refactor.</param>
        public ConcatAutoComplete(ScintillaEditor editor) : base(editor)
        {
            Debug.Log("ConcatAutoComplete initialized.");
        }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);
        }
        public override void VisitPartialShortHandAssignment(PartialShortHandAssignmentNode node)
        {
            if (!refactorApplied && node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                ProcessConcatShorthand(node);
            }

            base.VisitPartialShortHandAssignment(node);
        }

        /// <summary>
        /// Processes a concatenation shorthand assignment and transforms it to full form
        /// </summary>
        private void ProcessConcatShorthand(PartialShortHandAssignmentNode assignment)
        {
            if (assignment.LastToken == null) return;

            // Get the target expression (left-hand side) by reconstructing from the node
            var targetText = assignment.Target?.ToString();
            if (string.IsNullOrEmpty(targetText))
            {
                Debug.Log("ConcatAutoComplete: Could not extract target text");
                return;
            }

            

            
            // Calculate the positions
            int operatorStartIndex = assignment.LastToken.SourceSpan.Start.ByteIndex;
            int operatorEndIndex = assignment.LastToken.SourceSpan.End.ByteIndex;

            var concatChar = assignment.Operator switch
            {
                AssignmentOperator.Assign => throw new NotImplementedException(),
                AssignmentOperator.SubtractAssign => "-",
                AssignmentOperator.ConcatenateAssign => "|",
                AssignmentOperator.AddAssign => "+",
                _ => throw new NotImplementedException()
            };

            // Replace only the "lhs +=" part with "lhs = lhs +"
            string newText = $"{targetText} = {targetText} {concatChar}";

            Debug.Log($"ConcatAutoComplete: Applying refactor for ConcatShorthand.");
            Debug.Log($"  Original text: '{assignment.Target} {assignment.Operator.GetSymbol()}'");
            Debug.Log($"  Target text: '{targetText}'");
            Debug.Log($"  Operator: '{concatChar}='");
            Debug.Log($"  Replacement range: {assignment.SourceSpan.Start.ByteIndex} to {operatorEndIndex - 1}");
            Debug.Log($"  New text: '{newText}'");

            // Replace only the "target operator=" portion, leaving everything after untouched
            EditText(assignment.SourceSpan.Start.ByteIndex, operatorEndIndex, newText, RefactorDescription);

            refactorApplied = true; // Mark as applied to prevent re-application.
        }
    }
}