using Antlr4.Runtime;
using System.Diagnostics;
using AppRefiner.PeopleCode;
using AppRefiner.Services; // For ScintillaEditor
using static AppRefiner.PeopleCode.PeopleCodeParser; // For ConcatShortHandExprContext

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that provides auto-completion for += shorthand (concatenation).
    /// </summary>
    public class ConcatAutoComplete : ScopedRefactor<string>
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
        public ConcatAutoComplete(ScintillaEditor editor)
            : base(editor)
        {
            Debug.Log("ConcatAutoComplete initialized.");
        }

        /// <summary>
        /// Called when the parser enters a ConcatShortHandExpr rule.
        /// This is where the transformation from "lhs +=" to "lhs = lhs +" occurs.
        /// We only replace the left-hand side and operator, preserving everything else on the line.
        /// </summary>
        /// <param name="context">The context for the ConcatShortHandExpr rule.</param>
        public override void EnterConcatShortHandExpr(ConcatShortHandExprContext context)
        {
            base.EnterConcatShortHandExpr(context);

            if (refactorApplied)
            {
                // Ensure the refactor is applied only once if this method were somehow called multiple times for the same instance.
                return;
            }

            var lhsExprCtx = context.expression(0); // Left-hand side expression

            // Get the left-hand side text
            string lhsText = lhsExprCtx.GetText();
            
            // Determine the operator character
            var concatChar = "";
            if (context.ADD() != null)
            {
                concatChar = "+";
            }
            else if (context.SUBTR() != null)
            {
                concatChar = "-";
            } 
            else if (context.PIPE() != null)
            {
                concatChar = "|";
            }

            // Get the original text to find where the operator ends
            var originalText = GetOriginalText(context);
            
            // Find the operator pattern in the original text
            var operatorPattern = $@"({System.Text.RegularExpressions.Regex.Escape(concatChar)}=)";
            var operatorMatch = System.Text.RegularExpressions.Regex.Match(originalText, operatorPattern);
            
            if (!operatorMatch.Success)
            {
                Debug.Log($"Could not find operator pattern {concatChar}= in original text: '{originalText}'");
                refactorApplied = true;
                return;
            }

            var operatorMatchIndex = System.Text.Encoding.UTF8.GetByteCount(originalText.Substring(0, operatorMatch.Index));

            // Calculate the positions
            int operatorEndIndex =  operatorMatch.Index + operatorMatch.Length;

            // Replace only the "lhs +=" part with "lhs = lhs +"
            string newText = $"{lhsText} = {lhsText} {concatChar}";

            Debug.Log($"ConcatAutoComplete: Applying refactor for ConcatShortHandExpr.");
            Debug.Log($"  Original text: '{originalText}'");
            Debug.Log($"  LHS text: '{lhsText}'");
            Debug.Log($"  Operator: '{concatChar}='");
            Debug.Log($"  Replacement range: {context.Start.ByteStartIndex()} to {operatorEndIndex - 1}");
            Debug.Log($"  New text: '{newText}'");

            // Replace only the "lhs operator=" portion, leaving everything after untouched
            ReplaceText(context.Start.ByteStartIndex(), operatorEndIndex - 1, newText, RefactorDescription);
            
            refactorApplied = true; // Mark as applied to prevent re-application.
        }
    }
} 