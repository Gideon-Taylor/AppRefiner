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

        /// <summary>
        /// Check if this is a concatenation shorthand assignment at cursor position
        /// </summary>
        private bool IsConcatShorthandAtCursor(AssignmentNode assignment)
        {
            // Check for +=, -=, or |= operators
            if (assignment.Operator == AssignmentOperator.AddAssign) // String concatenation
            {
                if (assignment.SourceSpan.IsValid)
                {
                    var span = assignment.SourceSpan;
                    // Check if cursor is within the assignment
                    return CurrentPosition >= span.Start.ByteIndex && CurrentPosition <= span.End.ByteIndex + 1;
                }
            }
            return false;
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            if (!refactorApplied && IsConcatShorthandAtCursor(node))
            {
                ProcessConcatShorthand(node);
            }

            base.VisitAssignment(node);
        }

        /// <summary>
        /// Processes a concatenation shorthand assignment and transforms it to full form
        /// </summary>
        private void ProcessConcatShorthand(AssignmentNode assignment)
        {
            if (refactorApplied) return;

            // Get the target expression (left-hand side)
            var targetText = GetOriginalText(assignment.Target);
            if (string.IsNullOrEmpty(targetText))
            {
                Debug.Log("ConcatAutoComplete: Could not extract target text");
                return;
            }

            // Get the full assignment text to find the operator
            var originalText = GetOriginalText(assignment);
            if (string.IsNullOrEmpty(originalText))
            {
                Debug.Log("ConcatAutoComplete: Could not extract original assignment text");
                return;
            }

            // Determine the operator character based on the assignment operator
            string concatChar = "";
            if (assignment.Operator == AssignmentOperator.AddAssign)
            {
                concatChar = "+";
            }
            else if (assignment.Operator == AssignmentOperator.SubtractAssign)
            {
                concatChar = "-";
            }
            else if (assignment.Operator == AssignmentOperator.ConcatenateAssign)
            {
                concatChar = "|";
            }
            else
            {
                Debug.Log($"ConcatAutoComplete: Unsupported operator: {assignment.Operator}");
                return;
            }

            // Find the operator pattern in the original text
            var operatorPattern = $@"({Regex.Escape(concatChar)}=)";
            var operatorMatch = Regex.Match(originalText, operatorPattern);

            if (!operatorMatch.Success)
            {
                Debug.Log($"Could not find operator pattern {concatChar}= in original text: '{originalText}'");
                refactorApplied = true;
                return;
            }

            // Calculate the positions
            int operatorStartIndex = assignment.SourceSpan.Start.ByteIndex + operatorMatch.Index;
            int operatorEndIndex = operatorStartIndex + operatorMatch.Length;

            // Replace only the "lhs +=" part with "lhs = lhs +"
            string newText = $"{targetText} = {targetText} {concatChar}";

            Debug.Log($"ConcatAutoComplete: Applying refactor for ConcatShorthand.");
            Debug.Log($"  Original text: '{originalText}'");
            Debug.Log($"  Target text: '{targetText}'");
            Debug.Log($"  Operator: '{concatChar}='");
            Debug.Log($"  Replacement range: {assignment.SourceSpan.Start.ByteIndex} to {operatorEndIndex - 1}");
            Debug.Log($"  New text: '{newText}'");

            // Replace only the "target operator=" portion, leaving everything after untouched
            EditText(assignment.SourceSpan.Start.ByteIndex, operatorEndIndex - 1, newText, RefactorDescription);

            refactorApplied = true; // Mark as applied to prevent re-application.
        }
    }
}