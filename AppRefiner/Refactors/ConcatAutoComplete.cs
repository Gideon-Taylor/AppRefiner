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

        public override void VisitAssignment(AssignmentNode node)
        {
            // A complete shorthand assignment (e.g. "&x += 1;") parses as a real
            // AssignmentNode; expand it the same way, leaving the RHS untouched
            if (!refactorApplied &&
                node.Operator is AssignmentOperator.AddAssign or AssignmentOperator.SubtractAssign or AssignmentOperator.ConcatenateAssign &&
                node.OperatorToken != null &&
                node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                ProcessShorthand(node.Target, node.Operator,
                    node.SourceSpan.Start.ByteIndex, node.OperatorToken.SourceSpan.End.ByteIndex);
            }

            base.VisitAssignment(node);
        }

        /// <summary>
        /// Processes a concatenation shorthand assignment and transforms it to full form
        /// </summary>
        private void ProcessConcatShorthand(PartialShortHandAssignmentNode assignment)
        {
            if (assignment.LastToken == null) return;

            ProcessShorthand(assignment.Target, assignment.Operator,
                assignment.SourceSpan.Start.ByteIndex, assignment.LastToken.SourceSpan.End.ByteIndex);
        }

        private void ProcessShorthand(ExpressionNode? target, AssignmentOperator op, int startByteIndex, int operatorEndIndex)
        {
            // Get the target expression (left-hand side) by reconstructing from the node
            var targetText = target?.ToString();
            if (string.IsNullOrEmpty(targetText))
            {
                Debug.Log("ConcatAutoComplete: Could not extract target text");
                return;
            }

            var concatChar = op switch
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
            Debug.Log($"  Target text: '{targetText}'");
            Debug.Log($"  Operator: '{concatChar}='");
            Debug.Log($"  Replacement range: {startByteIndex} to {operatorEndIndex - 1}");
            Debug.Log($"  New text: '{newText}'");

            // Replace only the "target operator=" portion, leaving everything after untouched
            EditText(startByteIndex, operatorEndIndex, newText, RefactorDescription);

            refactorApplied = true; // Mark as applied to prevent re-application.
        }
    }
}